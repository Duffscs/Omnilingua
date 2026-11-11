# SDK Omnilingua — documentation publique

Cette documentation accompagne les packages du SDK et l'archive du produit Omnilingua.
Elle suffit pour développer un plugin sans disposer des sources de la plateforme.
La distribution actuelle cible Windows x64.

Le **guide utilisateur interactif VitePress** est livré dans `docs/site/` avec
les packages et le produit, ainsi que dans `l'archive de documentation`.
Servez ce dossier avec votre serveur statique, par exemple :

```powershell
python -m http.server 8080 --bind 127.0.0.1 --directory docs/site
```

Ouvrez ensuite `http://127.0.0.1:8080`. Le site généré n'exige ni Node.js ni
l'accès aux sources de la plateforme. Les pages Markdown ci-dessous restent la
référence textuelle. Le guide couvre setup, premier plugin, export, import et
connexion, avec des onglets .NET/C++/Java/Python et un configurateur JSON.

- [Installation, configuration et cycle de vie](installation.md)
- [Contrats, injection et exports .NET](dotnet.md)
- [Plugins et données C++](cpp.md)
- [SDK Java](java.md)
- [SDK Python](python.md)
- [Scripts de build](guide/scripts-build.md)
- [Contribution](contribution.md)

Un contrat décrit un service nommé et versionné, ses méthodes, paramètres et types.
Un consommateur définit sa propre interface locale ; il ne référence pas la classe
ni le langage du fournisseur. Le Omnilingua vérifie les descriptions et construit les
proxies .NET. Les données sont copiées aux frontières ; les appels sont synchrones
et passent par des fonctions C ABI, pas par un protocole réseau.

Les API et limites décrites ici sont celles réellement disponibles. Une évolution
de contrat, de signature ou de disposition binaire doit préserver la compatibilité
des consommateurs, ou changer explicitement de version. En cas de problème,
fournir version, contrat, méthode, fournisseur, configuration et reproduction
minimale avec les API publiques, sans dépendre d'une implémentation interne du Omnilingua.
