package omnilingua.java;

import java.lang.reflect.*;
import java.net.*;
import java.nio.file.*;
import java.util.*;

/** Construit les métadonnées et proxies depuis les annotations des plugins. */
public final class RuntimePlugin implements AutoCloseable {
    private record Operation(String contract, int version, Class<?> type, Method method, Object target) {}
    private record Import(Class<?> type, String name, int version, boolean optional) {}
    private final URLClassLoader loader;
    private IPlugin plugin;
    private final List<Operation> operations = new ArrayList<>();
    private final List<Import> imports = new ArrayList<>();
    private final Map<Class<?>, Object> services = new HashMap<>();
    private final Set<String> boundContracts = new HashSet<>();
    private long handle;

    private RuntimePlugin(String jar, String entryPoint) throws Exception {
        loader = new URLClassLoader(new URL[]{Path.of(jar).toUri().toURL()}, IPlugin.class.getClassLoader());
        try {
            plugin = (IPlugin)Class.forName(entryPoint, true, loader).getConstructor().newInstance();
            Set<String> names = new HashSet<>();
            PluginExport[] declarations = plugin.getClass().getAnnotationsByType(PluginExport.class);
            for (PluginExport export : declarations) {
                String name = export.name().isBlank() ? export.value().getSimpleName() : export.name();
                addExport(export.value(), name, export.version(), names);
            }
            Set<String> imported = new HashSet<>();
            Set<Class<?>> importedTypes = new HashSet<>();
            for (PluginImport declaration : plugin.getClass().getAnnotationsByType(PluginImport.class)) {
                String name = declaration.name().isBlank() ? declaration.value().getSimpleName() : declaration.name();
                validate(declaration.value(), name, declaration.version());
                if (!imported.add(name)) throw new IllegalArgumentException("Import dupliqué : " + name);
                if (!importedTypes.add(declaration.value())) throw new IllegalArgumentException("Interface importée plusieurs fois : " + declaration.value());
                imports.add(new Import(declaration.value(), name, declaration.version(), declaration.optional()));
            }
        } catch (Exception error) {
            try { if (plugin != null) plugin.close(); } catch (Exception closing) { error.addSuppressed(closing); }
            try { loader.close(); } catch (Exception closing) { error.addSuppressed(closing); }
            throw error;
        }
    }
    private static Method[] validate(Class<?> type, String name, int version) {
        if (!type.isInterface() || version < 1 || name.isBlank() || name.chars().anyMatch(Character::isISOControl)) throw new IllegalArgumentException("Contrat invalide : " + name);
        Method[] methods = type.getMethods();
        Set<String> names = new HashSet<>();
        for (Method method : methods) {
            if (Modifier.isStatic(method.getModifiers()) || method.getReturnType() != void.class || method.getParameterCount() > 16 ||
                    Arrays.stream(method.getParameterTypes()).anyMatch(parameter -> parameter != String.class) || !names.add(method.getName())) {
                throw new IllegalArgumentException("Signature non prise en charge : " + method + ". Attendu : void et paramètres String, sans surcharge.");
            }
        }
        if (methods.length == 0) throw new IllegalArgumentException("Contrat vide : " + name);
        Arrays.sort(methods, Comparator.comparing(Method::getName));
        return methods;
    }
    private void addExport(Class<?> type, String name, int version, Set<String> names) {
        if (!names.add(name)) throw new IllegalArgumentException("Export dupliqué : " + name);
        for (Method method : validate(type, name, version)) operations.add(new Operation(name, version, type, method, null));
    }
    public static RuntimePlugin load(String jar, String entryPoint) throws Exception { return new RuntimePlugin(jar, entryPoint); }
    public String[] describe() {
        return operations.stream().map(operation -> operation.contract() + "\t" + operation.version() + "\t" + operation.method().getName() + "\t" + operation.method().getParameterCount()).toArray(String[]::new);
    }
    public String[] describeImports() {
        List<String> rows = new ArrayList<>();
        for (Import requirement : imports) {
            for (Method method : validate(requirement.type(), requirement.name(), requirement.version())) {
                rows.add(requirement.name() + "\t" + requirement.version() + "\t" + method.getName() + "\t" + method.getParameterCount() + "\t" + (requirement.optional() ? "1" : "0"));
            }
        }
        return rows.toArray(String[]::new);
    }
    private static native void invokeImport(long handle, String contract, String method, String[] arguments);
    public void bind(String contract, long nativeHandle) {
        handle = nativeHandle;
        Import requirement = imports.stream().filter(value -> value.name().equals(contract)).findFirst().orElseThrow();
        if (!boundContracts.add(contract)) throw new IllegalStateException("Service déjà injecté : " + contract);
        Object proxy = java.lang.reflect.Proxy.newProxyInstance(loader, new Class<?>[]{requirement.type()}, (object, method, arguments) -> {
            if (method.getDeclaringClass() == Object.class) {
                return switch (method.getName()) {
                    case "toString" -> "Service importé " + contract;
                    case "hashCode" -> System.identityHashCode(object);
                    case "equals" -> object == arguments[0];
                    default -> throw new UnsupportedOperationException(method.getName());
                };
            }
            String[] values = arguments == null ? new String[0] : Arrays.copyOf(arguments, arguments.length, String[].class);
            for (String value : values) {
                if (value != null && (value.length() > 32768 || value.indexOf('\0') >= 0)) throw new IllegalArgumentException("Chaîne ABI trop longue ou contenant un caractère nul.");
            }
            invokeImport(handle, contract, method.getName(), values);
            return null;
        });
        services.put(requirement.type(), proxy);
    }
    public void invoke(int index, String[] arguments) throws Throwable {
        Operation operation = operations.get(index);
        if (operation.target() == null) throw new IllegalStateException("Plugin non initialisé.");
        try { operation.method().invoke(operation.target(), (Object[])arguments); }
        catch (InvocationTargetException error) { throw error.getCause(); }
    }
    public void init(String configuration) {
        for (Import requirement : imports) {
            if (!requirement.optional() && !services.containsKey(requirement.type())) throw new IllegalStateException("Import obligatoire absent : " + requirement.name());
        }
        plugin.init(new PluginContext(configuration, new ServiceProvider() {
            public <T> T getService(Class<T> type) { return type.cast(services.get(type)); }
        }));
        Object[] targets = Objects.requireNonNull(plugin.getExport(), "Tableau d'exports nul.");
        for (Object target : targets) {
            if (target == null || operations.stream().noneMatch(operation -> operation.type().isInstance(target))) throw new IllegalStateException("Objet exporté sans contrat annoncé.");
        }
        for (int index = 0; index < operations.size(); index++) {
            Operation operation = operations.get(index);
            Object[] matches = Arrays.stream(targets).filter(operation.type()::isInstance).toArray();
            if (matches.length != 1) throw new IllegalStateException("Une implémentation unique est requise pour " + operation.contract());
            operations.set(index, new Operation(operation.contract(), operation.version(), operation.type(), operation.method(), matches[0]));
        }
    }
    public void start() { plugin.start(); }
    public void stop() { plugin.stop(); }
    public void close() throws Exception {
        try { plugin.close(); } finally { services.clear(); boundContracts.clear(); handle = 0; loader.close(); }
    }
}
