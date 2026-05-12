import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { RouteLocationNormalized } from 'vue-router'
import { adminBasePath } from '@/config/admin'
import { useTabsStore } from '../tabs'

describe('后台标签页首页', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('默认首页标签应使用菜单化首页路由，避免刷新后出现两个首页', () => {
    const tabsStore = useTabsStore()
    const homePath = `${adminBasePath}/dashboard`

    expect(tabsStore.tabs).toHaveLength(1)
    expect(tabsStore.tabs[0].path).toBe(homePath)
    expect(tabsStore.tabs[0].closable).toBe(false)

    tabsStore.addTab({
      path: homePath,
      name: 'dashboard',
      meta: { title: '首页', icon: 'house' }
    } as RouteLocationNormalized, '首页')

    expect(tabsStore.tabs).toHaveLength(1)
  })
})
