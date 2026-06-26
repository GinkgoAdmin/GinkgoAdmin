import { defineStore } from 'pinia'
import { getUserMenus, getUserButtonCodes, type MenuItem } from '../api/menu'
import { adminBasePath } from '../config/admin'

// 版本号 v2：引入菜单 code 字段用于插件路由解析，旧缓存自动失效
const MENU_CACHE_KEY = 'ginkgo:web:menus:v2'
const BUTTONS_CACHE_KEY = 'ginkgo:web:buttonCodes'

export const useMenuStore = defineStore('menu', {
  state: () => ({
    menus: [] as MenuItem[],
    buttonCodes: [] as string[],
    loaded: false as boolean,
    collapsed: false as boolean
  }),
  
  getters: {
    // 获取启用的菜单项（递归过滤：父级禁用则整棵子树不显示）
    visibleMenus: (state) => {
      const filterEnabled = (items: MenuItem[]): MenuItem[] => {
        return items
          .filter(menu => menu.enabled)
          .map(menu => ({
            ...menu,
            children: menu.children ? filterEnabled(menu.children) : undefined
          }))
      }
      return filterEnabled(state.menus)
    },
    
    // 获取扁平化的菜单列表（用于路由匹配）
    flatMenus: (state) => {
      const flatten = (items: MenuItem[]): MenuItem[] => {
        const result: MenuItem[] = []
        items.forEach(item => {
          // 保持字段独立性：不合并、不改写 route 与 webRouteUrl
          if (Object.prototype.hasOwnProperty.call(item, 'webRouteUrl') || Object.prototype.hasOwnProperty.call(item, 'route')) {
            result.push(item)
          }
          if (item.children) {
            result.push(...flatten(item.children))
          }
        })
        return result
      }
      return flatten(state.menus)
    },
    
    // 根据路由查找菜单项（支持 route 与 webRouteUrl，并统一前缀）
    findMenuByRoute: (state) => (routePath: string) => {
      const flatten = (items: MenuItem[]): MenuItem[] => {
        const result: MenuItem[] = []
        items.forEach(item => {
          result.push(item)
          if (item.children) {
            result.push(...flatten(item.children))
          }
        })
        return result
      }

      const normalize = (p?: string): string => {
        if (!p) return ''
        const r = p.trim()
        // 映射任意 admin 前缀到当前 adminBasePath
        const mapAdminPrefix = (s: string) => s.replace(/^\/?admin\//i, `${adminBasePath.replace(/\/$/, '')}/`)
        // 如果已经是完整路径，先做前缀映射
        if (r.startsWith('/')) {
          const mapped = mapAdminPrefix(r)
          // 如果仍不是以 adminBasePath 开头，但以 /system/ 开头，则补上 adminBasePath
          if (!mapped.startsWith(adminBasePath + '/') && mapped.startsWith('/system/')) {
            return `${adminBasePath}${mapped}`
          }
          return mapped
        }
        // 处理相对路径
        if (r.startsWith('system/')) return `${adminBasePath}/${r}`
        if (r.startsWith('admin/')) return mapAdminPrefix(r.startsWith('/') ? r : `/${r}`)
        // 默认加上admin前缀
        return `${adminBasePath}/${r}`
      }

      const allMenus = flatten(state.menus)
      const targetPath = routePath.trim()

      // 特殊处理：如果目标路径是 adminBasePath（首页），优先匹配 route 为空、'/' 或 'home' 的菜单
      if (targetPath === adminBasePath || targetPath === `${adminBasePath}/`) {
        const homeMenu = allMenus.find(menu => {
          const route = String(menu.route || '').trim()
          // 排除有子菜单的目录节点（它们的空路由不代表首页）
          const hasChildren = menu.children && menu.children.length > 0
          if (route === '' && hasChildren) return false
          return route === '' || route === '/' || route === 'home' || route === adminBasePath
        })
        if (homeMenu) {
          return homeMenu
        }
        // 首页不在动态菜单数据中，直接返回 undefined
        return undefined
      }

      return allMenus.find(menu => {
        // 用途明确：导航匹配优先用 route；兼容 webRouteUrl 的绝对匹配
        const candidates = [
          normalize(menu.route),
          menu.route,
          (menu as any).webRouteUrl
        ].filter(Boolean)

        // 移除空字符串候选项，避免误匹配
        const validCandidates = candidates.filter(c => c && c.length > 0)

        const match = validCandidates.some(candidate => {
          // 精确匹配
          if (candidate === targetPath) return true

          // 目标路径以候选路径结尾（但候选路径不能是空字符串）
          if (targetPath.endsWith(candidate) && candidate.length > 0) return true

          // 候选路径以目标路径（去掉前缀）结尾（但去掉前缀后不能是空字符串）
          const pathWithoutPrefix = targetPath.replace(adminBasePath, '')
          if (pathWithoutPrefix.length > 0 && candidate.endsWith(pathWithoutPrefix)) return true

          return false
        })

        return match
      })
    },
    
    // 检查是否有按钮权限
    hasButtonPermission: (state) => (buttonCode: string) => {
      return state.buttonCodes.includes(buttonCode)
    }
  },
  
  actions: {
    initFromCache() {
      try {
        const menusRaw = localStorage.getItem(MENU_CACHE_KEY)
        const btnsRaw = localStorage.getItem(BUTTONS_CACHE_KEY)
        const menus = menusRaw ? JSON.parse(menusRaw) as MenuItem[] : []
        const btns = btnsRaw ? JSON.parse(btnsRaw) as string[] : []
        if (Array.isArray(menus) && menus.length > 0) {
          this.menus = menus
          this.buttonCodes = Array.isArray(btns) ? btns : []
          this.loaded = true
        }
      } catch (e) {
        // 忽略缓存解析错误
      }
    },

    saveToCache() {
      try {
        localStorage.setItem(MENU_CACHE_KEY, JSON.stringify(this.menus || []))
        localStorage.setItem(BUTTONS_CACHE_KEY, JSON.stringify(this.buttonCodes || []))
      } catch (e) {
        // 存储失败忽略
      }
    },

    clearCache() {
      try {
        localStorage.removeItem(MENU_CACHE_KEY)
        localStorage.removeItem(BUTTONS_CACHE_KEY)
      } catch {}
    },

    /**
     * 加载菜单数据。
     * @param forceRefresh 若为 true，则清空本地缓存并强制从接口获取最新数据（用于登录场景）。
     */
    async loadMenus(forceRefresh?: boolean) {
      try {
        if (forceRefresh) {
          this.clearCache()
          this.loaded = false
        }
        //this.clearCache()
        // 强制刷新或缓存未命中时，从接口获取最新菜单
        const menus = await getUserMenus()
        const buttonCodes = await getUserButtonCodes()
        
        // 保持数据结构完整，绝不合并/转换 route 与 webRouteUrl 字段
        this.menus = Array.isArray(menus) ? menus : []
        this.buttonCodes = Array.isArray(buttonCodes) ? buttonCodes : []
        this.loaded = true

        // 全量写入本地缓存，供离线与性能优化使用
        this.saveToCache()
      } catch (error) {
        this.loaded = true
      }
    },

    /**
     * 登录时调用：清理旧缓存并强制拉取最新菜单数据。
     */
    async loadMenusFresh() {
      await this.loadMenus(true)
    },
    
    toggleCollapse() {
      this.collapsed = !this.collapsed
    },
    
    setCollapse(collapsed: boolean) {
      this.collapsed = collapsed
    }
  }
})