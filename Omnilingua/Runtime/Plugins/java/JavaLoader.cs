using Omnilingua.Plugins.Native;
using System.Diagnostics.CodeAnalysis;

namespace Omnilingua.Plugins.java;

/// <summary>Charge un JAR Java et copie ses métadonnées depuis le pont JNI.</summary>
public static class JavaLoader {
    /// <summary>Crée le plugin sans conserver de ressources partielles en cas d’échec.</summary>
    public static bool TryLoad(PluginSettings settings, string assemblyPath, [NotNullWhen(true)] out ILoadedPlugin? plugin, out string? error) {
        plugin = null;
        error = null;
        try {
            if (!File.Exists(assemblyPath)) { throw new FileNotFoundException("JAR du plugin introuvable.", assemblyPath); }
            string entry = settings.EntryPoint ?? throw new ArgumentException("entryPoint Java est requis.");
            string? home = settings.JavaHome ?? Environment.GetEnvironmentVariable("JAVA_HOME");
            if (string.IsNullOrWhiteSpace(home)) {
                string executable = OperatingSystem.IsWindows() ? "java.exe" : "java";
                string? java = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
                    .Select(directory => Path.Combine(directory, executable)).FirstOrDefault(File.Exists);
                if (java is not null) { home = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(java))); }
            }
            if (string.IsNullOrWhiteSpace(home)) { throw new DirectoryNotFoundException("Définir JAVA_HOME ou javaHome pour le runtime Java 17+ x64."); }
            string jvm = Path.Combine(home, OperatingSystem.IsWindows() ? "bin/server/jvm.dll" : "lib/server/libjvm.so");
            if (!File.Exists(jvm)) { throw new FileNotFoundException("Bibliothèque JVM introuvable.", jvm); }
            string sdk = Path.Combine(AppContext.BaseDirectory, "Omnilingua.Java.jar");
            if (!File.Exists(sdk)) { throw new FileNotFoundException("Bibliothèque Omnilingua.Java introuvable.", sdk); }
            string module = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Omnilingua.Java.Bridge.dll" : "libOmnilingua.Java.Bridge.so");
            bool loaded = NativePlugin.TryCreate(settings, module, new NativeOptions { RuntimeLibrary = jvm, Sdk = sdk, Assembly = assemblyPath, EntryPoint = entry }, out NativePlugin? native, out error);
            plugin = native;
            return loaded;
        } catch (Exception exception) {
            error = $"Plugin Java '{settings.Name}' : {exception.GetBaseException().Message}";
            return false;
        }
    }
}
