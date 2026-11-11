---
name: cpp-plugin-abi
description: Modifier les headers C++20, le générateur et les bridges natifs du produit Omnilingua.
---

Omnilingua/Omnilingua.Cpp contient le SDK ; Omnilingua/Omnilingua.Java/Bridge et Omnilingua/Omnilingua.Python/Bridge
contiennent les bridges. Utiliser C++20, entiers fixes, extern C, OMNILINGUA_CALL,
PLUGIN_EXPORT et noexcept. Aucun objet STL ou exception ne traverse l'ABI.

Préserver les dispositions v3, masques de présence, compteurs et bornes.
MapRecord et Snapshot possèdent les copies ; les sorties empruntées restent
valides jusqu'à leur copie. Traiter l'échec d'une conversion sans détruire la
dernière sortie valide. Ne pas modifier silencieusement une ABI publiée.

Le générateur Clang couvre les scalaires et string_view ; les structures/listes
restent manuelles. Utiliser SamplePlugins/Logger/Cpp pour un export généré et
tests/Omnilingua.Cpp.Tests et Cpp.CodegenTests pour les conversions et signatures.
Compiler avec CMake 3.24+ et CTest ; MSVC sous Windows, GCC/Clang sous Linux.
libclang n'est requis qu'au build. Le SDK publie OmnilinguaCppConfig.cmake et fournit
Omnilingua::Cpp, omnilingua_add_plugin et omnilingua_generate_contracts. Les samples consomment
ce SDK via find_package, sans référence implicite aux sources du SDK.

Les plugins portent OMNILINGUA_PLUGIN_EXPORT et OMNILINGUA_PLUGIN_IMPORT (second argument true pour un import optionnel) sur leur classe
IPlugin et retournent les objets de service via GetExport (vector<IService*>).
Les interfaces héritent virtuellement de IService ; leur nom et version 1 sont
déduits, OMNILINGUA_PLUGIN_CONTRACT permet de les préciser. Déclarer le header dans
omnilingua_add_plugin(... HEADERS Plugin.h) et inclure son .g.h dans le .cpp. Le générateur produit les
métadonnées et liaisons ; le SDK associe les objets après Init et refuse les
exports absents, ambigus ou non déclarés. PluginContext.Services résout les imports.
OMNILINGUA_PLUGIN émet OmnilinguaPlugin_GetApi, table commune aux modules C++, Java et Python.
Pas de versionnement de cette API pour le moment ; reconstruire tous les binaires
concernés ensemble. Les statuts et diagnostics remontent les erreurs au Omnilingua.
Utiliser SamplePlugins/Hello/Cpp pour les imports et préserver l'invalidation à Stop.
