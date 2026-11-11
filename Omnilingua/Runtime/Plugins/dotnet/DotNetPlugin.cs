using Omnilingua.Dotnet;
using Omnilingua.Plugins.dotnet.Proxy;

namespace Omnilingua.Plugins.dotnet;

/// <summary>Adapte le cycle de vie .NET et conserve les objets injectés du consommateur.</summary>
public sealed class DotNetPlugin(PluginSettings settings, PluginMetadata metadata, IPlugin instance) : ILoadedPlugin, IPluginExportRegistry {
    private readonly Dictionary<Type, object> _services = [];
    private readonly Dictionary<string, ManagedExportAdapter> _exports = [];
    private bool _disposed;
    /// <summary>Paramètres de chargement et de configuration du plugin.</summary>
    public PluginSettings Settings { get; } = settings;
    /// <summary>Descriptions des exports et des dépendances résolues.</summary>
    public PluginMetadata Metadata { get; } = metadata;
    /// <summary>Instance du plugin chargée dans son contexte propre.</summary>
    public IPlugin Instance { get; } = instance;
    /// <summary>Nom du plugin dans la configuration du Omnilingua.</summary>
    public string Name => Settings.Name;

    /// <summary>Transmet la configuration et les objets injectés à l’instance.</summary>
    public void Init() {
        Instance.Init(new PluginContext(Settings.Config, new LocalServices(_services), this));
        object[] objects = Instance.GetExport() ?? throw new InvalidOperationException($"{Name} : tableau des exports nul.");
        ManagedServiceExport[] bindings = (Metadata.ProvidedServices ?? []).ToArray();
        foreach (object instance in objects) {
            if (instance is null || !bindings.Any(binding => binding.InterfaceType.IsInstanceOfType(instance))) {
                throw new InvalidOperationException($"{Name} : objet exporté sans contrat annoncé.");
            }
        }
        foreach (ManagedServiceExport binding in bindings) {
            object[] matches = objects.Where(binding.InterfaceType.IsInstanceOfType).ToArray();
            if (matches.Length != 1) { throw new InvalidOperationException($"{Name} : {binding.Description.Name} exige exactement une implémentation exportée."); }
            Register(binding.Bind(matches[0]));
        }
    }

    /// <summary>Crée le proxy générique dans le contexte de types du consommateur.</summary>
    public void BindService(ServiceRequirement requirement, ContractDescription provider, Func<string, nint> resolve) {
        if (provider.Version != requirement.Version) { throw new InvalidOperationException($"{Name} : version incompatible pour {requirement.Contract}."); }
        object proxy = ContractProxy.Create(requirement.InterfaceType, provider, resolve);
        if (!_services.TryAdd(requirement.InterfaceType, proxy)) { throw new InvalidOperationException($"{Name} : interface injectée plusieurs fois."); }
    }

    /// <summary>Résout un export opaque publié par l’instance.</summary>
    public nint GetExport(string contract, string method) => _exports.TryGetValue(contract, out ManagedExportAdapter? export) ? export.Resolve(method) : Instance.GetExport(contract, method);

    /// <summary>Enregistre les appels g?n?r?s d'un contrat annonc? dans les m?tadonn?es.</summary>
    public void Register(ManagedContractExport export) {
        ContractDescription declared = Metadata.Exports.SingleOrDefault(contract => contract.Name == export.Description.Name)
            ?? throw new InvalidOperationException($"{Name} : contrat export? non annonc? : {export.Description.Name}.");
        if (declared != export.Description || !declared.Methods.Order().SequenceEqual(export.Methods.Select(method => method.Description.Name).Order()) ||
            export.Methods.Any(method => declared.Signatures?.Contains(method.Description) != true)) {
            throw new InvalidOperationException($"{Name} : signatures export?es incoh?rentes pour {declared.Name}.");
        }
        if (_exports.ContainsKey(declared.Name)) { throw new InvalidOperationException($"{Name} : contrat d?j? enregistr? : {declared.Name}."); }
        _exports.Add(declared.Name, new ManagedExportAdapter(export));
    }

    /// <summary>Transmet une injection brute explicitement demandée par un consommateur.</summary>
    public void Inject(string contract, string method, nint pointer) => Instance.Inject(contract, method, pointer);

    /// <summary>Attend le démarrage du plugin.</summary>
    public Task StartAsync(CancellationToken cancellationToken) => Instance.StartAsync(cancellationToken);

    /// <summary>Attend l’arrêt du plugin.</summary>
    public Task StopAsync(CancellationToken cancellationToken) => Instance.StopAsync(cancellationToken);

    /// <summary>Libère l’instance après son arrêt et abandonne ses proxies.</summary>
    public void Dispose() {
        if (_disposed) { return; }
        _disposed = true;
        (Instance as IDisposable)?.Dispose();
        foreach (ManagedExportAdapter export in _exports.Values) { export.Dispose(); }
        _exports.Clear();
        _services.Clear();
    }

    private sealed class LocalServices(IReadOnlyDictionary<Type, object> services) : IServiceProvider {
        public object? GetService(Type type) => services.GetValueOrDefault(type);
    }
}
