package logger;

import omnilingua.java.IPlugin;
import omnilingua.java.PluginExport;

@PluginExport(ILogger.class)
public final class Plugin implements IPlugin {
    private final Logger logger = new Logger();


    @Override
    public Object[] getExport() { return new Object[]{logger}; }
}
