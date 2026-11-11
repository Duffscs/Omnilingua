using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using Omnilingua.Dotnet;

namespace Omnilingua.Plugins.dotnet.Proxy;

/// <summary>Convertit les types locaux à partir de descriptions binaires, sans connaissance d’un contrat métier.</summary>
internal sealed class AbiMarshaller : IDisposable {
    private readonly List<Action> _releases = [];

    public static Type RawType(AbiTypeDescription type, AbiPassing passing) {
        Type raw = passing == AbiPassing.Reference ? typeof(nint) : type.Kind switch {
            AbiKind.Void => typeof(void), AbiKind.Int32 or AbiKind.Boolean32 => typeof(int), AbiKind.UInt32 => typeof(uint),
            AbiKind.UInt64 => typeof(ulong), AbiKind.Utf8 => typeof(nint),
            _ when passing == AbiPassing.Out => typeof(nint),
            _ => throw new NotSupportedException($"Type {type.Name} : les structures doivent être passées par pointeur.")
        };
        return passing == AbiPassing.Out ? raw.MakeByRefType() : raw;
    }

    public static void Validate(AbiTypeDescription abi, Type local) {
        local = Nullable.GetUnderlyingType(local) ?? local;
        if (local.IsEnum) { local = Enum.GetUnderlyingType(local); }
        bool valid = abi.Kind switch {
            AbiKind.Void => local == typeof(void), AbiKind.Int32 => local == typeof(int), AbiKind.UInt32 => local == typeof(uint),
            AbiKind.UInt64 => local == typeof(ulong), AbiKind.Boolean32 => local == typeof(bool),
            AbiKind.Utf8 => local == typeof(string), AbiKind.Sequence => CollectionElement(local) is not null && abi.ElementType is not null,
            AbiKind.Record => local.IsClass && !local.IsAbstract && local.GetConstructor(Type.EmptyTypes) is not null && local.Name == abi.Name,
            AbiKind.Array => CollectionElement(local) is not null && abi.ElementType is not null,
            _ => false
        };
        if (!valid) { throw new InvalidOperationException($"Type incompatible : {local.FullName} ne correspond pas à {abi.Name} ({abi.Kind})."); }
        int expectedSize = abi.Kind switch {
            AbiKind.Int32 or AbiKind.UInt32 or AbiKind.Boolean32 => 4, AbiKind.UInt64 => 8,
            AbiKind.Utf8 or AbiKind.Array => IntPtr.Size, AbiKind.Void => 0, _ => abi.Size
        };
        if (abi.Size != expectedSize || (abi.Kind == AbiKind.Record && abi.Size <= 0)) { throw new InvalidOperationException($"Taille ABI incohérente pour {abi.Name}."); }
        if (abi.Kind == AbiKind.Sequence) {
            AbiFieldDescription? items = abi.Fields?.SingleOrDefault();
            if (abi.Size <= 0 || items is null || items.Type.Kind != AbiKind.Array || items.Offset < 0 ||
                items.Offset > abi.Size - IntPtr.Size || items.CountOffset < 0 || items.CountOffset > abi.Size - 4 ||
                items.Type.ElementType?.Name != abi.ElementType!.Name || abi.MaxCount is < 1 or > 65536) {
                throw new InvalidOperationException($"Description de liste invalide : {abi.Name}.");
            }
            Validate(abi.ElementType!, CollectionElement(local)!);
        }
        if (abi.Kind == AbiKind.Array) {
            if (abi.MaxCount is < 1 or > 65536) { throw new InvalidOperationException("Seuls les tableaux bornés de structures sont pris en charge."); }
            Validate(abi.ElementType!, CollectionElement(local)!);
        }
        if (abi.Kind == AbiKind.Record) {
            foreach (AbiFieldDescription field in abi.Fields ?? []) {
                PropertyInfo property = Property(local, field.Name);
                if (field.Offset < 0 || field.Offset > abi.Size - field.Type.Size || field.PresenceOffset < -1 || field.CountOffset < -1 ||
                    field.PresenceOffset > abi.Size - 4 || field.CountOffset > abi.Size - 4) {
                    throw new InvalidOperationException($"Disposition ABI invalide : {abi.Name}.{field.Name}.");
                }
                if (field.Type.Kind is AbiKind.Void or AbiKind.Sequence) { throw new InvalidOperationException($"Champ imbriqué non pris en charge : {field.Name}."); }
                if ((field.HasNullSentinel || field.PresenceOffset >= 0) && property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null) {
                    throw new InvalidOperationException($"Le champ {field.Name} doit être nullable.");
                }
                if (field.HasNullSentinel && field.Type.Kind is not (AbiKind.Int32 or AbiKind.Boolean32)) { throw new InvalidOperationException("Sentinelle nullable non prise en charge."); }
                Validate(field.Type, property.PropertyType);
                if (field.Type.Kind == AbiKind.Array && field.CountOffset < 0) { throw new InvalidOperationException($"Compteur de tableau absent : {field.Name}."); }
            }
        }
    }

