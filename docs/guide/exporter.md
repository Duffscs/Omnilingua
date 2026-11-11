# Exporter un service

Un plugin exporte **un contrat et ses objets de service**. Nous allons créer le
Logger : cinq méthodes `debug`, `info`, `warn`, `error` et `fatal`, avec deux
chaînes `context` et `message`.

Le code ci-dessous est inclus directement depuis les samples Logger de la plateforme.
Créez un projet distinct du premier exemple, en reprenant ses dépendances SDK.

## 1. Définir l'interface ILogger

**Toutes les méthodes publiques d'instance de l'interface sont exportées**,
avec leurs signatures.
Les méthodes supplémentaires de la classe d'implémentation, absentes du contrat,
ne sont pas exposées. Les signatures doivent être prises en charge par le SDK.

:::tabs key:language
== .NET

Fichier `ILogger.cs` :

<<< @/../SamplePlugins/Logger/Dotnet/ILogger.cs{csharp}

== C++

Fichier `Contracts/ILogger.h` :

<<< @/../SamplePlugins/Logger/Cpp/Contracts/ILogger.h{cpp}

La base virtuelle `IService` permet à un objet de fournir plusieurs interfaces.
Les méthodes du contrat sont virtuelles, publiques et pures.

== Java

Fichier `src/logger/ILogger.java` :

<<< @/../SamplePlugins/Logger/Java/src/logger/ILogger.java{java}

== Python

Fichier `logger_contract.py` :

<<< @/../SamplePlugins/Logger/Python/logger_contract.py{python}

Les annotations `str` et `-> None` font partie de la description du contrat.

:::

Le nom du contrat est `ILogger`, sa version est **1** par défaut. Ce nom identifie
le service ; il ne désigne ni une DLL, ni une classe du fournisseur.

## 2. Implémenter le Logger

La classe `Logger` contient la journalisation. La classe `Plugin` restera dédiée
au cycle de vie et à la déclaration des contrats.

:::tabs key:language
== .NET

Fichier `Logger.cs` :

<<< @/../SamplePlugins/Logger/Dotnet/Logger.cs{csharp}

== C++

Fichier `Logger.h` :

<<< @/../SamplePlugins/Logger/Cpp/Logger.h{cpp}

Fichier `Logger.cpp` :

<<< @/../SamplePlugins/Logger/Cpp/Logger.cpp{cpp}

== Java

Fichier `src/logger/Logger.java` :

<<< @/../SamplePlugins/Logger/Java/src/logger/Logger.java{java}

== Python

Fichier `logger.py` :

<<< @/../SamplePlugins/Logger/Python/logger.py{python}

:::

## 3. Annoncer l'export et retourner l'objet

**PluginExport** (`[PluginExport]` en .NET, `OMNILINGUA_PLUGIN_EXPORT` en C++,
`@PluginExport` en Java et Python) annonce explicitement le contrat fourni au Omnilingua.
C'est cette annotation qui permet de découvrir et d'exposer le service.
`GetExport` retourne le ou les objets
qui l'implémentent. Les métadonnées sont déduites automatiquement : vous n'avez
pas de méthode `GetMetadata` à écrire.

:::tabs key:language
== .NET

Fichier `Plugin.cs` :

<<< @/../SamplePlugins/Logger/Dotnet/Plugin.cs{csharp}

La classe est `partial` pour recevoir le code du source generator.

== C++

Fichier `Plugin.h` :

<<< @/../SamplePlugins/Logger/Cpp/Plugin.h{cpp}

Fichier `Plugin.cpp` :

<<< @/../SamplePlugins/Logger/Cpp/Plugin.cpp{cpp}

Le générateur analyse les annotations de `Plugin.h` et écrit `Plugin.g.h` dans
le dossier intermédiaire. Ce fichier n'est pas à créer ou à versionner.

== Java

Fichier `src/logger/Plugin.java` :

<<< @/../SamplePlugins/Logger/Java/src/logger/Plugin.java{java}

== Python

Fichier `plugin.py` :

<<< @/../SamplePlugins/Logger/Python/plugin.py{python}

Les imports relatifs supposent de conserver les trois fichiers Python ensemble.

:::

::: tip Une implémentation par contrat
Après `Init`, le Omnilingua ou le SDK récupère les objets exportés. Chaque contrat
annoncé doit correspondre à une unique implémentation. Un objet nul, manquant,
non déclaré ou ambigu est refusé. Un même objet peut fournir plusieurs contrats.
:::

## 4. Construire le fournisseur

Creez un projet Logger distinct avec les dependances du setup, puis utilisez
votre IDE ou votre outil de build habituel.

| Langage | Fichiers du projet | Artefact |
| --- | --- | --- |
| .NET | ILogger.cs, Logger.cs, Plugin.cs | Logger.Dotnet.dll |
| C++ | Contracts/ILogger.h, Logger.h, Logger.cpp, Plugin.h, Plugin.cpp | Logger.Cpp.dll |
| Java | src/logger/ILogger.java, Logger.java, Plugin.java | Logger.Java.jar |
| Python | logger_contract.py, logger.py, plugin.py | Dossier Logger.Python |

En C++, declarez Plugin.h dans omnilingua_add_plugin ; le generateur produit
Plugin.g.h pendant le build. Les [scripts de build](./scripts-build) montrent
la configuration et les commandes possibles.

Placez l'artefact dans le dossier Omnilingua, puis creez
[Hello, un second plugin qui importe ILogger](./importer).
