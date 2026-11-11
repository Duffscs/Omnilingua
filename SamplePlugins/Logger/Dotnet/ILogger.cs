using Omnilingua.Dotnet;

namespace Logger.Dotnet;
public interface ILogger {
    void debug(string context, string message);
    void info(string context, string message);
    void warn(string context, string message);
    void error(string context, string message);
    void fatal(string context, string message);
}
