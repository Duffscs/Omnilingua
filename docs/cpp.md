# Développer un plugin C++

Le SDK Omnilingua.Cpp fournit des headers C++20 et une integration CMake, utilisables
sous Windows et Linux. Le dossier build/native du package NuGet contient
OmnilinguaCppConfig.cmake ; indiquez ce dossier dans OmnilinguaCpp_DIR ou installez le SDK
avec CMake et utilisez CMAKE_PREFIX_PATH.

```cmake
cmake_minimum_required(VERSION 3.24)
project(MonPlugin LANGUAGES CXX)
find_package(OmnilinguaCpp CONFIG REQUIRED)
omnilingua_add_plugin(MonPlugin SOURCES Plugin.cpp HEADERS Plugin.h)
install(TARGETS MonPlugin RUNTIME DESTINATION . LIBRARY DESTINATION .)
```

Les .vcxproj Visual Studio sont generes par CMake si ce generateur est choisi.
Ils ne sont plus les fichiers sources du projet. Ninja et Make sont egalement
utilisables. Les bibliotheques produites sont des .dll sous Windows et des .so sous Linux.

## Interface simple générée

```cpp
#include <Omnilingua/Export.h>
#include <string_view>
namespace Example {
    class ILogger : public virtual Omnilingua::Cpp::IService {
    public:
        virtual ~ILogger() = default;
        virtual void info(std::string_view context, std::string_view message) = 0;
    };
}
```

Les interfaces ne nécessitent pas d'annotation. Leur nom devient le nom du contrat,
avec version 1 par défaut. OMNILINGUA_PLUGIN_CONTRACT("Nom", version) permet de les préciser.
Le générateur utilise Python et libclang **à la compilation**. Configurer au besoin
Python3_EXECUTABLE, OMNILINGUA_CPP_PYTHON_PATH, OMNILINGUA_CPP_LIBCLANG et OMNILINGUA_CPP_SYSTEM_INCLUDES.
Utiliser des headers MSVC compatibles avec la version de libclang.

Les plugins travaillent avec des classes et leurs interfaces locales. Pour un
consommateur, déclarer sa classe dans Plugin.h avec ses annotations :

```cpp
#include "Contracts/ILogger.h"
#include <Omnilingua/Plugin.h>

class OMNILINGUA_PLUGIN_IMPORT(Example::ILogger) Plugin final : public Omnilingua::Cpp::IPlugin {
    Example::ILogger* _logger = nullptr;
public:
    void Init(const Omnilingua::Cpp::PluginContext& context) override {
        _logger = &context.Services.GetRequiredService<Example::ILogger>();
    }
    void Start() override { _logger->info("MonPlugin", "Bonjour !"); }
};
```

Pour fournir un service, annoter le plugin OMNILINGUA_PLUGIN_EXPORT(Example::ILogger)
et retourner les objets depuis GetExport() :

```cpp
class OMNILINGUA_PLUGIN_EXPORT(Example::ILogger) Plugin final : public Omnilingua::Cpp::IPlugin {
    Logger _logger;
public:
    std::vector<Omnilingua::Cpp::IService*> GetExport() override { return {&_logger}; }
};
```

Déclarer le header avec `omnilingua_add_plugin(... HEADERS Plugin.h)`. Dans Plugin.cpp,
inclure `Plugin.g.h` puis écrire `OMNILINGUA_PLUGIN(Plugin)` une seule fois.
Le générateur analyse les interfaces référencées, produit les métadonnées et les
adaptateurs. Le SDK lie les objets après Init, comme pour les autres langages.
GetExport, Init, Start et Stop possèdent des implémentations vides par défaut.
Chaque contrat doit avoir une unique implémentation dans GetExport ; les objets
nuls, non déclarés, manquants ou ambigus sont refusés. Les interfaces héritent
virtuellement de IService pour permettre à un objet d'en implémenter plusieurs.
Le Omnilingua continue d'utiliser son protocole C ABI version 3 : aucun objet C++ ni
table virtuelle ne traverse la frontière entre modules ou langages.

`OMNILINGUA_PLUGIN_IMPORT(Interface, true)` demande un service optionnel ;
`context.Services.GetService<Interface>()` retourne alors nullptr s'il est absent.
GetRequiredService retourne une référence ou lève une exception locale si le
service est absent ou incomplet. Les proxys copient et valident les chaînes UTF-8,
convertissent les booléens et sont invalidés à Stop. Les objets doivent rester
dans leur module ; aucune référence ne doit survivre au déchargement de la DLL.
Configuration est une vue empruntée pendant Init : la copier pour la conserver.

Le générateur couvre les méthodes publiques virtuelles pures avec void, bool,
int32_t, uint32_t, uint64_t et string_view en entrée. Les structures et listes
nécessitent encore les déclarations manuelles ci-dessous.

## Contrats structurés et consommation

`Omnilingua/Declaration.h` fournit OMNILINGUA_RECORD, OMNILINGUA_FIELD, OMNILINGUA_OPTIONAL_FIELD,
OMNILINGUA_SENTINEL_FIELD, OMNILINGUA_ARRAY_FIELD, OMNILINGUA_CONTRACT et OMNILINGUA_METHOD. Une même table
sert à la description et à GetExport ; les templates vérifient les signatures
Cdecl noexcept exactes. Value/Reference décrivent le résultat, In/InRecord et
Out/OutRecord décrivent les paramètres ; maximum 16 paramètres.

Séparer les modèles métier propriétaires des structures ABI à entiers fixes,
pointeurs UTF-8 et compteurs explicites. Boolean32 est un int32_t, jamais un bool
ABI. La disposition, les bits de présence et limites de tableaux appartiennent
au contrat versionné. `PluginContract.Flags & 1` indique une dépendance optionnelle.

Pour consommer un contrat structuré non couvert par le générateur, déclarer sa dépendance dans les métadonnées,
recevoir les adresses via Plugin_Inject et les encapsuler derrière une interface
ou classe locale. La version actuelle ne fournit pas un proxy C++ universel de
tous les contrats. Ne jamais caster une adresse sans vérifier le contrat annoncé.

Les noms Ready, Inject et Invalidate sont réservés aux adaptateurs générés.
Les méthodes ordinaires peuvent lever une exception locale ; les méthodes noexcept
journalisent les erreurs et retournent zéro pour un résultat scalaire. Les fonctions
ABI Init/Start/Stop/Inject retournent un statut au Omnilingua. Le SDK intercepte leurs
exceptions et fournit un diagnostic ; le Omnilingua signale l’échec du chargement ou
du cycle de vie sans laisser une exception traverser l’ABI.

## Copies

`Omnilingua/Mapping.h` propose MapRecord, MapField, MapOptional, MapNullable, MapArray,
Path et ValidateMapping. Read reconstruit les modèles propriétaires. Snapshot.Set
possède une copie des chaînes et tableaux jusqu'au prochain Set réussi sur cette
instance ou à sa destruction ; un échec conserve le snapshot précédent. Une
instance thread_local par getter évite un buffer mutable partagé entre threads.

Aucun std::vector, std::string, allocateur ou objet propriétaire ne traverse l'ABI.
Les entrées pointées sont empruntées pendant l'appel : les copier avant stockage.
Les consommateurs copient immédiatement les sorties. Traiter toutes les exceptions
avant le retour d'une fonction ABI ; le Omnilingua ne protège pas d'un pointeur invalide.
