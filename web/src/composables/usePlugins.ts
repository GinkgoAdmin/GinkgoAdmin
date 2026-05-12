import { ref, computed, onMounted } from 'vue'
import { PluginManager } from '../plugins/core/PluginManager'
import type { PluginConfig } from '../plugins/core/types'

/**
 * 插件系统组合式函数
 */
export function usePlugins() {
  const pluginManager = PluginManager.getInstance()
  const loading = ref(false)
  const error = ref<string | null>(null)

  // 获取所有插件
  const plugins = computed(() => pluginManager.getPlugins())
  
  // 获取已启用的插件
  const enabledPlugins = computed(() => {
    return Object.values(plugins.value).filter(plugin => plugin.enabled)
  })
  
  // 获取已禁用的插件
  const disabledPlugins = computed(() => {
    return Object.values(plugins.value).filter(plugin => !plugin.enabled)
  })

  // 检查插件是否启用
  const isPluginEnabled = (name: string) => {
    return pluginManager.isPluginEnabled(name)
  }

  // 启用插件
  const enablePlugin = async (name: string) => {
    try {
      loading.value = true
      error.value = null
      await pluginManager.enablePlugin(name)
    } catch (err) {
      error.value = err instanceof Error ? err.message : '启用插件失败'
      throw err
    } finally {
      loading.value = false
    }
  }

  // 禁用插件
  const disablePlugin = async (name: string) => {
    try {
      loading.value = true
      error.value = null
      await pluginManager.disablePlugin(name)
    } catch (err) {
      error.value = err instanceof Error ? err.message : '禁用插件失败'
      throw err
    } finally {
      loading.value = false
    }
  }

  // 卸载插件
  const uninstallPlugin = async (name: string) => {
    try {
      loading.value = true
      error.value = null
      await pluginManager.unregisterPlugin(name)
    } catch (err) {
      error.value = err instanceof Error ? err.message : '卸载插件失败'
      throw err
    } finally {
      loading.value = false
    }
  }

  // 执行钩子
  const executeHook = (hookName: string, ...args: any[]) => {
    return pluginManager.executeHook(hookName, ...args)
  }

  // 异步执行钩子
  const executeHookAsync = async (hookName: string, ...args: any[]) => {
    return pluginManager.executeHookAsync(hookName, ...args)
  }

  // 获取组件
  const getPluginComponent = (name: string) => {
    return pluginManager.getComponent(name)
  }

  // 检查组件是否存在
  const hasPluginComponent = (name: string) => {
    return !!pluginManager.getComponent(name)
  }

  // 初始化插件系统
  const initializePlugins = async () => {
    try {
      loading.value = true
      error.value = null
      await pluginManager.initialize()
    } catch (err) {
      error.value = err instanceof Error ? err.message : '初始化插件系统失败'
      throw err
    } finally {
      loading.value = false
    }
  }

  // 热重载插件
  const reloadPlugin = async (name: string) => {
    try {
      loading.value = true
      error.value = null
      
      // 先卸载插件
      await pluginManager.unregisterPlugin(name)
      
      // 重新加载插件模块
      const pluginPath = `../plugins/installed/${name}/index.ts`
      const module = await import(/* @vite-ignore */ pluginPath + '?t=' + Date.now())
      
      if (module.default) {
        await pluginManager.registerPlugin(module.default)
      }
    } catch (err) {
      error.value = err instanceof Error ? err.message : '重载插件失败'
      throw err
    } finally {
      loading.value = false
    }
  }

  // 安装新插件
  const installPlugin = async (pluginData: any) => {
    try {
      loading.value = true
      error.value = null
      
      // 这里应该处理插件的下载、验证和安装
      // 暂时模拟安装过程
      await new Promise(resolve => setTimeout(resolve, 2000))
      
      // 注册插件
      await pluginManager.registerPlugin(pluginData)
    } catch (err) {
      error.value = err instanceof Error ? err.message : '安装插件失败'
      throw err
    } finally {
      loading.value = false
    }
  }

  // 获取插件统计信息
  const getPluginStats = () => {
    const allPlugins = Object.values(plugins.value)
    return {
      total: allPlugins.length,
      enabled: allPlugins.filter(p => p.enabled).length,
      disabled: allPlugins.filter(p => !p.enabled).length
    }
  }

  // 搜索插件
  const searchPlugins = (query: string) => {
    const allPlugins = Object.values(plugins.value)
    const lowerQuery = query.toLowerCase()
    
    return allPlugins.filter(plugin => 
      plugin.name.toLowerCase().includes(lowerQuery) ||
      plugin.description.toLowerCase().includes(lowerQuery) ||
      plugin.author.toLowerCase().includes(lowerQuery)
    )
  }

  // 按类别获取插件
  const getPluginsByCategory = (category: string) => {
    const allPlugins = Object.values(plugins.value)
    return allPlugins.filter(plugin => 
      plugin.name.includes(category) || 
      plugin.description.includes(category)
    )
  }

  return {
    // 状态
    loading: computed(() => loading.value || pluginManager.isLoading),
    error: computed(() => error.value),
    plugins,
    enabledPlugins,
    disabledPlugins,
    
    // 方法
    isPluginEnabled,
    enablePlugin,
    disablePlugin,
    uninstallPlugin,
    executeHook,
    executeHookAsync,
    getPluginComponent,
    hasPluginComponent,
    initializePlugins,
    reloadPlugin,
    installPlugin,
    getPluginStats,
    searchPlugins,
    getPluginsByCategory
  }
}

/**
 * 插件开发辅助函数
 */
export function usePluginDevelopment() {
  const pluginManager = PluginManager.getInstance()

  // 执行npm命令
  const executeNpmCommand = async (command: string, cwd?: string) => {
    try {
      // 在实际环境中，这应该通过后端API执行
      // 这里只是模拟
      
      // 模拟命令执行
      await new Promise(resolve => setTimeout(resolve, 3000))
      
      return {
        success: true,
        output: `npm ${command} executed successfully`
      }
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Command execution failed'
      }
    }
  }

  // 热重启开发服务器
  const hotRestart = async () => {
    try {
      // 通知后端重启开发服务器
      const response = await fetch('/api/dev/restart', {
        method: 'POST'
      })
      
      if (response.ok) {
        // 重新加载页面
        window.location.reload()
      } else {
        throw new Error('Failed to restart dev server')
      }
    } catch (error) {
      throw error
    }
  }

  // 安装插件依赖
  const installPluginDependencies = async (pluginName: string, dependencies: string[]) => {
    const results = []
    
    for (const dep of dependencies) {
      const result = await executeNpmCommand(`install ${dep}`, `plugins/${pluginName}`)
      results.push({ dependency: dep, ...result })
    }
    
    return results
  }

  // 构建插件
  const buildPlugin = async (pluginName: string) => {
    return executeNpmCommand('run build', `plugins/${pluginName}`)
  }

  // 监听插件文件变化
  const watchPlugin = async (pluginName: string) => {
    return executeNpmCommand('run dev', `plugins/${pluginName}`)
  }

  return {
    executeNpmCommand,
    hotRestart,
    installPluginDependencies,
    buildPlugin,
    watchPlugin
  }
}