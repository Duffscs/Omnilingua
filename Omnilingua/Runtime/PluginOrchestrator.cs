using Omnilingua.Plugins.Cpp;
using Omnilingua.Plugins.Native;
using Omnilingua.Plugins.dotnet;
using Omnilingua.Plugins;
using Omnilingua.Dotnet;
using Omnilingua.Plugins.java;
using Omnilingua.Plugins.python;

namespace Omnilingua;

internal sealed class PluginOrchestrator : IDisposable {
    private readonly Dictionary<string, ILoadedPlugin> _plugins = new(StringComparer.Ordinal);
    private readonly List<ILoadedPlugin> _startOrder = [];
    private readonly List<ILoadedPlugin> _started = [];

    public PluginOrchestrator(IEnumerable<PluginSettings> settings, string configDirectory) {
        try {
            foreach (PluginSettings setting in settings) {
                string assemblyPath = Path.GetFullPath(Path.Combine(configDirectory, setting.Assembly));
                ILoadedPlugin? plugin;
                string? error;

                bool loaded = setting.Type.ToLowerInvariant() switch {
                    "python" => PythonLoader.TryLoad(setting, assemblyPath, out plugin, out error),
                    "java" => JavaLoader.TryLoad(setting, assemblyPath, out plugin, out error),
                    "cpp" => CppLoader.TryLoad(setting, assemblyPath, out plugin,out error),
                    "dotnet" => DotNetLoader.TryLoad(setting, assemblyPath, out plugin, out error),
                    _ => throw new NotSupportedException($"Unsupported plugin type: {setting.Type}")
                };

                if (!loaded || plugin is null) {
                    throw new InvalidOperationException(
                        error ?? $"Failed to load plugin '{setting.Name}'.");
                }

                Console.WriteLine($"[Omnilingua] Chargé : {plugin.Name} ({setting.Type})");
                if (!_plugins.TryAdd(plugin.Name, plugin)) {
                    plugin.Dispose();
                    throw new InvalidOperationException($"Plugin dupliqué : {plugin.Name}.");
                }
            }

            HashSet<string> visited = new(StringComparer.Ordinal);
            HashSet<string> visiting = new(StringComparer.Ordinal);
            foreach (string name in _plugins.Keys) {
                Visit(name, visited, visiting);
            }
        } catch {
            foreach (ILoadedPlugin plugin in _plugins.Values.Reverse()) { plugin.Dispose(); }
            throw;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        foreach (ILoadedPlugin consumer in _startOrder) {
            foreach (ContractDescription dependency in consumer.Metadata.Dependencies) {
                if (dependency.Optional && (!consumer.Settings.Dependencies.TryGetValue(dependency.Name, out string? optionalProvider) || !_plugins.ContainsKey(optionalProvider))) {
                    continue;
                }
                string providerName = consumer.Settings.Dependencies.TryGetValue(dependency.Name, out string? resolved)
                    ? resolved
                    : throw new InvalidOperationException($"{consumer.Name}: fournisseur manquant pour {dependency.Name}.");

                ILoadedPlugin provider = _plugins[providerName];
                if (consumer is NativePlugin nativeConsumer) {
                    ContractDescription contract = provider.Metadata.Exports.SingleOrDefault(export => export.Name == dependency.Name)
                        ?? throw new InvalidOperationException($"{provider.Name} : contrat {dependency.Name} absent.");
                    nativeConsumer.BindDependency(dependency, contract, method => provider.GetExport(dependency.Name, method));
                    continue;
                }
                ServiceRequirement[] typed = (consumer.Metadata.RequiredServices ?? []).Where(requirement => requirement.Contract == dependency.Name).ToArray();
                if (typed.Length > 0) {
                    ContractDescription description = provider.Metadata.Exports.SingleOrDefault(export => export.Name == dependency.Name)
                        ?? throw new InvalidOperationException($"{provider.Name} : contrat {dependency.Name} absent des métadonnées.");
                    foreach (ServiceRequirement requirement in typed) {
                        consumer.BindService(requirement, description, method => provider.GetExport(dependency.Name, method));
                        Console.WriteLine($"[Omnilingua] Objet {dependency.Name} ({requirement.InterfaceType.FullName}) : {provider.Name} -> {consumer.Name}");
                    }
                    continue;
                }
                foreach (string method in dependency.Methods) {
                    nint pointer = provider.GetExport(dependency.Name, method);
                    if (pointer == nint.Zero) {
                        throw new MissingMethodException($"{provider.Name} n'exporte pas {dependency.Name}.{method}.");
                    }

                    consumer.Inject(dependency.Name, method, pointer);
                    Console.WriteLine($"[Omnilingua] {provider.Name}.{dependency.Name}.{method} -> {consumer.Name}");
                }
            }
            consumer.Init();
            Console.WriteLine($"[Omnilingua] Start : {consumer.Name}");
            await consumer.StartAsync(cancellationToken).ConfigureAwait(false);
            _started.Add(consumer);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        for (int index = _started.Count - 1; index >= 0; index--) {
            ILoadedPlugin plugin = _started[index];
            Console.WriteLine($"[Omnilingua] Stop : {plugin.Name}");
            await plugin.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        _started.Clear();
    }

    public void Dispose() {
        foreach (ILoadedPlugin plugin in _startOrder.AsEnumerable().Reverse()) {
            plugin.Dispose();
        }
    }

    private void Visit(string name, HashSet<string> visited, HashSet<string> visiting) {
        if (visited.Contains(name)) {
            return;
        }

        if (!visiting.Add(name)) {
            throw new InvalidOperationException($"Cycle de dépendances impliquant {name}.");
        }

        ILoadedPlugin consumer = _plugins[name];
        foreach (ContractDescription dependency in consumer.Metadata.Dependencies) {
            if (dependency.Optional && (!consumer.Settings.Dependencies.TryGetValue(dependency.Name, out string? optionalProvider) || !_plugins.ContainsKey(optionalProvider))) {
                continue;
            }
            if (!consumer.Settings.Dependencies.TryGetValue(dependency.Name, out string? providerName)) {
                throw new InvalidOperationException($"{name}: aucun fournisseur configuré pour {dependency.Name}.");
            }

            if (!_plugins.ContainsKey(providerName)) {
                throw new InvalidOperationException($"{name}: plugin fournisseur introuvable : {providerName}.");
            }

            Visit(providerName, visited, visiting);
        }

        visiting.Remove(name);
        visited.Add(name);
        _startOrder.Add(consumer);
    }
}
