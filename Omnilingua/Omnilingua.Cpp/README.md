# Omnilingua.Cpp

Le [générateur d’exports](generator/README.md) permet de déclarer les nouvelles
services avec `OMNILINGUA_PLUGIN_EXPORT` et `OMNILINGUA_PLUGIN_IMPORT` sur les classes plugins.
Les interfaces ordinaires héritent virtuellement de IService. Il produit les descriptions et
trampolines utilisés dans `SamplePlugins/Logger/Cpp`. Les outils manuels ci-dessous
restent utilisés pour les structures et autres formes non encore générées.

`Omnilingua/Plugin.h` fournit IPlugin et PluginContext. GetExport retourne les objets
de service sous forme de `std::vector<Omnilingua::Cpp::IService*>`. Pour les imports,
utiliser `context.Services.GetRequiredService<Interface>()` dans Init.
`OMNILINGUA_PLUGIN(ClassePlugin)` émet l'unique point d'entrée `OmnilinguaPlugin_GetApi` du module. Les exemples
`SamplePlugins/Hello/Cpp` et `SamplePlugins/Logger/Cpp` ne manipulent que des classes
et interfaces.

Bibliothèque C++20 composée uniquement de headers, équivalente à `Omnilingua.Dotnet`
pour les auteurs de plugins natifs. Elle fournit le protocole C ABI et des outils
de déclaration ; elle ne contient aucun contrat métier. Aucune DLL supplémentaire
n’est nécessaire à l’exécution.

Charger OmnilinguaCpp avec find_package(OmnilinguaCpp CONFIG REQUIRED), puis utiliser
omnilingua_add_plugin. La cible Omnilingua::Cpp fournit les headers et C++20. Les fonctions
CMake sont livrees avec le package NuGet et installables depuis CMakeLists.txt.

```cpp
#include <Omnilingua/Declaration.h>
using namespace Omnilingua::Cpp;

struct Position { std::int32_t X; std::int32_t Y; };
OMNILINGUA_RECORD(PositionType, Position, "Position",
    OMNILINGUA_FIELD(Position, X, Int32),
    OMNILINGUA_FIELD(Position, Y, Int32)
);

std::int32_t OMNILINGUA_CALL Move(const Position* position) noexcept {
    // Appliquer le déplacement ; aucun objet C++ ne traverse l’ABI.
    return 1;
}

OMNILINGUA_CONTRACT(Exports, "IExample", 1,
    OMNILINGUA_METHOD(Move, Value(Boolean32), InRecord("position", PositionType))
);
```

`Exports.Description()` fournit un `PluginContract` pour la table `OmnilinguaPlugin_GetApi`.
`Exports.GetExport(contract, method)` résout l’adresse pour `OmnilinguaPluginApi.GetExport`,
ou retourne `nullptr` si la demande est inconnue. Les noms, signatures, nombres
de paramètres et adresses proviennent ainsi d’une seule table.

Conserver les descriptions de types, les contrats et les métadonnées au niveau
du namespace pendant toute la durée de chargement du plugin. Les contrats ne
sont ni copiables ni déplaçables : leurs tables contiennent des pointeurs internes.
Le protocole C ABI est en version 3. `PluginContract.Flags & 1` décrit une
dépendance optionnelle ; reconstruire les fournisseurs et le Omnilingua ensemble.

## Champs et paramètres

- `OMNILINGUA_FIELD` calcule le nom et l’offset et vérifie le type C++ du champ.
- `OMNILINGUA_OPTIONAL_FIELD` ajoute un masque de présence `uint32_t`.
- `OMNILINGUA_SENTINEL_FIELD` déclare une valeur signifiant l’absence, par exemple `-1`.
- `OMNILINGUA_ARRAY_FIELD` relie un pointeur de tableau à son compteur `uint32_t`.
- `Array` décrit un tableau borné ; `Sequence` décrit un résultat liste avec son
  pointeur et son compteur. Les limites restent explicites.
