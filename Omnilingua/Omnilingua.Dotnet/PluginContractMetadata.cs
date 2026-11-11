namespace Omnilingua.Dotnet;

/// <summary>Contrat compilé d'une interface, utilisable pour demander un service ou publier son implémentation.</summary>
public sealed class PluginContractMetadata<T>(ContractDescription description, Func<T, ManagedContractExport> bind) where T : class {
    /// <summary>Description indépendante du langage, comprenant signatures et types utilisés.</summary>
    public ContractDescription Description { get; } = description;

    /// <summary>Déclare l'injection de cette interface dans le plugin consommateur.</summary>
    public ServiceRequirement Require(bool optional = false) => new(Description.Name, typeof(T), Description.Version, optional, Description);

    /// <summary>Associe une implémentation aux appels directs générés de l'interface.</summary>
    public ManagedContractExport Export(T implementation) {
        ArgumentNullException.ThrowIfNull(implementation);
        return bind(implementation);
    }
}
