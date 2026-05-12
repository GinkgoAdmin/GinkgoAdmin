import type { Plugin } from '../../core/types'

const MyPlugin: Plugin = {
  config: {
    name: 'my-plugin',
    version: '1.0.0',
    description: 'A basic plugin template',
    author: 'Your Name',
    enabled: true,
    hooks: []
  },

  async install(api) {
    api.log('Installing my plugin...')

    // 注册组件
    // api.registerComponent('my-component', MyComponent)

    // 注册钩子
    // api.addHook('my-hook', (data) => {
    //   // 处理钩子逻辑
    //   return data
    // }, 10)

    api.log('My plugin installed successfully')
  },

  async uninstall(api) {
    api.log('Uninstalling my plugin...')
    
    // 清理资源
    
    api.log('My plugin uninstalled')
  }
}

export default MyPlugin