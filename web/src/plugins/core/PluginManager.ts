import { reactive, ref } from 'vue'
import type { Plugin, PluginConfig, PluginAPI, PluginDependency, PluginAsset } from './types'
import { HookSystem } from './HookSystem'
import { DependencyManager } from './DependencyManager'

type PluginInitializeScope = 'admin' | 'portal' | 'standalone'
export interface PluginInitializeOptions {
  scope?: PluginInitializeScope
  targetPath?: string
  forceLoadAll?: boolean
  /**
   * 仅加载在自身 module.json 中声明 loadPolicy=always 的横切关注点插件（如 verify）。
   *
   * 用于：在静态注册的门户登录/注册等页面进入时，需要保证 verify 等全局
   * 钩子型插件被装载（以拦截 449 验证码挑战），但又不希望付出"全量加载
   * 所有业务插件"的首屏代价。优先级：forceLoadAll > coreOnly > targetPath。
   */
  coreOnly?: boolean
}

/**
 * 始终加载（横切关注点）插件的识别策略。
 *
 * 不在此处硬编码任何具体插件目录名；改由各插件在自身 module.json 中通过
 * `loadPolicy: "always"` 字段自行声明，插件管理器初始化时统一扫描汇总。
 *
 * 这类插件承担"全局横切关注点"，必须在任意一次插件系统初始化时无条件装载
 * （例如 verify 的 http:biz-error 449 拦截 + 验证码弹窗 + 透明重试），否则
 * 登录等敏感接口会直接报"需要验证"而看不到验证码组件。
 */
const ALWAYS_LOAD_POLICY = 'always'

interface PluginModuleManifest {
  moduleId?: string
  loadPolicy?: string
  [key: string]: unknown
}

/**
 * 插件管理器 - 核心插件系统管理类
 */
export class PluginManager {
  private static instance: PluginManager
  private plugins: Map<string, Plugin> = new Map()
  private pluginConfigs = reactive<Record<string, PluginConfig>>({})
  private components = reactive<Record<string, any>>({})
  private hookSystem = new HookSystem()
  private dependencyManager = DependencyManager.getInstance()
  private loading = ref(false)
  private debug = import.meta.env.DEV
  private configsLoaded = false
  private enabledPluginNamesCache: Set<string> | null = null
  private coreOnlyLoaded = false
  private pluginModules = import.meta.glob('../installed/*/index.ts')
  // module.json 文件体量极小（每个插件不到 1KB），eager 加载便于在路由前置守卫中
  // 同步判断 loadPolicy，避免每次导航都异步并发读取 JSON。
  private pluginModuleJsonModules = import.meta.glob<PluginModuleManifest>(
    '../installed/*/module.json',
    { eager: true, import: 'default' }
  )
  private pathAliasMapCache: Record<string, string> | null = null
  private backendBoundPluginCache: Set<string> | null = null
  private alwaysLoadPluginDirsCache: Set<string> | null = null

  private constructor() {
  }

  static getInstance(): PluginManager {
    if (!PluginManager.instance) {
      PluginManager.instance = new PluginManager()
    }
    return PluginManager.instance
  }

  /**
   * 注册插件
   */
  async registerPlugin(plugin: Plugin): Promise<void> {
    const { name } = plugin.config
    
    if (this.plugins.has(name)) {
      return
    }

    try {
      this.loading.value = true
      
      // 更新插件状态
      this.pluginConfigs[name] = {
        ...plugin.config,
        installStatus: 'installing',
        dependencyStatus: {}
      }

      // 检查插件依赖
      if (plugin.config.dependencies) {
        for (const dep of plugin.config.dependencies) {
          if (!this.plugins.has(dep)) {
            throw new Error(`Plugin ${name} requires dependency: ${dep}`)
          }
        }
      }

      // 加载NPM依赖
      if (plugin.config.npmDependencies) {
        await this.loadPluginDependencies(name, plugin.config.npmDependencies)
      }

      // 加载CDN依赖
      if (plugin.config.cdnDependencies) {
        await this.loadPluginDependencies(name, plugin.config.cdnDependencies)
      }

      // 加载静态资源
      if (plugin.config.assets) {
        await this.loadPluginAssets(name, plugin.config.assets)
      }

      // 创建插件API
      const api = this.createPluginAPI(plugin.config)
      
      // 安装插件
      await plugin.install(api)
      
      // 注册插件
      this.plugins.set(name, plugin)
      this.pluginConfigs[name] = {
        ...plugin.config,
        installTime: new Date().toISOString(),
        installStatus: 'installed'
      }

      if (this.debug) {
        // plugin registered
      }

      // 触发插件注册钩子
      this.hookSystem.executeHook('plugin:registered', plugin.config)
      
    } catch (error) {
      // 更新失败状态
      if (this.pluginConfigs[name]) {
        this.pluginConfigs[name].installStatus = 'failed'
      }
      throw error
    } finally {
      this.loading.value = false
    }
  }

