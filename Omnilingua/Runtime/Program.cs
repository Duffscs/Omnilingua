using System.Text.Json;
using Omnilingua;

string configPath = Path.GetFullPath(args.Length > 0 ? args[0] : "plugins.json");
string configDirectory = Path.GetDirectoryName(configPath)!;
string json = await File.ReadAllTextAsync(configPath);
List<PluginSettings> plugins = JsonSerializer.Deserialize<List<PluginSettings>>(json, JsonProtocol.Options)
    ?? throw new InvalidDataException("Configuration invalide.");

using CancellationTokenSource shutdown = new();
Console.CancelKeyPress += (_, eventArgs) => {
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

using PluginOrchestrator orchestrator = new(plugins, configDirectory);
try {
    await orchestrator.StartAsync(shutdown.Token);
    Console.WriteLine("[Omnilingua] Prêt. Ctrl+C pour arrêter.");
    await Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token);
} catch (OperationCanceledException) when (shutdown.IsCancellationRequested) {
    // Arrêt demandé.
} catch (Exception ex) {
    Environment.ExitCode = 1;
    Console.WriteLine($"[Omnilingua] Erreur : {ex}");
} finally {
    using CancellationTokenSource stopTimeout = new(TimeSpan.FromSeconds(10));
    await orchestrator.StopAsync(stopTimeout.Token);
}
