using Omnilingua.Plugins.Native;
using System.Diagnostics.CodeAnalysis;

namespace Omnilingua.Plugins.Cpp;

public static class CppLoader {
    public static bool TryLoad(PluginSettings settings, string assemblyPath,
        [NotNullWhen(true)] out ILoadedPlugin? plugin, out string? error) {
        bool loaded = NativePlugin.TryCreate(settings, assemblyPath, new NativeOptions { Assembly = assemblyPath }, out NativePlugin? native, out error);
        plugin = native;
        return loaded;
    }
}
