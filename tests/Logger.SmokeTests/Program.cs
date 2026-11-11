using Omnilingua.Plugins.Native;
using Omnilingua;
using Omnilingua.Dotnet;
using Omnilingua.Plugins;
using Omnilingua.Plugins.Cpp;
using Omnilingua.Plugins.java;
using Omnilingua.Plugins.python;
using Omnilingua.Plugins.dotnet;
using Omnilingua.Plugins.dotnet.Proxy;
using System.Diagnostics;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
string missingModule = Path.Combine(AppContext.BaseDirectory, "module-absent.dll");
if (NativeModule.TryLoad(missingModule, out NativeModule? missingNative, out string? missingError) || missingNative is not null || string.IsNullOrWhiteSpace(missingError)) {
    throw new InvalidOperationException("TryLoad doit retourner un diagnostic sans instance en cas d'echec.");
}
PluginSettings invalidSettings = new() { Name = "Absent", Type = "cpp", Assembly = missingModule };
if (NativePlugin.TryCreate(invalidSettings, missingModule, new NativeOptions(), out NativePlugin? missingPlugin, out string? pluginError) || missingPlugin is not null || string.IsNullOrWhiteSpace(pluginError)) {
    throw new InvalidOperationException("TryCreate doit retourner un diagnostic sans instance en cas d'echec.");
}

