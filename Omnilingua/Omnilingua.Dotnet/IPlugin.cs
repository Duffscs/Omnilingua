namespace Omnilingua.Dotnet;

/// <summary>Protocole commun des plugins .NET ; les contrats métier restent définis par les plugins.</summary>
public interface IPlugin {
    /// <summary>Déclare les exports et les interfaces locales que le Omnilingua doit fournir.</summary>
    PluginMetadata GetMetadata() => new([], []);
    /// <summary>Initialise le plugin après résolution des objets de contrat de son contexte.</summary>
    void Init(PluginContext context) { }
    /// <summary>Retourne un point d’entrée opaque pour une opération publiée.</summary>
    nint GetExport(string contract, string method) => 0;
    /// <summary>Retourne les objets qui implémentent les contrats annoncés par PluginExport.</summary>
    object[] GetExport() => [];
    /// <summary>Compatibilité avec les consommateurs qui demandent explicitement une injection brute.</summary>
    void Inject(string contract, string method, nint pointer) =>
        throw new InvalidOperationException($"Injection brute non implémentée pour {contract}.{method}.");
    /// <summary>Démarre le plugin et retourne la tâche réelle du démarrage.</summary>
    Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    /// <summary>Arrête le plugin avant la libération de ses fournisseurs.</summary>
    Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
