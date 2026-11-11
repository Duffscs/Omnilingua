# Omnilingua.Java

SDK Java 17+ et pont JNI du runtime. Le SDK ne référence aucun plugin métier.
Le plugin d’exemple est dans `SamplePlugins/Logger/Java` et se compile séparément.

Un plugin utilise `@PluginExport(ILogger.class)` et `@PluginImport(ILogger.class)`
sur sa classe `IPlugin`. `getExport()` retourne les objets des services fournis ;
les imports sont accessibles dans `context.services()`. Voir le guide distribué
[docs/java.md](../../docs/java.md).

Cette version fournit des imports et exports `void` avec de zéro à seize paramètres
`String`. Elle ne prend pas encore en charge les objets complexes ou les retours
de valeur. Le contrat du
Logger utilise exactement cinq méthodes `debug`, `info`, `warn`, `error`, `fatal`,
chacune avec deux chaînes : `context` (nom du plugin) et `message`.

Le chargeur du Omnilingua crée des callbacks Cdecl opaques depuis les métadonnées.
Les consommateurs .NET reçoivent un proxy de leur interface locale ; les
consommateurs C++ reçoivent les mêmes adresses par le protocole C ABI. Il n’y a
ni processus Java externe ni appel métier JSON/RPC.

Le pont conserve une JVM par processus et un chargeur de classes par JAR. Les
opérations JNI sont exécutées de façon synchrone sur un thread C++ dédié, avec
des cadres de références locales et des références globales libérées à la fermeture
du plugin. Les chaînes UTF-8 sont copiées en chaînes Java UTF-16 avant l’appel.
Les exceptions Java deviennent des diagnostics ; aucune exception ne traverse
un callback appelé depuis du C++. La JVM et son module restent chargés jusqu’à
la fin du processus, tandis que les instances et les callbacks des plugins sont
libérés après l’arrêt des consommateurs.

## Compiler et exécuter

La solution Windows x64 construit le pont, le SDK et le Logger. Un JDK 17+ est
nécessaire à la compilation : utiliser `/p:JavaHome="chemin du JDK"` ou un
`JAVA_HOME` contenant `javac`. La détection locale peut aussi utiliser un JDK
installé avec Android Studio. Le JRE indiqué par `JAVA_HOME` reste utilisable à
l’exécution, même lorsqu’un autre JDK sert à compiler.

Le runtime cherche la JVM via `javaHome` dans l’entrée du plugin, puis `JAVA_HOME`,
puis `PATH`. Il attend `Omnilingua.Java.Bridge.dll` et `Omnilingua.Java.jar` à côté du Omnilingua.
Les fichiers de configuration de développement incluent le Logger :

```json
{ "name": "Logger", "type": "java", "assembly": "Logger.Java.jar", "entryPoint": "logger.Plugin" }
```

Les consommateurs déclarent une dépendance optionnelle `ILogger`. Retirer l’entrée
Logger du fichier de configuration permet de vérifier le repli console sans
modifier leurs entrées. Une entrée Java présente mais invalide constitue en revanche
une erreur de chargement : elle n’est pas silencieusement ignorée.

L’apphost Windows .NET utilise `CetCompat=false` pour l’intégration de cette JVM.
Cette configuration a été vérifiée sur cette machine ; `dotnet Omnilingua.dll` fonctionne
également. Le pont et les builds sont actuellement validés sur Windows x64.

La publication NuGet/Maven n’est pas mise en place. Les SDK, le runtime et les
plugins restent physiquement séparés pour permettre ces distributions ultérieures.
