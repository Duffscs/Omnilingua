using Omnilingua.Dotnet;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Omnilingua.Plugins.dotnet;

public static class DotNetLoader {
    /// <summary>
    /// Attempts to load a .NET plugin and create its IPlugin instance.
    /// </summary>
    public static bool TryLoad(
        PluginSettings settings,
        string assemblyPath,
        [NotNullWhen(true)] out ILoadedPlugin? plugin,
        out string? error
    ) {
        plugin = null;
        error = null;

        try {
            string fullPath = Path.GetFullPath(assemblyPath);
            PluginLoadContext context = new(fullPath);
            Assembly assembly = context.LoadFromAssemblyPath(fullPath);

            string entryPoint = settings.EntryPoint ?? "Plugin";

            Type? pluginType = assembly.GetType(entryPoint);

            if (pluginType is null) {
                error = $"Plugin '{settings.Name}': type '{entryPoint}' not found.";
                return false;
            }

            if (!typeof(IPlugin).IsAssignableFrom(pluginType) ||
                !pluginType.IsClass || pluginType.IsAbstract || pluginType.ContainsGenericParameters) {
                error = $"Plugin '{settings.Name}': type '{entryPoint}' must be a concrete class implementing IPlugin.";
                return false;
            }

            if (pluginType.GetConstructor(Type.EmptyTypes) is null) {
                error = $"Plugin '{settings.Name}': type '{entryPoint}' must have a public parameterless constructor.";
                return false;
            }

            IPlugin instance = (IPlugin)Activator.CreateInstance(pluginType)!;
            PluginMetadata metadata = instance.GetMetadata()
                ?? throw new InvalidOperationException("GetMetadata returned null.");

            List<ContractDescription> dependencies = metadata.Dependencies.ToList();
            foreach (IGrouping<string, ServiceRequirement> requirements in (metadata.RequiredServices ?? []).GroupBy(requirement => requirement.Contract)) {
                if (dependencies.Any(dependency => dependency.Name == requirements.Key) || requirements.Select(requirement => requirement.Version).Distinct().Count() != 1) {
                    throw new InvalidOperationException($"{settings.Name} : dépendance dupliquée ou versions incompatibles pour {requirements.Key}.");
                }
                dependencies.Add(new(requirements.Key, requirements.SelectMany(requirement => requirement.Description?.Methods ?? requirement.InterfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(method => method.Name).ToArray()).Distinct(StringComparer.Ordinal).ToArray(), requirements.First().Version,
                    Optional: requirements.All(requirement => requirement.Optional)));
            }
            metadata = metadata with { Dependencies = dependencies };

            plugin = new DotNetPlugin(settings, metadata, instance);
            return true;
        } catch (Exception exception) {
            error = $"Plugin '{settings.Name}': {Unwrap(exception).Message}";
            return false;
        }
    }

    /// <summary>
    /// Extracts the original exception from a reflection invocation.
    /// </summary>
    private static Exception Unwrap(Exception exception) {
        return exception is TargetInvocationException { InnerException: not null }
            ? exception.InnerException
            : exception;
    }
}
