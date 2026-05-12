<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="user-notifications-wrapper">
  <div class="user-notifications">
    <div class="container">
      <div class="user-center-layout">
        <!-- 侧边导航 -->
        <WebUserSidebar :user-info="userInfo" @logout="handleLogout" />

        <!-- 主要内容区域 -->
        <main class="main-content">
          <div class="content-header">
            <h1>{{ t('notify_title') }}</h1>
            <p>{{ t('notify_subtitle') }}</p>
          </div>

          <!-- 通知筛选 -->
          <div class="notification-filters">
            <el-button-group>
              <el-button :type="filter === 'all' ? 'primary' : ''" @click="filter = 'all'">
                {{ t('notify_all') }} ({{ notifications.length }})
              </el-button>
              <el-button :type="filter === 'unread' ? 'primary' : ''" @click="filter = 'unread'">
                {{ t('notify_unread') }} ({{ unreadCount }})
              </el-button>
              <el-button :type="filter === 'read' ? 'primary' : ''" @click="filter = 'read'">
                {{ t('notify_read') }} ({{ readCount }})
              </el-button>
            </el-button-group>
            <div class="filter-actions">
              <el-button v-if="unreadCount > 0" @click="markAllAsRead" :loading="markingAll">
                {{ t('notify_mark_all') }}
              </el-button>
              <el-button @click="loadNotifications" :icon="Refresh" circle />
            </div>
          </div>

          <!-- 通知列表 -->
          <div class="notification-list">
            <div v-if="loading" class="loading-container">
              <el-skeleton :rows="5" animated />
            </div>
            <div v-else-if="filteredNotifications.length === 0" class="empty-state">
              <el-empty :description="t('notify_empty')" />
            </div>
            <div v-else class="notification-items">
              <div
                v-for="notification in filteredNotifications"
                :key="notification.id"
                :data-notification-id="notification.id"
                class="notification-wrapper"
                :class="{ 'expanded': expandedId === notification.id }"
              >
                <div
                  class="notification-item"
                  :class="{ 'unread': !notification.isRead, 'active': expandedId === notification.id }"
                  @click="handleNotificationClick(notification)"
                >
                  <div class="notification-icon">
                    <el-icon :class="getTypeClass(notification.type)">
                      <component :is="getTypeIcon(notification.type)" />
                    </el-icon>
                  </div>
                  <div class="notification-content">
                    <div class="notification-header">
                      <h3 class="notification-title">{{ notification.title }}</h3>
                      <div class="notification-badges">
                        <el-tag v-if="!notification.isRead" size="small" type="primary" effect="dark">
                          <span class="unread-indicator"></span>
                          {{ t('notify_unread') }}
                        </el-tag>
                        <el-icon v-if="expandedId === notification.id" class="expand-icon expanded">
                          <ArrowUp />
                        </el-icon>
                        <el-icon v-else class="expand-icon">
                          <ArrowDown />
                        </el-icon>
                      </div>
                    </div>
                    <p class="notification-text">{{ notification.content }}</p>
                    <div class="notification-meta">
                      <span class="notification-time">{{ formatTime(notification.createdAt) }}</span>
                    </div>
                  </div>
                  <div class="notification-actions">
                    <el-button v-if="!notification.isRead" @click.stop="markAsRead(notification)" size="small" text>
                      {{ t('notify_mark_read') }}
                    </el-button>
                  </div>
                </div>

                <!-- 优化的内联详情展开区 -->
                <transition name="slide-fade" mode="out-in">
                  <div v-if="expandedId === notification.id" class="notification-detail">
                    <div class="detail-container">
                      <div v-if="loadingDetailId === notification.id" class="detail-loading">
                        <div class="loading-content">
                          <el-skeleton :rows="3" animated />
                          <span class="loading-text">{{ t('notify_loading') }}</span>
                        </div>
                      </div>
                      <div v-else class="detail-content">
                        <div class="detail-section">
                          <h4 class="detail-title">{{ t('notify_content') }}</h4>
                          <div class="detail-body">
                            <div v-if="details[notification.id]?.contentHtml"
                                 class="content-html"
                                 v-html="DOMPurify.sanitize(details[notification.id]?.contentHtml ?? '')">
                            </div>
                            <div v-else class="content-text">
                              {{ details[notification.id]?.contentText || t('notify_no_content') }}
                            </div>
                          </div>
                        </div>

                        <div v-if="attachmentsMap[notification.id] && attachmentsMap[notification.id].length > 0"
                             class="detail-section attachments-section">
                          <h4 class="detail-title">
                            <el-icon class="title-icon"><Paperclip /></el-icon>
                            {{ t('notify_attachments') }} ({{ attachmentsMap[notification.id].length }})
                          </h4>
                          <div class="attachments-container">
                            <div v-for="att in attachmentsMap[notification.id]"
                                 :key="att.fileId"
                                 class="attachment-item"
                                 :class="getAttachmentClass(att)">

                              <!-- 图片类型 -->
                              <template v-if="isImage(att)">
                                <div class="attachment-preview image-preview">
                                  <el-image
                                    :src="resolveResourcePath(att.fileUrl || '')"
                                    :preview-src-list="[resolveResourcePath(att.fileUrl || '')]"
                                    preview-teleported
                                    fit="cover"
                                    class="image-thumb"
                                  >
                                    <template #error>
                                      <div class="image-error">
                                        <el-icon><Picture /></el-icon>
                                        <span>{{ t('notify_load_fail_img') }}</span>
                                      </div>
                                    </template>
                                  </el-image>
                                  <div class="image-overlay">
                                    <el-icon class="preview-icon"><ZoomIn /></el-icon>
                                  </div>
                                </div>
                                <div class="attachment-info">
                                  <span class="attachment-name" :title="att.name || t('notify_image')">{{ att.name || t('notify_image') }}</span>
                                  <span class="attachment-size">{{ formatFileSize(att.size) }}</span>
                                  <span class="attachment-type">{{ t('notify_image') }}</span>
                                </div>
                              </template>

                              <!-- 视频类型 -->
                              <template v-else-if="isVideo(att)">
                                <div class="attachment-preview video-preview"
                                     @click="openMediaDialog('video', att.fileUrl || '', att.name)">
                                  <div class="video-placeholder">
                                    <div class="video-icon">
                                      <el-icon><VideoPlay /></el-icon>
                                    </div>
                                    <div class="video-info">
                                      <span class="video-title">{{ att.name || t('notify_video') }}</span>
                                      <span class="video-hint">{{ t('notify_play') }}</span>
                                    </div>
                                  </div>
                                </div>
                                <div class="attachment-info">
                                  <span class="attachment-name" :title="att.name || t('notify_video')">{{ att.name || t('notify_video') }}</span>
                                  <span class="attachment-size">{{ formatFileSize(att.size) }}</span>
                                  <span class="attachment-type">{{ t('notify_video') }}</span>
                                </div>
                              </template>

                              <!-- 音频类型 -->
                              <template v-else-if="isAudio(att)">
                                <div class="attachment-preview audio-preview"
                                     @click="openMediaDialog('audio', att.fileUrl || '', att.name)">
                                  <div class="audio-placeholder">
                                    <div class="audio-icon">
                                      <el-icon><Headset /></el-icon>
                                    </div>
                                    <div class="audio-info">
                                      <span class="audio-title">{{ att.name || t('notify_audio') }}</span>
                                      <span class="audio-hint">{{ t('notify_play') }}</span>
                                    </div>
                                  </div>
                                </div>
                                <div class="attachment-info">
                                  <span class="attachment-name" :title="att.name || t('notify_audio')">{{ att.name || t('notify_audio') }}</span>
                                  <span class="attachment-size">{{ formatFileSize(att.size) }}</span>
                                  <span class="attachment-type">{{ t('notify_audio') }}</span>
                                </div>
                              </template>

                              <!-- 下载文件类型 -->
                              <template v-else>
                                <div class="attachment-preview file-preview">
                                  <div class="file-icon">
                                    <el-icon><Document /></el-icon>
                                  </div>
                                  <div class="file-type">{{ getFileExtension(att.name) }}</div>
                                </div>
                                <div class="attachment-info">
                                  <span class="attachment-name" :title="att.name || t('notify_unnamed')">{{ att.name || t('notify_unnamed') }}</span>
                                  <span class="attachment-size">{{ formatFileSize(att.size) }}</span>
                                  <span class="attachment-type">{{ t('notify_document') }}</span>
                                </div>
                                <div class="attachment-actions">
                                  <el-button type="primary" size="small" plain @click="downloadFile(notification.id, att.fileId)">
                                    <el-icon><Download /></el-icon>
                                    {{ t('notify_download') }}
                                  </el-button>
                                </div>
                              </template>
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>
                </transition>
              </div>
            </div>
          </div>
        </main>
      </div>
    </div>
  </div>

  <!-- 媒体播放遮罩背景 -->
  <div class="media-overlay" :class="{ visible: expandedVideoId || expandedAudioId }" @click="closeAllMedia"></div>

  <!-- 悬浮媒体播放框（无动画，直接弹出） -->
  <el-dialog
    v-model="mediaDialogVisible"
    :title="mediaDialogTitle"
    :width="mediaDialogType === 'video' ? '70vw' : '480px'"
    append-to-body
    destroy-on-close
    :close-on-click-modal="true"
    @closed="closeMediaDialog"
  >
    <template #default>
      <div v-if="mediaDialogType === 'video'" style="width:100%">
        <video :src="mediaDialogSrc" controls autoplay style="width:100%; height:auto; display:block;" />
      </div>
      <div v-else-if="mediaDialogType === 'audio'" style="width:100%">
        <audio :src="mediaDialogSrc" controls autoplay style="width:100%; display:block;" />
      </div>
    </template>
    <template #footer>
      <el-button @click="closeMediaDialog">{{ t('notify_close') }}</el-button>
    </template>
  </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import DOMPurify from 'dompurify'