  /**
   * 卸载插件
   */
  async unregisterPlugin(name: string): Promise<void> {
    const plugin = this.plugins.get(name)
    if (!plugin) {
      return
    }

    try {
      this.loading.value = true
      
      // 执行插件卸载逻辑
      if (plugin.uninstall) {
        const api = this.createPluginAPI(plugin.config)
        await plugin.uninstall(api)
      }

      // 清除插件的钩子
      this.hookSystem.clearPluginHooks(name)
      
      // 移除插件组件
      for (const [componentName, component] of Object.entries(this.components)) {
        if (component._pluginName === name) {
          delete this.components[componentName]
        }
      }

      // 移除插件
      this.plugins.delete(name)
      delete this.pluginConfigs[name]

      if (this.debug) {
        // plugin unregistered
      }

      // 触发插件卸载钩子
      this.hookSystem.executeHook('plugin:unregistered', name)
      
    } catch (error) {
      throw error
    } finally {
      this.loading.value = false
    }
  }

  /**
   * 启用插件
   */
  async enablePlugin(name: string): Promise<void> {
    const config = this.pluginConfigs[name]
    if (config) {
      config.enabled = true
      this.hookSystem.executeHook('plugin:enabled', config)
      if (this.debug) {
        // plugin enabled
      }
    }
  }

  /**
   * 禁用插件
   */
  async disablePlugin(name: string): Promise<void> {
    const config = this.pluginConfigs[name]
    if (config) {
      config.enabled = false
      this.hookSystem.executeHook('plugin:disabled', config)
      if (this.debug) {
        // plugin disabled
      }
    }
  }

  /**
   * 加载插件依赖
   */
  private async loadPluginDependencies(pluginName: string, dependencies: PluginDependency[]): Promise<void> {
    const dependencyStatus = this.pluginConfigs[pluginName]?.dependencyStatus || {}
    
    for (const dependency of dependencies) {
      try {
        dependencyStatus[dependency.name] = 'installing'
        await this.dependencyManager.loadDependency(dependency)
        dependencyStatus[dependency.name] = 'installed'
      } catch (error) {
        dependencyStatus[dependency.name] = 'failed'
        if (dependency.required) {
          throw error
        }
      }
    }
    
    if (this.pluginConfigs[pluginName]) {
      this.pluginConfigs[pluginName].dependencyStatus = dependencyStatus
    }
  }

  /**
   * 加载插件资源
   */
  private async loadPluginAssets(pluginName: string, assets: PluginAsset[]): Promise<void> {
    await this.dependencyManager.loadAssets(assets)
  }

  /**
   * 创建插件API
   */
  private createPluginAPI(config: PluginConfig): PluginAPI {
    return {
      addHook: (hookName: string, handler: Function, priority = 10) => {
        this.hookSystem.addHook(hookName, handler, priority, config.name)
      },
      removeHook: (hookName: string, handler: Function) => {
        this.hookSystem.removeHook(hookName, handler)
      },
      registerComponent: (name: string, component: any) => {
        this.components[name] = {
          ...component,
          _pluginName: config.name
        }
        if (this.debug) {
          // component registered
        }
      },
      getConfig: () => config,
      log: (message: string, level = 'info') => {
        // Plugin log API - intentionally silent in production
      },
      loadDependency: async (dependency: PluginDependency) => {
        await this.dependencyManager.loadDependency(dependency)
      },
      checkDependency: (name: string) => {
        return this.dependencyManager.checkDependency(name)
      },
      loadAsset: async (asset: PluginAsset) => {
        await this.dependencyManager.loadAsset(asset)
      },
      executeCommand: async (command: string) => {
        return this.dependencyManager['executeCommand'](command)
      }
    }
  }

