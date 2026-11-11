namespace Omnilingua.Dotnet;

public sealed record PluginMetadata(
    IReadOnlyList<ContractDescription> Dependencies,
    IReadOnlyList<ContractDescription> Exports,
    IReadOnlyList<ServiceRequirement>? RequiredServices = null,
    IReadOnlyList<ManagedServiceExport>? ProvidedServices = null
);
