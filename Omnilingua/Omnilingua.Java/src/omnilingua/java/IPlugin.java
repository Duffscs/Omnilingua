package omnilingua.java;

/** Cycle de vie commun ; les objets métier restent dans le plugin. */
public interface IPlugin extends AutoCloseable {
    default Object[] getExport() { return new Object[0]; }
    default void init(PluginContext context) {}
    default void start() {}
    default void stop() {}
    default void close() {}
}
