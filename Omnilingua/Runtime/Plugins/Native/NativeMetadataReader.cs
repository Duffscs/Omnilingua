using Omnilingua.Dotnet;
using System.Runtime.InteropServices;

namespace Omnilingua.Plugins.Native;

internal static class NativeMetadataReader {
    internal static PluginMetadata Read(IntPtr pointer) {
        if (Marshal.ReadInt32(pointer) != 3) { throw new InvalidDataException("Version du protocole C ABI incompatible : version 3 requise."); }
        NativePluginMetadata abi =
            Marshal.PtrToStructure<NativePluginMetadata>(pointer);

        return new PluginMetadata(
            ReadContracts(abi.Dependencies, abi.DependencyCount),
            ReadContracts(abi.Exports, abi.ExportCount));
    }

    /// <summary>
    /// Convertit les descriptions de contrats C ABI en objets .NET.
    /// </summary>
    private static List<ContractDescription> ReadContracts(
        IntPtr pointer,
        uint count) {
        List<ContractDescription> contracts = [];

        int stride = Marshal.SizeOf<NativePluginContract>();

        for (int i = 0; i < count; i++) {
            IntPtr contractPointer = pointer + i * stride;

            NativePluginContract abi =
                Marshal.PtrToStructure<NativePluginContract>(contractPointer);

            string name = Marshal.PtrToStringUTF8(abi.Name)
                ?? throw new InvalidOperationException("Invalid contract name.");

            List<string> methods = [];

            for (int j = 0; j < abi.MethodCount; j++) {
                IntPtr methodPointer = Marshal.ReadIntPtr(
                    abi.Methods,
                    j * IntPtr.Size);

                string method = Marshal.PtrToStringUTF8(methodPointer)
                    ?? throw new InvalidOperationException(
                        $"Invalid method name in contract '{name}'.");

                methods.Add(method);
            }

            IReadOnlyList<AbiMethodDescription> signatures = new AbiMetadataReader().ReadMethods(abi.Signatures, abi.SignatureCount);
            if (signatures.Any(signature => !methods.Contains(signature.Name))) { throw new InvalidDataException($"{name} : signature sans export déclaré."); }
            if ((abi.Flags & ~1u) != 0) { throw new InvalidDataException($"{name} : indicateurs de contrat inconnus."); }
            contracts.Add(new ContractDescription(name, methods, abi.Version, signatures, (abi.Flags & 1) != 0));
        }

        return contracts;
    }
}
