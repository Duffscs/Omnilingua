using Omnilingua.Dotnet;

namespace Logger.Dotnet;
[PluginExport(typeof(ILogger))]
public sealed partial class Plugin : IPlugin {
    private readonly Logger _logger = new();
    public object[] GetExport() => [_logger];
}
