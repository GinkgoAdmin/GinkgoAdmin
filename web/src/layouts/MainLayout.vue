<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <!-- 未登录时不渲染任何内容，直接跳转登录页 -->
  <div v-if="!isAuthenticated" class="auth-checking">
    <!-- 可选：显示加载状态 -->
  </div>
  <div v-else :class="['admin-layout', { 'admin-dark': auth.theme==='dark' }]" class="min-h-screen bg-gray-50 dark:bg-gray-900">
    <!-- 移动端侧边栏遮罩 -->
    <div v-if="isMobileSidebarOpen" class="sidebar-overlay" @click="closeMobileSidebar"></div>

    <el-container class="min-h-screen">
      <!-- 侧边栏 -->
      <el-aside :width="sidebarWidth"
                :class="['ginkgo-sidebar', { 'is-collapsed': menuStore.collapsed, 'mobile-open': isMobileSidebarOpen }]">
        <!-- 品牌区域 -->
        <div class="ginkgo-brand-header">
          <div class="ginkgo-brand-content">
            <!-- 展开状态：显示LOGO和标题 -->
            <div v-show="!menuStore.collapsed" class="brand-expanded">
              <div class="logo-container">
                <img v-if="logoUrl" :src="logoUrl" alt="logo"
                     class="h-10 w-10 rounded-lg ginkgo-logo-animate"/>
                  <div v-else class="h-10 w-10 bg-gradient-to-br from-blue-500 to-blue-600 rounded-lg flex items-center justify-center ginkgo-logo-animate shadow-lg">
                  <svg class="w-6 h-6 text-white" fill="currentColor" viewBox="0 0 20 20">
                    <path d="M10 2L3 7v11h4v-6h6v6h4V7l-7-5z"/>
                  </svg>
                </div>
              </div>
              <div class="brand-text">
                <h1 class="brand-title">{{ siteTitle }}</h1>
                <p class="brand-subtitle">管理后台</p>
              </div>
            </div>

            <!-- 折叠状态：只显示折叠按钮 -->
            <div v-show="menuStore.collapsed" class="brand-collapsed">
              <!-- 空白区域，只显示折叠按钮 -->
            </div>
          </div>

          <!-- 折叠按钮 - 重新定位到右侧 -->
          <button @click="toggleSidebar" class="ginkgo-collapse-btn">
            <svg class="collapse-icon"
                 :class="{ 'rotate-180': menuStore.collapsed }"
                 fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M15 19l-7-7 7-7"/>
            </svg>
          </button>
        </div>

        <!-- 菜单搜索（仅在展开状态显示） -->
        <div v-show="!menuStore.collapsed" class="ginkgo-menu-search">
          <el-input
            v-model="searchText"
            placeholder="搜索菜单..."
            :prefix-icon="Search"
            size="small"
            class="search-input"
            clearable
          />
        </div>

        <!-- 菜单区域 -->
        <div class="ginkgo-menu-container">
          <el-menu
            ref="menuRef"
            :default-active="active"
            router
            class="ginkgo-sidebar-menu"
            :collapse="menuStore.collapsed"
            :collapse-transition="true"
            :unique-opened="true"
          >
            <!-- 首页 -->
            <!-- 动态菜单 - 支持多级递归 -->
            <template v-for="menu in filteredMenus" :key="menu.id">
              <recursive-menu-item
                :menu="menu"
                :compute-admin-index="computeAdminIndex"
                :depth="1"
                :max-depth="4"
              />
            </template>
          </el-menu>
        </div>
      </el-aside>

      <!-- 主内容区域 -->
      <el-container class="main-container">
        <!-- 顶部导航栏 -->
        <el-header class="header-container">
          <div class="header-content">
            <!-- 移动端菜单切换按钮（仅在 768px 以下显示） -->
            <div class="mobile-menu-toggle">
              <el-button :icon="MenuIcon" circle size="small" @click="toggleMobileSidebar" />
            </div>

            <!-- 中间：标签页区域 -->
            <div class="header-tabs">
              <div ref="tabsScrollRef" class="tabs-scroll-container">
                <el-tabs
                  v-model="tabsStore.activeTab"
                  type="card"
                  class="header-admin-tabs"
                  @tab-click="onTabClick"
                  @tab-remove="onTabRemove"
                >
                  <el-tab-pane
                    v-for="tab in tabsStore.tabs"
                    :key="tab.id"
                    :label="tab.title"
                    :name="tab.id"
                    :closable="tab.closable"
                  >
                    <template #label>
                      <div class="header-tab-label">
                        <i :class="'bi header-tab-icon bi-'+tab.icon"></i>
                        <span class="header-tab-title">{{ tab.title }}</span>
                      </div>
                    </template>
                  </el-tab-pane>
                </el-tabs>
              </div>
              
              <!-- 标签页操作菜单 -->
              <el-dropdown class="header-tabs-actions" trigger="click">
                <el-button size="small" :icon="MoreFilled" circle />
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item @click="refreshCurrentTab">
                      <el-icon><Refresh /></el-icon>
                      刷新当前页
                    </el-dropdown-item>
                    <el-dropdown-item @click="closeOtherTabs">
                      <el-icon><Close /></el-icon>
                      关闭其他
                    </el-dropdown-item>
                    <el-dropdown-item @click="closeAllTabs">
                      <el-icon><CircleClose /></el-icon>
                      关闭所有
                    </el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </div>

            <!-- 右侧：操作区 -->
            <div class="header-right">
              <!-- 动态注册的头部组件 -->
              <template v-for="(widget, index) in headerWidgets" :key="'widget-' + index">
                <component :is="widget.component" />
              </template>

              <!-- 通知 -->
              <NotificationPopover />

              <!-- 主题切换 -->
              <el-tooltip content="切换主题" placement="bottom">
                <el-button :icon="isDark ? Sunny : Moon"
                          circle
                          size="small"
                          @click="toggleTheme"
                          class="theme-btn" />
              </el-tooltip>

              <!-- 全屏 -->
              <el-tooltip content="全屏" placement="bottom">
                <el-button :icon="FullScreen"
                          circle
                          size="small"
                          @click="toggleFullscreen"
                          class="fullscreen-btn" />
              </el-tooltip>

              <!-- 用户菜单 -->
              <el-dropdown class="user-dropdown" trigger="click">
                <div class="user-info">
                  <el-avatar :size="32" class="user-avatar" :src="auth.avatar || ''">
                    {{ (auth.displayName || auth.userName || '用户').charAt(0).toUpperCase() }}
                  </el-avatar>
                  <div v-show="!menuStore.collapsed" class="user-details">
                    <div class="user-name">{{ auth.displayName || auth.userName || '用户' }}</div>
                    <div class="user-role">{{ (auth.roles && auth.roles[0]) || '管理员' }}</div>
                  </div>
                  <el-icon class="dropdown-icon"><ArrowDown /></el-icon>
                </div>
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item @click="goToProfile">
                      <el-icon><User /></el-icon>
                      个人资料
                    </el-dropdown-item>
                    <el-dropdown-item @click="goToNotifications">
                      <el-icon><Bell /></el-icon>
                      我的通知
                      <el-badge :value="notificationStore.displayBadge" :hidden="!notificationStore.hasUnread" class="ml-2" />
                    </el-dropdown-item>
                    <el-dropdown-item @click="goToLogs">
                      <el-icon><Document /></el-icon>
                      我的日志
                    </el-dropdown-item>
                    <el-dropdown-item
                      v-for="(item, idx) in pluginUserMenuItems"
                      :key="'usr-plg-'+idx"
                      :divided="idx===0"
                      @click="handlePluginUserMenu(item)">
                      <el-icon v-if="item.icon"><component :is="getMenuIcon(item.icon)" /></el-icon>
                      {{ item.title || item.name }}
                    </el-dropdown-item>

                    <el-dropdown-item divided @click="handleClearCache">
                      <el-icon><RefreshRight /></el-icon>
                      清空缓存
                    </el-dropdown-item>
                    <el-dropdown-item divided @click="onLogout">
                      <el-icon><SwitchButton /></el-icon>
                      退出登录
                    </el-dropdown-item>
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </div>
          </div>
        </el-header>

        <!-- 主内容区 -->
        <el-main class="main-content">
          <div class="content-wrapper">
            <router-view :key="route.fullPath" />
          </div>
        </el-main>
      </el-container>
    </el-container>

    <!-- 全局浮动组件（由插件通过 layout:global 钩子注入，如 AI 对话浮动按钮） -->
    <template v-for="(item, index) in globalFloatingComponents" :key="'gfc-' + index">
      <component :is="item.component" />
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, shallowRef, onMounted, watch, onUnmounted, nextTick, defineAsyncComponent, type Ref, type ShallowRef } from 'vue'
import { useRoute, useRouter, type RouteLocationNormalized } from 'vue-router'
import { ElMessage, ElMessageBox, ElLoading } from 'element-plus'
import { useAuthStore } from '../stores/auth'
import { useSystemStore } from '../stores/system'
import { useMenuStore } from '../stores/menu'
import { useLanguageStore } from '../stores/language'
import { useTabsStore } from '../stores/tabs'
import { useNotificationStore } from '../stores/notification'
import { loginFullPath, ADMIN_TITLE, adminBasePath } from '../config/admin'
import { getUnreadNotificationCount } from '../api/user'
import {
  House, Search, Bell, Sunny, Moon, FullScreen, ArrowDown,
  User, Setting, SwitchButton, DataAnalysis, Tools, Menu as MenuIcon,
  UserFilled, Key, OfficeBuilding, Collection, Document, Folder,
  MoreFilled, Refresh, Close, CircleClose, RefreshRight
} from '@element-plus/icons-vue'
import { getPluginManager } from '../plugins'
import { injectAdminRoutes } from '../router/admin'
import type { TabsPaneContext } from 'element-plus'
import RecursiveMenuItem from '../components/RecursiveMenuItem.vue'
import NotificationPopover from '../components/NotificationPopover.vue'

