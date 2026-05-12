import { PluginManager } from './core/PluginManager'
import type { PluginInitializeOptions } from './core/PluginManager'

/**
 * 初始化插件系统
 */
export async function initializePluginSystem(options?: PluginInitializeOptions) {
  const pluginManager = PluginManager.getInstance()
  
  try {
    
    // 初始化插件管理器
    await pluginManager.initialize(options)
    
    // 返回插件管理器实例
    return pluginManager
  } catch (error) {
    throw error
  }
}

/**
 * 获取插件管理器实例
 */
export function getPluginManager() {
  return PluginManager.getInstance()
}

// 导出类型
export type { Plugin, PluginConfig, PluginAPI } from './core/types'
export { PluginManager } from './core/PluginManager'