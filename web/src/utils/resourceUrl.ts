/**
 * 资源 URL 工具 — 文件访问地址解析。
 * 根据 storageProvider 类型自动选择最优访问路径（OSS 直链 / 本地 /uploads/ 静态路径）。
 */
import { getEnabledPlugins } from '../api/module'
import http from '../api/http'

// ---------- 资源配置缓存 ----------
interface ResourceConfig {
  ossEnabled: boolean
  publicBaseUrl: string | null
}

let _config: ResourceConfig | null = null
let _loading: Promise<ResourceConfig> | null = null

function isEnabledOssModule(moduleIds: string[]): boolean {
  return moduleIds.some(id => {
    const trimmed = id.replace(/^Ginkgo\.Module\./i, '')
    const normalized = trimmed
      .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
      .replace(/([A-Z])([A-Z][a-z])/g, '$1-$2')
      .toLowerCase()
    return normalized === 'oss'
  })
}

/**
 * 获取 OSS 资源配置（带缓存，仅请求一次）
 */
export async function fetchResourceConfig(): Promise<ResourceConfig> {
  if (_config) return _config
  if (_loading) return _loading
  _loading = (async () => {
    const enabledModuleIds = await getEnabledPlugins()
    if (!isEnabledOssModule(enabledModuleIds)) {
      _config = { ossEnabled: false, publicBaseUrl: null }
      return _config
    }

    const data = await http.get<any, ResourceConfig>('/v1/oss/resource-config')
    _config = { ossEnabled: !!data?.ossEnabled, publicBaseUrl: data?.publicBaseUrl || null }
    return _config
  })()
    .catch(() => {
      _config = { ossEnabled: false, publicBaseUrl: null }
      return _config
    })
    .finally(() => { _loading = null })
  return _loading
}

/** 同步获取已缓存的资源配置 */
export function getResourceConfigSync(): ResourceConfig | null {
  return _config
}

/**
 * 根据文件对象构建最优访问地址。
 * 根据 storageProvider 判断：
 * - OssLibFileStorage → 用 OSS 插件缓存的 publicBaseUrl 拼接相对路径
 * - Local / LocalFileStorage → 用 /uploads/ 静态路径拼接
 * 兼容历史数据中的绝对 URL。
 */
export function resolveFileUrl(file: { id?: string | number; url?: string | null; storageProvider?: string | null; storagePath?: string | null }): string {
  // 已经是绝对 URL（兼容历史数据），直接返回
  if (file.url && /^https?:\/\//i.test(file.url)) return file.url

  const provider = file.storageProvider || ''
  // 与后端一致：非空且不含 "Local" 的提供者均视为云存储（兼容 OssLibFileStorage、UpyunFileStorage 等）
  const isOss = !!provider && !/local/i.test(provider)

  if (isOss && _config?.ossEnabled && _config.publicBaseUrl) {
    // OSS 文件：publicBaseUrl + 相对路径
    const relativePath = file.url || file.storagePath
    if (relativePath) {
      return `${_config.publicBaseUrl.replace(/\/+$/, '')}/${relativePath.replace(/^\/+/, '')}`
    }
  }

  // OSS 文件但配置未就绪（_config 未加载 / publicBaseUrl 为空），降级使用本地镜像路径
  if (isOss && file.url) {
    if (!file.url.startsWith('/')) {
      return `/uploads/${file.url}`
    }
    return file.url
  }

  // 本地文件：确保以 /uploads/ 开头（通过静态文件中间件直接访问）
  if (file.url && !isOss) {
    // DB 中本地文件 Url 可能是 "2026/02/11/xxx.png"（无 /uploads/ 前缀）
    if (!file.url.startsWith('/')) {
      return `/uploads/${file.url}`
    }
    return file.url
  }

  return file.url || ''
}

/**
 * @deprecated 请使用 resolveResourcePath(path) 代替。文件已通过 /uploads/ 静态路径直接访问。
 */
export function buildFileUrl(id: string | number): string {
  console.warn('[resourceUrl] buildFileUrl 已废弃，请使用 resolveResourcePath 替代')
  return ''
}

/** 重置缓存（模块热重载后调用） */
export function resetResourceConfig(): void {
  _config = null
  _loading = null
}

/**
 * 将相对资源路径解析为完整可访问 URL（Web 端唯一入口）。
 *
 * 支持的输入形态：
 * - http:// 或 https:// 完整地址 → 外部资源，原样返回
 * - /api/v1/files/{id}/content → 已是新版受控访问端点，原样返回
 * - /api/files/{id} → 历史遗留错误格式，自动重写为 /api/v1/files/{id}/content 并附带 access_token
 * - /uploads/... 或 /resource/... → 已带绝对前缀，原样返回
 * - 其它以 / 开头 → 视为已就位的站内路径，原样返回
 * - 纯相对路径（如 "2026/03/12/xxx.png"）→ 根据当前 OSS 配置自动拼接
 *   - OSS 已启用 → publicBaseUrl + 相对路径
 *   - OSS 未启用 → /uploads/ + 相对路径
 *
 * 调用前应在合适时机（一般是页面 onMounted）`await fetchResourceConfig()`，
 * 否则纯相对路径会回退到 /uploads/ 本地镜像。
 */
export function resolveResourcePath(path: string | null | undefined): string {
  if (!path) return ''
  // 已是完整 HTTP URL（外部资源或历史完整地址），直接返回
  if (/^https?:\/\//i.test(path)) return path
  // 新版受控访问端点直接返回
  if (path.startsWith('/api/v1/files/')) return path
  // 兼容历史遗留的错误格式 /api/files/{id} → 重写为 /api/v1/files/{id}/content 并附带 token
  const legacyMatch = path.match(/^\/api\/files\/([\d]+)$/)
  if (legacyMatch) {
    let url = `/api/v1/files/${legacyMatch[1]}/content`
    try {
      const tk = localStorage.getItem('web_user_token') || localStorage.getItem('auth-token') || ''
      if (tk) url += `?access_token=${encodeURIComponent(tk)}`
    } catch {}
    return url
  }
  // 已有绝对路径前缀，直接返回
  if (path.startsWith('/uploads/') || path.startsWith('/api/') || path.startsWith('/resource/')) return path
  // 纯相对路径：根据当前 OSS 配置判断
  const config = getResourceConfigSync()
  if (config?.ossEnabled && config.publicBaseUrl) {
    return `${config.publicBaseUrl.replace(/\/+$/, '')}/${path.replace(/^\/+/, '')}`
  }
  // 本地存储：补全 /uploads/ 前缀
  if (!path.startsWith('/')) {
    return `/uploads/${path}`
  }
  return path
}