- `Value` et `Reference` décrivent le résultat ; `In`, `InRecord`, `Out` et
  `OutRecord` décrivent les paramètres.

`OMNILINGUA_METHOD` vérifie la signature C++ exacte, y compris les pointeurs et leur
constance. Les fonctions doivent utiliser `OMNILINGUA_CALL` et être `noexcept`. Une
méthode accepte au maximum 16 paramètres. Les exceptions doivent être traitées
dans l’adaptateur du plugin avant de traverser la frontière C ABI.

`Boolean32` utilise `int32_t`, et non `bool`. Le choix sémantique des types,
les règles de présence et la durée de vie des buffers restent à déclarer par
l’auteur. Les données empruntées doivent rester valides pendant leur copie par
le consommateur ; aucun `std::vector`, `std::string` ni objet C++ propriétaire
n’est transmis directement au Omnilingua.

## Conversion des modèles et durée de vie

`Omnilingua/Mapping.h` fournit des correspondances bidirectionnelles entre modèles
propriétaires et structures C ABI. La conversion Unicode stricte est fournie par
`Omnilingua/TextEncoding.h` ; elle prend en charge UTF-8 et les chaînes larges UTF-16
ou UTF-32 selon la plateforme.

```cpp
struct Person { std::wstring Name; };
struct PersonAbi { const char* Name; };
constexpr auto PersonMapping = Omnilingua::Cpp::MapRecord<Person, PersonAbi>(
    Omnilingua::Cpp::MapField<&PersonAbi::Name>(
        Omnilingua::Cpp::Path<&Person::Name>{})
);

// Dans un point d’entrée C ABI qui traite ses exceptions :
thread_local Omnilingua::Cpp::Snapshot snapshot(PersonMapping);
const PersonAbi* result = snapshot.Set(Person{L"Alice"});
Person ownedCopy = PersonMapping.Read(result);
```

- `Path` peut parcourir plusieurs membres imbriqués du modèle métier.
- `MapField` copie les scalaires et les chaînes, et convertit les booléens 32 bits.
- `MapOptional` relie un `std::optional` à un bit de présence.
- `MapNullable` relie un `std::optional` à une sentinelle entière.
- `MapArray` copie un `std::vector` et tous ses éléments avec une limite explicite.
- `ValidateMapping` ajoute une validation propre au contrat.

`Read` reconstruit des valeurs propriétaires et ne conserve aucun pointeur ABI.
`Snapshot.Set` construit une copie dans ses propres buffers, y compris les chaînes
et tableaux imbriqués. Les pointeurs retournés sont empruntés jusqu’au prochain
`Set` réussi sur cette instance ou jusqu’à sa destruction. Un échec conserve le
résultat précédent et libère les allocations partielles. Les limites sont de
131072 octets par chaîne UTF-8 et au maximum 65536 éléments par tableau ; chaque
correspondance de tableau déclare sa propre limite inférieure ou égale.

Utiliser une instance `thread_local` par opération retournant un résultat emprunté.
Le consommateur doit copier le résultat avant un nouvel appel à cette opération
sur le même thread. Les pointeurs entrants doivent être valides pendant toute la
lecture ; ces outils ne constituent pas une protection contre des adresses C++
invalides. Le Omnilingua effectue ses propres copies entre les buffers ABI et les objets
du consommateur .NET ; il ne gère pas la mémoire du snapshot du fournisseur.

Les correspondances métier restent dans le plugin. Le runtime et Omnilingua.Cpp ne
référencent aucun de ces modèles.

## Vérification

Configurer le projet avec CMake, compiler puis lancer CTest. Les suites
`Omnilingua.Cpp.Tests`, `Cpp.CodegenTests` et `Cpp.PluginTests` couvrent les conversions,
les liaisons générées et le chargement de bibliothèques réelles.
