import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { useWebAuthStore } from '../stores/webAuth'
import { adminRoot, injectAdminRoutes, injectFilesystemRoutes } from './admin'
import { webRoutes } from './web'
import { adminBasePath, loginFullPath } from '../config/admin'
import { portalEnabled } from '../config/portal'
import { getDefaultUrlCode, isValidLang, switchLang, toDbCode } from '../utils/lang'

// 匹配前台路径（含语言前缀 /:lang/web/...）
const WEB_PATH_RE = /^\/([a-z]{2})(\/web(?:\/|$|\?|#).*)?$/
function isWebPath(path: string): boolean {
  return WEB_PATH_RE.test(path) || path === '/web' || path.startsWith('/web/')
}
function extractWebLang(path: string): string | null {
  const m = path.match(WEB_PATH_RE)
  return m ? m[1] : null
}

// 先不注册 notfound 兜底路由，等插件路由初始化完成后再添加
const routes: RouteRecordRaw[] = [
  // 根路径重定向到默认语言前台
  { path: '/', redirect: () => `/${getDefaultUrlCode()}/web`, meta: { public: true } },
  // 旧路径兼容：/web/xxx → /:defaultLang/web/xxx
  { path: '/web/:rest(.*)?', redirect: to => {
    const rest = (to.params.rest as string) || ''
    return `/${getDefaultUrlCode()}/web${rest ? '/' + rest : ''}`
  }, meta: { public: true } },
  { path: loginFullPath, name: 'login', component: () => import('../views/admin/Login.vue'), meta: { public: true } },
  adminRoot,
  webRoutes,
]

const router = createRouter({
  history: createWebHistory(),
  routes,
  // 每次路由切换回到页面顶部；浏览器前进/后退时恢复上次位置
  scrollBehavior(to, from, savedPosition) {
    if (savedPosition) return savedPosition
    return { top: 0, left: 0 }
  },
})

// 启动时仅注入文件系统的 system/* 路由，避免首次进入 404
injectFilesystemRoutes(router)

let pluginRoutesPromise: Promise<void> | null = null

let portalPluginRoutesPromise: Promise<void> | null = null

// notfound 路由是否已添加
let notfoundRouteAdded = false
let webOverrideApplied = false

// 监听动态 import / chunk 加载失败：通常发生在新版本发布后，
// 旧标签页持有的 index.html 引用的是旧 hash 的 JS 资源，部署后这些文件已经 404。
// 表现：用户点击菜单"无反应"、控制台刷出大量 "Failed to fetch dynamically imported module"。
// 处理：检测到这类错误时，自动硬跳转到目标地址，让浏览器拉取最新的 index.html 与新 hash chunk。
// 防护：用 sessionStorage 记录"上一次因 chunk 失败而刷新的路径"，避免在真的资源缺失场景下死循环刷新。
const CHUNK_RELOAD_KEY = '__ginkgo_chunk_reload_path'
function isChunkLoadError(err: any): boolean {
  if (!err) return false
  const msg = String(err?.message || err || '')
  // 覆盖 Chrome / Firefox / Safari 等不同浏览器的报错文案
  return (
    /Failed to fetch dynamically imported module/i.test(msg) ||
    /error loading dynamically imported module/i.test(msg) ||
    /Importing a module script failed/i.test(msg) ||
    /ChunkLoadError/i.test(msg) ||
    /Loading chunk \S+ failed/i.test(msg) ||
    /Loading CSS chunk \S+ failed/i.test(msg)
  )
}
function reloadOnceForChunkError(targetPath: string) {
  try {
    const last = sessionStorage.getItem(CHUNK_RELOAD_KEY)
    if (last === targetPath) {
      // 同一个路径已经因为 chunk 错误刷过一次了，再刷也没用，避免死循环
      sessionStorage.removeItem(CHUNK_RELOAD_KEY)
      return
    }
    sessionStorage.setItem(CHUNK_RELOAD_KEY, targetPath)
  } catch {
    // sessionStorage 不可用时直接刷新
  }
  // 用 location.replace 让浏览器重新拉取 index.html，从而拿到最新的 hash 资源
  if (typeof location !== 'undefined') {
    location.replace(targetPath)
  }
}

// 路由层动态 import 失败拦截
router.onError((err, to) => {
  if (isChunkLoadError(err)) {
    const target = (to && (to as any).fullPath) || (typeof location !== 'undefined' ? location.pathname + location.search + location.hash : '/')
    reloadOnceForChunkError(target)
  }
})

// 全局兜底：路由之外的动态 import（菜单点击触发的子组件懒加载、插件懒加载等）
if (typeof window !== 'undefined') {
  window.addEventListener('error', (e: ErrorEvent) => {
    if (isChunkLoadError(e?.error || e?.message)) {
      const target = location.pathname + location.search + location.hash
      reloadOnceForChunkError(target)
    }
  })
  window.addEventListener('unhandledrejection', (e: PromiseRejectionEvent) => {
    if (isChunkLoadError(e?.reason)) {
      const target = location.pathname + location.search + location.hash
      reloadOnceForChunkError(target)
    }
  })
  // 路由跳转成功一次后，认为已经走在新版本资源上，清掉刷新标记
  router.afterEach(() => {
    try { sessionStorage.removeItem(CHUNK_RELOAD_KEY) } catch {}
    // 变体C：Base64解码 + URL拼接 + 字符数组，写入 meta[name="generator"]
    try {
      let meta = document.querySelector<HTMLMetaElement>('meta[name="generator"]')
      if (!meta) {
        meta = document.createElement('meta')
        meta.name = 'generator'
        document.head.appendChild(meta)
      }
      const _gb  = atob('R2lua2dvQWRtaW4=')                         // GinkgoAdmin
      const _url = 'https://www.' + 'ginkgo' + 'admin' + '.com'     // https://www.ginkgoadmin.com
      const _cr  = ['C','o','p','y','r','i','g','h','t'].join('')    // Copyright
      meta.content = `${_gb} | ${_url} | ${_cr} \u00a9 2026 ${_gb}`
    } catch { /* 静默 */ }
  })
}

// 确保 notfound 兜底路由在所有动态路由之后添加
function ensureNotfoundRoute() {
  if (notfoundRouteAdded) return
  notfoundRouteAdded = true
  router.addRoute({ path: '/:pathMatch(.*)*', name: 'notfound', component: () => import('../views/common/NotFound.vue'), meta: { public: true } })
}

// 初始化插件路由的函数
async function initPluginRoutes(targetPath?: string, forceLoadAll = false) {
  // 串行化，避免并发重复注入
  if (pluginRoutesPromise) {
    await pluginRoutesPromise
  }

  pluginRoutesPromise = (async () => {
    try {
      const { initializePluginSystem, getPluginManager } = await import('../plugins')
      await initializePluginSystem({
        scope: 'admin',
        targetPath,
        forceLoadAll,
      })

      const pluginManager = getPluginManager()
      const pluginRoutes = pluginManager.executeHook('route:register', [])

      if (Array.isArray(pluginRoutes) && pluginRoutes.length > 0) {
        pluginRoutes.forEach((route: any) => {
          let routePath = route.path
          if (routePath && routePath.startsWith('/')) {
            routePath = routePath.slice(1)
          }

          // 检查路由是否已存在
          const existingRoute = router.getRoutes().find(r =>
            r.path === `${adminBasePath}/${routePath}` || r.name === route.name
          )
          if (existingRoute) {
            return
          }

          const childRoute = { ...route, path: routePath }
          router.addRoute('admin-root', childRoute)
        })
      }

    } catch (error) {
      // silently ignored
    } finally {
      pluginRoutesPromise = null
    }
  })()

  return pluginRoutesPromise
}

// 初始化前台插件路由的函数（通用能力，供所有插件使用）
async function initPortalPluginRoutes(targetPath?: string, scope: 'portal' | 'standalone' = 'portal', forceLoadAll = false) {
  // 串行化，避免并发重复注入
  if (portalPluginRoutesPromise) {
    await portalPluginRoutesPromise
  }

  portalPluginRoutesPromise = (async () => {
    try {
      const { initializePluginSystem, getPluginManager } = await import('../plugins')
      await initializePluginSystem({
        scope,
        targetPath,
        forceLoadAll,
      })

      const pluginManager = getPluginManager()

      // 处理 portal:route-register 钩子
      const portalRoutes = pluginManager.executeHook('portal:route-register', [])

      if (Array.isArray(portalRoutes) && portalRoutes.length > 0) {
        // 检查是否有路由声明了 __webOverride —— 用于覆盖 /web 的布局和首页
        const overrideRoute = portalRoutes.find((r: any) => r.meta?.__webOverride)

        if (overrideRoute) {
          // 获取原有的 web-root 子路由
          const existingWebRoot = router.getRoutes().find(r => r.name === 'web-root')
          const existingChildren = existingWebRoot?.children || []

          // 移除旧路由
          router.removeRoute('web-root')

          // 构建新的子路由列表：保留框架原有子路由（替换首页），添加CMS子路由
          const newChildren: any[] = []
          const cmsChildren = overrideRoute.children || []

          // 遍历框架原有子路由，如果CMS提供了同名路由则替换
          existingChildren.forEach((child: any) => {
            const cmsChild = cmsChildren.find((c: any) => c.name === child.name)
            if (cmsChild) {
              newChildren.push(cmsChild)
            } else {
              newChildren.push(child)
            }
          })

          // 添加CMS独有的路由（不在框架原有列表中）
          cmsChildren.forEach((cmsChild: any) => {
            if (!newChildren.find((c: any) => c.name === cmsChild.name)) {
              newChildren.push(cmsChild)
            }
          })

          // 用CMS布局注册新的 /:lang/web 路由
          router.addRoute({
            path: '/:lang/web',
            name: 'web-root',
            component: overrideRoute.component,
            meta: { public: true },
            children: newChildren,
          })
          webOverrideApplied = true
        }

        // 注册其余普通路由（不包含 __webOverride 的）
        portalRoutes.forEach((route: any) => {
          if (route.meta?.__webOverride) return // 已处理

          let routePath = route.path
          if (routePath && routePath.startsWith('/')) {
            routePath = routePath.slice(1)
          }
          const existingRoute = router.getRoutes().find(r =>
            r.path.endsWith(`/web/${routePath}`) || (route.name && r.name === route.name)
          )
          if (!existingRoute) {
            router.addRoute('web-root', { ...route, path: routePath })
          }
        })
      }

      // 处理 portal:standalone-route 钩子 —— 注册顶层路由（不挂载布局，无页头页脚）
      // 插件使用此钩子可注册独立页面（如弹窗登录页、全屏授权页等）
      const standaloneRoutes = pluginManager.executeHook('portal:standalone-route', [])
      if (Array.isArray(standaloneRoutes) && standaloneRoutes.length > 0) {
        standaloneRoutes.forEach((route: any) => {
          const existingRoute = router.getRoutes().find(r =>
            (route.path && r.path === route.path) || (route.name && r.name === route.name)
          )
          if (!existingRoute) {
            router.addRoute({ ...route, meta: { public: true, ...route.meta } })
          }
        })
      }

    } catch (error) {
      // silently ignored
    } finally {
      portalPluginRoutesPromise = null
    }
  })()

  return portalPluginRoutesPromise
}

// 记录已经做过动态路由重定向的路径，防止无限循环
const dynamicRouteRedirected = new Set<string>()

// 核心横切插件（如 verify）一次性兜底装载用 promise，避免并发重复触发
let corePluginsPromise: Promise<void> | null = null

// 门户全量插件一次性兜底装载用标记 + promise。
// 用于解决"门户共享容器静态路由进入时门户业务插件未装载、portal:user-menu 等
// 前台共享插槽钩子失效"的问题，详见 ensurePortalPluginsLoaded 注释。
let portalFullPluginsLoaded = false
let portalFullPluginsPromise: Promise<void> | null = null

/**
 * 确保"始终加载（横切关注点）"插件已经装载。
 *
 * 设计动机：插件懒加载改造后，进入门户登录/注册等"静态注册路由"的页面时,
 * 整个 initPortalPluginRoutes 都不会被触发，verify 等仅承担全局钩子的横切插件
 * 因此从未装载——表现为登录失败时后端返回 449 验证码挑战，前端只能堆出
 * "需要验证"错误提示而看不到验证码弹窗（详见 verify 插件的 http:biz-error 钩子）。
 *
 * 这里用 coreOnly 模式仅装载各插件 module.json 中声明 loadPolicy=always
 * 的横切关注点插件，不会引入任何业务插件代码，首屏代价基本可忽略。
 */
async function ensureCorePluginsLoaded() {
  try {
    const { getPluginManager } = await import('../plugins')
    if (getPluginManager().isCoreOnlyLoaded?.()) return
  } catch {
    // 插件模块入口加载失败时静默放行，避免阻塞导航
    return
  }

  if (corePluginsPromise) {
    await corePluginsPromise
    return
  }

  corePluginsPromise = (async () => {
    try {
      const { initializePluginSystem } = await import('../plugins')
      await initializePluginSystem({ coreOnly: true })
    } catch {
      // silently ignored
    } finally {
      corePluginsPromise = null
    }
  })()

  await corePluginsPromise
}

/**
 * 确保"门户共享容器"所需的业务插件已经全量装载。
 *
 * 设计动机：会员中心、消息中心等门户共享容器路由是静态注册（见 router/web.ts userRoutes），
 * 进入时 vue-router 直接命中，beforeEach 中的 initPortalPluginRoutes 因 isMatchedBeforePortal
 * 为真而被跳过 —— 但这些容器的侧边栏（如 WebUserSidebar.vue）会通过 portal:user-menu 钩子
 * 聚合各业务插件（plugin-store / license / ...）注入的菜单项；插件没装载，钩子就没注册，
 * 菜单就缺失。
 *
 * 这些钩子又不应升级为 loadPolicy:"always"（属于"门户用户场景下的共享插槽"，不是真正的
 * 全局横切，不应让首次进入框架就为它们付出加载代价）。
 *
 * 折中方案：当 router 命中 to.meta.portalShellNeedsPlugins 时调用本函数，按 portal 全量
 * 装载一次门户插件（首次执行后通过 portalFullPluginsLoaded 短路缓存，不会重复）。
 *
 * 触达路径：在静态注册的会员中心路由 meta 中显式标记，避免硬编码具体路径前缀。
 */
async function ensurePortalPluginsLoaded() {
  if (portalFullPluginsLoaded) return

  if (portalFullPluginsPromise) {
    await portalFullPluginsPromise
    return
  }

  portalFullPluginsPromise = (async () => {
    try {
      // forceLoadAll=true 让 PluginManager 跳过 targetPath 过滤、装载所有可用插件，
      // 同时收集 portal:route-register 注册的路由（虽然这里我们主要为了注册钩子）。
      await initPortalPluginRoutes(undefined, 'portal', true)
      portalFullPluginsLoaded = true
    } catch {
      // silently ignored，避免阻塞导航
    } finally {
      portalFullPluginsPromise = null
    }
  })()

  await portalFullPluginsPromise
}

router.beforeEach(async (to, _from) => {
  const auth = useAuthStore()

  // 首先从 storage 初始化认证状态（确保在任何检查之前执行）
  if (!auth.token) {
    auth.initFromStorage()
  }

  // 全局兜底：确保 verify 等横切关注点插件已装载，否则 449 验证码挑战
  // 在静态命中的登录/注册页将无法被拦截，用户看不到验证码弹窗。
  await ensureCorePluginsLoaded()

  // 已经登录的情况下访问登录页，直接跳转（并尝试解析可能的嵌套 redirect）
  if (to.path === loginFullPath && auth.token) {
    let redirectUrl = (to.query?.redirect as string) || adminBasePath
    try {
      while (redirectUrl.includes(loginFullPath)) {
        const urlObj = new URL(redirectUrl, typeof location !== 'undefined' ? location.origin : 'http://localhost')
        redirectUrl = urlObj.searchParams.get('redirect') || adminBasePath
      }
    } catch {
      // ignore
    }
    if (redirectUrl.includes(loginFullPath) || redirectUrl === '/') {
      redirectUrl = adminBasePath
    }
    return { path: redirectUrl, replace: true }
  }

  const webAuth = useWebAuthStore()
  if (!webAuth.token) {
    webAuth.initFromStorage()
  }

  // 从 URL 语言前缀同步语言状态
  const urlLang = to.params?.lang as string
  if (urlLang && isValidLang(urlLang)) {
    switchLang(urlLang)
  }

  if (to.name === 'web-login' && webAuth.isAuthenticated) {
    const langPrefix = urlLang ? `/${urlLang}` : `/${getDefaultUrlCode()}`
    let redirectUrl = (to.query?.redirect as string) || `${langPrefix}/web`
    try {
      while (redirectUrl.includes('/web/login')) {
        const urlObj = new URL(redirectUrl, typeof location !== 'undefined' ? location.origin : 'http://localhost')
        redirectUrl = urlObj.searchParams.get('redirect') || `${langPrefix}/web`
      }
    } catch {
      // ignore
    }
    if (redirectUrl.includes('/web/login') || redirectUrl === '/') {
      redirectUrl = `${langPrefix}/web`
    }
    return { path: redirectUrl, replace: true }
  }

  // 系统核心层初始化：任何 admin 路径（含登录页）都必须加载插件系统，
  // 现在改为：仅在需要插件路由时按目标路径懒加载插件，避免首开全量加载。
  if (to.path.startsWith(adminBasePath)) {
    // 登录页仅尝试加载验证类插件（例如验证码），避免全量插件初始化。
    if (to.path === loginFullPath) {
      await initPluginRoutes(`${adminBasePath}/verify`, false)
      ensureNotfoundRoute()
      return
    }

    // 未登录直接跳转登录页，不继续执行后续逻辑
    if (!auth.token) {
      return { name: 'login', query: { redirect: to.fullPath }, replace: true }
    }

    await injectAdminRoutes(router)

    const isMatchedBeforePlugin = to.matched.length > 0 &&
      !(to.matched.length === 1 && to.matched[0]?.name === 'admin-root') &&
      !to.matched.some(m => m.name === 'notfound')

    // 仅在未命中后台路由时才触发插件加载
    if (!isMatchedBeforePlugin) {
      await initPluginRoutes(to.path, false)
      ensureNotfoundRoute()

      const isMatchedAfterTargeted = to.matched.length > 0 &&
        !(to.matched.length === 1 && to.matched[0]?.name === 'admin-root') &&
        !to.matched.some(m => m.name === 'notfound')

      // 目标插件未命中时，回退全量一次，保证兼容旧插件
      if (!isMatchedAfterTargeted) {
        await initPluginRoutes(to.path, true)
      }
    }

    // 后台动态路由处理后，添加 notfound 兜底路由
    ensureNotfoundRoute()

    // 如果当前 to 还没匹配到真实路由（matched 为空、只有 admin-root、或匹配到 notfound），
    // 且还没有重定向过，则重新导航让 vue-router 重新匹配
    const isMatched = to.matched.length > 0 &&
      !(to.matched.length === 1 && to.matched[0]?.name === 'admin-root') &&
      !to.matched.some(m => m.name === 'notfound')

    if (!isMatched && !dynamicRouteRedirected.has(to.fullPath)) {
      dynamicRouteRedirected.add(to.fullPath)
      return { path: to.path, query: to.query, hash: to.hash, replace: true }
    }
    // 清理标记
    dynamicRouteRedirected.delete(to.fullPath)
  }

  // 非公开路由的通用认证检查
  // 安全原则：只有明确属于后台路径的路由才跳转登录页，
  // 不存在的路径不应跳转登录（会暴露后台地址），直接放行显示 404。
  if (!to.meta.public && !auth.token) {
    if (isWebPath(to.path)) {
      // web 前台路由：无 admin token 时不应跳转到后台登录页
      // 如果路由本身要求 web 认证，在下方的前台路由段统一处理
      // 否则直接放行（web 路由的认证由各路由自身的 requiresAuth 控制）
    } else if (to.path.startsWith(adminBasePath)) {
      // 只有确实属于后台路径时才跳转登录，防止未知路径暴露后台地址
      return { name: 'login', query: { redirect: to.fullPath } }
    }
    // 其他不存在的路径：直接放行，最终命中 notfound 路由显示 404
  }

  // 顶层独立页面（如第三方登录回调 /auth/callback/:provider）由插件通过 portal:standalone-route 注册。
  // 这些路径不在 /web 也不在后台基础路径下，需要单独触发一次插件路由初始化，否则会落到 notfound。
  if (!to.path.startsWith(adminBasePath) && !isWebPath(to.path)) {
    const isStandaloneCandidate = to.matched.length === 0 ||
      to.matched.some(m => m.name === 'notfound')
    if (isStandaloneCandidate) {
      await initPortalPluginRoutes(to.path, 'standalone', false)
      ensureNotfoundRoute()
      let isMatchedNow = to.matched.length > 0 && !to.matched.some(m => m.name === 'notfound')
      if (!isMatchedNow) {
        await initPortalPluginRoutes(to.path, 'standalone', true)
        isMatchedNow = to.matched.length > 0 && !to.matched.some(m => m.name === 'notfound')
      }
      if (!isMatchedNow && !dynamicRouteRedirected.has(to.fullPath)) {
        dynamicRouteRedirected.add(to.fullPath)
        return { path: to.path, query: to.query, hash: to.hash, replace: true }
      }
      dynamicRouteRedirected.delete(to.fullPath)
    }
  }

  // 前台需要登录的路由检查（匹配 /:lang/web/... 或旧的 /web/...）
  if (isWebPath(to.path)) {
    // 前台门户未启用时，显示关闭页面
    if (!portalEnabled) {
      if (to.name !== 'portal-closed') return { name: 'portal-closed', replace: true }
      return
    }

    // 门户共享容器路由（如会员中心 /web/user*）需要门户业务插件已装载，
    // 否则 portal:user-menu 等"门户共享插槽"钩子会因静态命中跳过 portal init 而失效。
    // 由路由 meta 显式声明，避免在 router 中硬编码具体路径前缀。
    if (to.matched.some(m => m.meta?.portalShellNeedsPlugins)) {
      await ensurePortalPluginsLoaded()
    }

    // 仅在未命中 web 静态路由时，才初始化插件路由
    const isMatchedBeforePortal = to.matched.length > 0 &&
      !to.matched.some(m => m.name === 'notfound')
    if (!isMatchedBeforePortal) {
      await initPortalPluginRoutes(to.path, 'portal', false)
      const isMatchedAfterTargeted = to.matched.length > 0 &&
        !to.matched.some(m => m.name === 'notfound')
      if (!isMatchedAfterTargeted) {
        await initPortalPluginRoutes(to.path, 'portal', true)
      }
    }

    // 所有前台动态路由注册完毕后，添加 notfound 兜底路由
    ensureNotfoundRoute()

    // 如果当前 to 还没匹配到真实路由，或有web-override导致路由变化了，需要重新导航
    const isMatched = to.matched.length > 0 &&
      !to.matched.some(m => m.name === 'notfound')
    // 如果web-root被CMS覆盖过，需要重新导航让新组件生效（仅在覆盖刚发生的首次导航时触发）
    const needsOverrideRedirect = webOverrideApplied && to.matched.some(m => m.name === 'web-root' || m.name === 'web-home')
    if ((!isMatched || needsOverrideRedirect) && !dynamicRouteRedirected.has(to.fullPath)) {
      dynamicRouteRedirected.add(to.fullPath)
      return { path: to.path, query: to.query, hash: to.hash, replace: true }
    }
    dynamicRouteRedirected.delete(to.fullPath)
    // web-root 覆盖后的首次重定向已完成，后续导航无需再触发重定向
    if (webOverrideApplied) webOverrideApplied = false

    // 前台需要登录的路由，跳转到 web-login，而非后台管理登录页
    if (to.meta.requiresAuth) {
      const webAuth = useWebAuthStore()
      webAuth.initFromStorage()
      if (!webAuth.isAuthenticated) {
        return { name: 'web-login', query: { redirect: to.fullPath } }
      }
    }
  }

  // 确保 notfound 路由最终被添加（处理非 admin 非 web 的路径）
  ensureNotfoundRoute()
})


export default router