// Router and stores
const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const system = useSystemStore()
const menuStore = useMenuStore()
const languageStore = useLanguageStore()
const tabsStore = useTabsStore()
const notificationStore = useNotificationStore()

// 认证状态检查 - 路由守卫已处理未登录跳转，这里仅用于模板条件渲染
const isAuthenticated = computed(() => !!auth.token)

// Reactive data
// 注：headerWidgets / globalFloatingComponents 数组元素包含 Vue 组件实例，
// 使用 shallowRef 避免 Vue 将组件包裹为 reactive 对象（会报“markRaw / shallowRef”警告）。
const headerWidgets: ShallowRef<Array<{ component: any, order?: number }>> = shallowRef([])
const globalFloatingComponents: ShallowRef<Array<{ component: any, order?: number }>> = shallowRef([])
const pluginUserMenuItems: Ref<Array<{ title?: string; name?: string; icon?: string; onClick?: Function; route?: string; path?: string; url?: string }>> = ref([])
const searchText: Ref<string> = ref('')

const loadPluginLayoutExtensions = async () => {
  try {
    const pluginManager = getPluginManager()

    const injectedItems = await pluginManager.executeHookAsync('user:menu:register', [])
    pluginUserMenuItems.value = Array.isArray(injectedItems) ? injectedItems : []

    const injectedWidgets = await pluginManager.executeHookAsync('header:widget', [])
    if (Array.isArray(injectedWidgets)) {
      headerWidgets.value = injectedWidgets.map(widget => {
        if (typeof widget.component === 'function') {
          return { ...widget, component: defineAsyncComponent(widget.component) }
        }
        return widget
      }).sort((a, b) => (a.order || 0) - (b.order || 0))
    } else {
      headerWidgets.value = []
    }

    const injectedFloating = await pluginManager.executeHookAsync('layout:global', [])
    if (Array.isArray(injectedFloating)) {
      globalFloatingComponents.value = injectedFloating.map(item => {
        if (typeof item.component === 'function') {
          return { ...item, component: defineAsyncComponent(item.component) }
        }
        return item
      }).sort((a, b) => (a.order || 0) - (b.order || 0))
    } else {
      globalFloatingComponents.value = []
    }
  } catch (error) {
    pluginUserMenuItems.value = []
    headerWidgets.value = []
    globalFloatingComponents.value = []
  }
}

