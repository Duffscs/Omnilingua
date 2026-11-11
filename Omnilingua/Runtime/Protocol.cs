using System.Text.Json;

namespace Omnilingua;

// Ces types sont internes au Omnilingua : aucun plugin ne les référence.
public sealed record PluginSettings {
    public required string Name { get; init; }
    public required string Type { get; init; }
    public required string Assembly { get; init; }
    public string? EntryPoint { get; init; }
    /// <summary>Répertoire du runtime Java ; utilise JAVA_HOME puis PATH lorsque cette valeur est absente.</summary>
    public string? JavaHome { get; init; }
    /// <summary>Exécutable CPython ; utilise PYTHON_EXECUTABLE puis python dans PATH lorsque cette valeur est absente.</summary>
    public string? PythonExecutable { get; init; }
    public JsonElement? Config { get; init; }
    public Dictionary<string, string> Dependencies { get; init; } = new(StringComparer.Ordinal);
}

internal static class JsonProtocol {
    public static readonly JsonSerializerOptions Options = new() {
        PropertyNameCaseInsensitive = true
    };
}