  /**
   * 获取插件列表
   */
  getPlugins(): Record<string, PluginConfig> {
    return this.pluginConfigs
  }

  /**
   * 获取插件
   */
  getPlugin(name: string): Plugin | undefined {
    return this.plugins.get(name)
  }

  /**
   * 检查插件是否启用
   */
  isPluginEnabled(name: string): boolean {
    const config = this.pluginConfigs[name]
    return config ? config.enabled : false
  }

  /**
   * 获取组件
   */
  getComponent(name: string): any {
    return this.components[name]
  }

  /**
   * 获取所有组件
   */
  getComponents(): Record<string, any> {
    return this.components
  }

  /**
   * 执行钩子
   */
  executeHook(hookName: string, ...args: any[]): any {
    return this.hookSystem.executeHook(hookName, ...args)
  }

  /**
   * 异步执行钩子
   */
  async executeHookAsync(hookName: string, ...args: any[]): Promise<any> {
    return this.hookSystem.executeHookAsync(hookName, ...args)
  }

  /**
   * 获取加载状态
   */
  get isLoading(): boolean {
    return this.loading.value
  }

  /**
   * 获取加载状态的响应式引用（用于 computed 依赖追踪）
   */
  getLoadingRef() {
    return this.loading
  }

  /**
   * 初始化插件系统
   */
  async initialize(options?: PluginInitializeOptions): Promise<void> {
    try {
      this.loading.value = true

      if (!this.configsLoaded) {
        const savedConfigs = localStorage.getItem('plugin-configs')
        if (savedConfigs) {
          const configs = JSON.parse(savedConfigs)
          Object.assign(this.pluginConfigs, configs)
        }
        this.configsLoaded = true
      }

      const enabledPluginNames = await this.getEnabledPluginNames()
      const targetPluginNames = this.resolveTargetPluginNames(options)
      await this.loadInstalledPlugins(enabledPluginNames, targetPluginNames)

      // 已尝试装载所有 always-load 插件，标记为 true，后续 coreOnly 调用可直接短路
      if (this.hasAllAlwaysLoadPlugins()) {
        this.coreOnlyLoaded = true
      }
      
      if (this.debug) {
        // plugin system initialized
      }
      
    } catch (error) {
      // silently ignored
    } finally {
      this.loading.value = false
    }
  }

  /**
   * 加载已安装的插件
   */
  private async loadInstalledPlugins(enabledPluginNames: Set<string> | null, targetPluginNames?: Set<string>): Promise<void> {
    const candidates = Object.entries(this.pluginModules)
      .filter(([path]) => {
        const match = path.match(/installed\/([^/]+)\/index\.ts$/)
        const pluginDirName = match ? match[1] : ''

        if (!pluginDirName) return false

        // 始终加载的横切关注点插件（由各插件 module.json 中 loadPolicy=always 自行声明，
        // 例如 verify）跳过 targetPluginNames 路由过滤，但仍需经过下方的
        // enabledPluginNames 后端启用过滤。
        const isAlwaysLoad = this.getAlwaysLoadPluginDirs().has(pluginDirName)

        if (!isAlwaysLoad && targetPluginNames && targetPluginNames.size > 0 && !targetPluginNames.has(pluginDirName)) {
          return false
        }

        if (enabledPluginNames && pluginDirName) {
          // 仅跳过明确对应后端模块但未启用的插件
          // 纯前端插件（如 rich-editor）不在任何后端模块列表中，始终加载
          const isBackendPlugin = this.isBackendBoundPlugin(pluginDirName)
          if (isBackendPlugin && !enabledPluginNames.has(pluginDirName)) {
            return false
          }
        }
        return true
      })
      .sort(([a], [b]) => a.localeCompare(b))

    const importedModules = await Promise.all(
      candidates.map(async ([path, loader]) => {
        try {
          const module = await loader() as { default: Plugin }
          return { path, module: module.default || null }
        } catch {
          return { path, module: null }
        }
      })
    )

    // 保持注册阶段顺序执行，避免插件之间潜在依赖出现竞态
    for (const item of importedModules) {
      if (item.module) {
        try {
          await this.registerPlugin(item.module)
        } catch {
          // 静默忽略加载失败的插件
        }
      }
    }
  }

