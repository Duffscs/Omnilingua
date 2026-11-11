using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Omnilingua.Plugins.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeOptions {
    [MarshalAs(UnmanagedType.LPUTF8Str)] public string? RuntimeLibrary;
    [MarshalAs(UnmanagedType.LPUTF8Str)] public string? RuntimeHome;
    [MarshalAs(UnmanagedType.LPUTF8Str)] public string? RuntimeExecutable;
    [MarshalAs(UnmanagedType.LPUTF8Str)] public string? Sdk;
    [MarshalAs(UnmanagedType.LPUTF8Str)] public string? Assembly;
    [MarshalAs(UnmanagedType.LPUTF8Str)] public string? EntryPoint;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeApi {
    public uint Capabilities, RuntimeVersion;
    public nint Create, Metadata, Init, Start, Stop, Export, Inject, Destroy, Error, Invoke, BindImports;
}

/// <summary>Résout l'unique point d'entrée commun aux modules C++, Java et Python.</summary>
internal sealed class NativeModule : IDisposable {
    private static readonly ConcurrentDictionary<string, nint> ProcessLibraries = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint GetApiFn();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate nint CreateFn(in NativeOptions options);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate nint PointerFn(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int StatusFn(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int InitFn(nint handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string config);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate nint ExportFn(nint handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string contract, [MarshalAs(UnmanagedType.LPUTF8Str)] string method);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int InjectFn(nint handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string contract, [MarshalAs(UnmanagedType.LPUTF8Str)] string method, nint pointer);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int InvokeFn(nint handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string contract, [MarshalAs(UnmanagedType.LPUTF8Str)] string method, [In] nint[] arguments, int count);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int BindImportsFn(nint handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string contract, nint callback);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int ImportCallbackFn(nint handle,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string contract, [MarshalAs(UnmanagedType.LPUTF8Str)] string method,
        [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] nint[] arguments, int count);

    private nint _library;
    internal bool DynamicCalls { get; }
    internal bool CallbackImports { get; }
    private readonly bool _processLifetime;
    internal uint RuntimeVersion { get; }
    internal readonly CreateFn Create;
    internal readonly PointerFn Metadata, Error;
    internal readonly InitFn Init;
    internal readonly StatusFn Start, Stop, Destroy;
    internal readonly ExportFn? Export;
    internal readonly InjectFn? Inject;
    internal readonly InvokeFn? Invoke;
    internal readonly BindImportsFn? BindImports;

    private sealed record Functions(CreateFn Create, PointerFn Metadata, PointerFn Error, InitFn Init,
        StatusFn Start, StatusFn Stop, StatusFn Destroy, ExportFn? Export, InjectFn? Inject, InvokeFn? Invoke, BindImportsFn? BindImports);

    private NativeModule(nint library, NativeApi api, Functions functions) {
        _library = library;
        DynamicCalls = (api.Capabilities & 1) != 0;
        CallbackImports = (api.Capabilities & 2) != 0;
        _processLifetime = (api.Capabilities & 4) != 0;
        RuntimeVersion = api.RuntimeVersion;
        Create = functions.Create; Metadata = functions.Metadata; Error = functions.Error;
        Init = functions.Init; Start = functions.Start; Stop = functions.Stop; Destroy = functions.Destroy;
        Export = functions.Export; Inject = functions.Inject; Invoke = functions.Invoke; BindImports = functions.BindImports;
    }

    internal static bool TryLoad(string path, [NotNullWhen(true)] out NativeModule? module, out string? error) {
        module = null; error = null;
        nint library = 0;
        try {
            string fullPath = Path.GetFullPath(path);
            library = NativeLibrary.Load(fullPath);
            GetApiFn getApi = Load<GetApiFn>(NativeLibrary.GetExport(library, "OmnilinguaPlugin_GetApi"));
            nint pointer = getApi();
            if (pointer == 0) { throw new InvalidDataException("Table native absente."); }
            NativeApi api = Marshal.PtrToStructure<NativeApi>(pointer);
            if ((api.Capabilities & ~7u) != 0) { throw new InvalidDataException("Capacités natives inconnues."); }
            Functions functions = new(Load<CreateFn>(api.Create), Load<PointerFn>(api.Metadata), Load<PointerFn>(api.Error),
                Load<InitFn>(api.Init), Load<StatusFn>(api.Start), Load<StatusFn>(api.Stop), Load<StatusFn>(api.Destroy),
                (api.Capabilities & 1) == 0 ? Load<ExportFn>(api.Export) : null,
                (api.Capabilities & 2) == 0 ? Load<InjectFn>(api.Inject) : null,
                (api.Capabilities & 1) != 0 ? Load<InvokeFn>(api.Invoke) : null,
                (api.Capabilities & 2) != 0 ? Load<BindImportsFn>(api.BindImports) : null);
            module = new NativeModule(library, api, functions);
            if (module._processLifetime && !ProcessLibraries.TryAdd(fullPath, library)) {
                NativeLibrary.Free(library);
            }
            library = 0;
            return true;
        } catch (Exception exception) {
            error = exception.GetBaseException().Message;
            return false;
        } finally { if (library != 0) { NativeLibrary.Free(library); } }
    }
    private static T Load<T>(nint pointer) where T : Delegate {
        if (pointer == 0) { throw new InvalidDataException("Fonction native obligatoire absente."); }
        return Marshal.GetDelegateForFunctionPointer<T>(pointer);
    }
    internal string Diagnostic(nint handle) => Marshal.PtrToStringUTF8(Error(handle)) ?? "Erreur native sans diagnostic.";
    public void Dispose() {
        nint library = Interlocked.Exchange(ref _library, 0);
        // Les workers et les runtimes utilisateur restent actifs jusqu'à la fin du processus.
        if (library != 0 && !_processLifetime) { NativeLibrary.Free(library); }
    }
}
