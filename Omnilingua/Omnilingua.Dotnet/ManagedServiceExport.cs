namespace Omnilingua.Dotnet;

/// <summary>Associe un contrat annoncé aux objets et appels compilés qui l'implémentent.</summary>
public sealed record ManagedServiceExport(Type InterfaceType, ContractDescription Description, Func<object, ManagedContractExport> Bind);
