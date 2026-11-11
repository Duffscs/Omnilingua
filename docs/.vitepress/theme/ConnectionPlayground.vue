<script setup>
import { computed, ref } from 'vue'

const languages = { dotnet: '.NET', cpp: 'C++', java: 'Java', python: 'Python' }
const provider = ref('java')
const consumer = ref('dotnet')
const connected = ref(true)
const copied = ref('')

// Les chemins sont relatifs à plugins.json, comme dans le chargeur du produit.
function entry(role, language) {
  const names = { dotnet: 'Dotnet', cpp: 'Cpp', java: 'Java', python: 'Python' }
  const name = `${role}.${names[language]}`
  const assembly = language === 'java' ? `${name}.jar` : language === 'python' ? `${name}/plugin.py` : `${name}.dll`
  const result = { name: role === 'Logger' ? 'Journal' : name, type: language, assembly }
  if (language !== 'cpp') result.entryPoint = language === 'dotnet' ? `${name}.Plugin` : language === 'java' ? `${role.toLowerCase()}.Plugin` : 'Plugin'
  return result
}

const configuration = computed(() => {
  const logger = entry('Logger', provider.value)
  const hello = entry('Hello', consumer.value)
  if (connected.value) hello.dependencies = { ILogger: 'Journal' }
  return JSON.stringify([logger, hello], null, 2)
})
const status = computed(() => connected.value
  ? `ILogger est fourni par Journal. Le Omnilingua démarre le Logger ${languages[provider.value]} avant Hello ${languages[consumer.value]}.`
  : `${entry('Hello', consumer.value).name}: aucun fournisseur configuré pour ILogger.`)

async function copy() {
  try {
    await navigator.clipboard.writeText(configuration.value)
    copied.value = 'Configuration copiée'
  } catch { copied.value = 'Sélectionnez le JSON pour le copier.' }
}
</script>

<template>
  <div class="connection-demo">
    <div class="connection-controls">
      <label>Langage du Logger
        <select v-model="provider" @change="copied = ''">
          <option v-for="(label, id) in languages" :key="id" :value="id">{{ label }}</option>
        </select>
      </label>
      <label>Langage de Hello
        <select v-model="consumer" @change="copied = ''">
          <option v-for="(label, id) in languages" :key="id" :value="id">{{ label }}</option>
        </select>
      </label>
    </div>
    <label class="connection-toggle"><input v-model="connected" type="checkbox" @change="copied = ''"> Déclarer la dépendance ILogger → Journal</label>
    <p class="connection-status" :class="{ 'is-error': !connected }" role="status">{{ status }}</p>
    <pre tabindex="0" aria-label="Contenu de plugins.json"><code>{{ configuration }}</code></pre>
    <button class="connection-copy" type="button" @click="copy">Copier plugins.json</button>
    <span aria-live="polite"> {{ copied }}</span>
  </div>
</template>