import {
  Refresh, InfoFilled, Warning, SuccessFilled,
  ArrowDown, ArrowUp, Paperclip, VideoPlay, Headset, Picture,
  ZoomIn, Download, Document
} from '@element-plus/icons-vue'
import { useWebAuthStore } from '../../../stores/webAuth'
import { getMyNotifications, markNotificationAsRead, getUnreadNotificationCount, type NotificationItem, getMyNotificationDetail, getNotificationAttachments, type AttachmentDto, buildAttachmentDownloadUrl } from '../../../api/user'
import { logout as apiLogout } from '../../../api/auth'
import { resolveResourcePath } from '@/utils/resourceUrl'
import { t } from '@/utils/lang'

const router = useRouter()
const webAuth = useWebAuthStore()
const userInfo = computed(() => webAuth.userInfo)

const loading = ref(false)
const markingAll = ref(false)
const notifications = ref<NotificationItem[]>([])
const unreadCount = ref(0)
const filter = ref<'all' | 'unread' | 'read'>('all')

const readCount = computed(() => notifications.value.filter(n => n.isRead).length)

const filteredNotifications = computed(() => {
  switch (filter.value) {
    case 'unread':
      return notifications.value.filter(n => !n.isRead)
    case 'read':
      return notifications.value.filter(n => n.isRead)
    default:
      return notifications.value
  }
})