    private static Type? CollectionElement(Type local) => local.IsArray ? local.GetElementType() : local.IsGenericType &&
        (local.GetGenericTypeDefinition() == typeof(List<>) || local.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)) ? local.GetGenericArguments()[0] : null;

    public object? Write(AbiTypeDescription type, object? value) => type.Kind switch {
        AbiKind.Record => value is null ? (nint)0 : WriteRecord(type, value),
        AbiKind.Sequence => value is null ? (nint)0 : WriteSequence(type, value),
        AbiKind.Utf8 => Utf8(value as string),
        AbiKind.Int32 => Convert.ToInt32(value), AbiKind.UInt32 => Convert.ToUInt32(value), AbiKind.UInt64 => Convert.ToUInt64(value),
        AbiKind.Boolean32 => value is true ? 1 : 0,
        _ => throw new NotSupportedException($"Écriture ABI non prise en charge : {type.Name}.")
    };

    public object? Read(AbiTypeDescription type, Type local, object? raw) {
        if (local.IsByRef) { local = local.GetElementType()!; }
        Type target = Nullable.GetUnderlyingType(local) ?? local;
        if (type.Kind == AbiKind.Void) { return null; }
        if (type.Kind is AbiKind.Record or AbiKind.Utf8 or AbiKind.Sequence) {
            nint pointer = (nint)(raw ?? 0);
            if (pointer == 0) { return null; }
            if (type.Kind == AbiKind.Record) { return ReadRecord(type, target, pointer); }
            if (type.Kind == AbiKind.Sequence) { return ReadSequence(type, target, pointer); }
            return Marshal.PtrToStringUTF8(pointer);
        }
        if (target.IsEnum) { return Enum.ToObject(target, raw!); }
        return type.Kind == AbiKind.Boolean32 ? Convert.ToInt32(raw) switch {
            0 => false, 1 => true, _ => throw new InvalidDataException("Booléen ABI invalide.")
        } : Convert.ChangeType(raw, target);
    }

    private object ReadSequence(AbiTypeDescription type, Type local, nint pointer) {
        AbiFieldDescription items = type.Fields!.Single();
        int count = Marshal.ReadInt32(pointer + items.CountOffset);
        nint buffer = Marshal.ReadIntPtr(pointer + items.Offset);
        if (count < 0 || count > type.MaxCount || (count > 0 && buffer == 0)) { throw new InvalidDataException("Liste abi invalide."); }
        return ReadCollection(type.ElementType!, local, buffer, count);
    }

    private object ReadCollection(AbiTypeDescription element, Type local, nint buffer, int count) {
        Type elementType = CollectionElement(local)!;
        Array array = Array.CreateInstance(elementType, count);
        for (int index = 0; index < count; index++) {
            nint address = buffer + index * element.Size;
            object? value = element.Kind == AbiKind.Record ? ReadRecord(element, elementType, address) :
                Read(element, elementType, element.Kind == AbiKind.Utf8 ? Marshal.ReadIntPtr(address) : ReadNumber(address, element.Kind));
            array.SetValue(value, index);
        }
        if (local.IsArray) { return array; }
        IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (object? value in array) { list.Add(value); }
        return list;
    }

    private nint WriteCollection(AbiTypeDescription type, object value, out int count) {
        object?[] items = ((IEnumerable)value).Cast<object?>().ToArray();
        count = items.Length;
        if (count > type.MaxCount) { throw new ArgumentException($"Collection {type.Name} trop grande."); }
        AbiTypeDescription element = type.ElementType!;
        nint buffer = Allocate(checked(count * element.Size));
        for (int index = 0; index < count; index++) {
            nint address = buffer + index * element.Size;
            if (element.Kind == AbiKind.Record) { FillRecord(element, items[index]!, address); }
            else if (element.Kind == AbiKind.Utf8) { Marshal.WriteIntPtr(address, Utf8(items[index] as string)); }
            else { WriteNumber(address, element.Kind, element.Kind == AbiKind.Boolean32 ? (items[index] is true ? 1 : 0) : items[index]!); }
        }
        return buffer;
    }

    private nint WriteSequence(AbiTypeDescription type, object value) {
        nint pointer = Allocate(type.Size);
        AbiFieldDescription field = type.Fields!.Single();
        nint buffer = WriteCollection(type, value, out int count);
        Marshal.WriteIntPtr(pointer + field.Offset, buffer);
        Marshal.WriteInt32(pointer + field.CountOffset, count);
        return pointer;
    }

    private nint WriteRecord(AbiTypeDescription type, object value) {
        nint pointer = Allocate(type.Size);
        FillRecord(type, value, pointer);
        return pointer;
    }
    private void FillRecord(AbiTypeDescription type, object value, nint pointer) {
        foreach (AbiFieldDescription field in type.Fields ?? []) {
            object? member = Property(value.GetType(), field.Name).GetValue(value);
            if (member is null) {
                if (field.HasNullSentinel) { WriteNumber(pointer + field.Offset, field.Type.Kind, field.NullSentinel); }
                continue;
            }
            if (field.PresenceOffset >= 0) {
                uint bits = unchecked((uint)Marshal.ReadInt32(pointer + field.PresenceOffset));
                Marshal.WriteInt32(pointer + field.PresenceOffset, unchecked((int)(bits | field.PresenceMask)));
            }
            if (field.Type.Kind == AbiKind.Array) {
                nint buffer = WriteCollection(field.Type, member, out int count);
                Marshal.WriteIntPtr(pointer + field.Offset, buffer);
                Marshal.WriteInt32(pointer + field.CountOffset, count);
            } else if (field.Type.Kind == AbiKind.Record) { FillRecord(field.Type, member, pointer + field.Offset);
            } else if (field.Type.Kind == AbiKind.Utf8) { Marshal.WriteIntPtr(pointer + field.Offset, Utf8(member as string)); }
            else { WriteNumber(pointer + field.Offset, field.Type.Kind, field.Type.Kind == AbiKind.Boolean32 ? ((bool)member ? 1 : 0) : member); }
        }
    }
    private object ReadRecord(AbiTypeDescription type, Type local, nint pointer) {
        object result = Activator.CreateInstance(local)!;
        foreach (AbiFieldDescription field in type.Fields ?? []) {
            PropertyInfo property = Property(local, field.Name);
            if (field.PresenceOffset >= 0 && (unchecked((uint)Marshal.ReadInt32(pointer + field.PresenceOffset)) & field.PresenceMask) == 0) {
                property.SetValue(result, null); continue;
            }
            if (field.HasNullSentinel && Convert.ToInt64(ReadNumber(pointer + field.Offset, field.Type.Kind)) == field.NullSentinel) {
                property.SetValue(result, null); continue;
            }
            if (field.Type.Kind == AbiKind.Array) {
                int count = Marshal.ReadInt32(pointer + field.CountOffset);
                nint buffer = Marshal.ReadIntPtr(pointer + field.Offset);
                if (count < 0 || count > field.Type.MaxCount || (count > 0 && buffer == 0)) { throw new InvalidDataException($"Tableau natif invalide : {field.Name}."); }
                property.SetValue(result, ReadCollection(field.Type.ElementType!, property.PropertyType, buffer, count));
            } else if (field.Type.Kind == AbiKind.Record) {
                property.SetValue(result, ReadRecord(field.Type, property.PropertyType, pointer + field.Offset));
            } else {
                object? raw = field.Type.Kind == AbiKind.Utf8 ? Marshal.ReadIntPtr(pointer + field.Offset) : ReadNumber(pointer + field.Offset, field.Type.Kind);
                property.SetValue(result, Read(field.Type, property.PropertyType, raw));
            }
        }
        return result;
    }

    private nint Allocate(int count) {
        nint pointer = Marshal.AllocHGlobal(count);
        _releases.Add(() => Marshal.FreeHGlobal(pointer));
        if (count > 0) { Marshal.Copy(new byte[count], 0, pointer, count); }
        return pointer;
    }
    private nint Utf8(string? value) {
        if (value is null) { return 0; }
        if (value.Contains('\0') || value.Length > 32768) { throw new ArgumentException("Chaîne ABI trop longue ou contenant un caractère nul."); }
        nint pointer = Marshal.StringToCoTaskMemUTF8(value);
        _releases.Add(() => Marshal.FreeCoTaskMem(pointer));
        return pointer;
    }
    private static PropertyInfo Property(Type type, string name) {
        PropertyInfo? property = type.GetProperty(name);
        return property is { CanRead: true, CanWrite: true } ? property : throw new InvalidOperationException($"Champ local absent ou non modifiable : {type.FullName}.{name}.");
    }
    private static object ReadNumber(nint pointer, AbiKind kind) => kind switch {
        AbiKind.UInt64 => unchecked((ulong)Marshal.ReadInt64(pointer)), AbiKind.UInt32 => unchecked((uint)Marshal.ReadInt32(pointer)),
        AbiKind.Int32 or AbiKind.Boolean32 => Marshal.ReadInt32(pointer), _ => throw new NotSupportedException($"Champ ABI non pris en charge : {kind}.")
    };
    private static void WriteNumber(nint pointer, AbiKind kind, object value) {
        if (kind == AbiKind.UInt64) { Marshal.WriteInt64(pointer, unchecked((long)Convert.ToUInt64(value))); }
        else if (kind == AbiKind.UInt32) { Marshal.WriteInt32(pointer, unchecked((int)Convert.ToUInt32(value))); }
        else { Marshal.WriteInt32(pointer, Convert.ToInt32(value)); }
    }
    public void Dispose() { foreach (Action release in _releases.AsEnumerable().Reverse()) { release(); } }
}
