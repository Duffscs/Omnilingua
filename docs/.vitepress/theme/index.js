import DefaultTheme from 'vitepress/theme'
import { enhanceAppWithTabs } from 'vitepress-plugin-tabs/client'
import ConnectionPlayground from './ConnectionPlayground.vue'
import './style.css'

export default {
  extends: DefaultTheme,
  enhanceApp({ app }) {
    enhanceAppWithTabs(app)
    app.component('ConnectionPlayground', ConnectionPlayground)
  }
}