const handlePluginsReloaded = () => {
  void loadPluginLayoutExtensions()
}
const menuRef = ref<any>(null)
const tabsScrollRef = ref<HTMLElement | null>(null)

// ===== 标签栏鼠标拖拽滚动 =====
let isDragging = false
let dragStartX = 0
let dragScrollLeft = 0
let hasDragged = false
let dragScrollTarget: HTMLElement | null = null

function onTabsDragStart(e: MouseEvent): void {
  // 仅左键触发
  if (e.button !== 0) return
  const container = tabsScrollRef.value
  if (!container) return
  // 获取 Element Plus 内部的实际滚动容器
  const scrollEl = container.querySelector('.el-tabs__nav-scroll') as HTMLElement
  if (!scrollEl) return
  isDragging = true
  hasDragged = false
  dragStartX = e.clientX
  dragScrollLeft = scrollEl.scrollLeft
  dragScrollTarget = scrollEl
  scrollEl.style.cursor = 'grabbing'
  scrollEl.style.userSelect = 'none'
  e.preventDefault()
}

function onTabsDragMove(e: MouseEvent): void {
  if (!isDragging || !dragScrollTarget) return
  const dx = e.clientX - dragStartX
  // 超过 3px 才算真正拖拽，用于区分点击和拖拽
  if (Math.abs(dx) > 3) {
    hasDragged = true
  }
  dragScrollTarget.scrollLeft = dragScrollLeft - dx
}

