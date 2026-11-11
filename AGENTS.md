# Plateforme de plugins multilangages

Ce projet développe un outil permettant de charger et d'interfacer des plugins
C++, .NET, Java et Python dans un même processus. Le runtime et les SDK sont
indépendants des contrats métier. Les seuls exemples de référence sont dans
`SamplePlugins`. Toutes les commandes ci-dessous partent de ce dossier.

## Périmètre

- `Omnilingua/Runtime` : chargement, orchestration, proxies et diagnostics.
- `Omnilingua/Omnilingua.Dotnet` et `Omnilingua/Omnilingua.Dotnet.SourceGen` : protocole et contrats annotés.
- `Omnilingua/Omnilingua.Cpp` : headers, conversions, snapshots et génération C++.
- `Omnilingua/Omnilingua.Java`, `Omnilingua/Omnilingua.Python` : SDK et bridges des runtimes utilisateur.
- `SamplePlugins/Logger` : quatre fournisseurs interchangeables pour les tests.
- `SamplePlugins/Hello/{Cpp,Dotnet,Java,Python}` : consommateur minimal du contrat ILogger injecté.
- `tests`, `tools`, `build` : vérification et publication propres à la plateforme.
- `docs` : documentation publique distribuée aux auteurs de plugins.

Le guide interactif utilise VitePress dans docs (Node.js 22.12+ pour le build).
Les snippets Logger/Hello sont importés depuis les samples ; les premiers plugins
sont dans docs/snippets. Le build exécute tools/Build-Documentation.ps1 avant les
packages. Les utilisateurs reçoivent le site statique dans docs/site, sans Node.js.

Ne pas explorer d'autres projets pour comprendre ou valider cette plateforme.
Reproduire une évolution dans un exemple local ou un test générique. Un SDK ne
référence aucun exemple, contrat métier ou application cliente.

## Invariants

GetMetadata décrit contrats, versions, signatures et dispositions ABI ; Les exports C ABI retournent une adresse opaque ; IPlugin.GetExport() retourne
les objets exportes, decrits par PluginExport et PluginImport. Conserver les appels Cdecl et les métadonnées C ABI
version 3, sans RPC JSON. Chaque dépendance est associée à un fournisseur nommé.
Une dépendance optionnelle absente est ignorée ; un fournisseur présent mais
invalide reste une erreur. Détecter les cycles et démarrer les fournisseurs avant
les consommateurs ; arrêter et libérer dans l'ordre inverse.

Copier les données aux frontières, conserver les callbacks et fournisseurs tant
qu'ils sont utilisés, traiter les exceptions avant l'ABI. Aucun objet STL ou objet
propriétaire ne traverse directement la frontière. Les chaînes et tableaux pointés
restent valides pendant leur copie. Les adresses arbitraires restent dangereuses.

La JVM et CPython sont ceux de l'utilisateur. Un seul interpréteur Python par
processus, worker dédié et GIL ; conserver le runtime jusqu'à la fin du processus.
Ne pas utiliser Py_Finalize pour recharger un plugin.

## Travail

Lire le skill local correspondant dans `.agents/skills` : `platform-runtime`,
`cpp-plugin-abi`, `dotnet-contracts`, `build-and-test`, `repository-checkpoints`.
Documentation, commentaires XML et diagnostics ajoutés : français. C# : types
explicites, namespaces par fichier, accolades K&R. C++ : entiers de taille fixe,
durées de vie explicites et fonctions ABI noexcept.

Publier les SDK et bridges en version de développement 9.9.9 avec `./build.ps1`.
Les feeds locaux sont configurés ici.
PROJECT_DIR_PACKAGE définit leur racine dans le .env local ou l'environnement ;
build/Import-BuildEnvironment.ps1 la charge pour les scripts et les projets MSBuild
lisent aussi le .env. Utiliser .env.example pour une nouvelle installation.
Purger uniquement la version 9.9.9 du package concerné, jamais l'ensemble d'un
cache. Maintenir `docs` avec toute évolution
publique et vérifier sa présence dans les distributions.

Valider avec `./tools/Test.ps1 -Configuration Release` et les exemples locaux.
Utiliser CMake et CTest pour les samples et tests C++ Windows/Linux ; les .vcxproj
sont des sorties générées, pas des sources. tools/Build-Cpp.ps1 utilise le SDK
NuGet publié ; ci/build-linux.sh valide également les bridges Linux.
Ne pas arrêter
un processus utilisateur pour débloquer un fichier ; employer une sortie isolée.
Créer des commits locaux sémantiques aux étapes validées, préserver le travail
utilisateur et nettoyer uniquement les dossiers réellement vides.

C++, Java et Python partagent OmnilinguaPlugin_GetApi, NativeModule et NativePlugin.
Cette API n’est pas versionnée pour le moment. Créer les objets susceptibles
d’échouer par des méthodes Try... dans leur classe, avec constructeur privé
sans chargement ni validation et libération des ressources partielles.
