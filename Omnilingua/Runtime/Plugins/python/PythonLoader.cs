using Omnilingua.Plugins.Native;
using System.Diagnostics.CodeAnalysis;

namespace Omnilingua.Plugins.python;

/// <summary>Charge un plugin dans l’installation CPython choisie par l’utilisateur.</summary>
public static class PythonLoader {
    /// <summary>Résout le runtime et les exports ; libère les références partielles en cas d’échec.</summary>
    public static bool TryLoad(PluginSettings settings, string assemblyPath, [NotNullWhen(true)] out ILoadedPlugin? plugin, out string? error) {
        plugin = null; error = null;
        try {
            if (!File.Exists(assemblyPath)) { throw new FileNotFoundException("Module du plugin Python introuvable.", assemblyPath); }
            string entry = settings.EntryPoint ?? "Plugin";
            PythonInstallation installation = PythonInstallation.Find(settings);
            string sdk = Path.Combine(AppContext.BaseDirectory, "Omnilingua.Python");
            if (!File.Exists(Path.Combine(sdk, "omnilingua_python", "__init__.py"))) { throw new DirectoryNotFoundException("SDK Omnilingua.Python introuvable à côté du Omnilingua."); }
            string module = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Omnilingua.Python.Bridge.dll" : "libOmnilingua.Python.Bridge.so");
            bool loaded = NativePlugin.TryCreate(settings, module, new NativeOptions { RuntimeLibrary = installation.Library,
                RuntimeHome = installation.Home, RuntimeExecutable = installation.Executable, Sdk = sdk, Assembly = assemblyPath, EntryPoint = entry },
                out NativePlugin? native, out error, (uint)installation.Version);
            plugin = native;
            return loaded;
        } catch (Exception exception) {
            error = $"Plugin Python '{settings.Name}' : {exception.GetBaseException().Message}";
            return false;
        }
    }
}