function onTabsDragEnd(): void {
  if (!isDragging) return
  isDragging = false
  if (dragScrollTarget) {
    dragScrollTarget.style.cursor = ''
    dragScrollTarget.style.userSelect = ''
  }
  // 如果发生了拖拽，短暂阻止标签的点击事件
  if (hasDragged) {
    const container = tabsScrollRef.value
    if (container) {
      container.style.pointerEvents = 'none'
      setTimeout(() => {
        container.style.pointerEvents = ''
      }, 50)
    }
  }
  dragScrollTarget = null
}

// Mobile sidebar state
const isMobileSidebarOpen = ref(false)
const isMobileViewport = ref(false)

// Plugin user menu handler
import { switchTheme } from '../styles/admin/theme-manager'

function handlePluginUserMenu(item: { onClick?: Function; route?: string; path?: string; url?: string }): void {
  try {
    if (item?.onClick && typeof item.onClick === 'function') {
      return item.onClick({ router })
    }
  } catch (error) {
    // silently ignored
  }

  if (item?.route) {
    router.push(item.route)
    return
  }

  if (item?.path) {
    router.push(item.path)
    return
  }

  if (item?.url) {
    window.open(item.url, '_blank')
  }
}

// Computed properties
const active = computed((): string => route.path)
const isDark = computed({
  get: (): boolean => auth.theme === 'dark',
  set: (value: boolean): void => {
    auth.theme = value ? 'dark' : 'light'
  }
})
const siteTitle = computed((): string => system.siteName || ADMIN_TITLE)
const logoUrl = computed((): string | undefined => system.logoUrl)

// Sidebar width: 0 on mobile (≤768px) to prevent layout issues, normal width on desktop
const sidebarWidth = computed((): string => {
  // Use reactive isMobileViewport instead of direct window check
  if (isMobileViewport.value) {
    return '0'
  }
  return menuStore.collapsed ? '80px' : '280px'
})

const currentMenu = computed(() => menuStore.findMenuByRoute(route.path))

// Filtered menus based on search text (case-insensitive, preserves hierarchy)
const filteredMenus = computed(() => {
  const search = (searchText.value || '').trim().toLowerCase()
  if (!search) {
    return menuStore.visibleMenus
  }

  const results = (menuStore.visibleMenus || []).map((parent: any) => {
    const parentName: string = String(parent?.name || '')
    const parentMatches = parentName.toLowerCase().includes(search)

    const allChildren: any[] = Array.isArray(parent?.children) ? parent.children : []
    const filteredChildren = allChildren.filter((child: any) => {
      const childName: string = String(child?.name || '')
      return child?.enabled && childName.toLowerCase().includes(search)
    })

    if (parentMatches || filteredChildren.length > 0) {
      return {
        ...parent,
        children: parentMatches ? allChildren.filter((c: any) => c?.enabled) : filteredChildren
      }
    }
    return null
  }).filter((menu): menu is NonNullable<typeof menu> => menu !== null)

  return results
})

// Auto expand/open matched parents when searching
watch(searchText, async (val) => {
  const search = (val || '').trim()
  if (!search) return
  await nextTick()
  try {
    const elMenu = menuRef.value as any
    if (!elMenu) return
    // Expand all parents that have visible children after filter
    const openKeys: string[] = []
    for (const parent of filteredMenus.value as any[]) {
      if (Array.isArray(parent?.children) && parent.children.length > 0) {
        openKeys.push(parent.id)
      }
    }
    if (typeof elMenu.open === 'function') {
      openKeys.forEach(key => {
        try { elMenu.open(key) } catch {}
      })
    } else if (Array.isArray(elMenu.openedMenus)) {
      elMenu.openedMenus = Array.from(new Set([...(elMenu.openedMenus || []), ...openKeys]))
    }
  } catch {}
})