  private resolveTargetPluginNames(options?: PluginInitializeOptions): Set<string> | undefined {
    if (!options) return undefined
    if (options.forceLoadAll) return undefined

    // coreOnly：限定候选集为始终加载白名单本身，loadInstalledPlugins 的
    // isAlwaysLoad 分支会兜底确保白名单插件被收纳，其余业务插件全部跳过。
    if (options.coreOnly) {
      return new Set<string>(this.getAlwaysLoadPluginDirs())
    }

    const path = (options.targetPath || '').toLowerCase()
    if (!path) return undefined

    const candidates = new Set<string>()
    const aliasMap = this.getPathAliasMap()

    if (options.scope === 'admin') {
      const seg = this.extractSegment(path, '/admin/')
      if (seg && aliasMap[seg]) candidates.add(aliasMap[seg])
    }

    if (options.scope === 'portal') {
      const webPathMatch = path.match(/^\/([a-z]{2})(\/web(?:\/|$).*)$/)
      const webPath = webPathMatch ? webPathMatch[2] : (path.startsWith('/web') ? path : '')
      const seg = this.extractSegment(webPath, '/web/')
      if (seg && aliasMap[seg]) candidates.add(aliasMap[seg])
    }

    if (options.scope === 'standalone') {
      if (path.includes('/auth/callback') || path.includes('/oauth')) {
        candidates.add('third-party-auth')
      }
    }

    // docs 允许按现有规则保持可加载
    if (path.includes('/docs')) {
      candidates.add('docs')
    }

    return candidates.size > 0 ? candidates : new Set<string>()
  }

  private extractSegment(fullPath: string, prefix: string): string {
    const idx = fullPath.indexOf(prefix)
    if (idx < 0) return ''
    const rest = fullPath.slice(idx + prefix.length)
    return (rest.split('/').find(Boolean) || '').toLowerCase()
  }

  private getPathAliasMap(): Record<string, string> {
    if (this.pathAliasMapCache) {
      return this.pathAliasMapCache
    }

    const aliasMap: Record<string, string> = {}

    // 1) 默认用插件目录名作为别名（例如 /tenant -> tenant）
    for (const path of Object.keys(this.pluginModules)) {
      const pluginDir = this.extractPluginDirFromPath(path)
      if (!pluginDir) continue
      aliasMap[pluginDir] = pluginDir
    }

    // 2) 添加轻量推断别名（不读取插件源码，避免首屏引入插件包内容）
    for (const pluginDir of Object.values(aliasMap)) {
      const parts = pluginDir.split('-').filter(Boolean)
      if (parts.length > 1) {
        aliasMap[parts[0]] = pluginDir
        // 例如 plugin-store -> plugins
        if (!parts[0].endsWith('s')) {
          aliasMap[`${parts[0]}s`] = pluginDir
        }
      }
    }

    this.pathAliasMapCache = aliasMap
    return aliasMap
  }

