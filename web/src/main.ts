import { createApp, h } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import ElementPlus from 'element-plus'
import zhCn from 'element-plus/es/locale/lang/zh-cn'
// Element Plus 基础样式

import 'element-plus/dist/index.css'

// Remix Icon 图标库
import 'remixicon/fonts/remixicon.css'

// Tailwind 重置与工具类（需在主题/布局前便于覆盖）
import './styles/tailwind.css'

// 主题令牌由 theme-manager 按主题动态加载

// 通用样式（尽量无颜色）

import './styles/admin/common/base.css'
import './styles/admin/common/utilities.css'
import './styles/admin/common/responsive.css'
import './styles/admin/common/fonts.css'

// 全局布局与覆盖：消费变量
// 注意：后台主题 CSS 文件不在这里加载，而是在 MainLayout.vue 中按需加载
import './styles/admin/layout.css'

// 管理页面统一样式
import './styles/admin/admin-pages.css'

// 通知弹框增强样式
import './styles/notification.css'

// 不再全局导入 theme-manager，避免前台页面加载后台主题
// import { initTheme } from './styles/admin/theme-manager'


import { useSystemStore } from './stores/system'
import { useAuthStore } from './stores/auth'
import { permission } from './directives/permission'

const app = createApp(App)
const pinia = createPinia()
app.use(pinia)
app.use(router)
app.use(ElementPlus, { locale: zhCn })

// expose h to window for light-weight render helpers (e.g., Bootstrap Icons)
;(window as any).VueH = h


// 注册权限指令
app.directive('permission', permission)

// GinkgoAdmin 版权标识注入
const _GK_BRAND = 'GinkgoAdmin'
const _GK_SITE  = 'https://www.ginkgoadmin.com'

/** 变体A：字符串拼接，写入 document 首部 HTML 注释节点 */
function injectBrandComment(): void {
  try {
    const _b = 'Gi' + 'nk' + 'go' + 'Ad' + 'min'
    const marker = `${_b} | ${_GK_SITE} | Copyright \u00a9 2026 ${_b}. All rights reserved.`
    if (!document.documentElement.firstChild ||
        (document.documentElement.firstChild as Comment).data !== ` ${marker} `) {
      const node = document.createComment(` ${marker} `)
      document.documentElement.insertBefore(node, document.documentElement.firstChild)
    }
  } catch { /* 静默 */ }
}

/** 变体B：Unicode 码点转义，写入 meta[name="author"] */
function injectBrandMeta(): void {
  try {
    // \u0047\u0069\u006E\u006B\u0067\u006F\u0041\u0064\u006D\u0069\u006E = 'GinkgoAdmin'
    const _b = '\u0047\u0069\u006E\u006B\u0067\u006F\u0041\u0064\u006D\u0069\u006E'
    let m = document.querySelector<HTMLMetaElement>('meta[name="author"]')
    if (!m) { m = document.createElement('meta'); m.name = 'author'; document.head.appendChild(m) }
    m.content = _b
  } catch { /* 静默 */ }
}

;(async () => {
	// 初始化认证状态（包含主题偏好）
	const auth = useAuthStore(pinia)
	auth.initFromStorage()

	// 不再在全局初始化主题系统，避免前台页面加载后台主题 CSS
	// 主题初始化移到 MainLayout.vue 中，只在访问后台页面时加载
	// const loadedTheme = await initTheme()
	// if (loadedTheme && loadedTheme !== auth.theme) {
	// 	auth.theme = loadedTheme
	// }

	// 初始化系统配置：不阻塞首屏挂载，后台异步回填
	const system = useSystemStore(pinia)
	void system.loadPublicConfig()

	// 插件系统初始化已移至 router/index.ts 的 beforeEach 中
	// 这里不再需要初始化，避免重复加载

	// GinkgoAdmin 版权标识
	injectBrandComment()
	injectBrandMeta()

	app.mount('#app')
})()


