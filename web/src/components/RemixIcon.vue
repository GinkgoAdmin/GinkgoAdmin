<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <i :class="iconClass" :style="iconStyle"></i>
</template>

<script setup lang="ts">
/**
 * Remix Icon 图标组件
 * 
 * 使用方式：
 * 1. 组件方式：<RemixIcon name="home-line" size="24" color="#409EFF" />
 * 2. 直接使用 class：<i class="ri-home-line"></i>
 * 
 * 图标名称参考：https://remixicon.com/
 * 
 * 常用图标示例：
 * - 首页：ri-home-line / ri-home-fill
 * - 用户：ri-user-line / ri-user-fill
 * - 设置：ri-settings-line / ri-settings-fill
 * - 搜索：ri-search-line / ri-search-fill
 * - 添加：ri-add-line / ri-add-circle-line
 * - 删除：ri-delete-bin-line / ri-close-line
 * - 编辑：ri-edit-line / ri-pencil-line
 * - 保存：ri-save-line / ri-check-line
 * - 刷新：ri-refresh-line / ri-loop-left-line
 * - 导出：ri-download-line / ri-export-line
 * - 导入：ri-upload-line / ri-import-line
 * - 文件：ri-file-line / ri-folder-line
 * - 图表：ri-bar-chart-line / ri-pie-chart-line / ri-line-chart-line
 * - 日历：ri-calendar-line / ri-time-line
 * - 通知：ri-notification-line / ri-bell-line
 * - 消息：ri-message-line / ri-chat-line
 * - 菜单：ri-menu-line / ri-more-line
 * - 箭头：ri-arrow-left-line / ri-arrow-right-line / ri-arrow-up-line / ri-arrow-down-line
 */

import { computed } from 'vue'

const props = withDefaults(defineProps<{
  /** 图标名称，不需要 ri- 前缀，如 "home-line" */
  name: string
  /** 图标大小，支持数字(px)或字符串 */
  size?: number | string
  /** 图标颜色 */
  color?: string
  /** 是否旋转动画 */
  spin?: boolean
}>(), {
  size: 16,
  spin: false
})

const iconClass = computed(() => {
  const classes = [`ri-${props.name}`]
  if (props.spin) {
    classes.push('ri-spin')
  }
  return classes.join(' ')
})

const iconStyle = computed(() => {
  const style: Record<string, string> = {}
  
  if (props.size) {
    const sizeValue = typeof props.size === 'number' ? `${props.size}px` : props.size
    style.fontSize = sizeValue
  }
  
  if (props.color) {
    style.color = props.color
  }
  
  return style
})
</script>

<style scoped>
/* 旋转动画 */
.ri-spin {
  animation: ri-spin 1s linear infinite;
}

@keyframes ri-spin {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}
</style>