if (args.Length == 2 && args[0] == "--child") {
    string provider = args[1];
    string assembly = Path.Combine(AppContext.BaseDirectory, provider switch {
        "cpp" => "Logger.Cpp.dll", "java" => "Logger.Java.jar", "dotnet" => "Logger.Dotnet.dll", _ => "Logger.Python/plugin.py"
    });
    PluginSettings settings = new() { Name = "Logger", Type = provider, Assembly = assembly,
        EntryPoint = provider == "java" ? "logger.Plugin" : provider == "dotnet" ? "Logger.Dotnet.Plugin" : "Plugin" };
    ILoadedPlugin? plugin;
    string? error;
    bool loaded = provider switch {
        "cpp" => CppLoader.TryLoad(settings, assembly, out plugin, out error),
        "java" => JavaLoader.TryLoad(settings, assembly, out plugin, out error),
        "dotnet" => DotNetLoader.TryLoad(settings, assembly, out plugin, out error),
        _ => PythonLoader.TryLoad(settings, assembly, out plugin, out error)
    };
    if (!loaded || plugin is null) { throw new InvalidOperationException(error); }
    using (plugin) {
        ContractDescription contract = plugin.Metadata.Exports.Single();
        if (contract.Name != "ILogger" || contract.Version != 1 || contract.Methods.Count != 5) { throw new InvalidDataException("Métadonnées ILogger incompatibles."); }
        plugin.Init(); await plugin.StartAsync(CancellationToken.None);
        ILog logger = (ILog)ContractProxy.Create(typeof(ILog), contract, method => plugin.GetExport("ILogger", method));
        logger.debug("MonPlugin", "abc🙂 été"); logger.info("MonPlugin", "abc🙂 été"); logger.warn("MonPlugin", "abc🙂 été");
        logger.error("MonPlugin", "abc🙂 été"); logger.fatal("MonPlugin", "abc🙂 été");
        string consumerAssembly = typeof(Hello.Dotnet.Plugin).Assembly.Location;
        PluginSettings consumerSettings = new() { Name = "Hello.Dotnet", Type = "dotnet", Assembly = consumerAssembly, EntryPoint = "Hello.Dotnet.Plugin" };
        if (!DotNetLoader.TryLoad(consumerSettings, consumerAssembly, out ILoadedPlugin? consumer, out string? consumerError)) {
            throw new InvalidOperationException(consumerError);
        }
        using (consumer) {
            ServiceRequirement requirement = consumer.Metadata.RequiredServices!.Single();
            consumer.BindService(requirement, contract, method => plugin.GetExport("ILogger", method));
            consumer.Init();
            await consumer.StartAsync(CancellationToken.None);
            await consumer.StopAsync(CancellationToken.None);
        }
        string javaConsumerJar = Path.Combine(AppContext.BaseDirectory, "Hello.Java.jar");
        PluginSettings javaSettings = new() { Name = "Hello.Java", Type = "java", Assembly = javaConsumerJar, EntryPoint = "hello.Plugin" };
        PluginSettings configuredJava = javaSettings with { Dependencies = new() { ["ILogger"] = "Logger" } };
        using (PluginOrchestrator orchestrator = new([settings, configuredJava], AppContext.BaseDirectory)) {
            await orchestrator.StartAsync(CancellationToken.None);
            await orchestrator.StopAsync(CancellationToken.None);
        }
        PluginSettings pythonSettings = new() { Name = "Hello.Python", Type = "python", Assembly = "Hello.Python/plugin.py", EntryPoint = "Plugin",
            Dependencies = new() { ["ILogger"] = "Logger" } };
        using (PluginOrchestrator orchestrator = new([settings, pythonSettings], AppContext.BaseDirectory)) {
            await orchestrator.StartAsync(CancellationToken.None);
            await orchestrator.StopAsync(CancellationToken.None);
        }
        PluginSettings cppSettings = new() { Name = "Hello.Cpp", Type = "cpp", Assembly = "Hello.Cpp.dll",
            Dependencies = new() { ["ILogger"] = "Logger" } };
        using (PluginOrchestrator orchestrator = new([settings, cppSettings], AppContext.BaseDirectory)) {
            await orchestrator.StartAsync(CancellationToken.None);
            await orchestrator.StopAsync(CancellationToken.None);
        }
        bool missingPythonRejected = false;
        try { using PluginOrchestrator missing = new([pythonSettings with { Dependencies = new() }], AppContext.BaseDirectory); }
        catch (InvalidOperationException) { missingPythonRejected = true; }
        if (!missingPythonRejected) { throw new InvalidOperationException("Import Python obligatoire absent accepté."); }
        string pythonFile = Path.Combine(AppContext.BaseDirectory, pythonSettings.Assembly);
        if (!PythonLoader.TryLoad(pythonSettings, pythonFile, out ILoadedPlugin? pythonConsumer, out string? pythonError)) {
            throw new InvalidOperationException(pythonError);
        }
        using (pythonConsumer) {
            NativePlugin imported = (NativePlugin)pythonConsumer;
            ContractDescription required = imported.Metadata.Dependencies.Single();
            bool versionRejected = false;
            try { imported.BindDependency(required with { Version = 2 }, contract, method => plugin.GetExport("ILogger", method)); }
            catch (InvalidOperationException) { versionRejected = true; }
            if (!versionRejected) { throw new InvalidOperationException("Version Python incompatible acceptée."); }
            ContractDescription badProvider = contract with { Signatures = contract.Signatures!.Select(method => method with { ReturnType = new(AbiKind.Int32, "int32", 4) }).ToArray() };
            bool signatureRejected = false;
            try { imported.BindDependency(required, badProvider, method => plugin.GetExport("ILogger", method)); }
            catch (InvalidOperationException) { signatureRejected = true; }
            if (!signatureRejected) { throw new InvalidOperationException("Signature Python incompatible acceptée."); }
        }
        bool missingRejected = false;
        try { using PluginOrchestrator missing = new([javaSettings], AppContext.BaseDirectory); }
        catch (InvalidOperationException) { missingRejected = true; }
        if (!missingRejected) { throw new InvalidOperationException("Import Java obligatoire absent accepte."); }
        if (!JavaLoader.TryLoad(javaSettings, javaConsumerJar, out ILoadedPlugin? javaConsumer, out string? javaError)) { throw new InvalidOperationException(javaError); }
        using (javaConsumer) {
            NativePlugin imported = (NativePlugin)javaConsumer;
            ContractDescription required = imported.Metadata.Dependencies.Single();
            bool versionRejected = false;
            try { imported.BindDependency(required with { Version = 2 }, contract, method => plugin.GetExport("ILogger", method)); }
            catch (InvalidOperationException) { versionRejected = true; }
            if (!versionRejected) { throw new InvalidOperationException("Version Java incompatible acceptee."); }
            ContractDescription badProvider = contract with { Signatures = contract.Signatures!.Select(method => method with { ReturnType = new(AbiKind.Int32, "int32", 4) }).ToArray() };
            bool signatureRejected = false;
            try { imported.BindDependency(required, badProvider, method => plugin.GetExport("ILogger", method)); }
            catch (InvalidOperationException) { signatureRejected = true; }
            if (!signatureRejected) { throw new InvalidOperationException("Signature Java incompatible acceptee."); }
        }
        await plugin.StopAsync(CancellationToken.None);
    }
    return;
}

