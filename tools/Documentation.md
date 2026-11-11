# Maintenir la documentation utilisateur

Le site utilise VitePress et vitepress-plugin-tabs. Les pages sont dans docs/guide,
les snippets du premier plugin dans docs/snippets. Les chapitres Logger et Hello
importent directement les fichiers SamplePlugins avec la syntaxe native VitePress.
Un sample absent casse le build au lieu de laisser un exemple périmé.

```powershell
cd docs
npm ci
npm run dev
```

Le serveur de développement écoute uniquement sur 127.0.0.1. Les onglets partagent
la clé language via vitepress-plugin-tabs ; le configurateur est un composant Vue.
VitePress fournit navigation, recherche locale, coloration et copie du code.

Depuis la racine de Omnilingua, `./tools/Build-Documentation.ps1` produit le site
dans docs/.vitepress/dist et l'archive artifacts/Omnilingua.Documentation-9.9.9.zip.
Le build plateforme l'exécute avant les packages pour embarquer les mêmes pages
et assets dans NuGet, le JAR, la wheel et le produit. Ne jamais empaqueter docs/
récursivement : ce dossier contient node_modules et les sources du site.

La version VitePress est verrouillee dans package-lock.json. Node.js 22.12+
est necessaire au build du site, pas a l'utilisation des plugins.

Il n'y a pas de tests navigateur dedies a la documentation. La construction
VitePress verifie les liens et les imports de snippets. Le guide de contribution
public est docs/contribution.md.

## GitHub Pages

Le workflow `.github/workflows/documentation-pages.yml` construit et publie le site
lors des modifications de `docs` ou des samples sur `main`. Un lancement manuel
est également disponible dans GitHub Actions.

Dans les paramètres du dépôt GitHub, ouvrir **Pages**, puis sélectionner
**GitHub Actions** comme source dans **Build and deployment**. Pousser ensuite
le workflow sur `main` ou le lancer depuis l'onglet **Actions**.

L'adresse prévue est https://duffscs.github.io/Omnilingua/. Le workflow récupère
le chemin auprès de GitHub Pages et le transmet par `VITEPRESS_BASE` : les assets
et les liens utilisent ainsi `/Omnilingua/`. Les builds locaux et les packages
conservent une base relative `./` lorsque cette variable n'est pas définie.

Pour reproduire le build publié depuis le dossier `docs` :

```powershell
$env:VITEPRESS_BASE = '/Omnilingua/'
npm run build
Remove-Item Env:VITEPRESS_BASE
```
