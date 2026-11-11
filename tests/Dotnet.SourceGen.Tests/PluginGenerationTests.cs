using System.Collections.Immutable;
using System.Runtime.Loader;
using Omnilingua.Dotnet;
using Omnilingua.Dotnet.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dotnet.SourceGen.Tests;

internal static class PluginGenerationTests {
    public static void Run(Action<bool, string> check) {
        ImmutableArray<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)).Cast<MetadataReference>().ToImmutableArray();
        string[] invalid = [
            "public interface ILog { void info(string message); } [PluginExport(typeof(ILog))] public class Plugin : IPlugin {}",
            "public interface ILog { void info(string message); } [PluginExport(typeof(ILog))] public partial class Plugin {}",
            "public interface ILog { void info(string message); } [PluginImport(typeof(ILog))] [PluginImport(typeof(ILog))] public partial class Plugin : IPlugin {}",
            "public interface ILog { void info(string message); } [PluginImport(typeof(ILog), Name=\"A\")] [PluginImport(typeof(ILog), Name=\"B\")] public partial class Plugin : IPlugin {}",
            "public interface ILog { void info(string message); } [PluginExport(typeof(ILog))] public partial class Plugin : IPlugin { public PluginMetadata GetMetadata() => new([],[]); }",
            "public interface ILog { System.DateTime info(); } [PluginExport(typeof(ILog))] public partial class Plugin : IPlugin {}"
        ];
        foreach (string source in invalid) {
            CSharpCompilation compilation = Create(source, references);
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new PluginContractGenerator().AsSourceGenerator());
            driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> diagnostics);
            check(diagnostics.Any(diagnostic => diagnostic.Id == "HSG001"), "Diagnostic de plugin absent : " + source);
        }
        CSharpCompilation valid = Create("""
            namespace GeneratedFixture;
            public interface ILog { void info(string context, string message); }
            public partial class Plugin { }
            [PluginExport(typeof(ILog), Name="Journal", Version=2)]
            [PluginImport(typeof(ILog), Name="Source", Version=3, Optional=true)]
            public partial class Plugin : IPlugin, ILog {
                public object[] GetExport() => [this];
                public void info(string context, string message) { }
            }
            """, references);
        GeneratorDriver validDriver = CSharpGeneratorDriver.Create(new PluginContractGenerator().AsSourceGenerator());
        validDriver.RunGeneratorsAndUpdateCompilation(valid, out Compilation generated, out ImmutableArray<Diagnostic> errors);
        check(errors.All(error => error.Severity != DiagnosticSeverity.Error), "Plugin valide refusé par le générateur.");
        using MemoryStream stream = new();
        Microsoft.CodeAnalysis.Emit.EmitResult result = generated.Emit(stream);
        check(result.Success, "Le plugin généré ne compile pas : " + string.Join("; ", result.Diagnostics));
        stream.Position = 0;
        System.Reflection.Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        IPlugin plugin = (IPlugin)Activator.CreateInstance(assembly.GetType("GeneratedFixture.Plugin")!)!;
        PluginMetadata metadata = plugin.GetMetadata();
        check(metadata.Exports.Single().Name == "Journal" && metadata.Exports.Single().Version == 2, "Nom/version d'export perdus.");
        ServiceRequirement import = metadata.RequiredServices!.Single();
        check(import.Contract == "Source" && import.Version == 3 && import.Optional, "Import optionnel ou version perdus.");
        check(metadata.ProvidedServices!.Single().InterfaceType.IsInstanceOfType(plugin.GetExport().Single()), "Objet exporté incompatible avec la liaison générée.");
    }

    private static CSharpCompilation Create(string source, ImmutableArray<MetadataReference> references) =>
        CSharpCompilation.Create("GeneratedPlugin" + Guid.NewGuid().ToString("N"), [CSharpSyntaxTree.ParseText("#nullable enable\nusing Omnilingua.Dotnet;\n" + source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
