using System.Runtime.InteropServices;
using Omnilingua.Dotnet;

namespace Omnilingua.Plugins.Native;

/// <summary>Copie et vérifie les descriptions binaires génériques publiées par un fournisseur.</summary>
internal sealed class AbiMetadataReader {
    private readonly Dictionary<nint, AbiTypeDescription> _types = [];
    private readonly HashSet<nint> _visiting = [];

    public IReadOnlyList<AbiMethodDescription> ReadMethods(nint pointer, uint count) {
        CheckArray(pointer, count, 128);
        List<AbiMethodDescription> methods = [];
        for (int index = 0; index < count; index++) {
            AbiMethodLayout method = Marshal.PtrToStructure<AbiMethodLayout>(pointer + index * Marshal.SizeOf<AbiMethodLayout>());
            CheckArray(method.Parameters, method.ParameterCount, 32);
            List<AbiParameterDescription> parameters = [];
            for (int parameterIndex = 0; parameterIndex < method.ParameterCount; parameterIndex++) {
                AbiParameterLayout parameter = Marshal.PtrToStructure<AbiParameterLayout>(method.Parameters + parameterIndex * Marshal.SizeOf<AbiParameterLayout>());
                if (!Enum.IsDefined(parameter.Passing)) { throw new InvalidDataException("Mode de passage ABI inconnu."); }
                parameters.Add(new(Name(parameter.Name), ReadType(parameter.Type), parameter.Passing));
            }
            if (!Enum.IsDefined(method.ReturnPassing) || method.ReturnPassing == AbiPassing.Out) { throw new InvalidDataException("Mode de retour ABI invalide."); }
            methods.Add(new(Name(method.Name), ReadType(method.ReturnType), method.ReturnPassing, parameters));
        }
        if (methods.Select(method => method.Name).Distinct(StringComparer.Ordinal).Count() != methods.Count) { throw new InvalidDataException("Méthode ABI dupliquée."); }
        return methods;
    }

    private AbiTypeDescription ReadType(nint pointer) {
        if (_types.TryGetValue(pointer, out AbiTypeDescription? known)) { return known; }
        if (pointer == 0 || !_visiting.Add(pointer) || _visiting.Count > 16) { throw new InvalidDataException("Description ABI nulle, récursive ou trop profonde."); }
        AbiTypeLayout type = Marshal.PtrToStructure<AbiTypeLayout>(pointer);
        if (!Enum.IsDefined(type.Kind) || type.Size > 65536 || type.MaxCount > 65536) { throw new InvalidDataException("Type ABI non pris en charge."); }
        CheckArray(type.Fields, type.FieldCount, 128);
        List<AbiFieldDescription> fields = [];
        for (int index = 0; index < type.FieldCount; index++) {
            AbiFieldLayout field = Marshal.PtrToStructure<AbiFieldLayout>(type.Fields + index * Marshal.SizeOf<AbiFieldLayout>());
            AbiTypeDescription fieldType = ReadType(field.Type);
            if (field.Offset > type.Size || fieldType.Size > type.Size - field.Offset ||
                (field.PresenceOffset >= 0 && field.PresenceOffset > (int)type.Size - 4) ||
                (field.CountOffset >= 0 && field.CountOffset > (int)type.Size - 4)) {
                throw new InvalidDataException("Champ ABI situé hors de sa structure.");
            }
            fields.Add(new(Name(field.Name), fieldType, checked((int)field.Offset), field.PresenceOffset,
                field.PresenceMask, field.HasNullSentinel != 0, field.NullSentinel, field.CountOffset));
        }
        if (fields.Select(field => field.Name).Distinct(StringComparer.Ordinal).Count() != fields.Count) { throw new InvalidDataException("Champ ABI dupliqué."); }
        AbiTypeDescription result = new(type.Kind, Name(type.Name), checked((int)type.Size), fields,
            type.ElementType == 0 ? null : ReadType(type.ElementType), checked((int)type.MaxCount));
        _visiting.Remove(pointer);
        _types.Add(pointer, result);
        return result;
    }

    private static string Name(nint pointer) => pointer == 0 ? throw new InvalidDataException("Nom ABI nul.") :
        Marshal.PtrToStringUTF8(pointer) ?? throw new InvalidDataException("Nom ABI invalide.");
    private static void CheckArray(nint pointer, uint count, uint maximum) {
        if (count > maximum || (count != 0 && pointer == 0)) { throw new InvalidDataException("Tableau de métadonnées ABI invalide."); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct AbiTypeLayout {
        public AbiKind Kind; public nint Name; public uint Size; public nint Fields; public uint FieldCount;
        public nint ElementType; public uint MaxCount;
    }
    [StructLayout(LayoutKind.Sequential)] private struct AbiFieldLayout {
        public nint Name; public nint Type; public uint Offset; public int PresenceOffset; public uint PresenceMask;
        public uint HasNullSentinel; public long NullSentinel; public int CountOffset;
    }
    [StructLayout(LayoutKind.Sequential)] private struct AbiParameterLayout { public nint Name; public nint Type; public AbiPassing Passing; }
    [StructLayout(LayoutKind.Sequential)] private struct AbiMethodLayout {
        public nint Name; public nint ReturnType; public AbiPassing ReturnPassing; public nint Parameters; public uint ParameterCount;
    }
}
