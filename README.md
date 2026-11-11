# Omnilingua

Omnilingua permet de développer et de connecter des plugins **C++, .NET, Java et
Python** dans un même processus. Chaque plugin expose ou utilise des services à
travers des interfaces, indépendamment du langage de leur implémentation.

**[Documentation](https://duffscs.github.io/Omnilingua/)** ·
[Premiers pas](https://duffscs.github.io/Omnilingua/guide/setup.html) ·
[Guide de contribution](docs/contribution.md)

## Principe

Un plugin fournit un service, un autre le demande. Omnilingua relie les deux à
partir de leurs contrats et de la configuration de l'application.

- **Exporter un service** : définir son interface, déclarer le contrat avec
  `PluginExport` et retourner l'objet qui l'implémente.
- **Importer un service** : déclarer le contrat avec `PluginImport`, puis récupérer
  son interface depuis le contexte du plugin.
- **Connecter les plugins** : associer chaque contrat à son fournisseur dans
  `plugins.json`. Une dépendance peut être obligatoire ou optionnelle.

Les SDK adaptent ces déclarations aux conventions de chaque langage. Le runtime
charge les plugins, résout leurs dépendances et démarre les fournisseurs avant
les consommateurs. L'arrêt s'effectue dans l'ordre inverse.

## Langages et SDK

| Langage | SDK | Référence |
| --- | --- | --- |
| .NET | `Omnilingua.Dotnet` et `Omnilingua.Dotnet.SourceGen` | [.NET](docs/dotnet.md) |
| C++ | `Omnilingua.Cpp`, avec intégration CMake | [C++](docs/cpp.md) |
| Java | `io.omnilingua:omnilingua-java` | [Java](docs/java.md) |
| Python | `omnilingua-python` | [Python](docs/python.md) |

Java et Python utilisent les runtimes installés sur la machine. Le guide
d'[installation](docs/installation.md) détaille les prérequis, la configuration
et le lancement de l'application.

## Exemples

Les exemples du dépôt illustrent le même contrat `ILogger` dans les quatre
langages :

- [Logger](SamplePlugins/Logger) fournit les méthodes de journalisation.
- [Hello](SamplePlugins/Hello) importe le service et l'utilise pour écrire un message.
- [Configurations](SamplePlugins/config) relie les fournisseurs et les consommateurs.

Un consommateur peut ainsi utiliser un logger écrit dans un autre langage sans
changer son interface ni son code d'appel.

## État du projet

Omnilingua est en cours de développement. Les API et les formats de contrats
peuvent encore évoluer. La distribution actuelle cible Windows x64 ; les
composants C++ et les bridges Java/Python sont également compilés et testés sous
Linux.

## Contribuer

Le [guide de contribution](docs/contribution.md) décrit les prérequis, la
compilation, les tests et la configuration des dépôts de packages locaux.

| Dossier | Contenu |
| --- | --- |
| [`Omnilingua`](Omnilingua) | Runtime, SDK, générateurs et bridges |
| [`SamplePlugins`](SamplePlugins) | Plugins d'exemple et configurations |
| [`tests`](tests) | Tests des contrats, des échanges entre langages et des packages |
| [`docs`](docs) | Sources de la documentation VitePress |
| [`tools`](tools) | Scripts de compilation et de vérification |
