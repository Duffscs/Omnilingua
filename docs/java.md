# Référence Java

## Prérequis et dépendance

| Élément | Version / besoin |
| --- | --- |
| JDK pour développer | 17 minimum, x64 |
| Java pour exécuter | 17 minimum, x64 |
| SDK | Maven `io.omnilingua:omnilingua-java`, même version que le Omnilingua |
| IDE | Au choix, avec prise en charge des projets Java |

Ajoutez la dépendance à votre projet avec son gestionnaire habituel.
`JAVA_HOME` ou le champ `javaHome` de la configuration permet de sélectionner Java.

## Classe plugin

La classe implémente `omnilingua.java.IPlugin` et possède un constructeur public sans
paramètre. `init`, `start`, `stop` et `close` sont facultatifs. Redéfinissez seulement
les étapes utiles. `getExport()` retourne un tableau d'objets de service.

## Annotations des contrats

| Annotation sur le plugin | Effet |
| --- | --- |
| `@PluginExport(ILogger.class)` | Annonce le contrat fourni par les objets de `getExport()` |
| `@PluginImport(ILogger.class)` | Demande un service obligatoire |
| `@PluginImport(value = ILogger.class, optional = true)` | Autorise l'absence du service |

Les annotations sont répétables. `name` et `version` personnalisent le nom et la
version du contrat ; par défaut, nom simple de l'interface et version 1.
Toutes les méthodes publiques d'instance de l'interface décrivent ses opérations.
Le SDK déduit les métadonnées depuis ces annotations.

## Services importés

Les services sont disponibles dans `init` via `context.services()` :

```java
logger = context.services().getRequiredService(ILogger.class);
```

Pour un import optionnel, `getService(ILogger.class)` retourne `null` s'il est absent.
La clé `dependencies` de l'entrée JSON sélectionne le fournisseur par son nom.
Une interface locale suffit : aucun projet du fournisseur n'est à référencer.

## Types pris en charge

| Élément | Prise en charge actuelle |
| --- | --- |
| Paramètres | De 0 à 16 `String` |
| Retour | `void` |
| Appels | Synchrones, sans surcharge de noms |
| Types complexes et méthodes génériques | Non pris en charge |

Conservez les ressources pendant l'utilisation du service et libérez-les dans
`stop`/`close`. Le Omnilingua démarre les fournisseurs avant les consommateurs et les
arrête dans l'ordre inverse.

Voir [exporter](./guide/exporter), [importer](./guide/importer) et
[scripts de build](./guide/scripts-build).
