import type { PluginConfig, PluginDependency } from './types'
import { DependencyManager } from './DependencyManager'

export interface PluginPackage {
  config: PluginConfig
  files: Record<string, string> // 文件路径 -> 文件内容
  dependencies?: PluginDependency[]
}

export interface InstallOptions {
  force?: boolean // 强制安装，覆盖现有插件
  skipDependencies?: boolean // 跳过依赖安装
  dryRun?: boolean // 只检查，不实际安装
}

export interface InstallResult {
  success: boolean
  pluginName: string
  installedDependencies: string[]
  failedDependencies: string[]
  errors: string[]
  warnings: string[]
}

/**
 * 插件安装器
 */
export class PluginInstaller {
  private static instance: PluginInstaller
  private dependencyManager = DependencyManager.getInstance()
  private debug = import.meta.env.DEV

  private constructor() {}

  static getInstance(): PluginInstaller {
    if (!PluginInstaller.instance) {
      PluginInstaller.instance = new PluginInstaller()
    }
    return PluginInstaller.instance
  }

  /**
   * 从URL安装插件
   */
  async installFromUrl(url: string, options: InstallOptions = {}): Promise<InstallResult> {
    try {
      const response = await fetch(url)
      if (!response.ok) {
        throw new Error(`Failed to fetch plugin from ${url}: ${response.statusText}`)
      }

      const pluginPackage = await response.json() as PluginPackage
      return this.installPackage(pluginPackage, options)
    } catch (error) {
      return {
        success: false,
        pluginName: 'unknown',
        installedDependencies: [],
        failedDependencies: [],
        errors: [error.message],
        warnings: []
      }
    }
  }

  /**
   * 从文件安装插件
   */
  async installFromFile(file: File, options: InstallOptions = {}): Promise<InstallResult> {
    try {
      const content = await this.readFileContent(file)
      let pluginPackage: PluginPackage

      if (file.name.endsWith('.json')) {
        pluginPackage = JSON.parse(content)
      } else if (file.name.endsWith('.zip')) {
        pluginPackage = await this.extractZipPackage(content)
      } else {
        throw new Error(`Unsupported file format: ${file.name}`)
      }

      return this.installPackage(pluginPackage, options)
    } catch (error) {
      return {
        success: false,
        pluginName: 'unknown',
        installedDependencies: [],
        failedDependencies: [],
        errors: [error.message],
        warnings: []
      }
    }
  }

  /**
   * 安装插件包
   */
  async installPackage(pluginPackage: PluginPackage, options: InstallOptions = {}): Promise<InstallResult> {
    const result: InstallResult = {
      success: false,
      pluginName: pluginPackage.config.name,
      installedDependencies: [],
      failedDependencies: [],
      errors: [],
      warnings: []
    }

    try {
      // 验证插件包
      const validation = this.validatePackage(pluginPackage)
      if (!validation.valid) {
        result.errors.push(...validation.errors)
        return result
      }

      if (options.dryRun) {
        result.success = true
        result.warnings.push('Dry run mode - no actual installation performed')
        return result
      }

      // 检查是否已存在
      if (await this.pluginExists(pluginPackage.config.name) && !options.force) {
        result.errors.push(`Plugin ${pluginPackage.config.name} already exists. Use force option to overwrite.`)
        return result
      }

      // 安装依赖
      if (!options.skipDependencies) {
        await this.installDependencies(pluginPackage, result)
      }

      // 创建插件文件
      await this.createPluginFiles(pluginPackage)

      // 注册插件
      await this.registerPlugin(pluginPackage.config)

      result.success = true
      
      if (this.debug) {
        // plugin installed
      }

    } catch (error) {
      result.errors.push(error.message)
    }

    return result
  }

  /**
   * 卸载插件
   */
  async uninstallPlugin(pluginName: string): Promise<InstallResult> {
    const result: InstallResult = {
      success: false,
      pluginName,
      installedDependencies: [],
      failedDependencies: [],
      errors: [],
      warnings: []
    }

    try {
      // 检查插件是否存在
      if (!(await this.pluginExists(pluginName))) {
        result.errors.push(`Plugin ${pluginName} not found`)
        return result
      }

      // 移除插件文件
      await this.removePluginFiles(pluginName)

      // 清理依赖（如果没有其他插件使用）
      this.dependencyManager.clearDependencies(pluginName)

      result.success = true
      
      if (this.debug) {
        // plugin uninstalled
      }

    } catch (error) {
      result.errors.push(error.message)
    }

    return result
  }