// 获取用户角色
const getUserRole = () => {
  if (!userInfo.value) return '访客'

  switch (userInfo.value.userName) {
    case 'admin':
      return '管理员'
    case 'user':
      return '普通用户'
    case 'demo':
      return '演示用户'
    default:
      return '用户'
  }
}

import WebUserSidebar from './components/WebUserSidebar.vue'

// 加载通知列表
const loadNotifications = async () => {
  try {
    loading.value = true
    const [notificationData, unreadCountData] = await Promise.all([
      getMyNotifications(),
      getUnreadNotificationCount()
    ])
    notifications.value = notificationData
    unreadCount.value = unreadCountData
  } catch (e) {
    ElMessage.error(t('notify_load_fail'))
  } finally {
    loading.value = false
  }
}

// 标记单个通知为已读
const markAsRead = async (notification: NotificationItem) => {
  try {
    await markNotificationAsRead(notification.id)
    notification.isRead = true
    unreadCount.value = Math.max(0, unreadCount.value - 1)
    ElMessage.success(t('notify_marked'))
  } catch (e) {
    ElMessage.error(t('notify_mark_fail'))
  }
}

// 全部标记为已读
const markAllAsRead = async () => {
  try {
    markingAll.value = true
    const unreadNotifications = notifications.value.filter(n => !n.isRead)
    await Promise.all(unreadNotifications.map(n => markNotificationAsRead(n.id)))
    notifications.value.forEach(n => n.isRead = true)
    unreadCount.value = 0
    ElMessage.success(t('notify_mark_all_ok'))
  } catch (e) {
    ElMessage.error(t('notify_mark_all_fail'))
  } finally {
    markingAll.value = false
  }
}

// 点击通知
const handleNotificationClick = async (notification: NotificationItem) => {
  if (!notification.isRead) {
    await markAsRead(notification)
  }
  await toggleExpand(notification.id)
}

// 获取通知类型图标
const getTypeIcon = (type: string) => {
  switch (type.toLowerCase()) {
    case 'success': return SuccessFilled
    case 'warning': return Warning
    case 'error': return Warning
    default: return InfoFilled
  }
}

// 获取通知类型样式
const getTypeClass = (type: string) => {
  switch (type.toLowerCase()) {
    case 'success': return 'icon-success'
    case 'warning': return 'icon-warning'
    case 'error': return 'icon-error'
    default: return 'icon-info'
  }
}

// 格式化时间
const formatTime = (dateString: string) => {
  const date = new Date(dateString)
  const now = new Date()
  const diff = now.getTime() - date.getTime()
  const minutes = Math.floor(diff / 60000)
  const hours = Math.floor(diff / 3600000)
  const days = Math.floor(diff / 86400000)

  if (minutes < 1) return t('time_just_now')
  if (minutes < 60) return t('time_min_ago', { n: minutes })
  if (hours < 24) return t('time_hour_ago', { n: hours })
  if (days < 7) return t('time_day_ago', { n: days })
  return date.toLocaleDateString('zh-CN')
}