// Ensure admin-dark class is applied to <html> and <body> for popper/teleported elements
// 使用 admin-dark 类而不是 dark 类，避免影响前台页面
function syncDocumentDarkClass(enabled?: boolean): void {
  const shouldEnable = enabled ?? (auth.theme === 'dark')
  const htmlEl = document.documentElement
  const bodyEl = document.body
  if (shouldEnable) {
    htmlEl.classList.add('admin-dark')
    bodyEl.classList.add('admin-dark')
  } else {
    htmlEl.classList.remove('admin-dark')
    bodyEl.classList.remove('admin-dark')
  }
}


// Icon mapping for menu items
const iconMap: Record<string, any> = {
  DataAnalysis,
  Setting,
  User,
  UserFilled,
  Key,
  OfficeBuilding,
  Menu: MenuIcon,
  Tools,
  Collection,
  Document,
  Folder,
  House
}


// Compute admin route index for menu items
function computeAdminIndex(route?: string, webRouteUrl?: string, fallbackId?: string): string {
  const addPrefix = (path: string): string => {
    return path.startsWith(adminBasePath + '/') ? path : `${adminBasePath}/${path.replace(/^\/+/, '')}`
  }

  const routePath = (route || '').trim()
  const webUrl = (webRouteUrl || '').trim()
  
  // 检查是否是模块路由（webRouteUrl 包含 /modules/）
  const isModuleRoute = webUrl.includes('/modules/')
  
  // 如果是模块路由，直接使用 route 字段
  if (isModuleRoute && routePath) {
    return addPrefix(routePath)
  }

  const isComponentLike = /\.vue$/i.test(routePath) || routePath.includes('/views/')
  const isSystemRoute = /^\/?system\//i.test(routePath) || routePath.startsWith(`${adminBasePath}/system/`)

  if (routePath && !isComponentLike && isSystemRoute) {
    return addPrefix(routePath)
  }
  
  // 如果 route 不是组件路径且不为空，直接使用
  if (routePath && !isComponentLike) {
    return addPrefix(routePath)
  }

  // Convert component path to system route (仅用于系统路由)
  if (routePath && /\.vue$/i.test(routePath) && !isModuleRoute) {
    const fileName = routePath.split('/').pop()!
    const baseName = fileName.replace(/\.vue$/i, '')
    const kebabCase = baseName.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase()
    return `${adminBasePath}/system/${kebabCase}`
  }

  if (webUrl && typeof webUrl === 'string' && !isModuleRoute) {
    if (/\.vue$/i.test(webUrl)) {
      const fileName = webUrl.split('/').pop()!
      const baseName = fileName.replace(/\.vue$/i, '')
      const kebabCase = baseName.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase()
      return `${adminBasePath}/system/${kebabCase}`
    }
    if (webUrl.startsWith('system/')) {
      return `${adminBasePath}/${webUrl}`
    }
  }

  return fallbackId || adminBasePath
}

// Get menu icon component
function getMenuIcon(iconName?: string): any {
  if (!iconName) return House
  return iconMap[iconName] || House
}

// UI toggle functions
function toggleSidebar(): void {
  menuStore.toggleCollapse()
}

// Mobile sidebar toggle functions
function toggleMobileSidebar(): void {
  isMobileSidebarOpen.value = !isMobileSidebarOpen.value
}

function closeMobileSidebar(): void {
  isMobileSidebarOpen.value = false
}

async function toggleTheme(): Promise<void> {
  const target = auth.theme === 'dark' ? 'light' : 'dark'
  // 先更新 auth store（会自动保存到 localStorage 的 'auth-theme' 键）
  auth.theme = target
  // 再调用 theme-manager 加载对应的 CSS 文件和更新 DOM class
  await switchTheme(target as 'light' | 'dark')
  // syncDocumentDarkClass 会由 watch 自动触发，无需手动调用
}

function toggleFullscreen(): void {
  if (!document.fullscreenElement) {
    document.documentElement.requestFullscreen()
  } else {
    document.exitFullscreen()
  }
}

// Navigation functions
function goToProfile(): void {
  router.push(`${adminBasePath}/user/profile`)
}

function goToNotifications(): void {
  router.push(`${adminBasePath}/user/notifications`)
}

