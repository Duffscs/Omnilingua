namespace Omnilingua.Dotnet;

/// <summary>Types binaires indépendants des langages pris en charge par le runtime.</summary>
public enum AbiKind { Void = 0, Int32 = 1, UInt32 = 2, UInt64 = 3, Boolean32 = 4, Utf8 = 5, Record = 6, Array = 7, Sequence = 8 }
/// <summary>Mode de passage d’une valeur à travers la frontière C ABI.</summary>
public enum AbiPassing { Value, Reference, Out }
/// <summary>Décrit un type binaire et ses champs éventuels, sans référence à une classe métier .NET.</summary>
public sealed record AbiTypeDescription(AbiKind Kind, string Name, int Size,
    IReadOnlyList<AbiFieldDescription>? Fields = null, AbiTypeDescription? ElementType = null, int MaxCount = 256,
    IReadOnlyList<AbiEnumValueDescription>? EnumValues = null);
/// <summary>Nom et valeur numérique d’un membre d’enum, sans type métier partagé.</summary>
public sealed record AbiEnumValueDescription(string Name, string Value);
/// <summary>Décrit la position d’un champ, ses règles de présence, son éventuelle sentinelle et son compteur de tableau.</summary>
public sealed record AbiFieldDescription(string Name, AbiTypeDescription Type, int Offset,
    int PresenceOffset = -1, uint PresenceMask = 0, bool HasNullSentinel = false, long NullSentinel = -1, int CountOffset = -1);
/// <summary>Décrit un paramètre nommé et son mode de passage natif.</summary>
public sealed record AbiParameterDescription(string Name, AbiTypeDescription Type, AbiPassing Passing);
/// <summary>Décrit une fonction Cdecl synchrone. Les sorties pointées sont empruntées et copiées avant le prochain appel.</summary>
public sealed record AbiMethodDescription(string Name, AbiTypeDescription ReturnType, AbiPassing ReturnPassing,
    IReadOnlyList<AbiParameterDescription> Parameters);
