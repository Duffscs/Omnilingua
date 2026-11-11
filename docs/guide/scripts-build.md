# Créer son script de build

Le Omnilingua ne requiert pas de script particulier. Votre IDE peut construire le
projet ; un script sert à reproduire ces mêmes étapes en terminal ou en CI.
Placez-le à la racine du projet plugin, avec ses fichiers de dépendances.

Un script doit restaurer les dépendances, construire l'artefact, puis le copier
dans un dossier de distribution. Gardez la version du SDK dans les fichiers du
projet, et arrêtez le script dès qu'une commande échoue.

Les exemples suivants construisent le Logger. Adaptez les noms pour Hello ou
votre propre plugin. Les exemples `sh` supposent un shell compatible Bash ; les scripts C++ fonctionnent aussi sous Linux avec GCC ou Clang.

## Commandes par langage

:::tabs key:language
== .NET

La restauration et la compilation utilisent le SDK .NET, quel que soit l'IDE.

`build.ps1` :

```powershell
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet restore Logger.Dotnet.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Restauration échouée.' }
    dotnet build Logger.Dotnet.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Compilation échouée.' }
    New-Item -ItemType Directory -Force dist | Out-Null
    Copy-Item bin/Release/net10.0/Logger.Dotnet.dll dist -Force
} finally { Pop-Location }
```

`build.sh` :

```sh
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
dotnet restore Logger.Dotnet.csproj
dotnet build Logger.Dotnet.csproj -c Release --no-restore
mkdir -p dist
cp bin/Release/net10.0/Logger.Dotnet.dll dist/
```

Ces exemples n'ont aucune dépendance propre supplémentaire. Si votre plugin en
ajoute, distribuez aussi les bibliothèques et ressources dont il a besoin.

== C++

Le meme CMakeLists.txt est utilise sous Windows et Linux. Pour le premier plugin :

<<< @/snippets/premier/cpp/CMakeLists.txt{cmake}

Pour Logger et Hello, voici les fichiers reels des samples :

<<< @/../SamplePlugins/Logger/Cpp/CMakeLists.txt{cmake}

<<< @/../SamplePlugins/Hello/Cpp/CMakeLists.txt{cmake}

OmnilinguaCpp_DIR indique le dossier contenant OmnilinguaCppConfig.cmake. Dans le package
NuGet Omnilingua.Cpp, il s'agit de build/native. Apres une installation CMake du SDK,
CMAKE_PREFIX_PATH peut aussi designer le prefixe d'installation.

`build.ps1` :

```powershell
param([Parameter(Mandatory)][string] $OmnilinguaCppDir)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    cmake -S . -B build "-DOmnilinguaCpp_DIR=$OmnilinguaCppDir" -DCMAKE_BUILD_TYPE=Release
    if ($LASTEXITCODE -ne 0) { throw 'Configuration CMake echouee.' }
    cmake --build build --config Release --parallel
    if ($LASTEXITCODE -ne 0) { throw 'Compilation echouee.' }
    cmake --install build --config Release --prefix "$PSScriptRoot/dist"
    if ($LASTEXITCODE -ne 0) { throw 'Installation echouee.' }
} finally { Pop-Location }
```

`build.sh`, sous Linux ou dans un shell Bash configure sous Windows :

```sh
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
: "${OmnilinguaCpp_DIR:?Renseignez le dossier du SDK Omnilingua.Cpp}"
cmake -S . -B build -DOmnilinguaCpp_DIR="$OmnilinguaCpp_DIR" -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release --parallel
cmake --install build --config Release --prefix "$PWD/dist"
```

Le repertoire dist contient Logger.Cpp.dll sous Windows et libLogger.Cpp.so sous
Linux. Pour Hello, les noms sont Hello.Cpp.dll et libHello.Cpp.so.

Lorsqu'un contrat est declare, Python et les bindings libclang sont necessaires
au build. OMNILINGUA_CPP_PYTHON_PATH selectionne un dossier de modules Python,
OMNILINGUA_CPP_LIBCLANG une bibliotheque libclang, OMNILINGUA_CPP_SYSTEM_INCLUDES les dossiers
de headers standard compatibles et OMNILINGUA_CPP_CLANG_ARGUMENTS les options du parseur.
Ces variables CMake se configurent dans l'IDE, les presets ou la ligne de commande.

Sous Windows, libclang 18.1.1 utilise les headers MSVC 14.44 compatibles et le dossier
ucrt du Windows SDK. Sous Linux, le generateur utilise les includes du compilateur
choisi par CMake ; choisissez une version de libclang compatible avec celui-ci.

== Java

Maven résout la dépendance du pom.xml ; les outils du JDK produisent le JAR.
Vous pouvez aussi employer le build Maven/Gradle déjà configuré dans votre projet.

`build.ps1` :

```powershell
param([Parameter(Mandatory)][string] $OmnilinguaVersion)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    mvn dependency:copy-dependencies -DincludeArtifactIds=omnilingua-java -DoutputDirectory=lib
    if ($LASTEXITCODE -ne 0) { throw 'Restauration échouée.' }
    New-Item -ItemType Directory -Force target/classes,dist | Out-Null
    $sources = @(Get-ChildItem src/logger -Filter *.java | ForEach-Object FullName)
    javac --release 17 -encoding UTF-8 -cp "lib/omnilingua-java-$OmnilinguaVersion.jar" -d target/classes @sources
    if ($LASTEXITCODE -ne 0) { throw 'Compilation échouée.' }
    jar --create --file dist/Logger.Java.jar -C target/classes .
    if ($LASTEXITCODE -ne 0) { throw 'Création du JAR échouée.' }
} finally { Pop-Location }
```

`build.sh` :

```sh
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
: "${OMNILINGUA_VERSION:?Renseignez la même version que dans pom.xml}"
mvn dependency:copy-dependencies -DincludeArtifactIds=omnilingua-java -DoutputDirectory=lib
mkdir -p target/classes dist
javac --release 17 -encoding UTF-8 -cp "lib/omnilingua-java-$OMNILINGUA_VERSION.jar" -d target/classes src/logger/*.java
jar --create --file dist/Logger.Java.jar -C target/classes .
```

Pour Hello, remplacez `src/logger` par `src/hello` et le nom du JAR par Hello.Java.jar.

== Python

Aucune compilation n'est requise. Votre script peut restaurer les dépendances
du projet et assembler les modules du plugin sans embarquer l'interpréteur.
Le fichier requirements.txt contient `omnilingua-python`, avec la version choisie.

`build.ps1` :

```powershell
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    python -m pip install -r requirements.txt
    if ($LASTEXITCODE -ne 0) { throw 'Installation des dépendances échouée.' }
    New-Item -ItemType Directory -Force dist/Logger.Python | Out-Null
    Copy-Item plugin.py,logger.py,logger_contract.py dist/Logger.Python -Force
} finally { Pop-Location }
```

`build.sh` :

```sh
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
python -m pip install -r requirements.txt
mkdir -p dist/Logger.Python
cp plugin.py logger.py logger_contract.py dist/Logger.Python/
```

Pour Hello, seuls plugin.py et logger_contract.py sont nécessaires. Les dépendances
Python tierces doivent être disponibles dans l'installation utilisée par le Omnilingua.

:::

## Distribuer le résultat

Copiez les artefacts dans le dossier du Omnilingua, conservez les modules relatifs des
plugins Python, puis créez [plugins.json](./connecter). Le script de build et le
fichier de configuration restent indépendants : reconstruire un plugin ne doit
pas changer automatiquement les fournisseurs choisis par l'utilisateur.
