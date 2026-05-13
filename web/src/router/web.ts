import type { RouteRecordRaw } from 'vue-router'

/**
 * 前台路由配置
 *
 * 通过 import.meta.glob 动态加载 ginkgoweb 目录下的所有页面组件：
 * - 目录存在时：使用 ginkgoweb 的完整布局和页面（Dev/Website 版）
 * - 目录不存在时：降级使用 index 的简化布局和首页（Free/Basic/Advanced 版）
 *
 * 重要：禁止使用静态 import() 引用 ginkgoweb 文件！
 * Vite 构建分析会在编译时解析所有 import() 路径，即使运行时走不到，
 * 当目录被裁剪后也会导致构建失败。必须全部通过 glob 结果查找。
 */

// 通过 import.meta.glob 收集 ginkgoweb 下所有 .vue 组件（懒加载模式）
const ginkgowebModules = import.meta.glob('../views/web/ginkgoweb/*.vue') as Record<string, () => Promise<any>>
const hasGinkgoweb = Object.keys(ginkgowebModules).length > 0

// 从 glob 结果中按文件名获取组件加载器
function gw(name: string): () => Promise<any> {
  return ginkgowebModules[`../views/web/ginkgoweb/${name}.vue`]
}

// 根据目录是否存在选择布局组件
const layoutLoader = hasGinkgoweb
  ? gw('GinkgoWebLayout')
  : () => import('../views/web/index/IndexLayout.vue')

// 首页组件
const homePageLoader = hasGinkgoweb
  ? gw('HomePage')
  : () => import('../views/web/index/HomePage.vue')

// ginkgoweb 专属页面路由（仅当 ginkgoweb 存在时才注册）
const ginkgowebChildRoutes: RouteRecordRaw[] = hasGinkgoweb
  ? [
      { path: 'download', name: 'web-download', component: gw('DownloadPage') },
      { path: 'plugins', name: 'web-plugins', component: gw('PluginStore') },
      { path: 'plugins/:id', name: 'web-plugin-detail', component: gw('PluginDetail') },
      { path: 'docs-home', name: 'web-docs-home', component: gw('DocsHome') },
      { path: 'docs', name: 'web-docs', component: gw('DocsCenter') },
      { path: 'docs-search', name: 'web-docs-search', component: gw('DocsSearch') },
      { path: 'pricing', name: 'web-pricing', component: gw('PricingPage') },
      // /web/checkout 已退役为别名跳板：所有结账流程统一走 plugin-store 提供的 /web/store/checkout（ps-checkout）。
      // 这样多发行版（Free/Basic/Advanced 无 ginkgoweb 目录）都能正常下单，且共用完整的 purchase / renew /
      // renew_perpetual / upgrade 能力。保留本路由仅为兼容历史链接（如分享出去的旧 URL、外部跳转等）。
      {
        path: 'checkout',
        name: 'web-checkout',
        meta: { requiresAuth: true },
        redirect: to => ({ path: `/${to.params.lang}/web/store/checkout`, query: to.query }),
      },
      { path: 'community', name: 'web-community', component: gw('CommunityPage') },
      { path: 'community/question/:id', name: 'web-question-detail', component: gw('QuestionDetailPage') },
      { path: 'community/ask', name: 'web-ask-question', component: gw('AskQuestionPage'), meta: { requiresAuth: true } },
      { path: 'tutorials', name: 'web-tutorials', component: gw('TutorialCenter') },
      { path: 'donate', name: 'web-donate', component: gw('DonatePage') },
      { path: 'about', name: 'web-about', component: gw('AboutPage') },
      { path: 'license', name: 'web-license', component: gw('LicensePage') },
      { path: 'plugin-license-agreement', name: 'web-plugin-license-agreement', component: gw('PluginLicenseAgreementPage') },
      { path: 'privacy', name: 'web-privacy', component: gw('PrivacyPolicyPage') },
      { path: 'terms', name: 'web-terms', component: gw('TermsPage') },
      { path: 'security', name: 'web-security', component: gw('SecurityPage') },
      { path: 'changelog', name: 'web-changelog', component: gw('ChangelogPage') },
    ]
  : []

// 登录/注册页面：各目录有自己的版本
const authRoutes: RouteRecordRaw[] = hasGinkgoweb
  ? [
      { path: 'login', name: 'web-login', component: gw('WebLoginPage') },
      { path: 'register', name: 'web-register', component: gw('RegisterPage') },
      { path: 'forgot-password', name: 'web-forgot-password', component: gw('ForgotPasswordPage') },
    ]
  : [
      { path: 'login', name: 'web-login', component: () => import('../views/web/index/WebLogin.vue') },
      { path: 'register', name: 'web-register', component: () => import('../views/web/index/WebRegister.vue') },
      { path: 'forgot-password', name: 'web-forgot-password', component: () => import('../views/web/index/ForgotPassword.vue') },
    ]

// 用户中心路由（所有版本共用）
//
// 重要：会员中心是"门户共享容器"，其侧边栏（@/web/src/views/web/user/components/WebUserSidebar.vue）
// 通过 portal:user-menu 钩子聚合各业务插件注入的菜单项（如 plugin-store 的"已买插件"、license 的
// "我的订单"）。由于这些路由是静态注册，进入时会被 vue-router 直接命中，路由前置守卫中的
// initPortalPluginRoutes 会因 isMatchedBeforePortal===true 而被跳过，导致业务插件不装载、
// portal:user-menu 钩子从未注册、侧边栏只剩框架内置项。
//
// 修复方式：在 meta 中标记 portalShellNeedsPlugins，让 router 在进入这类静态命中路由时
// 强制触发一次门户插件全量兜底装载（一次性、带短路缓存），从而让 portal:user-menu 等
// "前台共享容器插槽"钩子在渲染前就已就绪。
const userRoutes: RouteRecordRaw[] = [
  { path: 'user', name: 'web-user-center', component: () => import('../views/web/user/UserCenter.vue'), meta: { requiresAuth: true, portalShellNeedsPlugins: true } },
  { path: 'user/profile', name: 'web-user-profile', component: () => import('../views/web/user/UserProfile.vue'), meta: { requiresAuth: true, portalShellNeedsPlugins: true } },
  { path: 'user/notifications', name: 'web-user-notifications', component: () => import('../views/web/user/UserNotifications.vue'), meta: { requiresAuth: true, portalShellNeedsPlugins: true } },
  { path: 'user/logs', name: 'web-user-logs', component: () => import('../views/web/user/UserLogs.vue'), meta: { requiresAuth: true, portalShellNeedsPlugins: true } },
]


export const webRoutes: RouteRecordRaw = {
  path: '/:lang/web',
  name: 'web-root',
  component: layoutLoader,
  meta: { public: true },
  children: [
    { path: '', name: 'web-home', component: homePageLoader },
    ...authRoutes,
    ...userRoutes,
    ...ginkgowebChildRoutes,
  ]
}
