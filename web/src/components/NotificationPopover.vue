<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <el-popover
    ref="popoverRef"
    placement="bottom-end"
    :width="360"
    trigger="click"
    popper-class="notification-popover"
    @show="onShow"
  >
    <template #reference>
      <el-badge :value="displayBadge" :hidden="!hasUnread" class="notification-badge">
        <el-button :icon="Bell" circle size="small" class="notification-btn" />
      </el-badge>
    </template>

    <div class="notification-panel">
      <!-- 头部 -->
      <div class="notification-header">
        <span class="title">通知</span>
        <div class="actions">
          <el-button
            v-if="hasUnread"
            type="primary"
            link
            size="small"
            @click="handleMarkAllRead"
          >
            全部已读
          </el-button>
        </div>
      </div>

      <!-- 通知列表 -->
      <div class="notification-list" v-loading="isLoading">
        <template v-if="notifications.length > 0">
          <div
            v-for="item in displayNotifications"
            :key="item.id"
            :class="['notification-item', { unread: !item.isRead }]"
            @click="handleItemClick(item)"
          >
            <div class="item-icon">
              <el-icon :class="getTypeClass(item.type)">
                <component :is="getTypeIcon(item.type)" />
              </el-icon>
            </div>
            <div class="item-content">
              <div class="item-title">{{ item.title }}</div>
              <div class="item-time">{{ formatTime(item.createdAt) }}</div>
            </div>
            <div v-if="!item.isRead" class="item-dot"></div>
          </div>
        </template>
        <el-empty v-else description="暂无通知" :image-size="80" />
      </div>

      <!-- 底部 -->
      <div class="notification-footer">
        <el-button type="primary" link @click="handleViewAll">
          查看全部通知
        </el-button>
      </div>
    </div>
  </el-popover>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import { Bell, InfoFilled, WarningFilled, CircleCheckFilled, CircleCloseFilled } from '@element-plus/icons-vue'
import { useNotificationStore } from '../stores/notification'
import { adminBasePath } from '../config/admin'
import type { NotificationItem } from '../api/user'

const router = useRouter()
const notificationStore = useNotificationStore()
const popoverRef = ref()

// 计算属性
const notifications = computed(() => notificationStore.notifications)
const isLoading = computed(() => notificationStore.isLoading)
const hasUnread = computed(() => notificationStore.hasUnread)
const displayBadge = computed(() => notificationStore.displayBadge)

// 只显示最近10条
const displayNotifications = computed(() => notifications.value.slice(0, 10))

// 显示时加载数据
async function onShow(): Promise<void> {
  await notificationStore.loadNotifications()
}

// 点击通知项
async function handleItemClick(item: NotificationItem): Promise<void> {
  // 标记为已读
  if (!item.isRead) {
    await notificationStore.markAsRead(item.id)
  }
  
  // 关闭弹窗
  popoverRef.value?.hide()
  
  // 跳转到通知详情
  router.push(`${adminBasePath}/user/notifications?id=${item.id}`)
}

// 全部已读
async function handleMarkAllRead(): Promise<void> {
  await notificationStore.markAllAsRead()
}

// 查看全部
function handleViewAll(): void {
  popoverRef.value?.hide()
  router.push(`${adminBasePath}/user/notifications`)
}

// 获取类型图标
function getTypeIcon(type?: string) {
  switch (type) {
    case 'success': return CircleCheckFilled
    case 'warning': return WarningFilled
    case 'error': return CircleCloseFilled
    default: return InfoFilled
  }
}

// 获取类型样式类
function getTypeClass(type?: string): string {
  switch (type) {
    case 'success': return 'type-success'
    case 'warning': return 'type-warning'
    case 'error': return 'type-error'
    default: return 'type-info'
  }
}

// 格式化时间
function formatTime(dateStr: string): string {
  const date = new Date(dateStr)
  const now = new Date()
  const diff = now.getTime() - date.getTime()
  
  const minutes = Math.floor(diff / 60000)
  const hours = Math.floor(diff / 3600000)
  const days = Math.floor(diff / 86400000)
  
  if (minutes < 1) return '刚刚'
  if (minutes < 60) return `${minutes}分钟前`
  if (hours < 24) return `${hours}小时前`
  if (days < 7) return `${days}天前`
  
  return date.toLocaleDateString('zh-CN', { month: 'short', day: 'numeric' })
}
</script>

<style scoped>
.notification-panel {
  margin: -12px;
}

.notification-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.notification-header .title {
  font-weight: 600;
  font-size: 16px;
}

.notification-list {
  max-height: 400px;
  overflow-y: auto;
}

.notification-item {
  display: flex;
  align-items: flex-start;
  padding: 12px 16px;
  cursor: pointer;
  transition: background-color 0.2s;
  position: relative;
}

.notification-item:hover {
  background-color: var(--el-fill-color-light);
}

.notification-item.unread {
  background-color: var(--el-color-primary-light-9);
}

.notification-item.unread:hover {
  background-color: var(--el-color-primary-light-8);
}

.item-icon {
  flex-shrink: 0;
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background-color: var(--el-fill-color);
  margin-right: 12px;
}

.item-icon .el-icon {
  font-size: 16px;
}

.item-icon .type-info { color: var(--el-color-primary); }
.item-icon .type-success { color: var(--el-color-success); }
.item-icon .type-warning { color: var(--el-color-warning); }
.item-icon .type-error { color: var(--el-color-danger); }

.item-content {
  flex: 1;
  min-width: 0;
}

.item-title {
  font-size: 14px;
  color: var(--el-text-color-primary);
  line-height: 1.4;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.item-time {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  margin-top: 4px;
}

.item-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background-color: var(--el-color-primary);
  flex-shrink: 0;
  margin-left: 8px;
  margin-top: 6px;
}

.notification-footer {
  padding: 12px 16px;
  text-align: center;
  border-top: 1px solid var(--el-border-color-lighter);
}

/* 暗色主题适配 */
.admin-dark .notification-item.unread {
  background-color: rgba(var(--el-color-primary-rgb), 0.1);
}

.admin-dark .notification-item.unread:hover {
  background-color: rgba(var(--el-color-primary-rgb), 0.15);
}
</style>
