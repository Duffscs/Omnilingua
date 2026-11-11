using System.Diagnostics;
using System.Text.Json;

namespace Omnilingua.Plugins.python;

/// <summary>Découvre une installation CPython avec son exécutable, sans exécuter le plugin dans un sous-processus.</summary>
internal sealed record PythonInstallation(string Executable, string Home, string Library, int Version) {
    internal static PythonInstallation Find(PluginSettings settings) {
        string executable = settings.PythonExecutable ?? Environment.GetEnvironmentVariable("PYTHON_EXECUTABLE") ?? "python";
        ProcessStartInfo start = new(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("-I"); start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import sys,sysconfig,struct,json,os,platform; print(json.dumps(dict(executable=sys.executable,home=sys.base_prefix,prefix=sys.prefix,implementation=platform.python_implementation(),major=sys.version_info.major,minor=sys.version_info.minor,bits=struct.calcsize('P')*8,free_threaded=bool(sysconfig.get_config_var('Py_GIL_DISABLED')),library=os.path.join(sys.base_prefix,sysconfig.get_config_var('LDLIBRARY')) if os.name=='nt' else os.path.join(sysconfig.get_config_var('LIBDIR'),sysconfig.get_config_var('LDLIBRARY')))))");
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Impossible de démarrer la détection Python.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> diagnostic = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("La détection Python dépasse dix secondes."); }
        if (process.ExitCode != 0) { throw new InvalidOperationException($"Détection Python impossible : {diagnostic.GetAwaiter().GetResult()}"); }
        using JsonDocument document = JsonDocument.Parse(output.GetAwaiter().GetResult());
        JsonElement result = document.RootElement;
        if (result.GetProperty("implementation").GetString() != "CPython" || result.GetProperty("free_threaded").GetBoolean()) {
            throw new NotSupportedException("Une installation CPython standard avec GIL est requise.");
        }
        if (result.GetProperty("bits").GetInt32() != IntPtr.Size * 8) { throw new NotSupportedException("CPython et le Omnilingua doivent avoir la même architecture."); }
        if (result.GetProperty("prefix").GetString() != result.GetProperty("home").GetString()) {
            throw new NotSupportedException("Les environnements virtuels ne sont pas encore pris en charge : sélectionner le CPython de base.");
        }
        string library = result.GetProperty("library").GetString()!;
        if (!File.Exists(library)) { throw new FileNotFoundException("Bibliothèque partagée CPython introuvable.", library); }
        return new(result.GetProperty("executable").GetString()!, result.GetProperty("home").GetString()!, library,
            result.GetProperty("major").GetInt32() * 100 + result.GetProperty("minor").GetInt32());
    }
}
