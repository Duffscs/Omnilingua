package omnilingua.java;

/** Résout les interfaces locales déclarées par PluginImport. */
public interface ServiceProvider {
    <T> T getService(Class<T> contract);

    default <T> T getRequiredService(Class<T> contract) {
        T service = getService(contract);
        if (service == null) throw new IllegalStateException("Service non injecté : " + contract.getName());
        return service;
    }
}
