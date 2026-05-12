<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="page-container">
    <div class="page-header">
      <div class="page-title">
        <h1>我的通知</h1>
        <p>查看系统通知和消息</p>
      </div>
      <div class="page-actions">
        <el-button @click="markAllAsRead" :disabled="unreadCount === 0">
          全部标记为已读
        </el-button>
      </div>
    </div>

    <!-- 统计信息 -->
    <div class="stats-section">
      <el-row :gutter="16">
        <el-col :span="8">
          <div class="stat-card">
            <div class="stat-icon total">
              <el-icon><Bell /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ totalCount }}</div>
              <div class="stat-label">总通知</div>
            </div>
          </div>
        </el-col>
        <el-col :span="8">
          <div class="stat-card">
            <div class="stat-icon unread">
              <el-icon><ChatLineRound /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ unreadCount }}</div>
              <div class="stat-label">未读</div>
            </div>
          </div>
        </el-col>
        <el-col :span="8">
          <div class="stat-card">
            <div class="stat-icon read">
              <el-icon><CircleCheck /></el-icon>
            </div>
            <div class="stat-content">
              <div class="stat-value">{{ readCount }}</div>
              <div class="stat-label">已读</div>
            </div>
          </div>
        </el-col>
      </el-row>
    </div>

    <!-- 通知列表（使用通用 DataTable 组件） -->
    <DataTable
      :data="tableData"
      :loading="loading"
      :columns="columns"
      :pagination="dtPagination"
      :search-config="searchConfig"
      :compact-mode="true"
      :default-expand-search="false"
      cache-key="my-notifications"
      @search="onDtSearch"
      @page-change="onPageChange"
      @size-change="onSizeChange"
    >
      <template #header-actions>
        <el-button @click="markAllAsRead" :disabled="unreadCount === 0">全部标记为已读</el-button>
      </template>

      <template #column-type="{ row }">
        <el-tag :type="getNotificationTagType(row.type)" size="small">{{ getNotificationTypeText(row.type) }}</el-tag>
      </template>
      <template #column-title="{ row }">
        <el-link type="primary" underline="never" @click="openDetail(row)">{{ row.title }}</el-link>
      </template>
      <template #column-deliveryRole="{ row }">
        <el-tag :type="row.deliveryRole === 'primary' ? undefined : 'info'" size="small">
          {{ row.deliveryRole === 'primary' ? '主送' : '知会' }}
        </el-tag>
      </template>
      <template #column-content="{ row }">
        <span>{{ row.summary || row.content }}</span>
      </template>
      <template #column-createdAt="{ row }">
        {{ formatTime(row.createdAt) }}
      </template>
      <template #column-isRead="{ row }">
        <el-tag :type="row.isRead ? 'success' : 'warning'" size="small">{{ row.isRead ? '已读' : '未读' }}</el-tag>
      </template>
      <template #actions="{ row }">
        <el-button type="primary" link size="small" @click="handleNotificationClick(row)">查看</el-button>
        <el-button v-if="!row.isRead" type="success" link size="small" @click="markAsRead(row)">标记已读</el-button>
      </template>
    </DataTable>

    <!-- 通知详情对话框 -->
    <el-dialog v-model="showDetailDialog" :title="selectedNotification?.title" width="600px">
      <div v-if="selectedNotification" class="notification-detail">
        <div class="detail-meta">
          <el-tag :type="getNotificationTagType(selectedNotification.type)" size="small">
            {{ getNotificationTypeText(selectedNotification.type) }}
          </el-tag>
          <el-tag :type="selectedNotification.deliveryRole === 'primary' ? undefined : 'info'" size="small">
            {{ selectedNotification.deliveryRole === 'primary' ? '主送' : '知会' }}
          </el-tag>
          <span class="detail-time">{{ formatTime(selectedNotification.createdAt) }}</span>
        </div>
        <div class="detail-content">
          {{ selectedNotification.summary || selectedNotification.content }}
        </div>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, reactive, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { 
  Bell, ChatLineRound, CircleCheck, InfoFilled, 
  WarningFilled, SuccessFilled, CircleCloseFilled 
} from '@element-plus/icons-vue'
import { 
  getMessageList, 
  markMessageAsRead,
  markAllMessagesAsRead,
  type MessageListItem
} from '../../../api/message'
// 使用通用 DataTable
import DataTable from '../../../components/DataTable/index.vue'
// 使用通知 store 保持状态同步
import { useNotificationStore } from '../../../stores/notification'

const loading = ref(false)
const notifications = ref<MessageListItem[]>([])
const dtPagination = reactive({ page: 1, pageSize: 20, total: 0 })
const tableData = ref<MessageListItem[]>([])
const totalCount = computed(() => dtPagination.total)
const router = useRouter()
const route = useRoute()
const notificationStore = useNotificationStore()

