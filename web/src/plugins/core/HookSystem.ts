import type { PluginHook, HookContext } from './types'

/**
 * 钩子系统 - 实现插件的事件驱动机制
 */
export class HookSystem {
  private hooks: Map<string, PluginHook[]> = new Map()
  private debug = import.meta.env.DEV

  /**
   * 注册钩子
   */
  addHook(hookName: string, handler: Function, priority = 10, pluginName = 'unknown'): void {
    if (!this.hooks.has(hookName)) {
      this.hooks.set(hookName, [])
    }

    const hook: PluginHook = {
      name: pluginName,
      priority,
      handler
    }

    const hooks = this.hooks.get(hookName)!
    hooks.push(hook)
    
    // 按优先级排序（数字越小优先级越高）
    hooks.sort((a, b) => a.priority - b.priority)

    if (this.debug) {
      // hook registered
    }
  }

  /**
   * 移除钩子
   */
  removeHook(hookName: string, handler: Function): void {
    const hooks = this.hooks.get(hookName)
    if (hooks) {
      const index = hooks.findIndex(hook => hook.handler === handler)
      if (index > -1) {
        const removed = hooks.splice(index, 1)[0]
        if (this.debug) {
          // hook removed
        }
      }
    }
  }

  /**
   * 执行钩子 - 同步执行
   */
  executeHook(hookName: string, ...args: any[]): any {
    const hooks = this.hooks.get(hookName)
    if (!hooks || hooks.length === 0) {
      return args[0] // 返回第一个参数作为默认值
    }

    let result = args[0]
    
    for (const hook of hooks) {
      try {
        const context: HookContext = {
          pluginName: hook.name,
          hookName,
          args,
          result
        }
        
        const hookResult = hook.handler(result, context, ...args.slice(1))
        if (hookResult !== undefined) {
          result = hookResult
        }
      } catch (error) {
        // silently ignored
      }
    }

    return result
  }

  /**
   * 异步执行钩子
   */
  async executeHookAsync(hookName: string, ...args: any[]): Promise<any> {
    const hooks = this.hooks.get(hookName)
    if (!hooks || hooks.length === 0) {
      return args[0]
    }

    let result = args[0]
    
    for (const hook of hooks) {
      try {
        const context: HookContext = {
          pluginName: hook.name,
          hookName,
          args,
          result
        }
        
        const hookResult = await hook.handler(result, context, ...args.slice(1))
        if (hookResult !== undefined) {
          result = hookResult
        }
      } catch (error) {
        // silently ignored
      }
    }

    return result
  }

  /**
   * 获取钩子列表
   */
  getHooks(hookName?: string): Map<string, PluginHook[]> | PluginHook[] | undefined {
    if (hookName) {
      return this.hooks.get(hookName)
    }
    return this.hooks
  }

  /**
   * 清除所有钩子
   */
  clearHooks(): void {
    this.hooks.clear()
    if (this.debug) {
      // all hooks cleared
    }
  }

  /**
   * 清除指定插件的钩子
   */
  clearPluginHooks(pluginName: string): void {
    for (const [hookName, hooks] of this.hooks.entries()) {
      const filtered = hooks.filter(hook => hook.name !== pluginName)
      if (filtered.length !== hooks.length) {
        this.hooks.set(hookName, filtered)
        if (this.debug) {
          // cleared hooks for plugin
        }
      }
    }
  }
}