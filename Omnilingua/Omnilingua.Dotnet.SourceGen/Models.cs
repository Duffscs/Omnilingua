using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Omnilingua.Dotnet.SourceGen;

internal sealed record AnalysisResult(ContractModel? Contract, Location Location, string? Error);
internal sealed record ContractModel(string Namespace, string InterfaceName, bool Public, string HintName, string ContractName,
    uint Version, ImmutableArray<MethodModel> Methods, ImmutableArray<TypeModel> Types);
internal sealed record MethodModel(string Name, string EscapedName, string ReturnType, string ReturnId, ImmutableArray<ParameterModel> Parameters);
internal sealed record ParameterModel(string Name, string Type, string TypeId, bool Out);
internal sealed record TypeModel(string Id, string Name, string Kind, ImmutableArray<FieldModel> Fields, string? ElementId,
    ImmutableArray<EnumValueModel> EnumValues);
internal sealed record FieldModel(string Name, string TypeId, bool Optional);
internal sealed record EnumValueModel(string Name, string Value);