const columns = [
  { prop: 'title', label: '标题', minWidth: 220 },
  { prop: 'type', label: '类型', width: 120, slot: 'column-type' },
  { prop: 'deliveryRole', label: '送达角色', width: 100, slot: 'column-deliveryRole' },
  { prop: 'content', label: '内容', minWidth: 300, slot: 'column-content' },
  { prop: 'createdAt', label: '时间', width: 160, slot: 'column-createdAt' },
  { prop: 'isRead', label: '状态', width: 100, slot: 'column-isRead' }
]

// 搜索：标题 + 时间范围
const searchConfig = [
  { key: 'title', label: '标题', type: 'input' as const, simple: true, placeholder: '请输入标题关键字' },
  { key: 'deliveryRole', label: '送达角色', type: 'select' as const, simple: true, options: [
    { label: '主送', value: 'primary' },
    { label: '知会', value: 'cc' }
  ], placeholder: '选择送达角色' },
  { key: 'dateRange', label: '时间范围', type: 'daterange' as const, simple: true }
]
const lastFilters = ref<Record<string, any>>({})
const showDetailDialog = ref(false)
const selectedNotification = ref<MessageListItem | null>(null)

// 统计数据
const unreadCount = computed(() => notifications.value.filter(n => !n.isRead).length)
const readCount = computed(() => notifications.value.filter(n => n.isRead).length)

// 加载通知列表（使用新消息 API，服务端分页）
async function loadNotifications() {
  try {
    loading.value = true
    const params: { pageIndex?: number; pageSize?: number; isRead?: boolean; deliveryRole?: string } = {
      pageIndex: dtPagination.page,
      pageSize: dtPagination.pageSize
    }
    const deliveryRole = (lastFilters.value?.deliveryRole || '').toString().trim()
    if (deliveryRole) params.deliveryRole = deliveryRole

    const result = await getMessageList(params)
    notifications.value = result.items
    tableData.value = result.items
    dtPagination.total = result.total
  } catch (error) {
    notifications.value = []
    tableData.value = []
    ElMessage.warning('加载消息列表失败，请检查API连接')
  } finally {
    loading.value = false
  }
}

function onDtSearch(params: any) {
  dtPagination.page = 1
  lastFilters.value = params?.filters || {}
  loadNotifications()
}

function onPageChange(page: number) {
  dtPagination.page = page
  loadNotifications()
}

function onSizeChange(size: number) {
  dtPagination.pageSize = size
  dtPagination.page = 1
  loadNotifications()
}

// 标记单个通知为已读
async function markAsRead(notification: MessageListItem) {
  try {
    await markMessageAsRead(notification.id)
    notification.isRead = true
    // 同步更新 notification store
    await notificationStore.markAsRead(notification.id)
    ElMessage.success('已标记为已读')
  } catch (error) {
    ElMessage.error('操作失败')
  }
}

function openDetail(row: any) {
  router.push({ name: 'user-notifications-simple', query: { id: row.id } })
}

// 标记所有通知为已读
async function markAllAsRead() {
  try {
    await markAllMessagesAsRead()
    notifications.value.forEach(n => { n.isRead = true })
    // 同步更新 notification store
    await notificationStore.markAllAsRead()
    ElMessage.success('已全部标记为已读')
  } catch (error) {
    ElMessage.error('操作失败')
  }
}

// 点击通知
async function handleNotificationClick(notification: MessageListItem) {
  selectedNotification.value = notification
  showDetailDialog.value = true
  
  // 如果是未读通知，自动标记为已读
  if (!notification.isRead) {
    await markAsRead(notification)
  }
}

onMounted(async () => {
  await loadNotifications()
  
  // 检查 URL 参数，如果有 id 则自动打开详情
  const notifyId = route.query.id as string
  if (notifyId) {
    const notification = notifications.value.find(n => n.id === notifyId)
    if (notification) {
      await handleNotificationClick(notification)
    }
  }
  
  // 同步刷新 store 中的未读数
  await notificationStore.loadUnreadCount()
})

// 监听 store 中的通知列表变化，同步更新本地列表
watch(() => notificationStore.notifications, (storeNotifications) => {
  // 当 store 中有新通知时，刷新列表
  if (storeNotifications.length > 0) {
    const storeIds = new Set(storeNotifications.map(n => n.id))
    const localIds = new Set(notifications.value.map(n => n.id))
    
    // 检查是否有新通知
    const hasNew = storeNotifications.some(n => !localIds.has(n.id))
    if (hasNew) {
      loadNotifications()
    }
    
    // 同步已读状态
    for (const storeNotify of storeNotifications) {
      const localNotify = notifications.value.find(n => n.id === storeNotify.id)
      if (localNotify && localNotify.isRead !== storeNotify.isRead) {
        localNotify.isRead = storeNotify.isRead
      }
    }
  }
}, { deep: true })
// 获取通知图标
function getNotificationIcon(type: string) {
  switch (type) {
    case 'info': return InfoFilled
    case 'warning': return WarningFilled
    case 'success': return SuccessFilled
    case 'error': return CircleCloseFilled
    default: return Bell
  }
}

