<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <teleport to="body">
    <div class="notification-container">
      <transition-group name="notification" tag="div">
        <div
          v-for="notification in notifications"
          :key="notification.id"
          class="notification-popup"
          :class="[
            `notification-${notification.type}`,
            { 'notification-important': notification.isImportant }
          ]"
          @click="handleNotificationClick(notification)"
        >
          <div class="notification-icon">
            <el-icon :class="getIconClass(notification.type)">
              <component :is="getIcon(notification.type)" />
            </el-icon>
          </div>
          <div class="notification-content">
            <h4 class="notification-title">{{ notification.title }}</h4>
            <p class="notification-message">{{ notification.content }}</p>
            <span class="notification-time">{{ formatTime(notification.timestamp) }}</span>
          </div>
          <button 
            class="notification-close" 
            @click.stop="removeNotification(notification.id!)"
            aria-label="关闭通知"
          >
            <el-icon><Close /></el-icon>
          </button>
        </div>
      </transition-group>
    </div>
  </teleport>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { ElMessage } from 'element-plus'
import { 
  InfoFilled, 
  SuccessFilled, 
  Warning, 
  CircleCloseFilled, 
  Close 
} from '@element-plus/icons-vue'
import { getNotificationService, type NotificationMessage } from '../services/websocket'
import { useWebAuthStore } from '../stores/webAuth'
import { useAuthStore } from '../stores/auth'

interface PopupNotification extends NotificationMessage {
  id: string
}

const notifications = ref<PopupNotification[]>([])
const notificationService = getNotificationService()
const webAuth = useWebAuthStore()
const auth = useAuthStore()

// 生成唯一ID
function generateId(): string {
  return Date.now().toString(36) + Math.random().toString(36).substr(2)
}

// 添加通知
function addNotification(message: NotificationMessage) {
  const notification: PopupNotification = {
    ...message,
    id: message.id || generateId()
  }
  
  notifications.value.unshift(notification)
  
  // 自动移除（根据重要性设置不同的显示时间）
  const duration = notification.isImportant ? 10000 : 5000
  setTimeout(() => {
    removeNotification(notification.id)
  }, duration)
  
  // 限制最大显示数量
  if (notifications.value.length > 5) {
    notifications.value.splice(5)
  }
}

// 移除通知
function removeNotification(id: string) {
  const index = notifications.value.findIndex(n => n.id === id)
  if (index > -1) {
    notifications.value.splice(index, 1)
  }
}

// 点击通知
function handleNotificationClick(notification: PopupNotification) {
  // 可以在这里添加点击处理逻辑，比如跳转到通知详情页面
  removeNotification(notification.id)
}

// 获取图标
function getIcon(type: string) {
  switch (type) {
    case 'success': return SuccessFilled
    case 'warning': return Warning
    case 'error': return CircleCloseFilled
    default: return InfoFilled
  }
}

// 获取图标样式类
function getIconClass(type: string) {
  switch (type) {
    case 'success': return 'icon-success'
    case 'warning': return 'icon-warning'
    case 'error': return 'icon-error'
    default: return 'icon-info'
  }
}

// 格式化时间
function formatTime(timestamp: string) {
  const date = new Date(timestamp)
  return date.toLocaleTimeString('zh-CN', { 
    hour: '2-digit', 
    minute: '2-digit' 
  })
}

// 通知监听器
function onNotificationReceived(message: NotificationMessage) {
  // 只在用户已登录时显示通知
  if (webAuth.isAuthenticated || auth.isAuthenticated) {
    addNotification(message)
  }
}

onMounted(() => {
  // 添加通知监听器
  notificationService.addListener(onNotificationReceived)
  
  // 启动 WebSocket 连接（如果用户已登录）
  if (webAuth.isAuthenticated || auth.isAuthenticated) {
    notificationService.start()
  }
})

onUnmounted(() => {
  // 移除监听器
  notificationService.removeListener(onNotificationReceived)
})
</script>

<style scoped>
.notification-container {
  position: fixed;
  top: 1rem;
  right: 1rem;
  z-index: 9999;
  pointer-events: none;
}

.notification-popup {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
  min-width: 320px;
  max-width: 400px;
  padding: 1rem;
  margin-bottom: 0.75rem;
  background: white;
  border-radius: 12px;
  box-shadow: 0 10px 25px rgba(0, 0, 0, 0.15);
  border-left: 4px solid #3b82f6;
  cursor: pointer;
  pointer-events: all;
  transition: all 0.3s ease;
  position: relative;
}

.notification-popup:hover {
  transform: translateX(-4px);
  box-shadow: 0 15px 35px rgba(0, 0, 0, 0.2);
}

.notification-info {
  border-left-color: #3b82f6;
}

.notification-success {
  border-left-color: #22c55e;
}

.notification-warning {
  border-left-color: #f59e0b;
}

.notification-error {
  border-left-color: #ef4444;
}

.notification-important {
  background: linear-gradient(135deg, #fef3c7 0%, #fde68a 100%);
  border-left-color: #f59e0b;
  animation: pulse 2s infinite;
}

.notification-icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.25rem;
  flex-shrink: 0;
}

.icon-info {
  background: #eff6ff;
  color: #3b82f6;
}

.icon-success {
  background: #f0fdf4;
  color: #22c55e;
}

.icon-warning {
  background: #fffbeb;
  color: #f59e0b;
}

.icon-error {
  background: #fef2f2;
  color: #ef4444;
}

.notification-content {
  flex: 1;
  min-width: 0;
}

.notification-title {
  font-size: 1rem;
  font-weight: 600;
  color: #1f2937;
  margin: 0 0 0.25rem 0;
  line-height: 1.4;
}

.notification-message {
  color: #6b7280;
  margin: 0 0 0.5rem 0;
  font-size: 0.875rem;
  line-height: 1.4;
  word-wrap: break-word;
}

.notification-time {
  font-size: 0.75rem;
  color: #9ca3af;
}

.notification-close {
  position: absolute;
  top: 0.5rem;
  right: 0.5rem;
  width: 1.5rem;
  height: 1.5rem;
  background: none;
  border: none;
  color: #9ca3af;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: all 0.2s ease;
}

.notification-close:hover {
  background: #f3f4f6;
  color: #6b7280;
}

/* 动画效果 */
.notification-enter-active {
  transition: all 0.4s cubic-bezier(0.175, 0.885, 0.32, 1.275);
}

.notification-leave-active {
  transition: all 0.3s ease-in;
}

.notification-enter-from {
  opacity: 0;
  transform: translateX(100%) scale(0.8);
}

.notification-leave-to {
  opacity: 0;
  transform: translateX(100%) scale(0.8);
}

.notification-move {
  transition: transform 0.3s ease;
}

/* 重要通知脉冲动画 */
@keyframes pulse {
  0%, 100% {
    box-shadow: 0 10px 25px rgba(0, 0, 0, 0.15), 0 0 0 0 rgba(245, 158, 11, 0.4);
  }
  50% {
    box-shadow: 0 15px 35px rgba(0, 0, 0, 0.2), 0 0 0 8px rgba(245, 158, 11, 0.1);
  }
}

/* 响应式设计 */
@media (max-width: 640px) {
  .notification-container {
    top: 0.5rem;
    right: 0.5rem;
    left: 0.5rem;
  }
  
  .notification-popup {
    min-width: auto;
    max-width: none;
    margin-bottom: 0.5rem;
  }
}
</style>





