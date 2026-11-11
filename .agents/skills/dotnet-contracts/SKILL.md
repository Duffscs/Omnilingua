---
name: dotnet-contracts
description: Modifier Omnilingua.Dotnet, son générateur Roslyn et les exports ou injections .NET.
---

Conserver les responsabilités séparées : point d'entrée, SyntaxProvider,
Generator et CodeBuilder dans Omnilingua/Omnilingua.Dotnet.SourceGen. PluginExport et PluginImport portent sur la classe partial du plugin et annoncent
les interfaces locales. GetMetadata est genere et GetExport() retourne les objets.
Les anciens attributs ne sont pas conservés : utiliser PluginExport et PluginImport.
Les méthodes de cycle de vie ne deviennent pas des exports métier.

Les types non représentables doivent produire HSG001, pas des conversions
implicites dangereuses. Préserver les copies profondes et la durée de vie des
callbacks. Tester la génération et le fournisseur SamplePlugins/Logger/Dotnet
sans référence à une application cliente.

C# : types explicites, namespaces par fichier, accolades K&R, documentation XML
française. Mettre à jour docs/dotnet.md et republier les packages via build.ps1.
Les autres SDK et le runtime ne référencent aucun contrat d'exemple.
