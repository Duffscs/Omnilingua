namespace Omnilingua.Dotnet;

/// <summary>Champ logique dont les positions binaires sont calculées sans réflexion.</summary>
public sealed record AbiMemberDefinition(string Name, AbiTypeDescription Type, bool Optional = false);

/// <summary>Construit les dispositions C ABI à partir des descriptions compilées du générateur.</summary>
public static class AbiTypeLayouts {
    /// <summary>Décrit un scalaire ou une enum ayant la représentation d’un scalaire.</summary>
    public static AbiTypeDescription Primitive(AbiKind kind, string name, IReadOnlyList<AbiEnumValueDescription>? values = null) =>
        new(kind, name, kind switch {
            AbiKind.Void => 0, AbiKind.Int32 or AbiKind.UInt32 or AbiKind.Boolean32 => 4,
            AbiKind.UInt64 => 8, AbiKind.Utf8 => IntPtr.Size,
            _ => throw new ArgumentException("Un type scalaire est requis.", nameof(kind))
        }, EnumValues: values);

    /// <summary>Décrit un tableau pointé ; le compteur est porté par le champ qui l’utilise.</summary>
    public static AbiTypeDescription Array(string name, AbiTypeDescription element, int maxCount = 256) =>
        new(AbiKind.Array, name, IntPtr.Size, ElementType: element, MaxCount: maxCount);

    /// <summary>Décrit une liste autonome sous forme de pointeur et compteur.</summary>
    public static AbiTypeDescription Sequence(string name, AbiTypeDescription element, int maxCount = 256) =>
        new(AbiKind.Sequence, name, Align(IntPtr.Size + 4, IntPtr.Size),
            [new("Items", Array(name, element, maxCount), 0, CountOffset: IntPtr.Size)], element, maxCount);

    /// <summary>Calcule champs, compteurs et bits de présence avec l’alignement naturel de la plateforme.</summary>
    public static AbiTypeDescription Record(string name, IReadOnlyList<AbiMemberDefinition> members) {
        if (members.Count == 0) { throw new ArgumentException("Un modèle ABI ne peut pas être vide.", nameof(members)); }
        List<AbiFieldDescription> fields = [];
        int offset = 0, alignment = 1, optionalCount = 0;
        foreach (AbiMemberDefinition member in members) {
            int fieldAlignment = Alignment(member.Type);
            alignment = Math.Max(alignment, fieldAlignment);
            offset = Align(offset, fieldAlignment);
            int countOffset = member.Type.Kind == AbiKind.Array ? checked(offset + IntPtr.Size) : -1;
            fields.Add(new(member.Name, member.Type, offset));
            if (member.Optional) { optionalCount++; }
            offset = checked(offset + member.Type.Size + (countOffset >= 0 ? 4 : 0));
            if (countOffset >= 0) { fields[^1] = fields[^1] with { CountOffset = countOffset }; }
        }
        int presenceOffset = Align(offset, 4), optionalIndex = 0;
        for (int index = 0; index < members.Count; index++) {
            if (!members[index].Optional) { continue; }
            fields[index] = fields[index] with { PresenceOffset = presenceOffset + optionalIndex / 32 * 4, PresenceMask = 1u << (optionalIndex % 32) };
            optionalIndex++;
        }
        if (optionalCount > 0) { offset = checked(presenceOffset + (optionalCount + 31) / 32 * 4); alignment = Math.Max(alignment, 4); }
        return new(AbiKind.Record, name, Align(offset, alignment), fields.ToArray());
    }

    private static int Alignment(AbiTypeDescription type) => type.Kind switch {
        AbiKind.Record => Math.Max(4, (type.Fields ?? []).Max(field => Alignment(field.Type))),
        AbiKind.Array or AbiKind.Sequence or AbiKind.Utf8 => IntPtr.Size,
        _ => Math.Max(1, Math.Min(type.Size, 8))
    };
    private static int Align(int offset, int alignment) => checked((offset + alignment - 1) / alignment * alignment);
}
