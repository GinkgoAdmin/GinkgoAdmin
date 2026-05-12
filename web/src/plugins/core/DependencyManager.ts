import type { PluginDependency, PluginAsset } from './types'

/**
 * 插件依赖管理器
 */
export class DependencyManager {
  private static instance: DependencyManager
  private loadedDependencies = new Set<string>()
  private loadedAssets = new Set<string>()
  private loadingPromises = new Map<string, Promise<void>>()
  private debug = (import.meta as any).env?.DEV
  // 依赖安装模式：cdn（默认，不执行命令，仅加载）、server（调用后端受控接口）、none（只尝试本地import）
  private installMode: 'cdn' | 'server' | 'none' = ((import.meta as any).env?.VITE_PLUGIN_DEP_INSTALL_MODE as any) ?? 'cdn'

  private constructor() {}

  static getInstance(): DependencyManager {
    if (!DependencyManager.instance) {
      DependencyManager.instance = new DependencyManager()
    }
    return DependencyManager.instance
  }

  /**
   * 加载插件依赖
   */
  async loadDependency(dependency: PluginDependency): Promise<void> {
    const key = `${dependency.type}:${dependency.name}`
    
    // 如果已经加载过，直接返回
    if (this.loadedDependencies.has(key)) {
      return
    }

    // 如果正在加载，返回现有的Promise
    if (this.loadingPromises.has(key)) {
      return this.loadingPromises.get(key)!
    }

    // 开始加载
    const loadPromise = this.doLoadDependency(dependency)
    this.loadingPromises.set(key, loadPromise)

    try {
      await loadPromise
      this.loadedDependencies.add(key)
      if (this.debug) {
        // dependency loaded
      }
    } catch (error) {
      throw error
    } finally {
      this.loadingPromises.delete(key)
    }
  }

  /**
   * 实际加载依赖的逻辑
   */
  private async doLoadDependency(dependency: PluginDependency): Promise<void> {
    switch (dependency.type) {
      case 'npm':
        return this.loadNpmDependency(dependency)
      case 'cdn':
        return this.loadCdnDependency(dependency)
      case 'plugin':
        return this.loadPluginDependency(dependency)
      default:
        throw new Error(`Unsupported dependency type: ${dependency.type}`)
    }
  }

  /**
   * 加载NPM依赖（默认不执行任何安装命令）
   */
  private async loadNpmDependency(dependency: PluginDependency): Promise<void> {
    try {
      // 浏览器 cdn 模式下，可选 npm 依赖不在插件安装阶段预加载，
      // 改为由具体页面/组件按需加载，避免首屏拉取大量资源。
      if (this.installMode === 'cdn' && !dependency.required) {
        return
      }

      // 1) 优先尝试本地已存在的依赖（适用于开发/同机部署）：
      if (await this.checkNpmPackage(dependency.name)) {
        return
      }

      // 2) 根据模式决定行为
      if (this.installMode === 'server') {
        // 仅在明确配置为 server 时，才调用受控后端接口执行命令
        const command = dependency.installCommand || `npm install ${dependency.name}${dependency.version ? `@${dependency.version}` : ''}`
        const result = await this.executeCommand(command)
        if (!result.success) {
          throw new Error(`Failed to install npm package: ${result.output}`)
        }
        if (!(await this.checkNpmPackage(dependency.name))) {
          throw new Error(`Package ${dependency.name} was not installed correctly`)
        }
        return
      }

      if (this.installMode === 'none') {
        // 不做任何安装尝试
        if (dependency.required) {
          throw new Error(`Missing required dependency: ${dependency.name}`)
        } else {
          if (this.debug) {
            // optional dependency skipped
          }
          return
        }
      }

      // 3) 默认 cdn 模式：从声明的 URL 或 ESM CDN 加载（不执行命令）
      const loaded = await this.tryLoadFromDeclaredOrEsmCdn(dependency)
      if (!loaded) {
        if (dependency.required) {
          throw new Error(`Failed to load npm dependency via CDN: ${dependency.name}`)
        } else {
          if (this.debug) {
            // optional dependency failed via CDN
          }
        }
      }
    } catch (error) {
      if (dependency.required) {
        throw error
      } else {
        // silently ignored
      }
    }
  }

  /**
   * 从声明的URL或ESM CDN加载脚本
   */
  private async tryLoadFromDeclaredOrEsmCdn(dep: PluginDependency): Promise<boolean> {
    // a) 显式 URL 优先
    if (dep.url) {
      if (dep.url.endsWith('.css')) {
        await this.loadCSS(dep.url, dep.integrity, dep.crossorigin)
      } else {
        await this.loadScript(dep.url, dep.integrity, dep.crossorigin, dep.module === 'esm')
      }
      return true
    }
    // b) 构造 ESM CDN 地址（优先 esm.sh，失败可扩展 jsDelivr/unpkg）
    const ver = dep.version ? `@${dep.version}` : ''
    const esmUrl = `https://esm.sh/${dep.name}${ver}`
    try {
      await this.loadScript(esmUrl, dep.integrity, dep.crossorigin, true)
      return true
    } catch (e) {
      if (this.debug) {
        // esm.sh load failed, trying jsDelivr
      }
      const jsdelivrUrl = `https://cdn.jsdelivr.net/npm/${dep.name}${ver}`
      try {
        await this.loadScript(jsdelivrUrl, dep.integrity, dep.crossorigin, false)
        return true
      } catch (e2) {
        if (this.debug) {
          // cdn load failed
        }
        return false
      }
    }
  }