function goToLogs(): void {
  router.push(`${adminBasePath}/user/logs`)
}

function goToSettings(): void {
  router.push(`${adminBasePath}/system/settings`)
}

// Clear cache and reload menus
async function handleClearCache(): Promise<void> {
  try {
    await ElMessageBox.confirm(
      '确定要清空缓存吗？清空后将重新加载菜单和权限数据。',
      '清空缓存',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning',
      }
    )
  } catch {
    return
  }

  const loading = ElLoading.service({
    lock: true,
    text: '正在清空缓存并重新加载...',
    background: 'rgba(0, 0, 0, 0.7)',
  })

  try {
    menuStore.clearCache()
    languageStore.clearCache()

    const reloadMenus = menuStore.loadMenus(true)
    const timeout = new Promise<never>((_, reject) => {
      window.setTimeout(() => reject(new Error('菜单加载超时，请稍后重试')), 30000)
    })
    await Promise.race([reloadMenus, timeout])

    await injectAdminRoutes(router)
    await loadPluginLayoutExtensions()

    ElMessage.success('缓存已清空并重新加载')
  } catch (error: any) {
    const msg = String(error?.message || '')
    if (!msg.includes('登录') && !msg.includes('认证')) {
      ElMessage.error(msg || '重新加载失败')
    }
  } finally {
    loading.close()
  }
}

function onLogout(): void {
  // Disconnect notification SignalR before logout
  notificationStore.disconnect()

  auth.logout()
  location.href = loginFullPath
}

// Tab management functions
function onTabClick(tab: TabsPaneContext): void {
  const targetPath = tabsStore.switchTab(tab.paneName as string)
  if (targetPath && targetPath !== route.path) {
    router.push(targetPath)
  }
}

function onTabRemove(tabId: string): void {
  const targetPath = tabsStore.removeTab(tabId)
  if (targetPath && targetPath !== route.path) {
    router.push(targetPath)
  }
}

function refreshCurrentTab(): void {
  const targetPath = tabsStore.refreshTab()
  if (targetPath) {
    // Force component refresh with timestamp
    router.replace({ path: targetPath, query: { t: Date.now() } })
  }
}

function closeOtherTabs(): void {
  tabsStore.closeOtherTabs(tabsStore.activeTab)
}

function closeAllTabs(): void {
  const targetPath = tabsStore.closeAllTabs()
  if (targetPath !== route.path) {
    router.push(targetPath)
  }
}

// Watch route changes and add tabs automatically
watch(route, (newRoute: RouteLocationNormalized) => {
  if (newRoute.path.startsWith(adminBasePath)) {
    const menuItem = menuStore.findMenuByRoute(newRoute.path)
    const matched = newRoute.matched || []
    const metaTitle = matched.length > 0
      ? (matched[matched.length - 1].meta?.title as string)
      : (newRoute.meta?.title as string)

    const menuTitle = menuItem?.name || metaTitle || fallbackTitleByPath(newRoute.path) || tabsStore.getTabTitle(newRoute.path)

    // Add tab with resolved title
    tabsStore.addTab(newRoute, metaTitle || menuTitle)
  }
}, { immediate: true })

// Resolve menu metadata by path for tab titles
function resolveMenuMetaByPath(path: string): { title?: string; icon?: string } | undefined {
  const menuItem = menuStore.findMenuByRoute(path)
  if (menuItem) {
    return { title: menuItem.name, icon: menuItem.icon }
  }
  return undefined
}

// Fallback title resolution from URL path
function fallbackTitleByPath(path: string): string | undefined {
  const segment = path.split('/').filter(Boolean).pop() || ''
  if (!segment) return undefined

  const flatMenus = menuStore.flatMenus || []
  const matchedMenu = flatMenus.find((menu: any) => {
    const routeLeaf = String(menu.route || '').split('/').filter(Boolean).pop()
    const webLeaf = String(menu.webRouteUrl || '')
      .split('/').filter(Boolean).pop()?.replace(/\.vue$/i, '').toLowerCase()
    return routeLeaf === segment || webLeaf === segment
  })

  return matchedMenu?.name
}

// Watch menu loading state and refresh tab titles
watch(() => menuStore.loaded, (loaded: boolean) => {
  if (loaded) {
    tabsStore.refreshAllTitles(resolveMenuMetaByPath)
  }
}, { immediate: true })

