# Installer et exécuter les plugins

## Packages et prérequis

| Langage | Dépendance du projet | Prérequis |
| --- | --- | --- |
| .NET | NuGet Omnilingua.Dotnet et Omnilingua.Dotnet.SourceGen | SDK .NET 10 |
| C++ | NuGet Omnilingua.Cpp | C++20, Windows x64, chaîne MSVC/MSBuild adaptée |
| Java | Maven io.omnilingua:omnilingua-java | JDK 17 minimum |
| Python | Package omnilingua-python | CPython 3.13 standard x64 |

Utilisez la même version des SDK et du produit Omnilingua. Ajoutez les dépendances avec
votre gestionnaire de packages habituel. Les réglages propres au développement de
la plateforme sont dans la [contribution](./contribution).

La distribution actuelle cible Windows x64 et nécessite .NET 10 x64. Si son
runtimeconfig déclare Microsoft.AspNetCore.App, ASP.NET Core Runtime 10 x64 doit
également être présent. Java et Python utilisent les runtimes de la machine.

## Configuration

Placez les artefacts des plugins près du produit décompressé. Un tableau JSON
décrit les plugins et leurs relations :

```json
[
  {
    "name": "Journal",
    "type": "dotnet",
    "assembly": "Logger.Dotnet.dll",
    "entryPoint": "Logger.Dotnet.Plugin"
  },
  {
    "name": "Application",
    "type": "dotnet",
    "assembly": "Application.dll",
    "entryPoint": "Application.Plugin",
    "dependencies": {
      "ILogger": "Journal"
    },
    "config": {
      "mode": "demo"
    }
  }
]
```

| Clé | Signification |
| --- | --- |
| name | Nom unique du plugin dans cette configuration |
| type | dotnet, cpp, java ou python |
| assembly | Chemin relatif au JSON, ou chemin absolu de l'artefact |
| entryPoint | Classe .NET/Java, ou classe Python (Plugin par défaut) ; inutile en C++ |
| dependencies | Associe le nom du contrat au nom du plugin fournisseur |
| config | Paramètres propres au plugin transmis lors de son initialisation |

Une dépendance obligatoire sans fournisseur est une erreur. Une dépendance
optionnelle absente est autorisée ; un fournisseur configuré mais invalide reste
une erreur. Les cycles de dépendances sont refusés.

## Lancement et cycle de vie

```powershell
.\Omnilingua.exe .\plugins.json
```

Le Omnilingua initialise et démarre les fournisseurs avant leurs consommateurs, puis
les arrête dans l'ordre inverse. Les services importés sont disponibles dans Init.
Ctrl+C demande l'arrêt propre. Les méthodes StartAsync/StopAsync .NET doivent
retourner les tâches réelles si le travail est asynchrone.

Les détails par langage figurent dans les références [.NET](./dotnet), [C++](./cpp),
[Java](./java) et [Python](./python).
