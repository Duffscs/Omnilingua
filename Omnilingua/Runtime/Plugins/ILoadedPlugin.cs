using Omnilingua.Dotnet;

namespace Omnilingua.Plugins;

public interface ILoadedPlugin : IDisposable {
    string Name { get; }
    PluginSettings Settings { get; }
    PluginMetadata Metadata { get; }

    void Init();
    nint GetExport(string contract, string method);
    void Inject(string contract, string method, nint pointer);
    /// <summary>Demande au runtime de construire un objet à partir du contrat publié par le fournisseur.</summary>
    void BindService(ServiceRequirement requirement, ContractDescription provider, Func<string, nint> resolve) =>
        throw new NotSupportedException("Ce runtime ne prend pas en charge les interfaces .NET.");
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
