---
name: build-and-test
description: Construire, publier et tester uniquement la plateforme Omnilingua et ses exemples.
---

Toutes les commandes partent du dossier de cette solution. Utiliser
./build.ps1 -Configuration Debug puis Release pour le bootstrap et la publication,
et ./tools/Test.ps1 -Configuration Release pour les tests génériques locaux.

Utiliser CMake/CTest pour les samples et tests C++ et les bridges portables.
tools/Build-Cpp.ps1 configure Windows avec MSVC ; ci/build-linux.sh construit
sous Linux avec GCC/Clang. .NET 10, JDK 17+ et CPython standard sont requis
pour les parties correspondantes. Le bridge Python utilise les headers du Python
sélectionné ; la distribution Windows actuelle cible CPython 3.13.
OMNILINGUA_CPP_PYTHON_PATH, OMNILINGUA_CPP_LIBCLANG et OMNILINGUA_CPP_SYSTEM_INCLUDES sélectionnent
le parseur. Le dossier .tmp/cpp-codegen est local à ce projet.

Publier 9.9.9 dans les feeds locaux. Invalider seulement cette version du package
concerné et vérifier le chemin absolu avant toute suppression récursive.
PublishLocalPackages=false évite les purges pendant les tests. Les imports C++
doivent être restaurés avant l'évaluation des consommateurs.

Les guides docs sont inclus dans les SDK NuGet et dans l'archive produit.
Vérifier leur contenu avec tests/Packaging.Tests. Les tests ne doivent exiger
aucun code ni binaire compilé depuis un autre projet.
Ne pas arrêter un processus utilisateur pour libérer ses fichiers.

Le build plateforme construit d'abord le site VitePress (Node.js 22.12+ et npm).
La documentation n'a pas de suite de tests navigateur. Verifier sa construction
VitePress et les snippets references. Les tests runtime restent necessaires pour
les changements fonctionnels. Les packages incluent uniquement les Markdown
publics et docs/site, jamais node_modules.