  private extractPluginDirFromPath(path: string): string {
    const match = path.match(/installed\/([^/]+)\//)
    return match ? match[1] : ''
  }

  /**
   * 收集声明了 loadPolicy=always 的插件目录集合（横切关注点插件白名单）。
   * 不在代码中硬编码任何具体插件名；完全由各插件自身 module.json 自行声明。
   */
  private getAlwaysLoadPluginDirs(): Set<string> {
    if (this.alwaysLoadPluginDirsCache) {
      return this.alwaysLoadPluginDirsCache
    }

    const set = new Set<string>()
    for (const [path, manifest] of Object.entries(this.pluginModuleJsonModules)) {
      const dir = this.extractPluginDirFromPath(path)
      if (!dir || !manifest) continue
      if (manifest.loadPolicy === ALWAYS_LOAD_POLICY) {
        set.add(dir)
      }
    }

    this.alwaysLoadPluginDirsCache = set
    return set
  }

  /**
   * 判断当前已注册的插件是否覆盖了所有声明 loadPolicy=always 的白名单插件。
   * 用作 coreOnly 短路的标记：避免在每次路由切换时都重复触发初始化流水线。
   */
  private hasAllAlwaysLoadPlugins(): boolean {
    const alwaysLoadDirs = this.getAlwaysLoadPluginDirs()
    if (alwaysLoadDirs.size === 0) return true

    const installedDirs = new Set<string>()
    for (const path of Object.keys(this.pluginModules)) {
      const dir = this.extractPluginDirFromPath(path)
      if (dir) installedDirs.add(dir)
    }
    for (const name of alwaysLoadDirs) {
      if (!installedDirs.has(name)) continue
      if (!this.plugins.has(name)) return false
    }
    return true
  }

  /**
   * 是否已经装载过始终加载（核心横切）插件。供路由层判断是否还需要触发
   * coreOnly 初始化（用于静态命中的登录/注册页等不会走插件路由注册流程的场景）。
   */
  isCoreOnlyLoaded(): boolean {
    return this.coreOnlyLoaded && this.hasAllAlwaysLoadPlugins()
  }

  private async getEnabledPluginNames(): Promise<Set<string> | null> {
    if (this.enabledPluginNamesCache !== null) {
      return this.enabledPluginNamesCache
    }

    try {
      const { getEnabledPlugins } = await import('../../api/module')
      const enabledModuleIds = await getEnabledPlugins()
      if (enabledModuleIds.length > 0) {
        const enabledDirs = new Set<string>()

        // 1) 旧规则兼容：按"后端 ID 拆 Ginkgo.Module. 前缀 + 连字符化 + 小写"
        //    猜测前端插件目录名。仅适用于目录名与后端模块名命名一致的插件
        //    （如 docs ↔ Ginkgo.Module.docs、payment ↔ Ginkgo.Module.Payment）。
        for (const id of enabledModuleIds) {
          const prefix = 'Ginkgo.Module.'
          const name = id.startsWith(prefix) ? id.substring(prefix.length) : id
          const guess = name
            .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
            .replace(/([A-Z])([A-Z][a-z])/g, '$1-$2')
            .toLowerCase()
          if (guess) enabledDirs.add(guess)
        }

        // 2) 精准匹配：扫描各前端插件目录 module.json 中声明的 moduleId，
        //    与后端启用列表按字符串忽略大小写比对，把对应的目录名加入启用集合。
        //    用于解决目录名与后端模块名不一致的场景（典型如
        //    third-party-auth ↔ Ginkgo.Module.third）。
        const enabledIdSet = new Set(enabledModuleIds.map(s => String(s).toLowerCase()))
        for (const [path, manifest] of Object.entries(this.pluginModuleJsonModules)) {
          const dir = this.extractPluginDirFromPath(path)
          const moduleId = manifest?.moduleId
          if (dir && moduleId && enabledIdSet.has(String(moduleId).toLowerCase())) {
            enabledDirs.add(dir)
          }
        }

        this.enabledPluginNamesCache = enabledDirs
        return this.enabledPluginNamesCache
      }
    } catch {
      // API 调用失败时不过滤，加载所有插件
    }

    this.enabledPluginNamesCache = null
    return null
  }

  /**
   * 判断插件是否绑定后端模块（用于 enabled-plugins 过滤）
   * 规则：
   * 1) 若插件目录存在 module.json，视为后端绑定插件。
   */
  private isBackendBoundPlugin(pluginDirName: string): boolean {
    if (!this.backendBoundPluginCache) {
      const set = new Set<string>()

      for (const path of Object.keys(this.pluginModuleJsonModules)) {
        const dir = this.extractPluginDirFromPath(path)
        if (dir) set.add(dir)
      }

      this.backendBoundPluginCache = set
    }

    return this.backendBoundPluginCache.has(pluginDirName)
  }

  /**
   * 保存插件配置
   */
  saveConfigs(): void {
    localStorage.setItem('plugin-configs', JSON.stringify(this.pluginConfigs))
  }
}
