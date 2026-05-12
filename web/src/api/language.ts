/**
 * 多语言配置 API
 */
import http from './http'

export interface LanguageConfig {
  code: string        // 语言代码，如 zh-CN, en-US
  name: string        // 显示名称，如 简体中文, English
  nativeName: string  // 本地名称
  flag: string        // 国旗 emoji
  required: boolean   // 是否必填
  sortOrder: number   // 排序
  isActive: boolean   // 是否启用
}

export interface LanguageSettings {
  defaultLang: string   // 默认语言
  fallbackLang: string  // 回退语言
  languages: LanguageConfig[]
}

/**
 * 获取系统语言配置
 * 从系统设置表 /v1/settings 读取 Language.MultiLang.* 键
 */
export async function getLanguageSettings(): Promise<LanguageSettings> {
  try {
    // 从公开设置 API 读取（包含 Language.MultiLang.* 白名单）
    const res = await http.get('/v1/settings') as any
    const items = Array.isArray(res) ? res : (res?.data || [])
    const map = new Map<string, string>()
    items.forEach((it: any) => {
      if (it.key && it.value !== undefined) {
        map.set(it.key, it.value)
      }
    })

    // 读取多语言开关
    const enabledStr = map.get('Language.MultiLang.Enabled')
    const enabled = enabledStr ? enabledStr.toLowerCase() === 'true' : true

    // 读取默认语言
    const defaultLangStr = map.get('Language.MultiLang.Default') || 'zh-CN'

    // 读取语言列表 JSON
    const langsJson = map.get('Language.MultiLang.Languages')
    let languages: LanguageConfig[] = []
    if (langsJson) {
      try {
        const parsed = JSON.parse(langsJson)
        if (Array.isArray(parsed)) {
          languages = parsed.map((l: any) => ({
            code: l.code,
            name: l.label || l.name || l.code,
            nativeName: l.label || l.nativeName || l.code,
            flag: l.flag || '🌐',
            required: l.required === true,
            sortOrder: l.sortOrder || 0,
            isActive: true,
          }))
        }
      } catch {}
    }

    // 读取插件覆盖配置
    const overridesJson = map.get('Language.MultiLang.PluginOverrides')
    let pluginOverrides: Record<string, boolean> = {}
    if (overridesJson) {
      try { pluginOverrides = JSON.parse(overridesJson) } catch {}
    }

    const result: LanguageSettings = {
      defaultLang: defaultLangStr,
      fallbackLang: 'en',
      languages: languages.length > 0 ? languages : getDefaultLanguageSettings().languages,
    }

    // 附加额外信息供 store 使用
    ;(result as any)._extra = {
      multiLangEnabled: enabled,
      pluginOverrides,
    }

    return result
  } catch (error) {
    return getDefaultLanguageSettings()
  }
}

/**
 * 默认语言配置（当数据库无配置时使用）
 */
function getDefaultLanguageSettings(): LanguageSettings {
  return {
    defaultLang: 'zh-CN',
    fallbackLang: 'en-US',
    languages: [
      { code: 'zh-CN', name: '简体中文', nativeName: '简体中文', flag: '🇨🇳', required: true, sortOrder: 1, isActive: true },
      { code: 'en-US', name: 'English', nativeName: 'English', flag: '🇺🇸', required: false, sortOrder: 2, isActive: true },
      { code: 'ja-JP', name: '日本語', nativeName: '日本語', flag: '🇯🇵', required: false, sortOrder: 3, isActive: true }
    ]
  }
}
