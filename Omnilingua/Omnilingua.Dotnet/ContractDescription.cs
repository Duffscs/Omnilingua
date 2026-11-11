namespace Omnilingua.Dotnet;

public sealed record ContractDescription(
    string Name,
    IReadOnlyList<string> Methods,
    uint Version = 1,
    IReadOnlyList<AbiMethodDescription>? Signatures = null,
    bool Optional = false,
    IReadOnlyList<AbiTypeDescription>? Types = null
);
