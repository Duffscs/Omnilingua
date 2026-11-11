package logger;

public interface ILogger {
    void debug(String context, String message);
    void info(String context, String message);
    void warn(String context, String message);
    void error(String context, String message);
    void fatal(String context, String message);
}
