import axios from 'axios'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '../stores/auth'
import { useWebAuthStore } from '../stores/webAuth'
import router from '../router'
import { API_BASE_URL } from '../config/env'

const http = axios.create({
  baseURL: API_BASE_URL,
  timeout: 120000,  // 120 秒，兼容 PDF 合并等耗时操作
})

let unauthorizedRedirecting = false
// 是否正在执行"跳转到登录页"的路由守卫流程。
// 在 beforeEach 守卫内，initPluginRoutes/initializePluginSystem 会发起 API 请求，
// 若此时 unauthorizedRedirecting=true，请求会被永久挂起（createUnauthorizedPendingPromise），
// 导致守卫永远无法完成、路由跳转被阻塞、页面白屏。
// 通过此标志让守卫期间的请求改为 fail-fast（reject），使 try-catch 能正常捕获并继续。
let navigatingToLogin = false

/** 由路由守卫调用：通知 http 拦截器当前正处于"跳转登录页"的守卫流程中 */
export function setNavigatingToLogin(value: boolean) {
  navigatingToLogin = value
}

function isWebPortalPath(path?: string): boolean {
  try {
    const currentPath = path ?? (typeof location !== 'undefined' ? (location.pathname || '') : '')
    return /^(\/[a-z]{2}(-[a-z]{2})?)?\/web(\/|$)/i.test(currentPath)
  } catch {
    return false
  }
}

function isLoginRoute(): boolean {
  try {
    const routeName = router.currentRoute.value.name
    if (routeName === 'login' || routeName === 'web-login') {
      return true
    }

    const currentPath = router.currentRoute.value.path || location.pathname || ''
    return /(^|\/)login(\/|$)/i.test(currentPath)
  } catch {
    try {
      return /(^|\/)login(\/|$)/i.test(location.pathname || '')
    } catch {
      return false
    }
  }
}

function shouldStartUnauthorizedFlow(): boolean {
  return !isLoginRoute()
}

/**
 * 判断请求是否命中"插件商店"系列代理接口。
 *
 * 插件商店代理的 401/403 表示远端商城 license / 商城账号 维度的业务问题
 * （如未登录商城、付费过期、license 过期、风控拒绝等），与当前 admin 后台
 * 会话完全无关。绝不能把它当成 admin 会话失效而清 token + 跳登录页，
 * 否则会出现"下载免费插件结果被踢出系统"这类事故。
 *
 * 本地代理（PluginStoreController）已经通过 MapRemoteStoreFailure 将远端
 * 401/403 收敛为 400，这里是"后端忘记收敛"或"远端直连"等场景下的二道保险。
 */
function isPluginStoreProxyRequest(config: any): boolean {
  try {
    const url = String(config?.url || '')
    if (!url) return false
    return /(^|\/)system\/plugin-store(\/|$)/.test(url)
  } catch {
    return false
  }
}

function createUnauthorizedPendingPromise<T = never>(): Promise<T> {
  return new Promise<T>(() => { })
}

http.interceptors.request.use((config) => {
  // 规则：前台仅用前台 token，后台仅用后台 token，互不交叉
  let token = ''
  try {
    const fromAdmin = localStorage.getItem('auth-token') || ''
    const fromWeb = localStorage.getItem('web_user_token') || ''
    token = isWebPortalPath() ? fromWeb : fromAdmin
  } catch { }

  if (!token) {
    if (isWebPortalPath()) {
      const webAuth = useWebAuthStore()
      token = webAuth.token
    } else {
      const auth = useAuthStore()
      token = auth.token
    }
  }

  if (token && unauthorizedRedirecting) {
    unauthorizedRedirecting = false
  }

  if (unauthorizedRedirecting && shouldStartUnauthorizedFlow()) {
    if (navigatingToLogin) {
      // 正在执行跳转登录页的路由守卫，拒绝请求而非永久挂起，避免死锁导致白屏
      return Promise.reject(new Error('认证已过期，正在跳转登录页'))
    }
    return createUnauthorizedPendingPromise<any>()
  }

  if (token) {
    config.headers = config.headers || {}
      ; (config.headers as any)['Authorization'] = `Bearer ${token}`
  }

  // 仅对非 blob/arraybuffer 请求设置 Accept: application/json
  // PDF 导出等二进制请求不设置此头，避免影响响应格式
  const isBinaryResponse = config.responseType === 'blob' || config.responseType === 'arraybuffer'
  if (!isBinaryResponse) {
    config.headers = config.headers || {}
      ; (config.headers as any)['Accept'] = 'application/json'
  }

  return config
})

/**
 * 统一处理 401/403（清除 token、跳转登录页）
 */
