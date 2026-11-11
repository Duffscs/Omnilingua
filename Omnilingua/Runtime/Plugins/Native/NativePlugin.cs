using System.Diagnostics.CodeAnalysis;
using Omnilingua.Dotnet;
using Omnilingua.Plugins.dotnet.Proxy;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace Omnilingua.Plugins.Native;

/// <summary>Conserve les callbacks Cdecl et l’instance native tant que ses consommateurs sont chargés.</summary>
internal sealed class NativePlugin : ILoadedPlugin {
    private readonly NativeModule _module;
    private readonly Dictionary<(string Contract, string Method), (Delegate Callback, nint Pointer)> _exports = [];
    private readonly object _gate = new();
    private readonly Dictionary<(string Contract, string Method), Delegate> _imports = [];
    private readonly NativeModule.ImportCallbackFn _importCallback;
    private nint _handle;
    public PluginSettings Settings { get; }
    public PluginMetadata Metadata { get; }
    public string Name => Settings.Name;

    private NativePlugin(PluginSettings settings, NativeModule module, nint handle, PluginMetadata metadata) {
        Settings = settings; _module = module; _handle = handle; Metadata = metadata;
        _importCallback = InvokeImport;
    }

    internal static bool TryCreate(PluginSettings settings, string modulePath, NativeOptions options,
        [NotNullWhen(true)] out NativePlugin? plugin, out string? error, uint runtimeVersion = 0) {
        plugin = null;
        if (!NativeModule.TryLoad(modulePath, out NativeModule? module, out error)) { return false; }
        nint handle = 0;
        try {
            if (runtimeVersion != 0 && runtimeVersion != module.RuntimeVersion) {
                throw new NotSupportedException($"Version CPython incompatible : installation {runtimeVersion / 100}.{runtimeVersion % 100}, pont {module.RuntimeVersion / 100}.{module.RuntimeVersion % 100}.");
            }
            handle = module.Create(in options);
            if (handle == 0) { throw new InvalidOperationException(module.Diagnostic(0)); }
            nint metadataPointer = module.Metadata(handle);
            if (metadataPointer == 0) { throw new InvalidDataException(module.Diagnostic(handle)); }
            PluginMetadata metadata = NativeMetadataReader.Read(metadataPointer);
            NativePlugin instance = new(settings, module, handle, metadata);
            instance.PrepareExports();
            plugin = instance;
            handle = 0; module = null;
            return true;
        } catch (Exception exception) {
            error = $"Plugin '{settings.Name}' : {exception.GetBaseException().Message}";
            return false;
        } finally {
            if (handle != 0) { module!.Destroy(handle); }
            module?.Dispose();
        }
    }

