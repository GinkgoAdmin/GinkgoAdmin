/**
 * 多语言配置 Store
 * 从后端字典 API 加载语言配置，并同步到全局 lang.ts
 */
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { getLanguageSettings, type LanguageConfig, type LanguageSettings } from '@/api/language'
import {
  setLangConfig,
  toUrlCode,
  getAvailableLangs,
  getCurrentLang,
  getDefaultLang,
  parseLang as parseLangUtil,
  type LangItem,
} from '@/utils/lang'

const CACHE_KEY = 'ginkgo:language:settings'
const CACHE_TTL = 5 * 60 * 1000 // 5分钟缓存

export const useLanguageStore = defineStore('language', () => {
  const settings = ref<LanguageSettings | null>(null)
  const loading = ref(false)
  const lastFetch = ref(0)

  // 多语言全局开关
  const multiLangEnabled = ref(true)

  // 插件多语言覆盖配置 { pluginId: boolean }
  const pluginOverrides = ref<Record<string, boolean>>({})

  // 计算属性：语言列表
  const languages = computed(() => settings.value?.languages || [])

  // 计算属性：默认语言
  const defaultLang = computed(() => settings.value?.defaultLang || 'zh-CN')

  // 计算属性：回退语言
  const fallbackLang = computed(() => settings.value?.fallbackLang || 'en')

  // 计算属性：转换为 LangItem 格式（供 LangInput 等组件使用）
  const languagesForInput = computed((): LangItem[] => {
    return languages.value.map(lang => ({
      code: lang.code,
      urlCode: toUrlCode(lang.code),
      label: lang.nativeName || lang.name,
      flag: lang.flag,
      required: lang.required
    }))
  })

  // 从缓存初始化
  function initFromCache() {
    try {
      const cached = localStorage.getItem(CACHE_KEY)
      if (cached) {
        const { data, timestamp, enabled, overrides } = JSON.parse(cached)
        if (Date.now() - timestamp < CACHE_TTL) {
          settings.value = data
          multiLangEnabled.value = enabled !== false
          pluginOverrides.value = overrides || {}
          lastFetch.value = timestamp
          syncToGlobal()
          return true
        }
      }
    } catch {}
    return false
  }

  // 保存到缓存
  function saveToCache() {
    try {
      localStorage.setItem(CACHE_KEY, JSON.stringify({
        data: settings.value,
        timestamp: Date.now(),
        enabled: multiLangEnabled.value,
        overrides: pluginOverrides.value,
      }))
    } catch {}
  }

  // 同步配置到全局 lang.ts
  function syncToGlobal() {
    setLangConfig({
      enabled: multiLangEnabled.value,
      langs: languagesForInput.value,
      defaultLang: defaultLang.value,
    })
  }

  // 加载语言配置
  async function loadSettings(force = false) {
    // 如果已有数据且未过期，跳过
    if (!force && settings.value && Date.now() - lastFetch.value < CACHE_TTL) {
      return settings.value
    }

    // 尝试从缓存加载
    if (!force && initFromCache()) {
      return settings.value
    }

    loading.value = true
    try {
      const data = await getLanguageSettings()
      settings.value = data
      lastFetch.value = Date.now()

      // 解析额外配置（多语言开关、插件覆盖）
      try {
        const extra = (data as any)._extra || {}
        multiLangEnabled.value = extra.multiLangEnabled !== false
        pluginOverrides.value = extra.pluginOverrides || {}
      } catch {}

      syncToGlobal()
      saveToCache()
      return data
    } finally {
      loading.value = false
    }
  }

  // 清除缓存
  function clearCache() {
    localStorage.removeItem(CACHE_KEY)
    settings.value = null
    lastFetch.value = 0
  }

  // 检查指定插件是否启用多语言
  function isPluginMultiLang(pluginId: string): boolean {
    if (!multiLangEnabled.value) return false
    // 如果插件有单独配置，使用插件配置；否则跟随全局
    if (pluginId in pluginOverrides.value) {
      return pluginOverrides.value[pluginId]
    }
    return true // 默认跟随全局
  }

  // 创建空的多语言值对象
  function createEmptyLangValue(): Record<string, string> {
    const result: Record<string, string> = {}
    languages.value.forEach(lang => {
      result[lang.code] = ''
    })
    return result
  }

  // 解析多语言 JSON
  function parseLangJson(json: string | Record<string, string> | null | undefined): Record<string, string> {
    const result = createEmptyLangValue()

    if (!json) return result

    if (typeof json === 'string') {
      try {
        const parsed = JSON.parse(json)
        Object.assign(result, parsed)
      } catch {
        // 如果解析失败，将字符串作为默认语言的值
        result[defaultLang.value] = json
      }
    } else if (typeof json === 'object') {
      Object.assign(result, json)
    }

    return result
  }

  // 获取显示值（优先默认语言，然后回退语言）
  function getDisplayValue(langValue: Record<string, string> | string | null | undefined): string {
    return parseLangUtil(typeof langValue === 'string' ? langValue : JSON.stringify(langValue))
  }

  return {
    settings,
    loading,
    multiLangEnabled,
    pluginOverrides,
    languages,
    defaultLang,
    fallbackLang,
    languagesForInput,
    loadSettings,
    clearCache,
    initFromCache,
    syncToGlobal,
    isPluginMultiLang,
    createEmptyLangValue,
    parseLangJson,
    getDisplayValue,
  }
})
