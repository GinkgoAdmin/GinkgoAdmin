import { type RouteRecordRaw } from 'vue-router'
import { useMenuStore } from '../stores/menu'
import { adminBasePath } from '../config/admin'

const systemViewModules = (import.meta as any).glob('../views/admin/system/*.vue')

// 模块视图：扫描已安装模块目录
// 模块安装后位于 web/src/modules/Ginkgo.Module.*/views/**/*.vue
const moduleViewModules = (import.meta as any).glob('../modules/Ginkgo.Module.*/views/**/*.vue')

// 插件视图：扫描已安装插件目录
// 插件安装后位于 web/src/plugins/installed/{shortName}/views/**/*.vue
const pluginViewModules = (import.meta as any).glob('../plugins/installed/*/views/**/*.vue')

const MENU_CACHE_KEY = 'ginkgo:web:menus:v2'

function toKebabName(path: string) {
  return path.replace(/^\/+|\/+$/g, '').replace(/\//g, '-').toLowerCase()
}

function toPascalName(segment: string) {
  return segment
    .split(/[-_]/g)
    .filter(Boolean)
    .map((s: string) => s.charAt(0).toUpperCase() + s.slice(1))
    .join('')
}

function toDisplayTitleFromKebab(kebab: string) {
  return kebab
    .split('-')
    .filter(Boolean)
    .map((s: string) => s.charAt(0).toUpperCase() + s.slice(1))
    .join(' ')
}

function resolveModuleComponent(pathOrFile: string) {
  // 解析模块组件路径
  const modules = moduleViewModules as Record<string, () => Promise<any>>
  
  const keys = Object.keys(modules)
  
  // 直接尝试匹配
  if (modules[pathOrFile]) {
    return modules[pathOrFile]
  }
  
  // 从 WebRouteUrl 提取模块名和文件路径
  // 格式: ../views/modules/hhyxcompanywebsite/products/Categories.vue
  const viewsMatch = pathOrFile.match(/\.\.\/views\/modules\/([^/]+)\/(.+\.vue)$/i)
  if (viewsMatch) {
    const [, moduleName, filePath] = viewsMatch
    
    // 尝试匹配已安装模块目录（新路径格式）
    // 模块位于 ../modules/Ginkgo.Module.*/views/**/*.vue
    const possiblePaths = [
      `../modules/Ginkgo.Module.${toPascalCase(moduleName)}/views/${filePath}`,
      `../modules/Ginkgo.Module.${moduleName}/views/${filePath}`,
      `../modules/Ginkgo.Module.${moduleName.charAt(0).toUpperCase() + moduleName.slice(1)}/views/${filePath}`
    ]
    
    for (const modulePath of possiblePaths) {
      if (modules[modulePath]) {
        return modules[modulePath]
      }
    }
    
    // 模糊匹配：查找包含模块名和文件名的键
    const fileName = filePath.split('/').pop()
    const fuzzyMatch = keys.find(k => 
      k.toLowerCase().includes(moduleName.toLowerCase()) && 
      k.toLowerCase().endsWith(fileName!.toLowerCase())
    )
    if (fuzzyMatch) {
      return modules[fuzzyMatch]
    }
  }
  
  return null
}

// 辅助函数：转换为 PascalCase
function toPascalCase(str: string): string {
  return str
    .split(/[-_]/g)
    .map(s => s.charAt(0).toUpperCase() + s.slice(1).toLowerCase())
    .join('')
}

// PascalCase -> kebab-case，保持连续大写为整体（AISessions -> ai-sessions）
function pascalToKebab(str: string): string {
  return str
    .replace(/([A-Z]+)([A-Z][a-z])/g, '$1-$2')
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .toLowerCase()
}

/**
 * 解析插件组件：基于菜单 code 前缀定位插件目录，按路由末段 kebab 名匹配 .vue 文件。
 * - code 形如 'aicore:sessions' -> 插件短名 'aicore'
 * - routePath 形如 'ai-sessions' -> 匹配 views/**\/*.vue 文件中文件名（PascalCase）转 kebab 等于 'ai-sessions'
 */
function resolvePluginComponent(code: string | undefined, routePath: string) {
  if (!code || code.indexOf(':') < 0) return null
  const shortName = code.split(':')[0].trim().toLowerCase()
  if (!shortName) return null
  const leaf = (routePath || '').split('/').filter(Boolean).pop() || ''
  if (!leaf) return null
  const leafKebab = leaf.toLowerCase()

  const modules = pluginViewModules as Record<string, () => Promise<any>>
  const prefix = `../plugins/installed/${shortName}/views/`
  const keys = Object.keys(modules).filter(k => k.toLowerCase().startsWith(prefix))
  if (keys.length === 0) return null

  // 精确匹配：文件名（去 .vue）kebab 化后等于 leafKebab
  for (const k of keys) {
    const fileName = k.split('/').pop() || ''
    const base = fileName.replace(/\.vue$/i, '')
    if (pascalToKebab(base) === leafKebab) return modules[k]
    if (base.toLowerCase() === leafKebab.replace(/-/g, '')) return modules[k]
  }
  // 兜底：kebab 子串包含匹配
  const fallback = keys.find(k => {
    const base = (k.split('/').pop() || '').replace(/\.vue$/i, '')
    return pascalToKebab(base).includes(leafKebab)
  })
  return fallback ? modules[fallback] : null
}

function resolveSystemComponent(pathOrFile: string) {
  const modules = systemViewModules as Record<string, () => Promise<any>>
  
  // 直接匹配完整路径（如 ../views/admin/system/Users.vue）
  if (modules[pathOrFile]) {
    return modules[pathOrFile]
  }
  
  // 如果是 .vue 文件路径，尝试标准化
  if (/\.vue$/i.test(pathOrFile)) {
    // 提取文件名并构建标准路径
    const fileName = pathOrFile.split('/').pop()
    if (fileName) {
      const standardPath = `../views/admin/system/${fileName}`
      if (modules[standardPath]) {
        return modules[standardPath]
      }
    }
    return null
  }
  
  // 从路由路径推断组件（如 system/users -> Users.vue）
  const parts = pathOrFile.replace(/^\/+/, '').split('/')
  if (parts[0] !== 'system') return null
  const leaf = parts[1] || 'index'
  const fileName = `${toPascalName(leaf)}.vue`
  const full = `../views/admin/system/${fileName}`
  return modules[full] || null
}

export const adminRoot: RouteRecordRaw = {
  path: adminBasePath,
  name: 'admin-root',
  component: () => import('../layouts/MainLayout.vue'),
  children: [
    { path: '', name: 'admin-root-redirect', redirect: { name: 'dashboard' } },
    { path: 'dashboard', name: 'dashboard', component: () => import('../views/admin/Home.vue'), meta: { title: '首页', icon: 'House' } },
    
    // 用户个人页面路由
    { path: 'user/profile', name: 'user-profile', component: () => import('../views/admin/user/Profile.vue'), meta: { title: '个人资料', icon: 'User' } },
    { path: 'user/notifications', name: 'user-notifications', component: () => import('../views/admin/user/Notifications.vue'), meta: { title: '我的通知', icon: 'Bell' } },
    { path: 'user/notifications/simple', name: 'user-notifications-simple', component: () => import('../views/admin/user/NotificationsSimple.vue'), meta: { title: '通知详情', icon: 'Bell' } },
    { path: 'user/logs', name: 'user-logs', component: () => import('../views/admin/user/Logs.vue'), meta: { title: '我的日志', icon: 'Document' } },

    // 导航菜单管理（从菜单管理页面进入，不作为独立菜单项）
    { path: 'system/menu-groups', name: 'menu-groups', component: () => import('../views/admin/system/MenuGroups.vue'), meta: { title: '导航菜单管理', icon: 'Menu' } },
  ]
}

const injected = new Set<string>()

export function injectFilesystemRoutes(router: any) {
  // 改为基于本地缓存的菜单数据进行注入，避免扫描本地文件
  try {
    const raw = localStorage.getItem(MENU_CACHE_KEY)
    const menus = raw ? JSON.parse(raw) as any[] : []
    const flatten = (items: any[]): any[] => {
      const out: any[] = []
      items.forEach(it => {
        if (it) out.push(it)
        if (Array.isArray(it?.children) && it.children.length > 0) {
          out.push(...flatten(it.children))
        }
      })
      return out
    }
    const flat = Array.isArray(menus) ? flatten(menus) : []
    flat.forEach((item: any) => {
      const routeRaw = String(item.route || '').replace(/^\/+/, '').trim()
      const webUrl = String(item.webRouteUrl || '').trim()

      // 计算 system 路径：优先 route 中包含/以 system/ 开头；其次 WebRouteUrl；再次用 name/id 兜底
      let sysPath = ''
      if (routeRaw) {
        if (routeRaw === 'dashboard') sysPath = routeRaw
        const idx = routeRaw.indexOf('system/')
        if (idx >= 0) sysPath = routeRaw.slice(idx)
        else if (routeRaw.startsWith('system/')) sysPath = routeRaw
      }
      if (!sysPath && webUrl) {
        if (/^system\//i.test(webUrl)) sysPath = webUrl
        else if (/\.vue$/i.test(webUrl)) {
          const base = webUrl.split('/').pop()?.replace(/\.vue$/i, '') || 'index'
          const kebab = base.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase()
          sysPath = `system/${kebab}`
        }
      }
      if (!sysPath) {
        const rawId = String(item.id || 'page')
        const kebabId = rawId
          .trim()
          .replace(/[^a-zA-Z0-9]+/g, '-')
          .replace(/^-+|-+$/g, '')
          .toLowerCase() || 'page'
        sysPath = `system/${kebabId}`
      }

      if (injected.has(sysPath)) return
      const fullPath = `${adminBasePath}/${sysPath}`
      if (router.getRoutes().some((r: any) => r.path === fullPath)) return

      // 解析组件：优先 WebRouteUrl；否则用 sysPath 推断；失败则使用通用占位页
      let component: any = null
      if (webUrl) {
        component = resolveSystemComponent(webUrl)
      }
      if (!component) {
        component = resolveSystemComponent(sysPath)
      }
      const resolvedComponent = component || (() => import('../views/common/NotFound.vue'))

      router.addRoute('admin-root', {
        path: sysPath,
        name: toKebabName(sysPath),
        component: resolvedComponent,
        meta: { title: item.name, icon: item.icon }
      })
      injected.add(sysPath)
    })
  } catch {}
}

export async function injectAdminRoutes(router: any) {
  await ensureDynamicAdminRoutes(router)
}

export async function ensureDynamicAdminRoutes(router: any) {
  // 先注入文件系统路由（幂等），避免首次进入404
  //injectFilesystemRoutes(router)

  const menuStore = useMenuStore()
  // 优先尝试缓存
  if (!menuStore.loaded) {
    menuStore.initFromCache()
  }
  if (!menuStore.loaded) {
    try { await menuStore.loadMenus() } catch {}
  }
  const flat = menuStore.flatMenus as any[]
  // 基于菜单一次性注入所有路由（支持 system/* 和模块路由）
  flat.forEach(item => {
    const routeRaw = String(item.route || '').replace(/^\/+/, '').trim()
    const webUrl = String(item.webRouteUrl || '').trim()
    const code = String(item.code || '').trim()

    // 检查是否是模块路由（webUrl 包含 /modules/）
    const isModuleRoute = webUrl.includes('/modules/')
    // 检查是否是插件路由：
    //   仅当 code 含冒号、且 webRouteUrl 不是 .vue 文件、且 route 不以 system/ 开头时，
    //   才视为真正的插件路由（如 aicore:sessions / payment:config，对应组件由各插件
    //   自己通过 route:register 钩子注册）。
    //
    // 历史 bug：原判定只看 code.indexOf(':') > 0，导致 mysql_init_menus.sql 里所有
    // 系统菜单（sys:users / sys:roles / sys:dept ...，code 都含冒号、webRouteUrl 都是
    // ../views/admin/system/*.vue）被错误划入"插件路由"全部跳过，从未注入到 router。
    // 表现：新装环境首次进入后台后点击任何系统菜单都 404，需要某种刷新动作才能"修复"。
    const isPluginRoute = !isModuleRoute
      && code.indexOf(':') > 0
      && !/\.vue$/i.test(webUrl)
      && !/^system\//i.test(routeRaw)

    // 插件路由：按菜单 code 前缀 + route/webRouteUrl 解析 views 组件并注入（菜单驱动模式）。
    // 不可直接 return 跳过——否则仅依赖 route:register 的插件在新装/打包部署后若前端未重启，
    // 或 index.ts 被自动生成覆盖时，点击菜单会稳定 404。
    if (isPluginRoute) {
      const routePath = routeRaw || webUrl.replace(/^\/+/, '').trim()
      if (!routePath) return

      if (injected.has(routePath)) return

      const existing = router.getRoutes().find((r: any) =>
        r.path === routePath || r.path === `${adminBasePath}/${routePath}` || r.name === toKebabName(routePath)
      )
      if (existing) {
        existing.meta = { ...(existing.meta || {}), title: item.name, icon: item.icon }
        injected.add(routePath)
        return
      }

      const component = resolvePluginComponent(code, routePath)
      if (!component) return

      router.addRoute('admin-root', {
        path: routePath,
        name: toKebabName(routePath),
        component,
        meta: { title: item.name, icon: item.icon }
      })
      injected.add(routePath)
      return
    }

    // 计算路由路径
    let routePath = ''
    if (isModuleRoute) {
      // 模块路由：直接使用 route 作为路径
      routePath = routeRaw
    } else {
      // 系统路由：计算 system 路径
      if (routeRaw) {
        if (routeRaw === 'dashboard') routePath = routeRaw
        const idx = routeRaw.indexOf('system/')
        if (idx >= 0) routePath = routeRaw.slice(idx)
        else if (routeRaw.startsWith('system/')) routePath = routeRaw
      }
      if (!routePath && webUrl) {
        if (/^system\//i.test(webUrl)) routePath = webUrl
        else if (/\.vue$/i.test(webUrl)) {
          const base = webUrl.split('/').pop()?.replace(/\.vue$/i, '') || 'index'
          const kebab = base.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase()
          routePath = `system/${kebab}`
        }
      }
      if (!routePath) {
        const fallbackKey = (String(item.name || item.id || 'page')
          .replace(/\s+/g, '-')
          .replace(/[^a-zA-Z0-9\-]/g, '') || 'page').toLowerCase()
        routePath = `system/${fallbackKey}`
      }
    }

    const fullPath = `${adminBasePath}/${routePath}`
    const existing = router.getRoutes().find((r: any) => r.path === fullPath)
    if (existing) {
      existing.meta = { ...(existing.meta || {}), title: item.name, icon: item.icon }
      injected.add(routePath)
      return
    }
    if (injected.has(routePath)) {
      return
    }

    // 解析组件：优先插件组件，其次模块组件，再次系统组件，失败则使用通用占位页
    let component: any = null
    if (isPluginRoute) {
      component = resolvePluginComponent(code, routePath)
    }
    if (!component && webUrl) {
      if (isModuleRoute) {
        component = resolveModuleComponent(webUrl)
      }
      if (!component) {
        component = resolveSystemComponent(webUrl)
      }
    }
    if (!component && !isModuleRoute && !isPluginRoute) {
      component = resolveSystemComponent(routePath)
    }
    const resolvedComponent = component || (() => import('../views/common/NotFound.vue'))

    router.addRoute('admin-root', { 
      path: routePath, 
      name: toKebabName(routePath), 
      component: resolvedComponent,
      meta: { title: item.name, icon: item.icon }
    })
    injected.add(routePath)
    
    injected.add(routePath)
  })
}
