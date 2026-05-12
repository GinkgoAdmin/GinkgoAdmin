import axios from 'axios'
import http from './http'
import { API_BASE_URL } from '../config/env'


const rawHttp = axios.create({ baseURL: API_BASE_URL, timeout: 60000 })

export interface PluginStoreConfig {
  serverUrl: string
  enabled: boolean
  /** 是否允许在本地安装（开发环境） */
  canInstall?: boolean
  /** 远端可嵌入登录页路径，默认 /web/login */
  loginPath?: string
}

/** 商城用户信息 */
export interface StoreUserInfo {
  username: string
  nickname?: string
  balance?: number
  email?: string
}

export interface StoreLoginInput {
  userName: string
  password: string
  clientType?: string
}

export interface StoreLoginResult {
  token: string
  refreshToken?: string
  expiresAt?: string
  userName?: string
  displayName?: string
  avatar?: string
  email?: string
  phone?: string
  roles?: string[]
}

export interface AvailablePlugin {
  id: string
  name: string
  version: string
  description: string
  price: number
  purchased: boolean
  installed: boolean
  category?: string
  coverUrl?: string
  author?: string
  imageUrl?: string
  editionId?: string
  editionName?: string
  packageType?: string
  isFree?: boolean
}

export interface InstallPluginResult {
  ok: boolean
  message: string
}

export interface PurchasePluginResult {
  ok: boolean
  message: string
}

/** 获取插件商店配置 */
export async function getPluginStoreConfig(): Promise<PluginStoreConfig> {
  // http 拦截器已解包 resp.data，返回值即为数据对象
  return await http.get('/system/plugin-store/config') as any
}

/**
 * 获取远端商城实际启用的支付渠道代码列表（如 ['wechat']、['wechat','alipay']）。
 * 远端 Payment 模块未启用任何渠道或未安装时返回 []，前端应据此提示"暂不支持在线购买"。
 */
export async function getPaymentChannels(): Promise<string[]> {
  const result = await http.get('/system/plugin-store/payment-channels') as any
  // 远端按 Result<List<string>>.Success 返回，http 拦截器解包后通常为 string[] 或 { data: string[] }
  if (Array.isArray(result)) return result
  if (Array.isArray(result?.data)) return result.data
  return []
}

// 商城账号密码登录走本地后端代理，远端网页登录保留给第三方登录等备用场景。

/**
 * 直接用裸 axios 调用本地后台代理登录接口，便于独立处理 captcha 挑战。
 */
async function callLoginRaw(input: StoreLoginInput, captchaToken?: string): Promise<any> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  try {
    const adminToken = localStorage.getItem('auth-token') || ''
    if (adminToken) headers['Authorization'] = `Bearer ${adminToken}`
  } catch { /* ignore */ }
  if (captchaToken) headers['X-Captcha-Token'] = captchaToken

  const resp = await rawHttp.post('/system/plugin-store/login', {
    userName: input.userName,
    password: input.password,
    clientType: input.clientType || 'WEB_PORTAL'
  }, { headers, validateStatus: () => true })
  return resp.data
}

/** 商城登录验证码挑战信息（业务码 449 时由远端商城返回的守卫描述）。 */
export interface StoreCaptchaChallengeInfo {
  guardType?: string
  apiBase?: string
  pool?: string
  strategy?: string
  message?: string
}

/**
 * 调用方提供的验证码解析器：
 * 接收挑战信息，弹出验证码 UI，返回校验通过的一次性 token，或 null 表示用户取消。
 */
export type StoreCaptchaResolver = (challenge: StoreCaptchaChallengeInfo) => Promise<string | null>

/**
 * 通过本地后端代理登录远程商城。
 * - 第一次返回 449（验证码挑战）时：
 *   1) 优先调用调用方传入的 resolveCaptcha 回调（推荐方式，由 UI 层提供专属验证码面板）；
 *   2) 否则尝试通过插件钩子 verify:captcha（如已安装验证码插件）；
 *   3) 都拿不到 token 则抛错给调用方。
 * 拿到 token 后会带 X-Captcha-Token 头自动重试登录。
 */
