package logger;

import java.time.Instant;
import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.nio.charset.StandardCharsets;

public final class Logger implements ILogger {
    private static final PrintWriter OUTPUT = new PrintWriter(new OutputStreamWriter(System.out, StandardCharsets.UTF_8), true);

    private static synchronized void write(String level, String context, String message) {
        OUTPUT.printf("[LoggerJava] %s [%s] [%s] %s%n", Instant.now(), level, context, message);
    }
    @Override
    public void debug(String context, String message) { write("DEBUG", context, message); }
    @Override
    public void info(String context, String message) { write("INFO", context, message); }
    @Override
    public void warn(String context, String message) { write("WARN", context, message); }
    @Override
    public void error(String context, String message) { write("ERROR", context, message); }
    @Override
    public void fatal(String context, String message) { write("FATAL", context, message); }
}
