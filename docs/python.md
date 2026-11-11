# Référence Python

## Prérequis et dépendance

| Élément | Version / besoin |
| --- | --- |
| Interpréteur | CPython 3.13 standard x64, version exacte actuelle |
| SDK | Package `omnilingua-python`, même version que le Omnilingua |
| IDE | Au choix, avec l'interpréteur Python sélectionné |
| Environnements virtuels et Python sans GIL | Non pris en charge actuellement |

Ajoutez `omnilingua-python` aux dépendances de votre projet. Le Omnilingua utilise Python
installé sur la machine ; `pythonExecutable`, puis `PYTHON_EXECUTABLE`, puis le
`PATH` déterminent l'exécutable sélectionné.

## Classe plugin

La classe hérite de `omnilingua_python.IPlugin`, avec un constructeur sans argument.
`init`, `start`, `stop` et `close` sont facultatifs. `get_export()` retourne une
liste d'objets de service. Conservez ensemble les modules utilisés par imports relatifs.

## Décorateurs des contrats

| Décorateur sur le plugin | Effet |
| --- | --- |
| `@PluginExport(ILogger)` | Annonce le service fourni par les objets de `get_export()` |
| `@PluginImport(ILogger)` | Demande un service obligatoire |
| `@PluginImport(ILogger, optional=True)` | Autorise son absence |

Le nom simple de l'interface et la version 1 sont utilisés par défaut.
`name` et `version` permettent de les préciser. Plusieurs décorateurs peuvent être
appliqués au même plugin. Le SDK déduit les métadonnées depuis ces déclarations et
les annotations de type des méthodes publiques.

## Services importés

Dans `init`, utilisez le contexte :

```python
self._logger = context.services.get_required_service(ILogger)
```

Pour un import optionnel, `get_service(ILogger)` retourne `None` s'il est absent.
La clé `dependencies` du JSON choisit le fournisseur. Son langage et sa classe
d'implémentation n'interviennent pas dans le code du consommateur.

## Types pris en charge

| Élément | Prise en charge actuelle |
| --- | --- |
| Paramètres | De 0 à 16 `str`, annotés |
| Retour | `None`, annoté `-> None` |
| Appels | Synchrones |
| Objets complexes et méthodes async | Non pris en charge |

Les imports relatifs sont pris en charge. Les dépendances tierces utilisent
l'installation Python choisie ; elles ne sont pas isolées par plugin.
Arrêtez vos threads et libérez vos ressources dans `stop`/`close`.

Voir [exporter](./guide/exporter), [importer](./guide/importer) et
[scripts de build](./guide/scripts-build).
