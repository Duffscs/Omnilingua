using System.Collections.Immutable;
using System.Text.Json;
using Omnilingua;
using Omnilingua.Dotnet;
using Omnilingua.Dotnet.SourceGen;
using Omnilingua.Plugins.dotnet;
using Omnilingua.Plugins.dotnet.Proxy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dotnet.SourceGen.Tests;

internal static class Program {
    private static int _checks;
    private static void Check(bool condition, string message) {
        if (!condition) { throw new InvalidOperationException(message); }
        _checks++;
    }

    public static void Main(string[] args) {
        Fixture fixture = new();
        FixturePlugin instance = new(fixture);
        PluginMetadata metadata = instance.GetMetadata();
        ContractDescription description = metadata.Exports.Single();
        Check(description.Methods.Count == 5, "Seules les methodes publiques doivent etre exportees.");
        Check(description.Version == 3, "Version perdue.");
        Check(description.Types!.Single(type => type.Name == nameof(State)).EnumValues!.Any(value => value.Value == "9223372036854775808"), "Valeur enum ulong perdue.");
        using DotNetPlugin plugin = new(FixturePlugin.Settings, metadata, instance);
        plugin.Init();
        IFixture proxy = (IFixture)ContractProxy.Create(typeof(IFixture), description, method => plugin.GetExport("IFixture", method));
        Payload input = new() { Child = new() { Name = "été 漢字", Enabled = false }, Count = 0, Names = ["premier", null, ""], Numbers = [1, -2, int.MaxValue], State = State.Large };
        Payload result = proxy.Echo(input);
        Check(!ReferenceEquals(input, fixture.Last) && !ReferenceEquals(result, fixture.Last), "La frontière doit copier les objets.");
        Check(!ReferenceEquals(input.Child, fixture.Last!.Child) && !ReferenceEquals(result.Numbers, fixture.Last.Numbers), "Les copies doivent être profondes.");
        Check(result.Child!.Name == input.Child.Name && result.Child.Enabled == false && result.Count == 0 && result.State == State.Large, "Valeurs modèle altérées.");
        Check(result.Numbers.SequenceEqual(input.Numbers) && result.Names.SequenceEqual(input.Names), "Collections scalaires altérées.");
        input.Child.Name = "modifié";
        Check(fixture.Last.Child!.Name == "été 漢字", "L'entrée reste partagée.");
        result.Numbers[0] = 99;
        Check(fixture.Last.Numbers[0] == 1, "La sortie reste partagée.");
        proxy.Get(out Payload output, out ulong id);
        Check(id == ulong.MaxValue && output.Child!.Name == "été 漢字", "Paramètres out altérés.");
        Payload absent = proxy.Echo(new());
        Check(absent.Child is null && absent.Count is null && absent.Numbers.Length == 0, "Présence nullable ou collection vide altérée.");
        List<Payload> list = proxy.EchoList([input, absent]);
        Check(list.Count == 2 && list[0].Child!.Name == "modifié" && list[1].Child is null, "Liste de modèles altérée.");
        proxy.Notify("message Unicode é 🪟");
        Check(fixture.Message == "message Unicode é 🪟", "Appel void ou chaîne altéré.");
        Check(!proxy.Fail(out Payload? failed) && failed is null, "Une exception a traversé la frontière ABI.");
        Check(plugin.GetExport("IFixture", "absent") == 0, "Une méthode inconnue doit rester absente.");
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Check(proxy.Echo(new() { Count = 42 }).Count == 42, "Le callback a été collecté pendant son utilisation.");
        Task<bool>[] concurrent = Enumerable.Range(0, 8).Select(worker => Task.Run(() => {
            IFixture local = (IFixture)ContractProxy.Create(typeof(IFixture), description, method => plugin.GetExport("IFixture", method));
            for (int index = 0; index < 40; index++) {
                Payload copy = local.Echo(new() { Count = worker, Numbers = [index] });
                if (copy.Count != worker || copy.Numbers[0] != index) { return false; }
            }
            return true;
        })).ToArray();
        Check(Task.WhenAll(concurrent).GetAwaiter().GetResult().All(value => value), "Les sorties de threads concurrents se mélangent.");
        bool duplicateRejected = false;
        try { plugin.Register(metadata.ProvidedServices!.Single().Bind(fixture)); }
        catch (InvalidOperationException) { duplicateRejected = true; }
        Check(duplicateRejected, "Un contrat ne doit pas être enregistré deux fois.");
        Check(instance.GetMetadata().ProvidedServices?.Count == 1, "L'export type doit etre genere.");
        foreach (int exportCount in new[] { 0, 2 }) {
            FixturePlugin invalidInstance = new(fixture, exportCount);
            using DotNetPlugin invalidPlugin = new(FixturePlugin.Settings, invalidInstance.GetMetadata(), invalidInstance);
            bool rejected = false;
            try { invalidPlugin.Init(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Un export absent ou ambigu doit etre refuse avant appel ABI.");
        }
        Check(((IPlugin)new EmptyPlugin()).GetExport().Length == 0, "Exports par defaut non vides.");
        ((IPlugin)new EmptyPlugin()).Init(new PluginContext(null, new EmptyServices()));
        Check(((IPlugin)new EmptyPlugin()).StartAsync(CancellationToken.None).IsCompletedSuccessfully &&
            ((IPlugin)new EmptyPlugin()).StopAsync(CancellationToken.None).IsCompletedSuccessfully, "Cycle de vie par defaut incomplet.");
        CheckDiagnostics();
        PluginGenerationTests.Run(Check);
        Console.WriteLine($"{_checks} vérifications réussies : génération et aller-retour ABI .NET.");
    }

    private static void CheckDiagnostics() {
        ImmutableArray<MetadataReference> references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)).Cast<MetadataReference>().ToImmutableArray();
        string[] invalid = [
            "public class Bad { public void Run() {} }",
            "public partial interface Bad { public void Run(int value) {} public void Run(string value) {} }",
            "public partial interface Bad { public void Run(ref int value) {} }",
            "public partial interface Bad { public System.DateTime Run() => default; }",
            "public class Node { public Node? Next {get;set;} } public partial interface Bad { public Node Run() => new(); }",
            "public partial interface Bad { public async System.Threading.Tasks.Task Run() { await System.Threading.Tasks.Task.Yield(); } }"
        ];
        foreach (string source in invalid) {
            CSharpCompilation compilation = CSharpCompilation.Create("DiagnosticFixture", [CSharpSyntaxTree.ParseText("#nullable enable\nusing Omnilingua.Dotnet; " + source + " [PluginExport(typeof(Bad))] public partial class TestPlugin : IPlugin {}")], references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new PluginContractGenerator().AsSourceGenerator());
            driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out ImmutableArray<Diagnostic> diagnostics);
            Check(diagnostics.Any(diagnostic => diagnostic.Id == "HSG001"), "Diagnostic HSG001 absent : " + source);
        }
        CSharpCompilation valid = CSharpCompilation.Create("ValidFixture", [CSharpSyntaxTree.ParseText("""
            #nullable enable
            using Omnilingua.Dotnet;
            namespace Exemple;
            public record Model { public uint? Value { get; init; } public string? Text {get;init;} }
            [PluginExport(typeof(@event), Name = "IExemple")]
            public partial class TestPlugin : IPlugin {}
            public interface @event {
                Model[] @return(Model[] source);
                public string? Empty() => null;
            }
            """)], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver validDriver = CSharpGeneratorDriver.Create(new PluginContractGenerator().AsSourceGenerator());
        validDriver = validDriver.RunGeneratorsAndUpdateCompilation(valid, out Compilation generated, out ImmutableArray<Diagnostic> errors);
        Check(errors.All(error => error.Severity != DiagnosticSeverity.Error), "Le générateur refuse un contrat valide.");
        Diagnostic[] compilationErrors = generated.GetDiagnostics().Where(error => error.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning).ToArray();
        Check(compilationErrors.Length == 0, "Le code généré ne compile pas proprement : " + string.Join("; ", compilationErrors.Select(error => error.ToString())));
        Check(validDriver.GetRunResult().GeneratedTrees.Length == 3, "L'attribut et le contrat doivent être produits séparément.");
    }
}
