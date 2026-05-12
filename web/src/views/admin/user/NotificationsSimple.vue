<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="page-container">
    <div class="page-header">
      <div class="page-title">
        <h1>我的通知</h1>
        <p>查看系统通知和消息</p>
      </div>
    </div>

    <el-card class="notifications-card">
      <div v-loading="loading" class="notification-detail-wrapper">
        <div v-if="!detail">
          <el-empty description="暂无数据" />
        </div>
        <div v-else class="notification-detail">
          <h2 class="detail-title">{{ detail.title }}</h2>
          <div class="detail-meta">
            <span class="notification-time">{{ formatTime(detail.createdAt) }}</span>
            <el-tag v-if="detail.deliveryRole === 'cc'" size="small" type="info">知会</el-tag>
            <el-tag v-else size="small" type="primary">主送</el-tag>
            <el-tag v-if="detail.isRead" size="small" type="success">已读</el-tag>
          </div>
          <div class="detail-content" v-if="detail.summary">{{ detail.summary }}</div>
          <div class="detail-content" v-if="detail.content" v-html="sanitize(detail.content)"></div>

          <!-- 图片附件 -->
          <div class="attachments" v-if="imageAttachments.length">
            <h3 class="attachments-title">图片</h3>
            <div class="img-grid">
              <el-image
                v-for="att in imageAttachments"
                :key="att.id"
                :src="resolveResourcePath(att.fileUrl || '')"
                :preview-src-list="imageAttachments.map(a => resolveResourcePath(a.fileUrl || ''))"
                fit="cover"
                class="img-item"
              />
            </div>
          </div>

          <!-- 文件附件 -->
          <div class="attachments" v-if="fileAttachments.length">
            <h3 class="attachments-title">附件</h3>
            <el-table :data="fileAttachments" size="small" border>
              <el-table-column prop="fileName" label="文件名" />
              <el-table-column label="大小" width="120">
                <template #default="{ row }">{{ formatSize(row.fileSize) }}</template>
              </el-table-column>
              <el-table-column label="操作" width="100">
                <template #default="{ row }">
                  <el-button type="primary" link size="small" @click="downloadFile(row)">下载</el-button>
                </template>
              </el-table-column>
            </el-table>
          </div>

          <!-- 链接列表 -->
          <div class="links-section" v-if="detail.links && detail.links.length > 0">
            <h3 class="links-title">相关链接</h3>
            <div class="link-list">
              <div v-for="link in detail.links" :key="link.id" class="link-item" @click="openLink(link.url)">
                <i class="ri ri-link"></i>
                <span class="link-text">{{ link.title }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import DOMPurify from 'dompurify'
import { getMessageDetail, markMessageAsRead, type MessageDetail, type MessageAttachmentDto } from '../../../api/message'
import { buildFileDownloadUrl } from '../../../api/files'
import { resolveResourcePath } from '@/utils/resourceUrl'
import { useNotificationStore } from '../../../stores/notification'

// 净化 HTML 内容，防止 XSS
function sanitize(html: string): string {
  return DOMPurify.sanitize(html)
}

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const detail = ref<MessageDetail | null>(null)
const notificationStore = useNotificationStore()

const imageAttachments = computed<MessageAttachmentDto[]>(() =>
  detail.value?.attachments?.filter(a => a.attachmentType === 'image') ?? []
)

const fileAttachments = computed<MessageAttachmentDto[]>(() =>
  detail.value?.attachments?.filter(a => a.attachmentType === 'file') ?? []
)

function formatTime(dateStr?: string | null) {
  if (!dateStr) return '-'
  const d = new Date(dateStr)
  return d.toLocaleString('zh-CN', { hour12: false })
}

function formatSize(size?: number) {
  if (!size || size <= 0) return '-'
  const units = ['B', 'KB', 'MB', 'GB']
  let s = size, i = 0
  while (s >= 1024 && i < units.length - 1) { s /= 1024; i++ }
  return `${s.toFixed(1)} ${units[i]}`
}

function downloadFile(att: MessageAttachmentDto) {
  const url = buildFileDownloadUrl(String(att.fileId))
  window.open(url, '_blank')
}

function openLink(url: string) {
  if (url.startsWith('http')) {
    window.open(url, '_blank')
  } else {
    router.push(url)
  }
}

async function loadData() {
  const id = String(route.query.id || '')
  if (!id) { ElMessage.warning('缺少通知ID'); return }
  try {
    loading.value = true
    detail.value = await getMessageDetail(id)

    // 如果是未读消息，自动标记为已读
    if (detail.value && !detail.value.isRead) {
      try {
        await markMessageAsRead(id)
        detail.value.isRead = true
        await notificationStore.markAsRead(id)
      } catch (e) {
        // silently ignored
      }
    }
  } catch (e) {
    ElMessage.error('加载消息详情失败')
  } finally {
    loading.value = false
  }
}

onMounted(loadData)
</script>

<style scoped>
.page-container {
  padding: 0;
}

.page-header {
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

.notifications-card {
  border-radius: 12px;
  border: 1px solid #e5e7eb;
}

.notification-detail-wrapper { padding: 16px; }
.detail-title { font-size: 20px; font-weight: 600; margin: 0 0 8px 0; }
.detail-meta { display: flex; align-items: center; gap: 12px; color: #6b7280; margin-bottom: 12px; }
.detail-content { white-space: pre-wrap; line-height: 1.7; color: #374151; margin-top: 12px; }
.attachments { margin-top: 16px; }
.attachments-title { font-size: 16px; font-weight: 600; margin: 12px 0; }
.img-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(120px, 1fr)); gap: 8px; }
.img-item { width: 100%; height: 120px; border-radius: 8px; overflow: hidden; }

.links-section { margin-top: 16px; }
.links-title { font-size: 16px; font-weight: 600; margin: 12px 0; }
.link-list { display: flex; flex-direction: column; gap: 8px; }
.link-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border-radius: 6px;
  background: #f9fafb;
  cursor: pointer;
  transition: background 0.2s;
}
.link-item:hover { background: #f3f4f6; }
.link-item i { color: #6b7280; font-size: 16px; }
.link-text { color: #2563eb; font-size: 14px; }

.notification-time {
  font-size: 12px;
  color: #9ca3af;
}
</style>
