# Écrire son premier plugin

Commençons par une seule responsabilité : écrire un message dans la console au
démarrage. Ce plugin ne fournit et ne demande encore aucun service.

## Créer la classe Plugin

Seule la méthode de démarrage est nécessaire. Les autres méthodes du cycle de vie
disposent d'implémentations vides dans le SDK.

:::tabs key:language
== .NET

Ajoutez `Plugin.cs` à côté de `Premier.Dotnet.csproj` :

<<< @/snippets/premier/dotnet/Plugin.cs{csharp}

== C++

Ajoutez `Plugin.cpp` à côté de `CMakeLists.txt` :

<<< @/snippets/premier/cpp/Plugin.cpp{cpp}

`OMNILINGUA_PLUGIN(Plugin)` déclare les points d'entrée que le Omnilingua chargera dans la DLL.
Le SDK assure le chargement de la classe par le Omnilingua.

== Java

Créez `src/premier/Plugin.java` :

<<< @/snippets/premier/java/src/premier/Plugin.java{java}

Le nom complet de la classe est `premier.Plugin`. Il sera utilisé dans le JSON.

== Python

Créez un dossier `Premier.Python` et ajoutez `plugin.py` :

<<< @/snippets/premier/python/plugin.py{python}

`flush=True` affiche immédiatement la ligne dans la console du Omnilingua.

:::

## Construire et placer le plugin

Utilisez votre IDE ou votre outil de build habituel. Le Omnilingua ne depend pas de
votre editeur : Rider, Visual Studio, VS Code, IntelliJ IDEA ou PyCharm conviennent.

| Langage | Artefact a placer dans le dossier Omnilingua |
| --- | --- |
| .NET | Premier.Dotnet.dll avec ses dependances propres |
| C++ | Premier.Cpp.dll (Windows) ou libPremier.Cpp.so (Linux) |
| Java | Premier.Java.jar |
| Python | Dossier Premier.Python contenant plugin.py |

Pour automatiser, consultez [Creer son script de build](./scripts-build).

## Déclarer le plugin dans plugins.json

Créez ce fichier à côté de `Omnilingua.exe`. `assembly` est relatif au fichier JSON.
`entryPoint` est le nom de la classe à instancier ; C++ utilise les exports de la DLL.

:::tabs key:language
== .NET

```json
[
  {
    "name": "Premier.Dotnet",
    "type": "dotnet",
    "assembly": "Premier.Dotnet.dll",
    "entryPoint": "Premier.Dotnet.Plugin"
  }
]
```

== C++

```json
[
  {
    "name": "Premier.Cpp",
    "type": "cpp",
    "assembly": "Premier.Cpp.dll"
  }
]
```

== Java

```json
[
  {
    "name": "Premier.Java",
    "type": "java",
    "assembly": "Premier.Java.jar",
    "entryPoint": "premier.Plugin"
  }
]
```

== Python

```json
[
  {
    "name": "Premier.Python",
    "type": "python",
    "assembly": "Premier.Python/plugin.py",
    "entryPoint": "Plugin"
  }
]
```

:::

## Lancer

Depuis le dossier du produit :

```powershell
.\Omnilingua.exe .\plugins.json
```

La console affiche `[Premier.…] Le plugin a démarré !`, puis le message du Omnilingua
indiquant qu'il est prêt. Utilisez **Ctrl+C** pour l'arrêter proprement.

Au prochain chapitre, nous allons remplacer ce premier exemple par un
[Logger qui exporte un contrat](./exporter).
