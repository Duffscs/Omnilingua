# Générateur de contrats .NET

Le générateur incrémental crée PluginExport et PluginImport pendant sa postinitialisation.
SyntaxProvider analyse les contrats et modèles ; Generator produit GetMetadata et
les liaisons directes. CodeBuilder construit le code émis. Les types non
représentables produisent HSG001.

Le guide public est [docs/dotnet.md](../../docs/dotnet.md). L'exemple de référence
est SamplePlugins/Logger/Dotnet et les tests génériques sont dans
 tests/Dotnet.SourceGen.Tests, depuis la racine de cette solution.

```powershell
dotnet run --project tests/Dotnet.SourceGen.Tests -c Release -p:PublishLocalPackages=false
```

Conserver la séparation entre génération des exports et proxies dynamiques du
runtime. Les changements d'API publique doivent être documentés et livrés dans
les packages ; ne pas ajouter de contrat métier dans le SDK.

Les classes de plugins partial annoncent les interfaces via PluginExport et
PluginImport. Le générateur ne fournit aucun ancien attribut de compatibilité.
