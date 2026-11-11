# Setup

## Prérequis communs

| Élément | Version / besoin |
| --- | --- |
| Distribution actuelle | Windows x64 |
| Runtime du Omnilingua | .NET 10 x64 |
| Framework déclaré par la distribution | ASP.NET Core Runtime 10 x64 si Microsoft.AspNetCore.App figure dans son runtimeconfig |
| SDK Omnilingua | Même version que la distribution Omnilingua utilisée |

Décompressez la distribution du Omnilingua dans un dossier dédié. Les exemples de projet
ci-dessous utilisent **VERSION_OMNILINGUA** : remplacez cette valeur par la version des
packages correspondant à votre distribution.

## Choisir son langage

:::tabs key:language
== .NET

| Prérequis | Version / besoin |
| --- | --- |
| SDK .NET | 10 minimum pour cibler net10.0 |
| Omnilingua.Dotnet | Package NuGet, même version que le Omnilingua |
| Omnilingua.Dotnet.SourceGen | Package NuGet, même version que Omnilingua.Dotnet |

Créez une bibliothèque de classes .NET et ajoutez ces deux packages avec le
 gestionnaire NuGet de votre environnement. Exemple de projet :

<<< @/snippets/premier/dotnet/Premier.Dotnet.csproj{xml}

== C++

| Prerequis | Version / besoin |
| --- | --- |
| Langage | C++20 |
| Build | CMake 3.24 minimum, Ninja/Make ou generateur Visual Studio |
| Compilateur Windows | MSVC avec prise en charge C++20 |
| Compilateur Linux | GCC ou Clang avec prise en charge C++20 |
| SDK | Omnilingua.Cpp, meme version que le Omnilingua |
| Generation des contrats | Python 3.10 minimum, bindings et bibliotheque libclang compatibles avec les headers standard |

Le package SDK contient OmnilinguaCppConfig.cmake. CMake le charge avec find_package(OmnilinguaCpp)
et fournit la cible Omnilingua::Cpp et la fonction omnilingua_add_plugin. Les samples utilisent
le meme CMakeLists.txt sous Windows et Linux, sans projet propre a un IDE.

Le premier plugin sans contrat ne necessite pas de generation. Pour un plugin
annote, la fonction omnilingua_add_plugin recoit le header de la classe et genere son .g.h.
Les [scripts de build](./scripts-build) donnent les exemples complets.

== Java

| Prérequis | Version / besoin |
| --- | --- |
| JDK pour développer | 17 minimum, x64 |
| Runtime pour exécuter | Java 17 minimum, x64 |
| SDK | Dépendance Maven io.omnilingua:omnilingua-java, même version que le Omnilingua |
| Gestionnaire de dépendances | Maven ou intégration équivalente de votre environnement |

Créez un projet Java et ajoutez la dépendance à votre gestionnaire habituel :

<<< @/snippets/premier/java/pom.xml{xml}

== Python

| Prérequis | Version / besoin |
| --- | --- |
| Interpréteur | CPython 3.13 standard x64, version exacte actuelle |
| SDK | Package omnilingua-python, même version que le Omnilingua |
| Environnements virtuels et Python sans GIL | Non pris en charge actuellement |

Ajoutez omnilingua-python aux dépendances de votre projet avec votre gestionnaire de
packages habituel.

Continuez avec [votre premier plugin](./premier-plugin).