  /**
   * 验证插件包
   */
  private validatePackage(pluginPackage: PluginPackage): { valid: boolean; errors: string[] } {
    const errors: string[] = []

    if (!pluginPackage.config) {
      errors.push('Missing plugin config')
    } else {
      if (!pluginPackage.config.name) {
        errors.push('Missing plugin name')
      }
      if (!pluginPackage.config.version) {
        errors.push('Missing plugin version')
      }
      if (!pluginPackage.config.author) {
        errors.push('Missing plugin author')
      }
    }

    if (!pluginPackage.files || Object.keys(pluginPackage.files).length === 0) {
      errors.push('No plugin files provided')
    }

    return {
      valid: errors.length === 0,
      errors
    }
  }

  /**
   * 安装依赖
   */
  private async installDependencies(pluginPackage: PluginPackage, result: InstallResult): Promise<void> {
    const allDependencies = [
      ...(pluginPackage.config.npmDependencies || []),
      ...(pluginPackage.config.cdnDependencies || [])
    ]

    for (const dependency of allDependencies) {
      try {
        await this.dependencyManager.loadDependency(dependency)
        result.installedDependencies.push(dependency.name)
      } catch (error) {
        result.failedDependencies.push(dependency.name)
        if (dependency.required) {
          throw new Error(`Failed to install required dependency: ${dependency.name}`)
        } else {
          result.warnings.push(`Failed to install optional dependency: ${dependency.name}`)
        }
      }
    }
  }

  /**
   * 创建插件文件
   */
  private async createPluginFiles(pluginPackage: PluginPackage): Promise<void> {
    const pluginDir = `plugins/installed/${pluginPackage.config.name}`
    
    // 在实际环境中，这应该通过后端API创建文件
    // 这里只是模拟
    for (const [filePath, content] of Object.entries(pluginPackage.files)) {
      const fullPath = `${pluginDir}/${filePath}`
      
      // 模拟文件创建
      if (this.debug) {
        // creating file
      }
      
      // 实际实现中应该调用文件系统API
      // await this.createFile(fullPath, content)
    }
  }

  /**
   * 移除插件文件
   */
  private async removePluginFiles(pluginName: string): Promise<void> {
    const pluginDir = `plugins/installed/${pluginName}`
    
    // 在实际环境中，这应该通过后端API删除文件
    if (this.debug) {
      // removing plugin directory
    }
    
    // 实际实现中应该调用文件系统API
    // await this.removeDirectory(pluginDir)
  }

  /**
   * 注册插件
   */
  private async registerPlugin(config: PluginConfig): Promise<void> {
    // 这里应该通知插件管理器重新加载插件
    // 实际实现中可能需要重启应用或热重载
    if (this.debug) {
      // registering plugin
    }
  }

  /**
   * 检查插件是否存在
   */
  private async pluginExists(pluginName: string): Promise<boolean> {
    // 检查插件目录是否存在
    // 实际实现中应该调用文件系统API
    return false // 暂时返回false
  }

  /**
   * 读取文件内容
   */
  private readFileContent(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader()
      reader.onload = () => resolve(reader.result as string)
      reader.onerror = () => reject(new Error('Failed to read file'))
      reader.readAsText(file)
    })
  }

  /**
   * 解压ZIP包
   */
  private async extractZipPackage(content: string): Promise<PluginPackage> {
    // 这里应该实现ZIP文件解压逻辑
    // 可以使用JSZip库
    throw new Error('ZIP package extraction not implemented yet')
  }

  /**
   * 获取安装状态
   */
  getInstallationStatus(): {
    installing: string[]
    installed: string[]
    failed: string[]
  } {
    // 返回当前安装状态
    return {
      installing: [],
      installed: [],
      failed: []
    }
  }
}