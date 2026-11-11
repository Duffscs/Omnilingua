namespace Omnilingua.Dotnet;

/// <summary>Fonctions gérées compilées et description ABI d’un contrat publié.</summary>
public sealed record ManagedContractExport(ContractDescription Description, IReadOnlyList<ManagedMethodExport> Methods);

/// <summary>Appel typé produit par le générateur ; les paramètres out sont réécrits dans le tableau.</summary>
public sealed record ManagedMethodExport(AbiMethodDescription Description, Type ReturnType,
    IReadOnlyList<Type> ParameterTypes, Func<object?[], object?> Invoke);

/// <summary>Enregistre les points d’entrée générés pendant l’initialisation d’un plugin.</summary>
public interface IPluginExportRegistry {
    /// <summary>Publie un contrat annoncé dans GetMetadata avant l’utilisation de ses pointeurs.</summary>
    void Register(ManagedContractExport contract);
}
