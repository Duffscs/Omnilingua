using System.Runtime.InteropServices;

namespace Omnilingua.Plugins.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct NativePluginContract {
    public nint Name;
    public nint Methods;
    public uint MethodCount;
    public uint Version;
    public nint Signatures;
    public uint SignatureCount;
    public uint Flags;
}
