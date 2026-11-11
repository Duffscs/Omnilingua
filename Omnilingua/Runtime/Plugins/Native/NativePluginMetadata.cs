using System.Runtime.InteropServices;

namespace Omnilingua.Plugins.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct NativePluginMetadata {
    public uint ProtocolVersion;
    public nint Dependencies;
    public uint DependencyCount;

    public nint Exports;
    public uint ExportCount;
}
