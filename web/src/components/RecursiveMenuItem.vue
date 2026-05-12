<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <!-- 自身被禁用则不渲染 -->
  <template v-if="menu.enabled !== false">
  <!-- 有子菜单的目录 -->
  <el-sub-menu
    v-if="hasVisibleChildren && depth < maxDepth"
    :index="menu.id"
    :class="['menu-submenu', `menu-depth-${depth}`]"
  >
    <template #title>
      <i v-if="menu.icon" :class="'bi bi-' + menu.icon"></i>
      <span>{{ menu.name }}</span>
    </template>

    <!-- 递归渲染子菜单 -->
    <template v-for="child in visibleChildren" :key="child.id">
      <recursive-menu-item
        :menu="child"
        :compute-admin-index="computeAdminIndex"
        :depth="depth + 1"
        :max-depth="maxDepth"
      />
    </template>
  </el-sub-menu>

  <!-- 叶子菜单项（无子菜单或已达最大深度） -->
  <el-menu-item
    v-else-if="isLeafMenu"
    :index="computeAdminIndex(menu.route, menu.webRouteUrl, menu.id)"
    :class="['menu-item', `menu-depth-${depth}`]"
  >
    <i v-if="menu.icon" :class="'bi bi-' + menu.icon"></i>
    <template #title>
      <span>{{ menu.name }}</span>
    </template>
  </el-menu-item>
  </template>
</template>

<script setup lang="ts">
import { computed } from 'vue'

// 定义组件名称，用于递归引用
defineOptions({
  name: 'RecursiveMenuItem'
})

interface MenuItem {
  id: string
  name: string
  icon?: string
  route?: string
  webRouteUrl?: string
  enabled?: boolean
  children?: MenuItem[]
}

const props = defineProps<{
  menu: MenuItem
  computeAdminIndex: (route?: string, webRouteUrl?: string, fallbackId?: string) => string
  depth: number
  maxDepth: number
}>()

// 过滤出可见的子菜单（enabled 为 true）
const visibleChildren = computed(() => {
  if (!props.menu.children || !Array.isArray(props.menu.children)) {
    return []
  }
  return props.menu.children.filter((child: MenuItem) => {
    return child && child.enabled !== false
  })
})

// 是否有可见的子菜单
const hasVisibleChildren = computed(() => {
  return visibleChildren.value.length > 0
})

// 是否是叶子菜单（有路由且启用）
const isLeafMenu = computed(() => {
  return (props.menu.webRouteUrl || props.menu.route) && props.menu.enabled !== false
})
</script>

<style scoped>
/* 多级菜单缩进样式 */
.menu-depth-2 {
  --el-menu-base-level-padding: 40px;
}

.menu-depth-3 {
  --el-menu-base-level-padding: 60px;
}

.menu-depth-4 {
  --el-menu-base-level-padding: 80px;
}
</style>