// 处理退出登录
const handleLogout = async () => {
  try {
    await ElMessageBox.confirm(t('confirm_logout'), t('tip'), {
      confirmButtonText: t('confirm'),
      cancelButtonText: t('cancel'),
      type: 'warning'
    })

    try { await apiLogout() } catch (_) { /* 忽略后端异常，继续前端清理 */ }
    webAuth.logout()
    ElMessage.success(t('logged_out'))
    router.push('/web')
  } catch (e) {
    // 用户取消退出
  }
}

onMounted(() => {
  webAuth.initFromStorage()
  if (!webAuth.isAuthenticated) {
    router.push('/web/login')
  } else {
    loadNotifications()
  }
})

// 内联详情展开逻辑
const expandedId = ref('')
const loadingDetailId = ref('')
const details = ref<Record<string, { id: string; title: string; contentType: number; contentText?: string | null; contentHtml?: string | null; isRead: boolean }>>({})
const attachmentsMap = ref<Record<string, AttachmentDto[]>>({})

async function toggleExpand(id: string) {
  if (expandedId.value === id) {
    expandedId.value = ''
    return
  }
  expandedId.value = id

  // 自动滚动到通知标题位置
  await scrollToNotification(id)

  if (!details.value[id]) {
    loadingDetailId.value = id
    try {
      const d = await getMyNotificationDetail(id)
      details.value[id] = d
      try {
        attachmentsMap.value[id] = await getNotificationAttachments(id)
      } catch {
        attachmentsMap.value[id] = []
      }
    } catch (e) {
      ElMessage.error(t('notify_detail_fail'))
    } finally {
      if (loadingDetailId.value === id) loadingDetailId.value = ''
    }
  }
}

// 滚动到指定通知位置
async function scrollToNotification(id: string) {
  // 等待DOM更新
  await nextTick()

  const notificationElement = document.querySelector(`[data-notification-id="${id}"]`)
  if (notificationElement) {
    const headerHeight = 120 // 考虑固定头部高度
    const elementTop = notificationElement.getBoundingClientRect().top + window.pageYOffset
    const targetPosition = elementTop - headerHeight

    // 平滑滚动到目标位置
    window.scrollTo({
      top: Math.max(0, targetPosition),
      behavior: 'smooth'
    })
  }
}

function isImage(att: AttachmentDto): boolean {
  const name = (att.name || '').toLowerCase()
  const ct = (att.contentType || '').toLowerCase()
  return /image\//.test(ct) || /(\.png|\.jpg|\.jpeg|\.gif|\.webp|\.bmp|\.svg)$/.test(name)
}
function isVideo(att: AttachmentDto): boolean {
  const name = (att.name || '').toLowerCase()
  const ct = (att.contentType || '').toLowerCase()
  return /video\//.test(ct) || /(\.mp4|\.webm|\.ogg)$/.test(name)
}
function isAudio(att: AttachmentDto): boolean {
  const name = (att.name || '').toLowerCase()
  const ct = (att.contentType || '').toLowerCase()
  return /audio\//.test(ct) || /(\.mp3|\.wav|\.ogg)$/.test(name)
}
function openInNewTab(url: string) {
  window.open(url, '_blank', 'noopener')
}

// 视频/音频播放控制（点击后再展示控件与播放）
const videoRefs: Record<string, HTMLVideoElement | undefined> = {}
const audioRefs: Record<string, HTMLAudioElement | undefined> = {}
const playingVideoIds = ref<Record<string, boolean>>({})
const playingAudioIds = ref<Record<string, boolean>>({})

function setVideoRef(id: string, el?: HTMLVideoElement) { if (el) videoRefs[id] = el }
function setAudioRef(id: string, el?: HTMLAudioElement) { if (el) audioRefs[id] = el }

function isVideoPlaying(id: string) { return !!playingVideoIds.value[id] }
function isAudioPlaying(id: string) { return !!playingAudioIds.value[id] }

function playVideo(fileId: string) {
  playingVideoIds.value[fileId] = true
  const el = videoRefs[fileId]
  if (el) { el.play().catch(() => {}) }
}

function playAudio(fileId: string) {
  playingAudioIds.value[fileId] = true
  const el = audioRefs[fileId]
  if (el) { el.play().catch(() => {}) }
}

