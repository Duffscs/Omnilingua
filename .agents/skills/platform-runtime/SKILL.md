---
name: platform-runtime
description: Modifier les chargeurs, proxies et le cycle de vie du produit Omnilingua multilangages.
---

Travailler dans Omnilingua/Runtime et les SDK de ce projet seulement. Les exemples de
référence sont SamplePlugins/Logger ; une reproduction générique appartient à tests.

Conserver les contrats indépendants du métier, les métadonnées C ABI v3, les
pointeurs Cdecl, les copies et l'ordre de durée de vie des fournisseurs.
Valider dépendances obligatoires/optionnelles, cycles, signatures, exceptions,
arrêt et libération. Ne pas masquer un fournisseur configuré mais invalide.

Java et Python utilisent les runtimes utilisateur. Préserver les workers JNI/GIL,
les références et les traces d'erreur ; ne pas finaliser puis relancer CPython.
Tester avec tools/Test.ps1 et les quatre loggers. Documenter tout changement
public dans docs, livré avec les packages. Ne chercher aucun exemple externe.

Java importe les contrats void/String via PluginImport et le ServiceProvider du
contexte. Preserver les appels JNI reentrants Java vers Java sur le worker.

Python importe les contrats None/str via PluginImport et context.services.
Conserver la reentrance Python vers Python sur le worker et les callbacks
ctypes tant que les consommateurs vivent ; invalider les proxies a la fermeture.

Plugins/Native centralise NativeModule et NativePlugin pour C++, Java et Python.
OmnilinguaPlugin_GetApi est le seul export de chargement, sans version d’API.
Les chargeurs spécialisés préparent seulement les options du runtime utilisateur.
Privilégier les méthodes bool TryLoad/TryCreate dans la classe concernée avec
sorties annotées NotNullWhen et diagnostic. Les constructeurs privés ne font pas
de chargement ou de validation susceptible d’échouer ; pas de factory séparée
pour un mode de construction unique.
