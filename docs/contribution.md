# Contribuer à Omnilingua

Cette page concerne le développement de la plateforme et ses exemples. Pour
écrire un plugin avec les packages publiés, commencez par le [setup](./guide/setup).

## Prérequis du dépôt

| Outil | Version utilisée / requise |
| --- | --- |
| Système | Windows x64 pour les distributions actuelles |
| .NET SDK | 10 |
| Build C++ | CMake 3.24 minimum et Ninja/Make ou Visual Studio |
| Compilateur C++ | MSVC sous Windows, GCC/Clang sous Linux, C++20 |
| JDK | 17 minimum, x64 |
| Python | CPython 3.13 standard x64 |
| Générateur C++ | libclang 18.1.1 avec headers MSVC 14.44 compatibles |
| Documentation | Node.js 22.12 minimum et npm |

L'IDE est libre. Les commandes ci-dessous sont lancées depuis la racine de
`Omnilingua`. Les SDK sont validés avec `SamplePlugins`, sans dépendance à une
application consommatrice extérieure.

## Packages de développement

Le dépôt publie une version de développement mutable **9.9.9** pour simuler la
consommation de packages. **PROJECT_DIR_PACKAGE est l'emplacement où tous les
packages sont placés lors de la compilation**, répartis dans les sous-dossiers
correspondant à leur format.

Créez un fichier `.env` à la racine de Omnilingua en reprenant `.env.example` :

```dotenv
PROJECT_DIR_PACKAGE=./artifacts/packages
```

Remplacez la valeur par le dossier de votre choix. Un chemin relatif est résolu
depuis Omnilingua. Ce fichier est local et ignoré par Git. Une variable
d'environnement PROJECT_DIR_PACKAGE déjà définie est prioritaire sur le fichier.
Sans configuration, le dossier utilisé est `artifacts/packages`.

| Format | Dépôt local par défaut |
| --- | --- |
| NuGet | `$PROJECT_DIR_PACKAGE/local_nuget` |
| Maven | `$PROJECT_DIR_PACKAGE/local_maven` |
| Python wheels | `$PROJECT_DIR_PACKAGE/local_pypi` |

Les scripts de build et de tests chargent ce fichier automatiquement. Les projets
MSBuild lisent également la variable et le `.env`, y compris depuis un IDE.
Pour rendre la variable disponible aux autres commandes du terminal PowerShell :

```powershell
. ./build/Import-BuildEnvironment.ps1
python -m pip install --target .tmp/cpp-codegen -r Omnilingua/Omnilingua.Cpp/generator/requirements.txt
```

La source NuGet locale est ajoutée par les propriétés de build du dépôt.
Pour un projet consommateur extérieur, vous pouvez ajouter la source ainsi :

```powershell
dotnet nuget add source "$env:PROJECT_DIR_PACKAGE/local_nuget" --name omnilingua-development
```

Configuration Maven locale, après chargement de l'environnement :

```xml
<repositories>
  <repository>
    <id>omnilingua-development</id>
    <url>file:///${env.PROJECT_DIR_PACKAGE}/local_maven</url>
  </repository>
</repositories>
```

Consommer la wheel locale dans un dossier isolé :

```powershell
python -m pip install --no-index --find-links "$env:PROJECT_DIR_PACKAGE/local_pypi" --no-cache-dir --upgrade --target artifacts/Omnilingua.Python omnilingua-python==9.9.9
```

Les builds publient les packages et invalident uniquement leur version 9.9.9
dans le cache utilisateur. Ne purgez pas les autres versions. Après une
republication, restaurez avec `--force --no-cache` pour éviter un ancien package.

## Construire et vérifier la plateforme

```powershell
./build.ps1 -Configuration Debug
./build.ps1 -Configuration Release
./tools/Test.ps1 -Configuration Release
```

`PublishLocalPackages=false` évite de republier les packages pendant des contrôles
ciblés. Le build utilise CMake pour les samples et tests C++. Les bridges
du packaging Windows disposent encore de projets MSBuild ; ils sont aussi
compilables avec CMake via OMNILINGUA_BUILD_BRIDGES=ON.

### Construire les parties natives sous Linux

Avec CMake, un compilateur C++20, les bindings libclang compatibles, un JDK 17+
et les headers de développement Python présents :

```sh
bash ci/build-linux.sh
```

Ce script installe le SDK CMake dans artifacts/sdk, construit les plugins et les
bridges, puis exécute CTest. Le bridge Python produit doit être utilisé avec la
même version majeure/mineure de CPython que ses headers de compilation.
Les distributions Linux sont nommées libLogger.Cpp.so et libHello.Cpp.so.
La configuration JSON doit utiliser ces noms dans assembly.

## Maintenir la documentation

Le site utilise VitePress et vitepress-plugin-tabs. Les pages utilisateur sont
dans `docs/guide`, les références dans `docs`, et les premiers exemples dans
`docs/snippets`. Les chapitres Logger/Hello incluent directement le code des samples.

```powershell
cd docs
npm ci
npm run dev
```

Le build du site vérifie les liens et les snippets référencés. Il n'y a pas de
suite de tests navigateur dédiée à la documentation.

```powershell
./tools/Build-Documentation.ps1
```

Cette commande, depuis la racine, crée le site et son archive dans `artifacts`.
Le build plateforme l'exécute avant les packages. N'empaquetez jamais `docs`
récursivement : seuls les guides publics et le site généré doivent être distribués,
sans `node_modules`, caches ni fichiers temporaires.

Préservez les modifications utilisateur et créez des commits locaux sémantiques
aux étapes validées. Ne supprimez que les dossiers réellement vides.

## Chargement natif commun

Les plugins C++ et les bridges Java/Python exposent uniquement
`OmnilinguaPlugin_GetApi()`. Cette fonction retourne la table `OmnilinguaPluginApi` définie
dans `Omnilingua/NativeApi.h`. Elle ne comporte pas de version d’API : reconstruisez le
runtime, les bridges et les plugins C++ ensemble lors d’une modification.
Les versions des contrats métier restent utilisées pour vérifier les dépendances.

`Create` crée une instance opaque et `Destroy` la libère. Les opérations de
cycle de vie et d’injection retournent 1 en cas de succès, 0 en cas d’échec.
`GetError` retourne un diagnostic emprunté que le Omnilingua copie immédiatement.
Les descriptions retournées par `GetMetadata` vivent avec leur instance ; elles
sont copiées dans les modèles du Omnilingua avant utilisation.

`NativeModule.TryLoad` charge et valide la table. `NativePlugin.TryCreate`
crée l’instance, copie ses métadonnées et prépare ses exports. Ces méthodes
retournent `false`, une sortie nulle et un diagnostic en cas d’échec, tout en
libérant les ressources partielles. Les constructeurs privés reçoivent un état
préparé et ne font pas de chargement.

Les capacités de la table sélectionnent les exports directs ou les appels
indirects, les imports par callbacks et la conservation du module pendant toute
la vie du processus. Le chemin commun reste dans `Plugins/Native` ; les
chargeurs Java et Python préparent seulement l’installation utilisateur et les
options de création. Leurs workers JNI/GIL restent propres à chaque bridge.
