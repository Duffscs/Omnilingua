# Omnilingua.Python

SDK Python et pont CPython du Omnilingua. L’interpréteur installé chez l’utilisateur
est chargé dans le processus : ni Python, ni sa bibliothèque standard ne sont
copiés dans la distribution du Omnilingua. Le petit pont compilé et les sources du SDK
doivent accompagner le runtime.

Cette première version est validée avec **CPython 3.13 standard x64 sur Windows**.
Le pont est compilé avec les headers CPython de l’installation de développement.
À l’exécution, la version majeure/mineure doit correspondre à ces headers ; le
chargeur refuse une autre version avant l’initialisation. Les builds sans GIL et
les environnements virtuels ne sont pas encore pris en charge. Aucun fichier
python3xx.dll n’est lié ou copié avec le pont : sa résolution est dynamique.

## Contrats et services

Les plugins utilisent `@PluginExport(ILogger)` et `@PluginImport(ILogger)` sur
leur classe `IPlugin`. `get_export()` retourne les objets de service ; les imports
sont disponibles via `context.services.get_required_service(ILogger)` dans init.
Les interfaces sont des classes locales avec des annotations `str` et `-> None`.
Les autres types et les appels async ne sont pas encore pris en charge.

Le guide complet et ses exemples se trouvent dans
[docs/python.md](../../docs/python.md). Les exemples locaux sont
`SamplePlugins/Logger/Python` et `SamplePlugins/Hello/Python`.

## Installation et configuration

Le chargeur sélectionne `pythonExecutable` dans l’entrée du plugin, puis la variable
`PYTHON_EXECUTABLE`, puis `python` dans PATH. Une courte commande de découverte
interroge cet exécutable ; le code du plugin s’exécute ensuite dans l’interpréteur
embarqué, pas dans un processus externe. Le chemin explicite doit être absolu.

```json
{
  "name": "Logger",
  "type": "python",
  "assembly": "Logger.Python/plugin.py",
  "entryPoint": "Plugin"
}
```

La solution copie `Omnilingua.Python.Bridge.dll`, le dossier `Omnilingua.Python/omnilingua_python`
et le plugin d’exemple près du Omnilingua. La configuration `plugins.python.json` choisit
le Logger Python sans modifier les consommateurs. Depuis la racine du dépôt :

```powershell
& Omnilingua/Runtime/bin/Release/net10.0/Omnilingua.exe Omnilingua/Runtime/bin/Release/net10.0/plugins.python.json
```

La configuration Java reste la configuration de développement par défaut.
Dans une configuration existante, remplacer uniquement l’entrée fournisseur
Logger par celle ci-dessus suffit, en conservant son nom `Logger`.

`SamplePlugins/Logger/Python` expose debug/info/warn/error/fatal avec `(context, message)`.
Les lignes commencent par `[LoggerPython]` ; contexte et message sont conservés
sans inversion, comme dans les autres fournisseurs de `SamplePlugins/Logger`.

## Cycle de vie et mémoire

Le pont initialise un interpréteur partagé avec PyConfig, en mode isolé, sans
intercepter les signaux du Omnilingua et avec des sorties UTF-8. Un thread C++ dédié
exécute les opérations CPython et prend le GIL pendant chaque appel ; le GIL est
libéré lorsque ce thread attend. Les chaînes entrantes sont copiées en str avant
l’appel. Les références des instances et des callbacks sont conservées jusqu’à
l’arrêt des consommateurs et libérées à la fermeture du plugin.

Chaque chargement utilise un nom de module unique. Les imports relatifs entre les
fichiers du plugin sont pris en charge. Les dépendances tierces restent celles de
l’installation CPython choisie ; elles ne sont pas isolées par plugin. Une seconde
installation Python ne peut pas être chargée dans le même Omnilingua.

Les exceptions Python produisent un diagnostic avec traceback, sans traverser la
frontière C ABI. Pour un export void appelé par pointeur, l’erreur est journalisée
par le runtime ; elle ne peut pas devenir une exception propagée au code C++.

Comme la JVM, CPython et sa bibliothèque restent chargés jusqu’à la fin du processus.
Le Omnilingua n’appelle pas Py_Finalize pour redémarrer un interpréteur. Les plugins doivent
libérer leurs ressources et arrêter leurs threads dans stop/close ; ils ne doivent
pas compter sur atexit pour leur cycle de vie normal.

## Compilation et vérification

Le build cherche les headers via `PythonHome` explicite ou via l’exécutable
`PythonExecutable` (`PYTHON_EXECUTABLE`, puis python). L’installation CPython
classique pour Windows fournit ces headers. Exemple avec un chemin personnalisé :

```powershell
MSBuild.exe Omnilingua.slnx /restore /m /p:Configuration=Release /p:Platform=x64 /p:PythonExecutable="C:\Python313\python.exe"
python -I -B tests/Python.SdkTests/test_sdk.py -v
```

Le build publie le wheel omnilingua-python 9.9.9 dans le feed local. Les exemples
installent ce package, sans importer implicitement les sources du SDK.