  /**
   * 加载CDN依赖
   */
  private async loadCdnDependency(dependency: PluginDependency): Promise<void> {
    if (!dependency.url) {
      throw new Error(`CDN dependency ${dependency.name} missing URL`)
    }

    try {
      if (dependency.url.endsWith('.css')) {
        await this.loadCSS(dependency.url, dependency.integrity, dependency.crossorigin)
      } else {
        await this.loadScript(dependency.url, dependency.integrity, dependency.crossorigin, dependency.module === 'esm')
      }
    } catch (error) {
      if (dependency.required) {
        throw error
      } else {
        // silently ignored
      }
    }
  }

  /**
   * 加载插件依赖
   */
  private async loadPluginDependency(dependency: PluginDependency): Promise<void> {
    // 这里可以实现插件间的依赖加载
    // 暂时抛出错误，提示需要手动安装依赖插件
    throw new Error(`Plugin dependency ${dependency.name} must be installed manually`)
  }

  /**
   * 加载静态资源
   */
  async loadAsset(asset: PluginAsset): Promise<void> {
    const key = `${asset.type}:${asset.url}`
    
    if (this.loadedAssets.has(key)) {
      return
    }

    if (this.loadingPromises.has(key)) {
      return this.loadingPromises.get(key)!
    }

    const loadPromise = asset.type === 'css' 
      ? this.loadCSS(asset.url, asset.integrity, asset.crossorigin)
      : this.loadScript(asset.url, asset.integrity, asset.crossorigin)

    this.loadingPromises.set(key, loadPromise)

    try {
      await loadPromise
      this.loadedAssets.add(key)
      if (this.debug) {
        // asset loaded
      }
    } catch (error) {
      throw error
    } finally {
      this.loadingPromises.delete(key)
    }
  }

  /**
   * 检查依赖是否已加载
   */
  checkDependency(name: string, type: 'npm' | 'cdn' | 'plugin' = 'npm'): boolean {
    const key = `${type}:${name}`
    return this.loadedDependencies.has(key)
  }

  /**
   * 加载CSS文件
   */
  private loadCSS(url: string, integrity?: string, crossorigin?: string): Promise<void> {
    return new Promise((resolve, reject) => {
      // 检查是否已经存在
      const existing = document.querySelector(`link[href="${url}"]`)
      if (existing) {
        resolve()
        return
      }

      const link = document.createElement('link')
      link.rel = 'stylesheet'
      link.href = url
      
      if (integrity) {
        link.integrity = integrity
      }
      
      if (crossorigin) {
        link.crossOrigin = crossorigin
      }

      link.onload = () => resolve()
      link.onerror = () => reject(new Error(`Failed to load CSS: ${url}`))
      
      document.head.appendChild(link)
    })
  }

  /**
   * 加载JS文件
   */
  private loadScript(url: string, integrity?: string, crossorigin?: string, module = false): Promise<void> {
    return new Promise((resolve, reject) => {
      // 检查是否已经存在
      const existing = document.querySelector(`script[src="${url}"]`)
      if (existing) {
        resolve()
        return
      }

      const script = document.createElement('script')
      script.src = url
      script.type = module ? 'module' : 'text/javascript'
      
      if (integrity) {
        script.integrity = integrity
      }
      
      if (crossorigin) {
        script.crossOrigin = crossorigin
      }

      script.onload = () => resolve()
      script.onerror = () => reject(new Error(`Failed to load script: ${url}`))
      
      document.head.appendChild(script)
    })
  }

  /**
   * 检查NPM包是否已安装
   */
  private async checkNpmPackage(packageName: string): Promise<boolean> {
    try {
      // 尝试动态导入包来检查是否存在
      await import(/* @vite-ignore */ packageName)
      return true
    } catch (error) {
      return false
    }
  }

  /**
   * 执行命令（仅当 installMode === 'server' 时使用）
   */
  private async executeCommand(command: string): Promise<{ success: boolean; output: string }> {
    if (this.installMode !== 'server') {
      return { success: false, output: 'executeCommand is disabled in non-server mode' }
    }
    try {
      if (this.debug) {
        // executing command
      }
      const response = await fetch('/api/plugins/execute-command', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ command })
      })

      if (response.ok) {
        const result = await response.json()
        return result
      } else {
        throw new Error(`Command execution failed: ${response.statusText}`)
      }
    } catch (error: any) {
      return { success: false, output: error.message }
    }
  }

  /**
   * 批量加载依赖
   */
  async loadDependencies(dependencies: PluginDependency[]): Promise<void> {
    const results = await Promise.allSettled(
      dependencies.map(dep => this.loadDependency(dep))
    )

    const failures = results
      .map((result, index) => ({ result, dependency: dependencies[index] }))
      .filter(({ result, dependency }) => result.status === 'rejected' && dependency.required)

    if (failures.length > 0) {
      const failedNames = failures.map(({ dependency }) => dependency.name).join(', ')
      throw new Error(`Failed to load required dependencies: ${failedNames}`)
    }
  }

  /**
   * 批量加载资源
   */
  async loadAssets(assets: PluginAsset[]): Promise<void> {
    await Promise.all(assets.map(asset => this.loadAsset(asset)))
  }

  /**
   * 清理依赖（用于插件卸载）
   */
  clearDependencies(pluginName: string): void {
    if (this.debug) {
      // clearing dependencies
    }
  }

  /**
   * 获取依赖状态
   */
  getDependencyStatus(): {
    loaded: string[]
    loading: string[]
    failed: string[]
  } {
    return {
      loaded: Array.from(this.loadedDependencies),
      loading: Array.from(this.loadingPromises.keys()),
      failed: []
    }
  }
}