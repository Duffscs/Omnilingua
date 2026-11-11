# Loggers interchangeables

Les exemples sont regroupés par langage :

```text
Logger/
  Cpp/       Classe annotee, GetExport, service Logger et adaptateurs générés
  Dotnet/    Plugin [PluginExport] et métadonnées générées par Roslyn
  Java/      Plugin @PluginExport
  Python/    Plugin @PluginExport, service Logger séparé
```

Ils exposent le même contrat `ILogger` version 1 avec debug, info, warn, error et
fatal. Chaque méthode reçoit `(context, message)` et ne retourne aucune valeur.
Le contexte est le nom du plugin appelant ; les quatre fournisseurs conservent
désormais le message, y compris Python.

Les lignes commencent par `[LoggerCPP]`, `[LoggerJava]`, `[LoggerPython]` ou `[LoggerDotnet]` :

```text
[LoggerCPP] [INFO] [Application] Surveillance démarrée.
```

Le build copie les exemples près du Omnilingua. Depuis la racine, choisir le fournisseur :

```powershell
.\Omnilingua\Runtime\bin\Release\net10.0\Omnilingua.exe .\Omnilingua\Runtime\bin\Release\net10.0\plugins.json
.\Omnilingua\Runtime\bin\Release\net10.0\Omnilingua.exe .\Omnilingua\Runtime\bin\Release\net10.0\plugins.cpp.json
.\Omnilingua\Runtime\bin\Release\net10.0\Omnilingua.exe .\Omnilingua\Runtime\bin\Release\net10.0\plugins.python.json
.\Omnilingua\Runtime\bin\Release\net10.0\Omnilingua.exe .\Omnilingua\Runtime\bin\Release\net10.0\plugins.dotnet.json
```

La première configuration utilise Java. Les autres changent le fournisseur nommé
Logger sans modifier les consommateurs Hello de chaque langage. Ces exemples
déclarent un logger obligatoire ; le SDK permet aussi les imports optionnels.

Le Logger C++ ne nécessite ni Clang ni Python à l’exécution. Pour l’outillage de
build et ses limites, voir `Omnilingua/Omnilingua.Cpp/generator/README.md`.

Le fournisseur .NET sépare Plugin (cycle de vie et enregistrement) de Logger
(implémentation de ILogger et écriture des messages).

Le fournisseur Java sépare également Plugin de Logger : getExport() retourne
l’instance du service Logger, qui implémente ILogger.

PluginExport sur les classes annonce les services .NET et Java ; GetMetadata
est automatique et GetExport()/getExport() retourne les objets implémentant les contrats.