function handleUnauthorized(message?: string) {
  if (unauthorizedRedirecting) {
    return
  }

  unauthorizedRedirecting = true

  try { ElMessage.closeAll() } catch { }
  try { ElMessage.error(message || '未登录或登录已过期') } catch { }
  // 仅清除当前端的 token，不影响另一端
  try {
    if (isWebPortalPath()) {
      const webAuth = useWebAuthStore()
      if (webAuth.token) { webAuth.logout() }
    } else {
      const auth = useAuthStore()
      if (auth.token) { auth.logout?.() ?? (auth.token = ''); auth.saveToStorage?.() }
    }
  } catch { }
  if (router.currentRoute.value.name !== 'login' && router.currentRoute.value.name !== 'web-login') {
    const target = isWebPortalPath() ? 'web-login' : 'login'
    router.replace({ name: target, query: { redirect: location.pathname + location.search } })
      .catch(() => {
        // 路由跳转失败时（如 NavigationDuplicated/NavigationCancelled），
        // 使用 location.replace 做硬跳转兜底，确保用户一定能回到登录页
        try {
          const loginPath = isWebPortalPath()
            ? `/${(location.pathname.split('/')[1] || 'zh')}/web/login`
            : '/ginkgo-admin/login'
          location.replace(loginPath)
        } catch { /* 静默 */ }
      })
  }
}

/**
 * 从后端响应体提取错误信息（兼容多种格式）
 */
function extractErrorMessage(data: any): string {
  if (!data) return ''
  if (typeof data === 'string' && data.trim()) return data
  if (typeof data !== 'object') return ''
  if (typeof data.message === 'string' && data.message.trim()) return data.message
  if (typeof data.title === 'string' && data.title.trim()) return data.title
  if (typeof data.detail === 'string' && data.detail.trim()) return data.detail
  if (data.errors && typeof data.errors === 'object') {
    try {
      const first = Object.values(data.errors as Record<string, any>)
        .flat().map((x: any) => String(x)).find((x: string) => x && x.trim())
      if (first) return first
    } catch { }
  }
  return ''
}

http.interceptors.response.use((resp) => {
  // blob/arraybuffer 响应直接返回原始数据，不做 code/JSON 解析
  if (resp.config?.responseType === 'blob' || resp.config?.responseType === 'arraybuffer') {
    return resp.data
  }

  const data = resp.data
  if (data && typeof data === 'object' && ('code' in data || 'Code' in data)) {
    const appCode = Number((data as any).code ?? (data as any).Code)
    const appMessage = (data as any).message || (data as any).Message || ''
    if (appCode === 0) return (data as any).data ?? (data as any).Data
    // 兼容后端用业务码 401/403 返回未登录但响应码被包装的情形
    if (appCode === 401 || appCode === 403) {
      // 插件商店代理的 401/403 是远端 license/商城账号 维度错误，不影响当前 admin 会话，
      // 仅作为业务错误抛出，避免把用户踢回登录页。
      if (isPluginStoreProxyRequest(resp.config)) {
        return Promise.reject(new Error(appMessage || '插件商店操作未获授权'))
      }
      if (appCode === 401 && shouldStartUnauthorizedFlow()) {
        handleUnauthorized(appMessage || '未登录或登录已过期')
        return createUnauthorizedPendingPromise()
      }
      // 403 只提示无权限，不清 token 也不跳登录页
      if (appCode === 403) {
        try { ElMessage.error(appMessage || '没有权限执行此操作') } catch { }
        return Promise.reject(new Error(appMessage || '没有权限执行此操作'))
      }
      return Promise.reject(new Error(appMessage || '未登录或没有权限'))
    }
    // 将非零业务错误派发给全部启用的插件进行截获（如：触发各类验证机制、特殊自动刷新等）
    return import('../plugins').then(({ getPluginManager }) => {
      return getPluginManager().executeHookAsync('http:biz-error', {
        appCode,
        appMessage,
        data,
        config: resp.config,
        httpInstance: http
      })
    }).then((hookRes: any) => {
      // 若有插件宣布拦截并处理该异常（按约定返回 { handled: true, result: ... }）
      if (hookRes && hookRes.handled) {
        return hookRes.result
      }
      // 没有任何插件接管时，按标准业务报错终止
      return Promise.reject(new Error(appMessage || '请求失败'))
    }).catch(err => {
      // 将插件抛出的错或原始错原样向上透传
      return Promise.reject(err)
    })
  }
  return resp.data
}, (error) => {
  const resp = error?.response
  // 插件商店代理的 401/403 是远端 license/商城账号 维度错误，仅作业务错误抛出，
  // 不清 admin token、不跳登录页。参考 isPluginStoreProxyRequest 的详细说明。
  if ((resp?.status === 401 || resp?.status === 403) && isPluginStoreProxyRequest(error?.config)) {
    const bizMsg = extractErrorMessage(resp?.data) || '插件商店操作未获授权'
    return Promise.reject(new Error(bizMsg))
  }
  if (resp?.status === 401) {
    if (shouldStartUnauthorizedFlow()) {
      handleUnauthorized(extractErrorMessage(resp?.data) || '登录已过期，请重新登录')
      return createUnauthorizedPendingPromise()
    }
    return Promise.reject(error)
  } else if (resp?.status === 403) {
    // 403 只提示无权限，不清 token 也不跳登录页
    const bizMsg = extractErrorMessage(resp?.data) || '没有权限执行此操作'
    try { ElMessage.error(bizMsg) } catch { }
    return Promise.reject(new Error(bizMsg))
  }

  // 非 401/403：提取后端 message 作为错误信息
  const message = extractErrorMessage(resp?.data)
  return Promise.reject(new Error(message || error?.message || '请求失败'))
})

export default http
