# Importer un service

Créez maintenant **un second projet**, Hello, avec les mêmes dépendances SDK que
le premier plugin. Il demande un `ILogger`, puis appelle `info` au démarrage.
Il ne référence ni le projet Logger, ni sa classe d'implémentation.

## 1. Définir un contrat local

Reprenez le nom `ILogger`, la version **1** et les signatures compatibles.
Hello n'utilise que `info` : son interface peut donc décrire cette seule opération.
Le fournisseur peut en exposer davantage. Les noms de paramètres ne sélectionnent
pas le fournisseur ; le nom du contrat et les signatures comptent.

:::tabs key:language
== .NET

`ILogger.cs` :

<<< @/../SamplePlugins/Hello/Dotnet/ILogger.cs{csharp}

== C++

`Contracts/ILogger.h` :

<<< @/../SamplePlugins/Hello/Cpp/Contracts/ILogger.h{cpp}

== Java

`src/hello/ILogger.java` :

<<< @/../SamplePlugins/Hello/Java/src/hello/ILogger.java{java}

== Python

`logger_contract.py` :

<<< @/../SamplePlugins/Hello/Python/logger_contract.py{python}

:::

## 2. Déclarer l'import et obtenir le service

**PluginImport** (`[PluginImport]` en .NET, `OMNILINGUA_PLUGIN_IMPORT` en C++,
`@PluginImport` en Java et Python) déclare explicitement le contrat demandé au Omnilingua.
Sans cette annotation, le Omnilingua ne prépare pas de service pour cette interface.
Le contexte fournit ensuite un objet qui implémente
votre interface locale. Vous l'utilisez comme un service du même langage.

:::tabs key:language
== .NET

`Plugin.cs` :

<<< @/../SamplePlugins/Hello/Dotnet/Plugin.cs{csharp}

== C++

`Plugin.h` :

<<< @/../SamplePlugins/Hello/Cpp/Plugin.h{cpp}

`Plugin.cpp` :

<<< @/../SamplePlugins/Hello/Cpp/Plugin.cpp{cpp}

Conservez `omnilingua_add_plugin(Hello.Cpp SOURCES Plugin.cpp HEADERS Plugin.h)` et la configuration du générateur
du chapitre précédent. Seul `Plugin.cpp` doit être compilé dans ce projet Hello.

== Java

`src/hello/Plugin.java` :

<<< @/../SamplePlugins/Hello/Java/src/hello/Plugin.java{java}

== Python

`plugin.py` :

<<< @/../SamplePlugins/Hello/Python/plugin.py{python}

:::

Le Omnilingua initialise le fournisseur et prépare ses exports avant d'initialiser
Hello. Les services importés sont donc disponibles dans `Init`/`init`.

## 3. Rendre l'import optionnel

Un import est **obligatoire par défaut**. Pour qu'un plugin fonctionne sans logger,
annoncez un import optionnel et utilisez une résolution qui accepte son absence.
Ces variantes remplacent la déclaration et les méthodes correspondantes de Hello.

:::tabs key:language
== .NET

```csharp
[PluginImport(typeof(ILogger), Optional = true)]
public sealed partial class Plugin : IPlugin {
    private ILogger? _logger;
    public void Init(PluginContext context) => _logger = context.Services.GetService(typeof(ILogger)) as ILogger;
    public Task StartAsync(CancellationToken cancellationToken) {
        if (_logger is not null) { _logger.info("Hello.Dotnet", "Bonjour !"); }
        else { Console.WriteLine("[Hello.Dotnet] Bonjour !"); }
        return Task.CompletedTask;
    }
}
```

== C++

```cpp
#include <iostream>
class OMNILINGUA_PLUGIN_IMPORT(ILogger, true) Plugin final : public Omnilingua::Cpp::IPlugin {
    ILogger* _logger = nullptr;
public:
    void Init(const Omnilingua::Cpp::PluginContext& context) override {
        _logger = context.Services.GetService<ILogger>();
    }
    void Start() override {
        if (_logger) { _logger->info("Hello.Cpp", "Bonjour !"); }
        else { std::cout << "[Hello.Cpp] Bonjour !" << std::endl; }
    }
};
```

Placez cette classe dans le namespace `HelloCpp`, comme dans le sample.

== Java

```java
@PluginImport(value = ILogger.class, optional = true)
public final class Plugin implements IPlugin {
    private ILogger logger;
    @Override
    public void init(PluginContext context) {
        logger = context.services().getService(ILogger.class);
    }
    @Override
    public void start() {
        if (logger != null) logger.info("Hello.Java", "Bonjour !");
        else System.out.println("[Hello.Java] Bonjour !");
    }
}
```

Conservez `package hello;` et les imports `omnilingua.java` du sample.

== Python

```python
@PluginImport(ILogger, optional=True)
class Plugin(IPlugin):
    def init(self, context: PluginContext) -> None:
        self._logger = context.services.get_service(ILogger)

    def start(self) -> None:
        if self._logger is not None:
            self._logger.info("Hello.Python", "Bonjour !")
        else:
            print("[Hello.Python] Bonjour !", flush=True)
```

Conservez les imports du sample. `get_service` retourne `None` si le service est absent.

:::

L'optionnel autorise un fournisseur absent. Il ne masque pas un fournisseur présent
mais invalide, par exemple une méthode requise manquante. N'utilisez pas la résolution
obligatoire pour ce repli, sinon votre propre code échouera malgré l'annotation.

## 4. Construire Hello

Creez un projet Hello distinct avec les dependances du setup, sans reference au
projet Logger.

| Langage | Artefact a placer dans le dossier Omnilingua |
| --- | --- |
| .NET | Hello.Dotnet.dll |
| C++ | Hello.Cpp.dll ou libHello.Cpp.so |
| Java | Hello.Java.jar |
| Python | Dossier Hello.Python avec plugin.py et logger_contract.py |

En C++, conservez le header Plugin.h dans omnilingua_add_plugin. Voir les [scripts de build](./scripts-build)
pour automatiser. Il reste a [connecter Hello au fournisseur](./connecter).
