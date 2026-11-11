package omnilingua.java;

/** Configuration JSON initiale opaque, à interpréter par le plugin. */
public record PluginContext(String configuration, ServiceProvider services) {}