function formatFileSize(size?: number): string {
  if (!size) return t('notify_unknown_size')
  if (size < 1024) return `${size} B`
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`
  if (size < 1024 * 1024 * 1024) return `${(size / (1024 * 1024)).toFixed(1)} MB`
  return `${(size / (1024 * 1024 * 1024)).toFixed(1)} GB`
}

function getAttachmentClass(att: AttachmentDto): string {
  if (isImage(att)) return 'image-type'
  if (isVideo(att)) return 'video-type'
  if (isAudio(att)) return 'audio-type'
  return 'file-type'
}

function getFileExtension(filename?: string): string {
  if (!filename) return 'FILE'
  const ext = filename.split('.').pop()?.toUpperCase()
  return ext || 'FILE'
}

function downloadFile(notifyId: string, fileId: string) {
  const url = buildAttachmentDownloadUrl(notifyId, fileId)
  window.open(url, '_blank')
}

const playingMedia = ref<Record<string, 'video' | 'audio' | undefined>>({})
const expandedVideoId = ref<string>('')
const expandedAudioId = ref<string>('')

// 悬浮播放框状态
const mediaDialogVisible = ref(false)
const mediaDialogType = ref<'video' | 'audio' | ''>('')
const mediaDialogTitle = ref<string>('')
const mediaDialogSrc = ref<string>('')

function playMedia(notifyId: string, fileId: string, type: 'video' | 'audio') {
  // Stop any currently playing media
  for (const key in playingMedia.value) {
    if (key !== fileId) {
      playingMedia.value[key] = undefined
    }
  }
  playingMedia.value[fileId] = type
}

function toggleVideoPlayer(notifyId: string, fileId: string) {
  if (expandedVideoId.value === fileId) {
    closeVideoPlayer()
  } else {
    expandedVideoId.value = fileId
    expandedAudioId.value = ''
    playMedia(notifyId, fileId, 'video')
  }
}

function toggleAudioPlayer(notifyId: string, fileId: string) {
  if (expandedAudioId.value === fileId) {
    closeAudioPlayer()
  } else {
    expandedAudioId.value = fileId
    expandedVideoId.value = ''
    playMedia(notifyId, fileId, 'audio')
  }
}

function closeVideoPlayer() {
  expandedVideoId.value = ''
  for (const key in playingMedia.value) {
    if (playingMedia.value[key] === 'video') {
      playingMedia.value[key] = undefined
    }
  }
}

function closeAudioPlayer() {
  expandedAudioId.value = ''
  for (const key in playingMedia.value) {
    if (playingMedia.value[key] === 'audio') {
      playingMedia.value[key] = undefined
    }
  }
}

function closeAllMedia() {
  closeVideoPlayer()
  closeAudioPlayer()
}

function openMediaDialog(type: 'video' | 'audio', fileUrl: string, name?: string) {
  mediaDialogType.value = type
  mediaDialogSrc.value = resolveResourcePath(fileUrl || '')
  mediaDialogTitle.value = name || (type === 'video' ? t('notify_video') : t('notify_audio'))
  mediaDialogVisible.value = true
}

function closeMediaDialog() {
  mediaDialogVisible.value = false
  mediaDialogSrc.value = ''
  mediaDialogType.value = ''
}
</script>

<style scoped>
.user-notifications {
  min-height: calc(100vh - 4rem - 200px);
  background: #f8fafc;
  padding: 2rem 0;
}

.container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 0 1rem;
}

.user-center-layout {
  display: grid;
  grid-template-columns: 280px 1fr;
  gap: 2rem;
  align-items: start;
}

/* 侧边栏样式 - 复用 UserCenter 的样式 */
.sidebar {
  background: white;
  border-radius: 16px;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05);
  overflow: hidden;
  position: sticky;
  top: 6rem;
}

.user-profile-card {
  padding: 2rem;
  text-align: center;
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  color: white;
}

.user-avatar-large {
  width: 80px;
  height: 80px;
  border-radius: 50%;
  margin: 0 auto 1rem;
  overflow: hidden;
  background: rgba(255, 255, 255, 0.2);
  display: flex;
  align-items: center;
  justify-content: center;
}

.user-avatar-large img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.avatar-placeholder {
  color: rgba(255, 255, 255, 0.8);
}

.user-name {
  font-size: 1.25rem;
  font-weight: 600;
  margin: 0 0 0.5rem 0;
}

.user-role {
  opacity: 0.9;
  margin: 0;
  font-size: 0.875rem;
}

.sidebar-nav {
  padding: 1rem 0;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.75rem 1.5rem;
  color: #6b7280;
  text-decoration: none;
  transition: all 0.2s ease;
  border-left: 3px solid transparent;
  position: relative;
}

.nav-item:hover {
  background: #f3f4f6;
  color: #3b82f6;
}

.nav-item.router-link-active {
  background: #eff6ff;
  color: #3b82f6;
  border-left-color: #3b82f6;
}

.nav-badge {
  margin-left: auto;
}

.logout-item {
  color: #ef4444;
  border-top: 1px solid #f3f4f6;
  margin-top: 0.5rem;
}

.logout-item:hover {
  background: #fef2f2;
  color: #dc2626;
}

/* 主要内容区域 - 统一白色卡片 */
.main-content {
  background: white;
  border-radius: 16px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.06);
  padding: 2rem;
}

.content-header {
  margin-bottom: 2rem;
}

.content-header h1 {
  font-size: 1.75rem;
  font-weight: 700;
  color: #1e293b;
  margin: 0 0 0.5rem 0;
}

.content-header p {
  color: #64748b;
  margin: 0;
  font-size: 1rem;
}

/* 通知筛选 */
.notification-filters {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid #e5e7eb;
}

.filter-actions {
  display: flex;
  align-items: center;
  gap: 1rem;
}

/* 通知列表 */
.notification-list {
  min-height: 400px;
}

.loading-container {
  padding: 2rem;
}

.empty-state {
  padding: 3rem;
  text-align: center;
}

.notification-items {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.notification-wrapper {
  border-radius: 16px;
  overflow: hidden;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  background: white;
  border: 1px solid #f1f5f9;
  position: relative;
}

.notification-wrapper:hover {
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.08);
  border-color: #e2e8f0;
  transform: translateY(-1px);
}

.notification-wrapper.expanded {
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.12);
  transform: translateY(-2px);
  border-color: #3b82f6;
}

.notification-item {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
  padding: 1.5rem;
  background: white;
  transition: all 0.3s ease;
  cursor: pointer;
  position: relative;
  border-radius: 16px 16px 0 0; /* 确保顶部圆角正确 */
}

.notification-item:hover {
  background: #f8fafc;
}

.notification-item.unread {
  background: linear-gradient(135deg, #eff6ff 0%, #f0f9ff 100%);
  position: relative;
}

.notification-item.unread::before {
  content: '';
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 4px;
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  border-radius: 2px 0 0 0; /* 左上角圆角 */
}

.notification-item.unread:hover {
  background: linear-gradient(135deg, #dbeafe 0%, #e0f2fe 100%);
}

.notification-item.active {
  background: #f1f5f9;
}

.notification-icon {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
  flex-shrink: 0;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}

.icon-info {
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  color: white;
}

.icon-success {
  background: linear-gradient(135deg, #22c55e 0%, #16a34a 100%);
  color: white;
}

.icon-warning {
  background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
  color: white;
}

.icon-error {
  background: linear-gradient(135deg, #ef4444 0%, #dc2626 100%);
  color: white;
}

.notification-content {
  flex: 1;
  min-width: 0;
}

.notification-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 0.75rem;
  gap: 1rem;
}

.notification-badges {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-shrink: 0;
}

.expand-icon {
  color: #6b7280;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  transform: rotate(0deg);
  font-size: 1.1rem;
  padding: 0.25rem;
  border-radius: 6px;
  background: rgba(107, 114, 128, 0.1);
}

.expand-icon:hover {
  background: rgba(59, 130, 246, 0.1);
  color: #3b82f6;
}

.expand-icon.expanded {
  transform: rotate(180deg);
  color: #3b82f6;
  background: rgba(59, 130, 246, 0.15);
}

.notification-title {
  font-size: 1.125rem;
  font-weight: 700;
  color: #1f2937;
  margin: 0;
  line-height: 1.4;
  letter-spacing: -0.01em;
}

.notification-text {
  color: #6b7280;
  margin: 0 0 0.75rem 0;
  line-height: 1.5;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.notification-meta {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.notification-time {
  font-size: 0.875rem;
  color: #9ca3af;
  font-weight: 500;
}

.notification-actions {
  flex-shrink: 0;
}

/* 未读指示器样式 */
.unread-indicator {
  display: inline-block;
  width: 6px;
  height: 6px;
  background: #ffffff;
  border-radius: 50%;
  margin-right: 0.375rem;
  animation: pulse 2s infinite;
}

@keyframes pulse {
  0%, 100% {
    opacity: 1;
    transform: scale(1);
  }
  50% {
    opacity: 0.7;
    transform: scale(1.1);
  }
}

/* 详情展开区域 */
.notification-detail {
  background: #f8fafc;
  border-top: 1px solid #e5e7eb;
  border-radius: 0 0 16px 16px; /* 确保底部圆角正确 */
}

.detail-container {
  padding: 2rem;
}

.detail-loading {
  text-align: center;
}

.loading-content {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
}

.loading-text {
  color: #6b7280;
  font-size: 0.875rem;
}

.detail-content {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.detail-section {
  background: white;
  border-radius: 12px;
  padding: 1.5rem;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.04);
}

.detail-title {
  font-size: 1rem;
  font-weight: 600;
  color: #1f2937;
  margin: 0 0 1rem 0;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.detail-body {
  line-height: 1.6;
}

.content-html {
  color: #374151;
}

.content-html :deep(img) {
  max-width: 100%;
  height: auto;
  border-radius: 8px;
  margin: 0.5rem 0;
}

.content-html :deep(p) {
  margin: 0.5rem 0;
}

.content-text {
  color: #374151;
  white-space: pre-wrap;
}

/* 附件区域 */
.attachments-section {
  background: linear-gradient(135deg, #f0f9ff 0%, #f8fafc 100%);
  border: 1px solid #e0f2fe;
}

.detail-title .title-icon {
  margin-right: 0.5rem;
  color: #3b82f6;
}

.attachments-container {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr)); /* 略放大卡片，避免内容截断 */
  gap: 1rem;
  max-width: 100%;
  padding: 0.5rem 0;
}

.attachment-item {
  background: white;
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.06);
  transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
  border: 1px solid #f1f5f9;
  position: relative;
  height: 220px; /* 给下方信息留出空间 */
  display: flex;
  flex-direction: column;
}

.attachment-item::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 3px;
  background: linear-gradient(90deg, #3b82f6, #8b5cf6, #06b6d4);
  opacity: 0;
  transition: opacity 0.3s ease;
}

.attachment-item:hover {
  box-shadow: 0 8px 30px rgba(0, 0, 0, 0.12);
  transform: translateY(-2px);
  border-color: #e0f2fe;
}

.attachment-item:hover::before {
  opacity: 1;
}

/* 图片类型样式 */
.attachment-item.image-type {
  display: flex;
  flex-direction: column;
}

.image-preview {
  position: relative;
  width: 100%;
  height: 130px;
  overflow: hidden;
  background: linear-gradient(135deg, #f8fafc 0%, #f1f5f9 100%);
  flex-shrink: 0;
}

.image-thumb {
  width: 100%;
  height: 100%;
  cursor: pointer;
}

.image-thumb :deep(.el-image__inner) {
  object-fit: cover;
  width: 100%;
  height: 100%;
  transition: transform 0.3s ease;
}

.image-preview:hover .image-thumb :deep(.el-image__inner) {
  transform: scale(1.05);
}

.image-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  transition: opacity 0.3s ease;
  color: white;
  font-size: 2rem;
  pointer-events: none; /* 让点击事件传递给 ElImage，触发全屏预览 */
}

.image-preview:hover .image-overlay {
  opacity: 1;
}

.image-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  color: #6b7280;
  font-size: 1rem;
  gap: 0.5rem;
}

/* 视频类型样式 */
.attachment-item.video-type {
  display: flex;
  flex-direction: column;
}

.video-preview {
  position: relative;
  width: 100%;
  height: 130px;
  overflow: hidden;
  background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%);
  cursor: pointer;
  flex-shrink: 0;
}

.video-preview.expanded {
  position: fixed;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  width: 80vw;
  height: 60vh;
  z-index: 9999;
  border-radius: 12px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.4);
  transition: all 0.5s cubic-bezier(0.25, 0.8, 0.25, 1);
}

.video-player {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.video-placeholder {
  width: 100%;
  height: 100%;
  background: linear-gradient(135deg, #1f2937 0%, #374151 100%);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: white;
  gap: 1rem;
  transition: background 0.3s ease;
}

.video-placeholder:hover {
  background: linear-gradient(135deg, #111827 0%, #1f2937 100%);
}

.video-icon {
  font-size: 3rem;
  opacity: 0.9;
}

.video-info {
  text-align: center;
  max-width: 80%;
}

.video-title {
  display: block;
  font-weight: 500;
  font-size: 0.9rem;
  margin-bottom: 0.25rem;
  word-break: break-all;
  line-clamp: 2;
  -webkit-line-clamp: 2;
  display: -webkit-box;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.video-hint {
  display: block;
  font-size: 0.75rem;
  opacity: 0.8;
}

/* 音频类型样式 */
.attachment-item.audio-type {
  display: flex;
  flex-direction: column;
}

.audio-preview {
  position: relative;
  width: 100%;
  height: 120px;
  overflow: hidden;
  cursor: pointer;
  background: linear-gradient(135deg, #4338ca 0%, #7c3aed 50%, #c026d3 100%);
  flex-shrink: 0;
}

.audio-preview.expanded {
  position: fixed;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  width: 60vw;
  height: 300px;
  z-index: 9999;
  border-radius: 12px;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.4);
  background: #0b1020; /* 扩展播放时提供深色背景，避免全黑无层次 */
  transition: all 0.5s cubic-bezier(0.25, 0.8, 0.25, 1);
}

.audio-player {
  width: 100%;
  height: 100%;
  padding: 1rem;
  background: #f8fafc;
}

.audio-placeholder {
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  color: white;
  gap: 0.75rem;
  transition: all 0.3s ease;
  position: relative;
}

.audio-placeholder::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(255, 255, 255, 0.1);
  opacity: 0;
  transition: opacity 0.3s ease;
}

.audio-placeholder:hover::before {
  opacity: 1;
}

.audio-icon {
  font-size: 2.5rem;
  opacity: 0.9;
}

.audio-info {
  text-align: center;
  max-width: 80%;
}

.audio-title {
  display: block;
  font-weight: 500;
  font-size: 0.9rem;
  margin-bottom: 0.25rem;
  word-break: break-all;
  line-clamp: 2;
  -webkit-line-clamp: 2;
  display: -webkit-box;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.audio-hint {
  display: block;
  font-size: 0.75rem;
  opacity: 0.8;
}

/* 文件类型样式 */
.attachment-item.file-type {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 0.75rem;
}

.file-preview {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 60px;
  height: 60px;
  background: linear-gradient(135deg, #f8fafc 0%, #f1f5f9 100%);
  border-radius: 10px;
  position: relative;
  overflow: hidden;
  border: 2px solid #e2e8f0;
  transition: all 0.3s ease;
  margin-bottom: 0.5rem;
}

.file-preview:hover {
  border-color: #3b82f6;
  transform: scale(1.05);
}

.file-icon {
  font-size: 1.5rem;
  color: #6b7280;
  margin-bottom: 0.125rem;
}

.file-type {
  font-size: 0.6rem;
  font-weight: 600;
  color: #374151;
  text-align: center;
  background: rgba(255, 255, 255, 0.9);
  padding: 0.125rem 0.25rem;
  border-radius: 4px;
  position: absolute;
  bottom: 0.25rem;
  left: 50%;
  transform: translateX(-50%);
  white-space: nowrap;
  max-width: calc(100% - 0.5rem);
  overflow: hidden;
  text-overflow: ellipsis;
}

/* 附件信息样式 */
.attachment-info {
  padding: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  min-width: 0;
  flex: 1;
  background: linear-gradient(135deg, #ffffff 0%, #fafbfc 100%);
  text-align: center;
}

.attachment-item.image-type .attachment-info,
.attachment-item.video-type .attachment-info,
.attachment-item.audio-type .attachment-info {
  padding: 0.75rem;
  background: transparent;
  text-align: center;
}

.attachment-name {
  font-weight: 600;
  color: #1e293b;
  font-size: 0.85rem;
  line-height: 1.35;
  word-break: break-all;
  display: -webkit-box;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  overflow: hidden;
  margin-bottom: 0.125rem;
}
/* 标题仅一行显示，超出省略 */
.attachment-name {
  -webkit-line-clamp: 1;
  line-clamp: 1;
}

.attachment-size {
  font-size: 0.75rem;
  color: #64748b;
  font-weight: 500;
}

.attachment-type {
  font-size: 0.65rem;
  color: #3b82f6;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  background: linear-gradient(135deg, #dbeafe 0%, #bfdbfe 100%);
  padding: 0.2rem 0.4rem;
  border-radius: 8px;
  display: inline-block;
  margin-top: 0.25rem;
}

/* 附件操作按钮 */
.attachment-actions {
  padding: 0.5rem;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-top: auto;
}

.attachment-actions .el-button {
  border-radius: 6px;
  font-weight: 600;
  font-size: 0.7rem;
  padding: 0.3rem 0.6rem;
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.2);
  transition: all 0.3s ease;
}

.attachment-actions .el-button:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 16px rgba(59, 130, 246, 0.3);
}

/* 播放器遮罩背景 */
.media-overlay {
  position: fixed;
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: rgba(0, 0, 0, 0.8);
  z-index: 9998;
  opacity: 0;
  transition: opacity 0.3s ease;
  pointer-events: none; /* 隐藏状态不拦截鼠标事件 */
}

.media-overlay.visible {
  opacity: 1;
  pointer-events: auto; /* 仅显示时接收鼠标事件 */
}

/* 关闭按钮 */
.media-close-btn {
  position: absolute;
  top: 1rem;
  right: 1rem;
  width: 40px;
  height: 40px;
  background: rgba(255, 255, 255, 0.9);
  border: none;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  font-size: 1.2rem;
  z-index: 10000;
  transition: all 0.3s ease;
}

.media-close-btn:hover {
  background: white;
  transform: scale(1.1);
}

/* 动画效果 */
.slide-fade-enter-active {
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.slide-fade-leave-active {
  transition: all 0.2s cubic-bezier(0.4, 0, 1, 1);
}

.slide-fade-enter-from {
  opacity: 0;
  transform: translateY(-20px);
  max-height: 0;
}

.slide-fade-leave-to {
  opacity: 0;
  transform: translateY(-10px);
  max-height: 0;
}

/* 响应式设计 */
@media (max-width: 1024px) {
  .user-center-layout {
    grid-template-columns: 1fr;
    gap: 1.5rem;
  }

  .sidebar {
    position: static;
  }
}

@media (max-width: 768px) {
  .user-notifications {
    padding: 1rem 0;
  }

  .main-content {
    padding: 1.5rem;
  }

  .notification-filters {
    flex-direction: column;
    gap: 1rem;
    align-items: stretch;
  }

  .notification-item {
    padding: 1rem;
  }
}
</style>