export async function loginStore(input: StoreLoginInput, resolveCaptcha?: StoreCaptchaResolver): Promise<StoreLoginResult> {
  let body = await callLoginRaw(input)

  if (body && Number(body.code) === 449) {
    const guard = body.data || {}
    const challenge: StoreCaptchaChallengeInfo = {
      guardType: guard.guardType || 'captcha',
      apiBase: guard.captchaApiBase,
      pool: guard.pool,
      strategy: guard.strategy,
      message: body.message
    }

    let captchaToken: string | null = null

    if (typeof resolveCaptcha === 'function') {
      captchaToken = await resolveCaptcha(challenge)
    } else {
      // 兜底：尝试通过插件钩子（如已安装 verify 插件）
      try {
        const { getPluginManager } = await import('../plugins')
        const hookResult: any = await getPluginManager().executeHookAsync('verify:captcha', {
          type: challenge.guardType,
          guardType: challenge.guardType,
          apiBase: challenge.apiBase,
          captchaApiBase: challenge.apiBase
        })
        if (hookResult && typeof hookResult === 'object' && typeof hookResult.token === 'string') {
          captchaToken = hookResult.token
        }
      } catch { /* ignore */ }
    }

    if (!captchaToken) {
      throw new Error(body.message || '商城登录需要验证，请完成验证码后重试')
    }
    body = await callLoginRaw(input, captchaToken)
  }

  if (!body) throw new Error('商城登录失败：远端无响应')
  // 兼容两类成功响应：
  //  A) 包了 Result 信封：{ code: 0, data: { token, ... } }
  //  B) 后端代理直接返回裸登录结果：{ token, userName, ... }（无 code 字段）
  const hasCode = Object.prototype.hasOwnProperty.call(body, 'code') || Object.prototype.hasOwnProperty.call(body, 'Code')
  if (hasCode) {
    const code = Number((body as any).code ?? (body as any).Code)
    if (code !== 0) {
      throw new Error(body.message || (body as any).Message || '商城登录失败')
    }
    return ((body as any).data ?? (body as any).Data ?? body) as StoreLoginResult
  }
  // 无 code 字段：直接当作登录结果返回（必须包含 token，否则视为异常）
  if (!(body as any).token && !(body as any).Token) {
    throw new Error('商城登录失败：响应缺少 token')
  }
  return body as StoreLoginResult
}

/** 插件商店分类项（远端 ItemCategory 对应字段简化版） */
export interface StoreCategoryItem {
  /** 主键 ID（字符串：远端使用雪花 ID） */
  id: string
  /** 英文代码，用作 category 过滤参数 */
  code: string
  /** 中文显示名 */
  name: string
  icon?: string | null
  sortOrder?: number
  isHot?: boolean
}

/** 分页插件列表返回值：前端据此构建 el-pagination */
export interface AvailablePluginsPage {
  items: AvailablePlugin[]
  total: number
  page: number
  pageSize: number
}

/**
 * 获取可购买插件列表（支持分类筛选 + 关键词搜索 + 分页，token 可选）。
 * <p>
 * 后端代理到远端 <c>GET /api/plugin-store/portal/items</c>，响应结构为
 * <c>{ items: AvailablePlugin[], total, page, pageSize }</c>。
 * 为兼容旧调用方，当响应是数组时按 <c>items=数组、total=length</c> 回退。
 * </p>
 */
export async function getAvailablePlugins(
  token?: string,
  category?: string,
  keyword?: string,
  page: number = 1,
  pageSize: number = 20,
): Promise<AvailablePluginsPage> {
  const params: Record<string, string | number> = { page, pageSize }
  if (category) params.category = category
  if (keyword) params.keyword = keyword
  const headers: Record<string, string> = {}
  if (token) headers['X-Store-Token'] = token
  const res = await http.get('/system/plugin-store/available-plugins', {
    headers,
    params,
  }) as any

  // 兼容历史响应（裸数组 / { data: [...] }）与新分页响应（{ items, total, ... }）
  if (Array.isArray(res)) {
    return { items: res as AvailablePlugin[], total: res.length, page, pageSize }
  }
  if (res && Array.isArray(res.items)) {
    return {
      items: res.items as AvailablePlugin[],
      total: Number(res.total) || res.items.length,
      page: Number(res.page) || page,
      pageSize: Number(res.pageSize) || pageSize,
    }
  }
  if (res && Array.isArray(res.data)) {
    return { items: res.data as AvailablePlugin[], total: res.data.length, page, pageSize }
  }
  return { items: [], total: 0, page, pageSize }
}

/**
 * 获取远端商城启用的商品分类列表。
 * <p>
 * 后端代理 <c>/api/plugin-store/categories/enabled</c>。用于：
 * 1) 商店前台分类筛选按钮的本地化标签；
 * 2) 把 <c>plugin.category</c>（英文 code）渲染成对应的中文名，
 *    避免前端硬编码 <c>getCategoryLabel</c> map 遗漏新增分类时退化成英文。
 * </p>
 */
export async function getStoreCategories(token?: string): Promise<StoreCategoryItem[]> {
  const headers: Record<string, string> = {}
  if (token) headers['X-Store-Token'] = token
  const res = await http.get('/system/plugin-store/categories', { headers }) as any
  const raw = Array.isArray(res) ? res : (res?.data ?? res?.items ?? [])
  return (raw as any[]).map(x => ({
    id: String(x.id ?? x.Id ?? ''),
    code: String(x.code ?? x.Code ?? ''),
    name: String(x.name ?? x.Name ?? ''),
    icon: x.icon ?? x.Icon ?? null,
    sortOrder: Number(x.sortOrder ?? x.SortOrder ?? 0),
    isHot: Boolean(x.isHot ?? x.IsHot ?? false),
  })).filter(x => x.code)
}

