// 插件系统类型定义
export interface PluginDependency {
  name: string
  version?: string
  type: 'npm' | 'cdn' | 'plugin'
  url?: string
  required: boolean
  installCommand?: string
  description?: string // 依赖说明
  // 以下为前端加载安全/兼容性选项
  integrity?: string
  crossorigin?: string
  module?: 'esm' | 'umd'
}

export interface PluginAsset {
  type: 'css' | 'js'
  url: string
  integrity?: string
  crossorigin?: string
}

export interface PluginConfig {
  name: string
  version: string
  description: string
  author: string
  dependencies?: string[] // 插件依赖
  npmDependencies?: PluginDependency[] // NPM包依赖
  cdnDependencies?: PluginDependency[] // CDN依赖
  assets?: PluginAsset[] // 静态资源
  hooks?: string[]
  components?: Record<string, any>
  enabled: boolean
  installTime?: string
  updateTime?: string
  installStatus?: 'installing' | 'installed' | 'failed' | 'pending'
  dependencyStatus?: Record<string, 'pending' | 'installing' | 'installed' | 'failed'>
}

export interface PluginHook {
  name: string
  priority: number
  handler: (...args: any[]) => any
}

export interface PluginSlot {
  name: string
  props?: Record<string, any>
  context?: Record<string, any>
}

export interface PluginComponent {
  name: string
  component: any
  props?: Record<string, any>
  slots?: string[]
}

export interface PluginAPI {
  // 注册钩子
  addHook: (hookName: string, handler: Function, priority?: number) => void
  // 移除钩子
  removeHook: (hookName: string, handler: Function) => void
  // 注册组件
  registerComponent: (name: string, component: any) => void
  // 获取配置
  getConfig: () => PluginConfig
  // 日志记录
  log: (message: string, level?: 'info' | 'warn' | 'error') => void
  // 依赖管理
  loadDependency: (dependency: PluginDependency) => Promise<void>
  checkDependency: (name: string) => boolean
  // 资源管理
  loadAsset: (asset: PluginAsset) => Promise<void>
  // 执行命令（默认关闭，仅 server 模式可用）
  executeCommand: (command: string) => Promise<{ success: boolean; output: string }>
}

export interface Plugin {
  config: PluginConfig
  install: (api: PluginAPI) => void | Promise<void>
  uninstall?: (api: PluginAPI) => void | Promise<void>
  update?: (api: PluginAPI) => void | Promise<void>
}

export interface HookContext {
  pluginName: string
  hookName: string
  args: any[]
  result?: any
}