    private void PrepareExports() {
        if (!_module.DynamicCalls) { return; }
        foreach (ContractDescription contract in Metadata.Exports) {
            foreach (AbiMethodDescription method in contract.Signatures ?? []) {
                if (method.ReturnType.Kind != AbiKind.Void || method.Parameters.Any(parameter => parameter.Type.Kind != AbiKind.Utf8 || parameter.Passing != AbiPassing.Value)) {
                    throw new NotSupportedException($"{contract.Name}.{method.Name} : seuls void et les paramètres String sont pris en charge par ce module natif.");
                }
                ParameterExpression[] parameters = method.Parameters.Select(parameter => Expression.Parameter(typeof(nint))).ToArray();
                Type delegateType = AbiDelegateFactory.Create(typeof(void), parameters.Select(parameter => parameter.Type).ToArray());
                MethodCallExpression body = Expression.Call(Expression.Constant(this), typeof(NativePlugin).GetMethod(nameof(Invoke), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!,
                    Expression.Constant(contract.Name), Expression.Constant(method.Name), Expression.NewArrayInit(typeof(nint), parameters));
                Delegate callback = Expression.Lambda(delegateType, body, parameters).Compile();
                _exports.Add((contract.Name, method.Name), (callback, Marshal.GetFunctionPointerForDelegate(callback)));
            }
        }
    }
    private void Invoke(string contract, string method, nint[] arguments) {
        // Une exception gérée ne doit jamais traverser un callback appelé depuis du C++.
        try {
            lock (_gate) {
                if (_handle == 0 || _module.Invoke!(_handle, contract, method, arguments, arguments.Length) == 0) {
                    Console.Error.WriteLine($"[Omnilingua/Native:{Name}] Échec du callback : {(_handle == 0 ? "plugin fermé" : _module.Diagnostic(_handle))}");
                }
            }
        } catch (Exception exception) {
            try { Console.Error.WriteLine($"[Omnilingua/Native:{Name}] {exception.Message}"); }
            catch { /* La console peut elle-même être fermée ; ne pas franchir la frontière ABI. */ }
        }
    }
    private void Lifecycle(string operation) {
        lock (_gate) {
            ObjectDisposedException.ThrowIf(_handle == 0, this);
            int status = operation switch { "init" => _module.Init(_handle, Settings.Config?.GetRawText() ?? "{}"), "start" => _module.Start(_handle), "stop" => _module.Stop(_handle), _ => throw new ArgumentException(operation) };
            if (status == 0) { throw new InvalidOperationException($"{Name}.{operation} : {_module.Diagnostic(_handle)}"); }
        }
    }
    public void Init() => Lifecycle("init");
    public nint GetExport(string contract, string method) => !_module.DynamicCalls ? _module.Export!(_handle, contract, method) : _exports.TryGetValue((contract, method), out (Delegate Callback, nint Pointer) export) ? export.Pointer : 0;
    public void BindDependency(ContractDescription required, ContractDescription provider, Func<string, nint> resolve) {
        if (!_module.CallbackImports) {
            foreach (string method in required.Methods) { Inject(required.Name, method, resolve(method)); }
            return;
        }
        if (provider.Version != required.Version) { throw new InvalidOperationException($"{Name} : version incompatible pour {required.Name}."); }
        foreach (AbiMethodDescription method in required.Signatures ?? []) {
            AbiMethodDescription signature = provider.Signatures?.SingleOrDefault(candidate => candidate.Name == method.Name)
                ?? throw new MissingMethodException($"{Name} : signature absente pour {required.Name}.{method.Name}.");
            if (signature.ReturnType.Kind != AbiKind.Void || signature.ReturnType.Size != 0 || signature.ReturnPassing != AbiPassing.Value || signature.Parameters.Count != method.Parameters.Count ||
                signature.Parameters.Any(parameter => parameter.Type.Kind != AbiKind.Utf8 || parameter.Type.Size != IntPtr.Size || parameter.Passing != AbiPassing.Value)) {
                throw new InvalidOperationException($"{Name} : signature incompatible pour {required.Name}.{method.Name}.");
            }
        }
        foreach (string method in required.Methods) { Inject(required.Name, method, resolve(method)); }
    }
    public void Inject(string contract, string method, nint pointer) {
        if (pointer == 0) { throw new MissingMethodException($"{Name} : export nul {contract}.{method}."); }
        if (!_module.CallbackImports) {
            if (_module.Inject!(_handle, contract, method, pointer) == 0) { throw new InvalidOperationException(_module.Diagnostic(_handle)); }
            return;
        }
        ContractDescription requirement = Metadata.Dependencies.Single(item => item.Name == contract);
        AbiMethodDescription signature = requirement.Signatures!.Single(item => item.Name == method);
        Type type = AbiDelegateFactory.Create(typeof(void), signature.Parameters.Select(_ => typeof(nint)).ToArray());
        if (!_imports.TryAdd((contract, method), Marshal.GetDelegateForFunctionPointer(pointer, type))) {
            throw new InvalidOperationException($"{Name} : import duplique {contract}.{method}.");
        }
        if (requirement.Methods.All(name => _imports.ContainsKey((contract, name))) &&
            _module.BindImports!(_handle, contract, Marshal.GetFunctionPointerForDelegate(_importCallback)) == 0) {
            throw new InvalidOperationException($"{Name} : injection de {contract} impossible : {_module.Diagnostic(_handle)}");
        }
    }
    private int InvokeImport(nint handle, string contract, string method, nint[] arguments, int count) {
        try {
            if (handle != _handle || handle == 0 || count != arguments.Length) { return 0; }
            Delegate function = _imports[(contract, method)];
            function.DynamicInvoke(arguments.Cast<object?>().ToArray());
            return 1;
        } catch (Exception exception) {
            try { Console.Error.WriteLine($"[Omnilingua/Native:{Name}] {contract}.{method} : {exception}"); } catch { }
            return 0;
        }
    }
    public Task StartAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); Lifecycle("start"); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken cancellationToken) { Lifecycle("stop"); return Task.CompletedTask; }
    public void Dispose() {
        lock (_gate) {
            if (_handle == 0) { return; }
            int status = _module.Destroy(_handle);
            string? error = status == 0 ? _module.Diagnostic(_handle) : null;
            _handle = 0; _exports.Clear(); _imports.Clear();
            _module.Dispose();
            if (error is not null) { throw new InvalidOperationException(error); }
            GC.KeepAlive(_importCallback);
        }
    }
}