/** 获取商城用户信息 */
export async function getStoreUserInfo(token: string): Promise<StoreUserInfo> {
  return await http.get('/system/plugin-store/user-info', {
    headers: { 'X-Store-Token': token },
  }) as any
}

/** 在本系统内购买插件 */
export async function purchasePlugin(pluginId: string, editionId: string, token: string, channelType: string = 'wechat'): Promise<any> {
  return await http.post('/system/plugin-store/purchase', { pluginId, editionId, channelType }, {
    headers: { 'X-Store-Token': token },
  }) as any
}

/** 轮询查询订单付款状态（仅查商城本地 DB，不打第三方网关） */
export async function getStoreOrder(orderNo: string, token: string): Promise<any> {
  return await http.get(`/system/plugin-store/orders/${orderNo}`, {
    headers: { 'X-Store-Token': token },
  }) as any
}

/**
 * 按支付订单号 fallback 拉取支付订单详情（用于 store 创建订单后 payParams 缺失时兜底）。
 * <p>
 * admin 端代理转发到远端 <code>GET /api/payment/orders?OrderNo={paymentOrderNo}&PageSize=1</code>，
 * 返回结构为分页响应 <code>{ items: [{ payParams, ... }], total, ... }</code>。前端取 <code>items[0].payParams</code> 即可。
 * </p>
 */
export async function getPaymentOrderByNo(paymentOrderNo: string, token: string): Promise<any> {
  return await http.get(`/system/plugin-store/payment-orders/by-no/${paymentOrderNo}`, {
    headers: { 'X-Store-Token': token },
  }) as any
}

/**
 * 主动查询第三方支付网关并同步订单状态。
 * <p>
 * 当支付回调因网络/防火墙/NotifyUrl 配置等原因未到达远端商城时，前端可通过此接口触发
 * 远端主动向支付网关查询真实支付状态；若已支付则远端自动完成订单确认与授权生成。
 * </p>
 * <p>
 * 服务端做了 60 次/分钟/IP 的限流（payment-check 策略），调用方仍应自行控制频率
 * （建议人工触发或周期性兜底，每 9~12 秒一次足以应付回调彻底丢失场景）。
 * </p>
 */
export async function checkPaymentStatus(orderNo: string, token: string): Promise<any> {
  return await http.post(`/system/plugin-store/orders/${orderNo}/check-payment`, null, {
    headers: { 'X-Store-Token': token },
  }) as any
}

/** 下载并安装插件
 * <p>
 * 当不传 <code>releaseId</code> 时，远端会自动选取「license 升级窗口内的最新可用版本」；
 * 当从「版本选择对话框」点击安装时传入具体 <code>releaseId</code>，远端会再次校验该版本是否
 * 在升级窗口内（或为关键安全版本），超出窗口直接拒绝下发令牌，防止越权下载。
 * </p>
 */
export async function installPlugin(pluginId: string, editionId: string, token: string, releaseId?: string): Promise<InstallPluginResult> {
  return await http.post('/system/plugin-store/install', { pluginId, editionId, releaseId }, {
    headers: { 'X-Store-Token': token }
  }) as any
}

/**
 * 当前 license 视角下「该档位的某条发版是否可下载」的信息。
 * 用于框架「模块管理 → 插件商店 → 下载安装」前的版本选择对话框：
 * 仅升级窗口内的版本（或关键安全版本）可下载，其余前端置灰提示「需续费」。
 */
export interface AvailableReleaseDto {
  /** 发版记录 ID（字符串，避免 JS Number 精度丢失） */
  id: string
  /** 所属档位 ID */
  editionId: string
  /** 语义化版本号 */
  version: string
  /** 数值版本号 */
  versionCode: number
  /** 实际发版时间 */
  releasedAt: string | null
  /** 是否关键安全版本 */
  isCriticalSecurity: boolean
  /** 灰度阶段：whitelist/percentage/full/paused */
  rolloutStage: string | null
  /** 对当前 license 是否可下载 */
  available: boolean
  /** 是否在 license 升级窗口内 */
  inUpgradeWindow: boolean
  /** 是否为 license 视角下的最新可下载版本 */
  isLatest: boolean
  /** 不可下载原因：out_of_window / unknown；为 null 表示可下载 */
  unavailableReason: string | null
  /** 更新日志（Markdown） */
  updateLog: string | null
  /** 包大小（字节） */
  packageSize: number | null
}

/**
 * 列出当前 license 视角下「该档位的全部已发布版本」及其可下载状态。
 * 草稿版本不会出现在结果中；返回顺序按 versionCode 倒序，其中 isLatest=true 的是默认下载目标。
 */
export async function listAvailableReleases(editionId: string, token: string): Promise<AvailableReleaseDto[]> {
  return await http.get(`/system/plugin-store/editions/${editionId}/available-releases`, {
    headers: { 'X-Store-Token': token }
  }) as any
}