// 获取通知图标样式类
function getNotificationIconClass(type: string) {
  switch (type) {
    case 'info': return 'text-blue-500'
    case 'warning': return 'text-orange-500'
    case 'success': return 'text-green-500'
    case 'error': return 'text-red-500'
    default: return 'text-gray-500'
  }
}

// 获取通知标签类型
function getNotificationTagType(type: string) {
  switch (type) {
    case 'info': return 'info'
    case 'warning': return 'warning'
    case 'success': return 'success'
    case 'error': return 'danger'
    default: return undefined
  }
}

// 获取通知类型文本
function getNotificationTypeText(type: string) {
  switch (type) {
    case 'info': return '信息'
    case 'warning': return '警告'
    case 'success': return '成功'
    case 'error': return '错误'
    default: return '通知'
  }
}

// 格式化时间
function formatTime(dateString: string) {
  const date = new Date(dateString)
  const now = new Date()
  const diff = now.getTime() - date.getTime()
  
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)}分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}小时前`
  if (diff < 604800000) return `${Math.floor(diff / 86400000)}天前`
  
  return date.toLocaleDateString('zh-CN')
}


</script>

<style scoped>
.page-container {
  padding: 0;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 24px;
}

.page-title h1 {
  font-size: 24px;
  font-weight: 600;
  color: #1f2937;
  margin: 0 0 4px 0;
}

.page-title p {
  font-size: 14px;
  color: #6b7280;
  margin: 0;
}

.stats-section {
  margin-bottom: 24px;
}

.stat-card {
  background: white;
  border-radius: 12px;
  padding: 20px;
  border: 1px solid #e5e7eb;
  display: flex;
  align-items: center;
  gap: 16px;
}

.stat-icon {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  font-size: 20px;
}

.stat-icon.total {
  background: linear-gradient(135deg, #3b82f6, #2563eb);
}

.stat-icon.unread {
  background: linear-gradient(135deg, #f59e0b, #d97706);
}

.stat-icon.read {
  background: linear-gradient(135deg, #10b981, #059669);
}

.stat-value {
  font-size: 24px;
  font-weight: 700;
  color: #1f2937;
  line-height: 1.2;
}

.stat-label {
  font-size: 14px;
  color: #6b7280;
}

.notifications-card {
  border-radius: 12px;
  border: 1px solid #e5e7eb;
}

.notifications-list {
  min-height: 400px;
}

.empty-state {
  text-align: center;
  padding: 60px 20px;
  color: #9ca3af;
}

.empty-state p {
  margin: 16px 0 0 0;
  font-size: 16px;
}

.notification-item {
  display: flex;
  align-items: flex-start;
  gap: 16px;
  padding: 16px;
  border-bottom: 1px solid #f3f4f6;
  cursor: pointer;
  transition: all 0.2s ease;
  position: relative;
}

.notification-item:hover {
  background: #f9fafb;
}

.notification-item:last-child {
  border-bottom: none;
}

.notification-item.unread {
  background: #fef3c7;
}

.notification-item.unread:hover {
  background: #fef3c7;
}

.notification-icon {
  width: 40px;
  height: 40px;
  border-radius: 8px;
  background: #f3f4f6;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.notification-content {
  flex: 1;
  min-width: 0;
}

.notification-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 8px;
}

.notification-title {
  font-size: 16px;
  font-weight: 600;
  color: #1f2937;
  margin: 0;
  line-height: 1.4;
}

.notification-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.notification-time {
  font-size: 12px;
  color: #9ca3af;
}

.notification-text {
  font-size: 14px;
  color: #6b7280;
  line-height: 1.5;
  margin: 0;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.notification-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.unread-dot {
  width: 8px;
  height: 8px;
  background: #3b82f6;
  border-radius: 50%;
}

.notification-detail {
  padding: 16px 0;
}

.detail-meta {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  padding-bottom: 16px;
  border-bottom: 1px solid #f3f4f6;
}

.detail-time {
  font-size: 14px;
  color: #6b7280;
}

.detail-content {
  font-size: 14px;
  line-height: 1.6;
  color: #374151;
  white-space: pre-wrap;
}

/* 深色模式 */
.dark .page-title h1 {
  color: #f9fafb;
}

.dark .page-title p {
  color: #9ca3af;
}

.dark .stat-card,
.dark .notifications-card {
  background: #1f2937;
  border-color: #374151;
}

.dark .stat-value {
  color: #f9fafb;
}

.dark .stat-label {
  color: #9ca3af;
}

.dark .notification-item {
  border-bottom-color: #374151;
}

.dark .notification-item:hover {
  background: #374151;
}

.dark .notification-item.unread {
  background: #451a03;
}

.dark .notification-item.unread:hover {
  background: #451a03;
}

.dark .notification-icon {
  background: #374151;
}

.dark .notification-title {
  color: #f9fafb;
}

.dark .notification-text {
  color: #9ca3af;
}

.dark .notification-time {
  color: #6b7280;
}

.dark .detail-meta {
  border-bottom-color: #374151;
}

.dark .detail-time {
  color: #9ca3af;
}

.dark .detail-content {
  color: #e5e7eb;
}
</style>