using System.Text.Json;

namespace Omnilingua.Dotnet;

/// <summary>Configuration et objets de contrats construits par le Omnilingua pour un consommateur.</summary>
public sealed record PluginContext(JsonElement? Configuration, IServiceProvider Services, IPluginExportRegistry? Exports = null) {
    /// <summary>Obtient le proxy correspondant à l’interface locale demandée.</summary>
    public T GetRequiredService<T>() where T : class => Services.GetService(typeof(T)) as T
        ?? throw new InvalidOperationException($"Service non injecté : {typeof(T).FullName}.");
}
/// <summary>Associe un contrat et une version à l’interface locale que le consommateur souhaite recevoir.</summary>
public sealed record ServiceRequirement(string Contract, Type InterfaceType, uint Version = 1, bool Optional = false, ContractDescription? Description = null);
