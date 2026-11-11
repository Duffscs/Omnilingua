using Omnilingua.Dotnet;

namespace Premier.Dotnet;
public sealed class Plugin : IPlugin {
    public Task StartAsync(CancellationToken cancellationToken) {
        Console.WriteLine("[Premier.Dotnet] Le plugin a démarré !");
        return Task.CompletedTask;
    }
}
