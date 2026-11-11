using Omnilingua.Dotnet;

namespace Hello.Dotnet;
[PluginImport(typeof(ILogger))]
public sealed partial class Plugin : IPlugin {
    private ILogger? _logger;
    public void Init(PluginContext context) => _logger = context.GetRequiredService<ILogger>();
    public Task StartAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        ILogger logger = _logger ?? throw new InvalidOperationException("Le plugin doit être initialisé avant son démarrage.");
        logger.info("Hello.Dotnet", "Bonjour depuis le plugin .NET !");
        return Task.CompletedTask;
    }
}
