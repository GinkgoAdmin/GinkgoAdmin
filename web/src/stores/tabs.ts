import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { RouteLocationNormalized } from 'vue-router'
import { adminBasePath } from '../config/admin'

export interface TabItem {
  id: string
  title: string
  path: string
  name?: string
  icon?: string
  closable: boolean
  meta?: any
}

export const useTabsStore = defineStore('tabs', () => {
  const tabs = ref<TabItem[]>([])
  const activeTab = ref('')
  const homePath = `${adminBasePath}/dashboard`

  function normalizeTabPath(path: string): string {
    if (path === adminBasePath || path === `${adminBasePath}/`) return homePath
    return path
  }

  // 默认首页标签
  const homeTab: TabItem = {
    id: 'home',
    title: '首页',
    path: homePath,
    name: 'dashboard',
    icon: 'House',
    closable: false
  }

  // 初始化时添加首页标签
  if (tabs.value.length === 0) {
    tabs.value.push(homeTab)
    activeTab.value = homeTab.id
  }

  const currentTab = computed(() => {
    return tabs.value.find(tab => tab.id === activeTab.value) || homeTab
  })

  // 添加标签页
  function addTab(route: RouteLocationNormalized, menuTitle?: string) {
    const path = normalizeTabPath(route.path)
    const id = path.replace(/\//g, '-').replace(/^-/, '') || 'home'
    
    // 检查是否已存在
    const existingTab = tabs.value.find(tab => tab.path === path)
    if (existingTab) {
      // 如果传入了中文标题且不同，则更新已存在标签的标题/图标
      const resolvedTitle = menuTitle || (route.meta?.title as string) || existingTab.title
      if (resolvedTitle && existingTab.title !== resolvedTitle) {
        existingTab.title = resolvedTitle
      }
      const resolvedIcon = (route.meta?.icon as string | undefined)
      if (resolvedIcon && existingTab.icon !== resolvedIcon) {
        existingTab.icon = resolvedIcon
      }
      activeTab.value = existingTab.id
      return
    }

    // 创建新标签页
    const newTab: TabItem = {
      id,
      title: menuTitle || route.meta?.title || route.name as string || '未命名页面',
      path,
      name: route.name as string,
      icon: route.meta?.icon,
      closable: path !== homePath, // 首页不可关闭
      meta: route.meta
    }

    tabs.value.push(newTab)
    activeTab.value = newTab.id
  }

  // 移除标签页
  function removeTab(tabId: string) {
    const index = tabs.value.findIndex(tab => tab.id === tabId)
    if (index === -1 || !tabs.value[index].closable) return

    const removedTab = tabs.value[index]
    tabs.value.splice(index, 1)

    // 如果移除的是当前活动标签，切换到相邻标签
    if (activeTab.value === tabId) {
      if (tabs.value.length > 0) {
        // 优先选择右侧标签，否则选择左侧标签
        const targetIndex = index < tabs.value.length ? index : tabs.value.length - 1
        activeTab.value = tabs.value[targetIndex].id
        return tabs.value[targetIndex].path
      } else {
        // 如果没有标签了，回到首页
        addTab({ path: homePath, name: 'dashboard', meta: {} } as RouteLocationNormalized)
        return homePath
      }
    }
  }

  // 切换到指定标签
  function switchTab(tabId: string) {
    const tab = tabs.value.find(t => t.id === tabId)
    if (tab) {
      activeTab.value = tabId
      return tab.path
    }
  }

  // 关闭其他标签页
  function closeOtherTabs(keepTabId: string) {
    tabs.value = tabs.value.filter(tab => !tab.closable || tab.id === keepTabId)
    if (!tabs.value.find(tab => tab.id === activeTab.value)) {
      activeTab.value = keepTabId
    }
  }

  // 关闭所有标签页（除了首页）
  function closeAllTabs() {
    tabs.value = tabs.value.filter(tab => !tab.closable)
    activeTab.value = homeTab.id
    return homePath
  }

  // 刷新标签页
  function refreshTab(tabId?: string) {
    const targetTabId = tabId || activeTab.value
    const tab = tabs.value.find(t => t.id === targetTabId)
    return tab?.path
  }

  // 根据路径设置标签标题和图标
  function setTabTitleByPath(path: string, title?: string, icon?: string) {
    const tab = tabs.value.find(t => t.path === path)
    if (!tab) return
    if (title && tab.title !== title) tab.title = title
    if (icon && tab.icon !== icon) tab.icon = icon
  }

  // 批量刷新所有标签标题
  function refreshAllTitles(resolve: (path: string) => { title?: string; icon?: string } | undefined) {
    if (!resolve) return
    tabs.value.forEach(t => {
      // 首页标签标题由初始化确定，不从菜单数据覆盖
      if (normalizeTabPath(t.path) === homePath) return
      const r = resolve(t.path)
      if (r?.title && r.title !== t.title) t.title = r.title
      if (r?.icon && r.icon !== t.icon) t.icon = r.icon
    })
  }

  // 根据路径获取标签标题
  function getTabTitle(path: string, menuTitle?: string): string {
    if (normalizeTabPath(path) === homePath) return '首页'
    if (menuTitle) return menuTitle
    
    // 从路径推断标题
    const segments = path.replace(adminBasePath, '').split('/').filter(Boolean)
    if (segments.length > 0) {
      const lastSegment = segments[segments.length - 1]
      return lastSegment.split('-').map(word => 
        word.charAt(0).toUpperCase() + word.slice(1)
      ).join(' ')
    }
    
    return '未命名页面'
  }

  return {
    tabs,
    activeTab,
    currentTab,
    addTab,
    removeTab,
    switchTab,
    closeOtherTabs,
    closeAllTabs,
    refreshTab,
    getTabTitle,
    setTabTitleByPath,
    refreshAllTitles
  }
})