// Watch auth.theme changes and sync to document for teleported elements (popovers, dropdowns)
watch(() => auth.theme, (newTheme) => {
  syncDocumentDarkClass(newTheme === 'dark')
}, { immediate: true })

// Watch route changes and close mobile sidebar
watch(route, () => {
  if (isMobileSidebarOpen.value) {
    closeMobileSidebar()
  }
})

// Check if viewport is mobile (≤768px)
function checkMobileViewport(): void {
  isMobileViewport.value = window.innerWidth <= 768
}

// Component lifecycle hooks
onMounted(async () => {
  // 初始化标签栏鼠标拖拽滚动
  const tabsEl = tabsScrollRef.value
  if (tabsEl) {
    tabsEl.addEventListener('mousedown', onTabsDragStart)
    document.addEventListener('mousemove', onTabsDragMove)
    document.addEventListener('mouseup', onTabsDragEnd)
  }
  // 初始化后台主题系统（只在后台布局加载时执行）
  // 这样可以确保前台页面不会加载后台主题 CSS 文件
  const { initTheme } = await import('../styles/admin/theme-manager')
  const loadedTheme = await initTheme()
  // 同步 auth store 的主题状态（防止 localStorage 和 store 不一致）
  if (loadedTheme && loadedTheme !== auth.theme) {
    auth.theme = loadedTheme
  }

  // Initial sync of dark class (already handled by watch above with immediate: true)
  // syncDocumentDarkClass() - removed duplicate call

  // Check initial viewport size
  checkMobileViewport()

  // Add resize listener to update viewport state
  window.addEventListener('resize', checkMobileViewport)
  window.addEventListener('ginkgo:plugins:reloaded', handlePluginsReloaded)

  // Load menu data if not already loaded
  if (!menuStore.loaded) {
    await menuStore.loadMenus()
  }

  await loadPluginLayoutExtensions()

  // Setup notification SignalR connection for authenticated users
  if (auth.isAuthenticated && auth.token) {
    // 从系统配置加载通知音频设置
    if (system.notificationAudioUrl) {
      notificationStore.setAudioUrl(system.notificationAudioUrl)
    }
    notificationStore.setAudioEnabled(system.notificationAudioEnabled)
    
    await notificationStore.initConnection(auth.token)
  }

  // 变体E：charCode 数字串解码，写入 meta[name="framework"]
  try {
    const _decode = (s: string) => s.split(',').map(n => String.fromCharCode(+n)).join('')
    const _b  = _decode('71,105,110,107,103,111,65,100,109,105,110')  // GinkgoAdmin
    const _u  = _decode('104,116,116,112,115,58,47,47,119,119,119,46,103,105,110,107,103,111,97,100,109,105,110,46,99,111,109') // https://www.ginkgoadmin.com
    const _cr = _decode('67,111,112,121,114,105,103,104,116')           // Copyright
    let m = document.querySelector<HTMLMetaElement>('meta[name="framework"]')
    if (!m) { m = document.createElement('meta'); m.name = 'framework'; document.head.appendChild(m) }
    m.content = `${_b} | ${_u} | ${_cr} \u00a9 2026 ${_b}`
  } catch { /* 静默 */ }
})

// Cleanup on unmount
onUnmounted(() => {
  // 清理标签栏鼠标拖拽滚动事件
  const tabsEl = tabsScrollRef.value
  if (tabsEl) {
    tabsEl.removeEventListener('mousedown', onTabsDragStart)
  }
  document.removeEventListener('mousemove', onTabsDragMove)
  document.removeEventListener('mouseup', onTabsDragEnd)

  // Disconnect notification SignalR
  notificationStore.disconnect()
  
  // Remove resize listener
  window.removeEventListener('resize', checkMobileViewport)
  window.removeEventListener('ginkgo:plugins:reloaded', handlePluginsReloaded)

  // 清理后台主题样式和类名，避免影响前台页面
  // 移除 admin-dark 类
  const htmlEl = document.documentElement
  const bodyEl = document.body
  htmlEl.classList.remove('admin-dark')
  bodyEl.classList.remove('admin-dark')
  htmlEl.removeAttribute('data-admin-theme')
  bodyEl.removeAttribute('data-admin-theme')

  // 移除后台主题 CSS 文件
  document.querySelectorAll('link[data-admin-theme-link]').forEach((el) => {
    el.parentElement?.removeChild(el)
  })
})
</script>
