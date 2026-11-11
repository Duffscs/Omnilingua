# Generation des services C++

Le generateur Clang lit OMNILINGUA_PLUGIN_EXPORT(Interface), OMNILINGUA_PLUGIN_IMPORT(Interface)
et OMNILINGUA_PLUGIN_IMPORT(Interface, true) sur les classes qui implementent IPlugin.
Les interfaces sont des classes ordinaires avec des methodes virtuelles pures et
une base virtuelle Omnilingua::Cpp::IService. Leur nom et version 1 sont deduits ;
OMNILINGUA_PLUGIN_CONTRACT("Nom", version) est facultatif pour les personnaliser.

GetExport() retourne un vector<Omnilingua::Cpp::IService*> des objets fournis, conserves
par le plugin. Les metadonnees sont disponibles avant Init ; les objets sont lies
apres Init. Le SDK refuse les objets nuls, absents, ambigus ou non declares.
Chaque instance du plugin dispose de ses propres liaisons. Le generateur reserve
32 liaisons simultanees par interface et par module ; un depassement est refuse. Un objet peut implementer
plusieurs interfaces grace a la base virtuelle commune. Les imports sont resolus
via context.Services.GetRequiredService<Interface>() ou GetService pour un besoin
optionnel. Le plugin n'ecrit aucune methode Configure ni metadonnee manuelle.

Declarer Plugin.h dans omnilingua_add_plugin et inclure Plugin.g.h dans Plugin.cpp, puis
OMNILINGUA_PLUGIN(Namespace::Plugin). Le .g.h produit les adaptateurs d'import/export
et PluginDeclaration. Le SDK conserve le protocole C ABI v3 et intercepte les
exceptions avant la frontiere. GetExport, Init, Start et Stop sont vides par defaut.

Le guide public avec exemples est [docs/cpp.md](../../../docs/cpp.md).
Les exemples sont SamplePlugins/Logger/Cpp et SamplePlugins/Hello/Cpp.

Les chaines UTF-8 sont validees et copiees ; string_view reste valide pendant
l'appel. Copier un texte pour le conserver. Configuration est empruntee pendant
Init. Les proxys sont invalides apres Stop et ne doivent jamais survivre au
dechargement du module. Aucun objet C++ ne traverse directement l'ABI.

## Outillage de développement

Clang n’est pas requis chez l’utilisateur final : seul le plugin compilé est
distribué. Installer libclang dans un environnement Python ou dans le cache local :

```powershell
python -m pip install --target .tmp/cpp-codegen -r Omnilingua/Omnilingua.Cpp/generator/requirements.txt
```

OMNILINGUA_CPP_PYTHON_PATH selectionne un environnement Python ; Python3_EXECUTABLE
selectionne l'interpreteur. OMNILINGUA_CPP_LIBCLANG selectionne la bibliotheque native.
La version validée est libclang 18.1.1.

Les headers standard doivent être compatibles avec Clang. L’exemple analyse ceux
de MSVC 14.44 installés sur cette machine avec libclang 18 ; les headers MSVC 14.51
demandent des builtins Clang plus récents. MSVC compile ensuite avec son toolset
v145 courant. Sur une autre installation, sélectionner des headers compatibles via
`OMNILINGUA_CPP_SYSTEM_INCLUDES` ou un libclang adapté à ceux du compilateur. Les diagnostics
de parsing restent bloquants.

## CMake

Utiliser find_package(OmnilinguaCpp CONFIG REQUIRED), puis
omnilingua_add_plugin(MonPlugin SOURCES Plugin.cpp HEADERS Plugin.h).
Le generateur est lance automatiquement pour chaque header, avec un dossier
prive au target. Les headers doivent avoir des noms distincts.
OMNILINGUA_CPP_PYTHON_PATH selectionne les bindings Python, OMNILINGUA_CPP_LIBCLANG la
bibliotheque native, OMNILINGUA_CPP_SYSTEM_INCLUDES les headers standard compatibles.
Les includes du compilateur CMake sont utilises par defaut. MSVC, GCC et Clang
sont pris en charge ; les versions du parseur et des headers doivent etre compatibles.
Les anciens fichiers MSBuild du SDK restent disponibles pour les consommateurs
qui les utilisent deja, mais les samples sont exclusivement decrits en CMake.

## Types et limites

Les interfaces non génériques déclarent des méthodes virtuelles publiques pures,
avec noms uniques et au plus seize paramètres. Sont pris en charge : bool converti
en Boolean32, int32, uint32, uint64, string_view UTF-8 en entrée et void en retour.
Les méthodes const et noexcept simples sont acceptées.

Ready, Inject et Invalidate sont réservés à l'adaptateur généré. Une erreur dans
un appel noexcept est journalisée et produit zéro pour un résultat scalaire ; une
méthode ordinaire peut lever une exception locale avant de franchir l'ABI.
Init/Start/Stop/Inject retournent un statut au Omnilingua via la table OmnilinguaPlugin_GetApi.
Les exceptions de ces points d’entrée sont interceptées par le SDK ; le Omnilingua
copie leur diagnostic et signale l’échec sans franchir la frontière ABI.

Les surcharges, modèles de fonctions, champs, héritages de contrats, paramètres
ref/out et types complexes sont refusés avec un diagnostic sur la déclaration.
Les structures, listes et champs optionnels des contrats complexes nécessitent une
extension ultérieure : ses descriptions et mappings manuels restent donc utilisés.

## Exemples et tests

- `SamplePlugins/Logger/Cpp/Contracts/ILogger.h` : interface ordinaire du service.
- `SamplePlugins/Logger/Cpp/Logger.cpp` : implémentation et en-tête `[LoggerCPP]`.
- `SamplePlugins/Logger/Cpp/Plugin.h` : classe annotée et objets fournis par GetExport.
- `SamplePlugins/Hello/Cpp/Plugin.h` : annotation d'import et interface injectée.
- `tests/Cpp.CodegenTests` : diagnostics et contrat indépendant avec valeurs et exceptions.
- `tests/Logger.SmokeTests` : cinq appels identiques pour C++, Java et Python,
  vérifiant les en-têtes, le contexte et le message Unicode conservé.
