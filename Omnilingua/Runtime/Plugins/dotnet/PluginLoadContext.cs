using Omnilingua.Dotnet;
using System.Reflection;
using System.Runtime.Loader;

namespace Omnilingua.Plugins.dotnet;

internal sealed class PluginLoadContext(string assemblyPath) : AssemblyLoadContext(isCollectible: false) {
    private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName) {
        // Share the protocol assembly so the plugin implements the Omnilingua's IPlugin type.
        if (assemblyName.Name == typeof(IPlugin).Assembly.GetName().Name) {
            return typeof(IPlugin).Assembly;
        }

        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName) {
        string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? 0 : LoadUnmanagedDllFromPath(path);
    }
}
