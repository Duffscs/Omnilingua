import { defineConfig } from 'vitepress'
import { tabsMarkdownPlugin } from 'vitepress-plugin-tabs'

export default defineConfig({
  lang: 'fr-FR',
  title: 'Omnilingua',
  description: 'Développer et connecter des plugins .NET, C++, Java et Python.',
  base: process.env.VITEPRESS_BASE || './',
  srcExclude: ['snippets/**', 'tests/**', 'test-results/**', 'playwright-report/**'],
  lastUpdated: false,
  markdown: {
    config(md) { md.use(tabsMarkdownPlugin) },
    lineNumbers: true
  },
  themeConfig: {
    siteTitle: 'Omnilingua · Documentation',
    nav: [
      { text: 'Guide utilisateur', link: '/guide/setup' },
      { text: 'Référence SDK', link: '/installation' },
      { text: 'Contribuer', link: '/contribution' }
    ],
    sidebar: [
      { text: 'Vos premiers plugins', items: [
        { text: '01 · Setup', link: '/guide/setup' },
        { text: '02 · Premier plugin', link: '/guide/premier-plugin' },
        { text: '03 · Exporter un service', link: '/guide/exporter' },
        { text: '04 · Importer un service', link: '/guide/importer' },
        { text: '05 · Connecter et lancer', link: '/guide/connecter' },
        { text: 'Automatiser le build', link: '/guide/scripts-build' }
      ] },
      { text: 'Référence', collapsed: true, items: [
        { text: 'Installation et cycle de vie', link: '/installation' },
        { text: '.NET', link: '/dotnet' },
        { text: 'C++', link: '/cpp' },
        { text: 'Java', link: '/java' },
        { text: 'Python', link: '/python' },
        { text: 'Contribution', link: '/contribution' }
      ] }
    ],
    search: { provider: 'local', options: { locales: { root: { translations: {
      button: { buttonText: 'Rechercher', buttonAriaLabel: 'Rechercher dans la documentation' },
      modal: { noResultsText: 'Aucun résultat pour', resetButtonTitle: 'Effacer',
        footer: { selectText: 'sélectionner', navigateText: 'naviguer', closeText: 'fermer' } }
    } } } } },
    outline: { label: 'Sur cette page', level: [2, 3] },
    docFooter: { prev: 'Précédent', next: 'Suivant' },
    darkModeSwitchLabel: 'Apparence',
    sidebarMenuLabel: 'Parcourir le guide',
    returnToTopLabel: 'Retour en haut',
    footer: { message: 'Omnilingua · Windows x64 · Documentation des API disponibles' }
  }
})
