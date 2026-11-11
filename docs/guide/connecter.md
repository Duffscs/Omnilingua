# Connecter les deux plugins

Les annotations disent **quel contrat** un plugin fournit ou demande.
La configuration indique **quel plugin** fournit ce contrat à Hello.
Les deux déclarations sont nécessaires.

## Une dépendance obligatoire non configurée est une erreur

Avec la version obligatoire de Hello, ajouter les deux entrées au JSON sans
`dependencies` ne suffit pas. Le Omnilingua ne choisit pas automatiquement un Logger,
même s'il n'y en a qu'un dans le fichier. Pour Hello .NET, le diagnostic est :

```text
Hello.Dotnet: aucun fournisseur configuré pour ILogger.
```

La configuration est rejetée avant le démarrage. Si Hello ne porte pas du tout
d'annotation d'import, le Omnilingua ne déduit pas son besoin depuis les appels au
contexte : la résolution obligatoire du service échouera dans votre code.

## Relier ILogger à un fournisseur nommé

Dans Hello, ajoutez `"dependencies": { "ILogger": "Journal" }`.
`ILogger` est le nom du **contrat**, `Journal` est le champ **name** de l'entrée
du Logger. Le nom de la classe ou du fichier ne remplace pas ce nom de plugin.

Choisissez les langages, puis décochez la dépendance pour voir la configuration
incorrecte et le diagnostic correspondant. Cet exemple est un aperçu de configuration,
il n'exécute pas les plugins dans le navigateur.

<ConnectionPlayground />

Un Logger Java peut servir Hello .NET, un Logger C++ peut servir Hello Python, etc.
Le code de Hello et son interface locale ne changent pas. Les cinq méthodes du Logger
restent disponibles aux consommateurs qui les déclarent dans leur propre contrat.

## Placer les artefacts près du Omnilingua

Pour l'exemple Java → .NET affiché par défaut :

```text
Omnilingua/
  Omnilingua.exe
  … fichiers fournis dans l'archive du produit …
  Logger.Java.jar
  Hello.Dotnet.dll
  plugins.json
```

Conservez les fichiers du produit décompressé, notamment les bridges et SDK.
Pour Python, copiez tout le dossier du plugin avec ses modules relatifs.
Les chemins `assembly` sont résolus **depuis le dossier de plugins.json**.

## Lancer les plugins

Enregistrez le JSON généré dans `plugins.json`. Depuis le dossier du produit :

```powershell
.\Omnilingua.exe .\plugins.json
```

Avec Logger Java et Hello .NET, la sortie contient notamment :

```text
[LoggerJava] … [INFO] [Hello.Dotnet] Bonjour depuis le plugin .NET !
[Omnilingua] Prêt. Ctrl+C pour arrêter.
```

Le Omnilingua démarre le fournisseur avant Hello et arrête Hello avant le fournisseur.
Utilisez **Ctrl+C** pour déclencher cet arrêt propre. Il reste actif après le message
de Hello ; ce n'est pas un programme qui se termine immédiatement après le log.

## Vérifier un import optionnel

Avec la variante optionnelle du chapitre précédent, retirez l'entrée Logger
et la clé `dependencies` de Hello. Hello démarre et utilise son repli console.
Avec la variante obligatoire, cette même configuration doit être refusée.

Si vous configurez un fournisseur présent mais qu'il ne fournit pas `ILogger`
ou une méthode nécessaire, c'est une erreur. Les cycles de dépendances sont
également refusés. Pour rechercher un problème :

| Vérifier | Valeur attendue dans cet exemple |
| --- | --- |
| Annotation de Hello | Import `ILogger`, version 1 |
| Annotation du Logger | Export `ILogger`, version 1 |
| Liaison JSON de Hello | `"ILogger": "Journal"` |
| Entrée du fournisseur | `"name": "Journal"` |
| Fichiers | Présents aux chemins `assembly` |
| Runtimes | .NET 10 et runtime Java/Python selon les plugins choisis |

Vous avez maintenant un fournisseur et un consommateur indépendants. Pour aller
plus loin, consultez la référence [.NET](../dotnet), [C++](../cpp) ou
[Java](../java) ou [Python](../python).
