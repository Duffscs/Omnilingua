namespace Logger.Dotnet;
public sealed class Logger : ILogger {
    private static readonly object OutputGate = new();
    public void debug(string context, string message) => Write("DEBUG", context, message);
    public void info(string context, string message) => Write("INFO", context, message);
    public void warn(string context, string message) => Write("WARN", context, message);
    public void error(string context, string message) => Write("ERROR", context, message);
    public void fatal(string context, string message) => Write("FATAL", context, message);

    private static void Write(string level, string context, string message) {
        lock (OutputGate) { Console.WriteLine($"[LoggerDotnet] {DateTimeOffset.UtcNow:O} [{level}] [{context}] {message}"); }
    }
}
