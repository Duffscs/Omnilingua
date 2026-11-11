package hello;

import omnilingua.java.IPlugin;
import omnilingua.java.PluginContext;
import omnilingua.java.PluginImport;

@PluginImport(ILogger.class)
public final class Plugin implements IPlugin {
    private ILogger logger;

    @Override
    public void init(PluginContext context) {
        logger = context.services().getRequiredService(ILogger.class);
    }

    @Override
    public void start() {
        logger.info("Hello.Java", "Bonjour depuis le plugin Java !");
    }
}