foreach ((string provider, string prefix) in new[] { ("cpp", "[LoggerCPP]"), ("java", "[LoggerJava]"), ("python", "[LoggerPython]"), ("dotnet", "[LoggerDotnet]") }) {
    ProcessStartInfo start = new("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
    start.ArgumentList.Add(typeof(ILog).Assembly.Location); start.ArgumentList.Add("--child"); start.ArgumentList.Add(provider);
    using Process process = Process.Start(start)!;
    Task<string> output = process.StandardOutput.ReadToEndAsync(), diagnostics = process.StandardError.ReadToEndAsync();
    using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
    try { await process.WaitForExitAsync(timeout.Token); }
    catch { process.Kill(entireProcessTree: true); throw; }
    string text = await output, errors = await diagnostics;
    if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(errors)) { throw new InvalidOperationException($"{provider} : {errors}\n{text}"); }
    string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(line => !line.StartsWith("[Omnilingua]", StringComparison.Ordinal)).ToArray();
    if (lines.Length != 9 || lines.Any(line => !line.StartsWith(prefix))) { throw new InvalidDataException($"En-têtes {provider} incorrects : {text}"); }
    foreach (string level in new[] { "DEBUG", "INFO", "WARN", "ERROR", "FATAL" }) {
        if (!lines.Any(line => line.Contains($"[{level}] [MonPlugin] abc🙂 été"))) { throw new InvalidDataException($"Message ou contexte modifié pour {provider}.{level} : {text}"); }
    }
    if (lines.Count(line => line.Contains("[INFO] [Hello.Dotnet] Bonjour depuis le plugin .NET !")) != 1) {
        throw new InvalidDataException($"Le consommateur Hello.Dotnet n'a pas journalisé exactement un message avec {provider} : {text}");
    }
    if (lines.Count(line => line.Contains("[INFO] [Hello.Java] Bonjour depuis le plugin Java !")) != 1) {
        throw new InvalidDataException($"Hello.Java : message incorrect avec {provider} : {text}");
    }
    if (lines.Count(line => line.Contains("[INFO] [Hello.Python] Bonjour depuis le plugin Python ! été🙂")) != 1) {
        throw new InvalidDataException($"Hello.Python : message incorrect avec {provider} : {text}");
    }
    if (lines.Count(line => line.Contains("[INFO] [Hello.Cpp] Bonjour depuis le plugin C++ ! \u00e9t\u00e9\ud83d\ude42")) != 1) {
        throw new InvalidDataException($"Hello.Cpp : message incorrect avec {provider} : {text}");
    }
    Console.WriteLine($"PASS: {provider}, cinq méthodes, Unicode et messages des consommateurs Hello.Dotnet, Hello.Java, Hello.Python et Hello.Cpp via {prefix}.");
}

public interface ILog {
    void debug(string context, string message);
    void info(string context, string message);
    void warn(string context, string message);
    void error(string context, string message);
    void fatal(string context, string message);
}
