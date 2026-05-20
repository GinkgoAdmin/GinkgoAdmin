<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <el-dialog
    v-model="visible"
    :title="title"
    width="80vw"
    top="5vh"
    :close-on-click-modal="false"
    destroy-on-close
    append-to-body
    class="file-selector-dialog"
    @closed="handleClosed"
  >
    <div class="file-selector-container">
      <!-- 工具栏 -->
      <div class="toolbar">
        <div class="toolbar-left">
          <el-button-group>
            <el-button 
              :type="viewMode === 'grid' ? 'primary' : 'default'" 
              @click="viewMode = 'grid'"
              size="small"
            >
              <el-icon><Grid /></el-icon>
              网格
            </el-button>
            <el-button 
              :type="viewMode === 'list' ? 'primary' : 'default'" 
              @click="viewMode = 'list'"
              size="small"
            >
              <el-icon><List /></el-icon>
              列表
            </el-button>
          </el-button-group>
          
          <el-select v-model="fileTypeFilter" placeholder="文件类型" size="small" style="width: 120px; margin-left: 12px;">
            <el-option label="全部" value="" />
            <el-option label="图片" value="image" />
            <el-option label="视频" value="video" />
            <el-option label="音频" value="audio" />
            <el-option label="文档" value="document" />
          </el-select>
        </div>
        
        <div class="toolbar-right">
          <el-upload
            :action="uploadAction"
            :headers="uploadHeaders"
            :data="uploadData"
            :before-upload="beforeUpload"
            :on-success="onUploadSuccess"
            :on-error="onUploadError"
            :show-file-list="true"
            :http-request="customUpload"
            multiple
            :accept="acceptTypes"
            class="file-selector-upload"
          >
            <el-button type="primary" size="small">
              <el-icon><Upload /></el-icon>
              上传文件
            </el-button>
          </el-upload>
          
          <el-button @click="refreshFiles" size="small" :loading="loading">
            <el-icon><Refresh /></el-icon>
            刷新
          </el-button>
        </div>
      </div>

      <!-- 文件列表区域 -->
      <div class="file-content">
        <div v-if="loading" class="loading-container">
          <el-skeleton :rows="6" animated />
        </div>
        
        <div v-else-if="files.length === 0" class="empty-container">
          <el-empty description="暂无文件">
            <el-upload
              :action="uploadAction"
              :headers="uploadHeaders"
              :data="uploadData"
              :before-upload="beforeUpload"
              :on-success="onUploadSuccess"
              :on-error="onUploadError"
              :show-file-list="true"
              :http-request="customUpload"
              multiple
              :accept="acceptTypes"
              class="file-selector-upload"
            >
              <el-button type="primary">上传第一个文件</el-button>
            </el-upload>
          </el-empty>
        </div>
        
        <!-- 网格视图 -->
        <div v-else-if="viewMode === 'grid'" class="file-grid">
          <div
            v-for="file in filteredFiles"
            :key="file.id"
            class="file-card"
            :class="{ selected: isSelected(file.id) }"
            @click="toggleSelection(file)"
          >
            <div class="file-preview">
              <!-- 图片预览 -->
              <img
                v-if="isImageFile(file.contentType, file.fileName)"
                :src="getFilePreviewUrl(file)"
                :alt="file.fileName"
                class="file-thumbnail"
                @error="onImageError"
              />
              <!-- 视频预览 -->
              <div v-else-if="isVideoFile(file.contentType, file.fileName)" class="video-preview">
                <el-icon class="file-icon video-icon"><VideoPlay /></el-icon>
                <span class="file-type-text">视频</span>
              </div>
              <!-- 音频预览 -->
              <div v-else-if="isAudioFile(file.contentType, file.fileName)" class="audio-preview">
                <el-icon class="file-icon audio-icon"><Headset /></el-icon>
                <span class="file-type-text">音频</span>
              </div>
              <!-- 其他文件 -->
              <div v-else class="file-preview-default">
                <el-icon class="file-icon"><Document /></el-icon>
                <span class="file-extension">{{ getFileExtension(file.fileName) }}</span>
              </div>
              
              <!-- 选中标识 -->
              <div v-if="isSelected(file.id)" class="selection-overlay">
                <el-icon class="selection-icon"><Check /></el-icon>
              </div>
            </div>
            
            <div class="file-info">
              <div class="file-name" :title="file.fileName">{{ file.fileName }}</div>
              <div class="file-meta">
                <span class="file-size">{{ formatFileSize(file.size) }}</span>
                <span class="file-date">{{ formatDate(file.createdAt) }}</span>
              </div>
            </div>
          </div>
        </div>
        
        <!-- 列表视图 -->
        <div v-else class="file-list">
          <el-table
            :data="filteredFiles"
            @row-click="toggleSelection"
            row-class-name="file-row"
          >
            <el-table-column width="50">
              <template #default="{ row }">
                <el-checkbox
                  :model-value="isSelected(row.id)"
                  @change="toggleSelection(row)"
                />
              </template>
            </el-table-column>
            
            <el-table-column label="文件名" min-width="200">
              <template #default="{ row }">
                <div class="file-name-cell">
                  <el-icon class="file-type-icon">
                    <Picture v-if="isImageFile(row.contentType, row.fileName)" />
                    <VideoPlay v-else-if="isVideoFile(row.contentType, row.fileName)" />
                    <Headset v-else-if="isAudioFile(row.contentType, row.fileName)" />
                    <Document v-else />
                  </el-icon>
                  <span class="file-name">{{ row.fileName }}</span>
                </div>
              </template>
            </el-table-column>
            
            <el-table-column label="大小" width="100">
              <template #default="{ row }">
                {{ formatFileSize(row.size) }}
              </template>
            </el-table-column>
            
            <el-table-column label="类型" width="120">
              <template #default="{ row }">
                <el-tag size="small" :type="getFileTypeTagType(row)">
                  {{ getFileTypeText(row) }}
                </el-tag>
              </template>
            </el-table-column>
            
            <el-table-column label="上传时间" width="150">
              <template #default="{ row }">
                {{ formatDate(row.createdAt) }}
              </template>
            </el-table-column>
          </el-table>
        </div>
      </div>

      <!-- 分页 -->
      <div v-if="total > 0" class="pagination-container">
        <el-pagination
          v-model:current-page="currentPage"
          v-model:page-size="pageSize"
          :total="total"
          :page-sizes="[20, 50, 100]"
          layout="total, sizes, prev, pager, next, jumper"
          @size-change="loadFiles"
          @current-change="loadFiles"
        />
      </div>
    </div>

    <template #footer>
      <div class="dialog-footer">
        <div class="selected-info">
          <span v-if="selectedFiles.length > 0">
            已选择 {{ selectedFiles.length }} 个文件
          </span>
        </div>
        <div class="dialog-actions">
          <el-button @click="handleCancel">取消</el-button>
          <el-button 
            type="primary" 
            @click="handleConfirm"
            :disabled="selectedFiles.length === 0"
          >
            确定选择
          </el-button>
        </div>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import {
  Grid, List, Upload, Refresh, VideoPlay, Headset, Document, Picture, Check
} from '@element-plus/icons-vue'
import {
  getFiles,
  uploadFile,
  formatFileSize,
  isImageFile,
  isVideoFile,
  isAudioFile,
  buildFileContentUrl,
  type FileListItemDto
} from '../api/files'
import { useWebAuthStore } from '../stores/webAuth'
import { API_BASE_URL } from '../config/env'
import { fetchResourceConfig } from '@/utils/resourceUrl'

interface Props {
  modelValue: boolean
  title?: string
  multiple?: boolean
  accept?: string
  maxSize?: number // MB
  fileType?: string
  dataScope?: string // 数据范围: Self / Dept / DeptAndChildren / All
}

interface Emits {
  (e: 'update:modelValue', value: boolean): void
  (e: 'confirm', files: FileListItemDto[]): void
  (e: 'cancel'): void
}

const props = withDefaults(defineProps<Props>(), {
  title: '选择文件',
  multiple: true,
  maxSize: 100,
  fileType: 'default',
  dataScope: ''
})

const emit = defineEmits<Emits>()

const webAuth = useWebAuthStore()

// 响应式数据
const visible = computed({
  get: () => props.modelValue,
  set: (value) => emit('update:modelValue', value)
})

const loading = ref(false)
const viewMode = ref<'grid' | 'list'>('grid')
const fileTypeFilter = ref('')
const currentPage = ref(1)
const pageSize = ref(20)
const total = ref(0)
const files = ref<FileListItemDto[]>([])
const selectedFiles = ref<FileListItemDto[]>([])

// 上传相关
const uploadAction = `${API_BASE_URL}/v1/files/upload`
const uploadHeaders = computed(() => ({
  'Authorization': `Bearer ${webAuth.token}`
}))
const uploadData = computed(() => ({
  type: props.fileType
}))

// 统一走 @/api/files 的 uploadFile()，与文件管理页 / 其他业务保持唯一上传入口；
// OSS 路由由后端 /v1/files/upload 根据存储策略决定（启用 OSS 时自动落 OSS Provider）。
// 进度通过 el-upload 的 onProgress(event, file) 回调反馈给内置文件列表的进度条与百分比文字。
const customUpload = async (options: any) => {
  const { file, onProgress, onSuccess, onError } = options
  try {
    const id = await uploadFile(file, props.fileType || undefined, undefined, (percent) => {
      // el-upload 期望 ProgressEvent 风格对象，至少含 percent 字段
      try {
        onProgress?.({ percent } as any, file)
      } catch { /* 忽略进度回调异常 */ }
    })
    // 标记 100% 完成
    try { onProgress?.({ percent: 100 } as any, file) } catch {}
    onSuccess(id, file)
    ElMessage.success(`「${file.name}」上传完成`)
    loadFiles()
  } catch (error: any) {
    onError(error)
    ElMessage.error(error?.message || `「${file.name}」上传失败`)
  }
}

// 计算属性
const acceptTypes = computed(() => {
  if (props.accept) return props.accept
  return '*/*'
})

// 根据 accept 属性判断文件是否可选
const isFileAccepted = (file: FileListItemDto): boolean => {
  if (!props.accept || props.accept === '*/*') return true
  
  const acceptList = props.accept.split(',').map(t => t.trim())
  const contentType = file.contentType || ''
  const fileName = (file.fileName || '').toLowerCase()
  
  return acceptList.some(accept => {
    // 处理 MIME 类型通配符，如 image/*
    if (accept.includes('*')) {
      const pattern = accept.replace('*', '.*')
      return new RegExp(pattern).test(contentType)
    }
    // 处理扩展名，如 .jpg
    if (accept.startsWith('.')) {
      return fileName.endsWith(accept.toLowerCase())
    }
    // 精确匹配 MIME 类型
    return contentType === accept
  })
}

const filteredFiles = computed(() => {
  // 首先根据 accept 属性过滤
  let result = files.value.filter(isFileAccepted)
  
  // 然后根据用户选择的类型过滤
  if (!fileTypeFilter.value) return result
  
  return result.filter(file => {
    switch (fileTypeFilter.value) {
      case 'image':
        return isImageFile(file.contentType, file.fileName)
      case 'video':
        return isVideoFile(file.contentType, file.fileName)
      case 'audio':
        return isAudioFile(file.contentType, file.fileName)
      case 'document':
        return !isImageFile(file.contentType, file.fileName) && 
               !isVideoFile(file.contentType, file.fileName) && 
               !isAudioFile(file.contentType, file.fileName)
      default:
        return true
    }
  })
})

// 方法
const loadFiles = async () => {
  loading.value = true
  try {
    const result = await getFiles({
      page: currentPage.value,
      pageSize: pageSize.value,
      type: props.fileType === 'default' ? undefined : props.fileType,
      dataScope: props.dataScope || undefined
    })
    files.value = result.items
    total.value = result.total
  } catch (error) {
    ElMessage.error('加载文件列表失败')
  } finally {
    loading.value = false
  }
}

const refreshFiles = () => {
  loadFiles()
}

const isSelected = (fileId: string): boolean => {
  return selectedFiles.value.some(f => f.id === fileId)
}

const toggleSelection = (file: FileListItemDto) => {
  const index = selectedFiles.value.findIndex(f => f.id === file.id)
  if (index > -1) {
    selectedFiles.value.splice(index, 1)
  } else {
    if (props.multiple) {
      selectedFiles.value.push(file)
    } else {
      selectedFiles.value = [file]
    }
  }
}

const getFileExtension = (fileName: string): string => {
  const ext = fileName.split('.').pop()?.toUpperCase()
  return ext || 'FILE'
}

const getFileTypeText = (file: FileListItemDto): string => {
  if (isImageFile(file.contentType, file.fileName)) return '图片'
  if (isVideoFile(file.contentType, file.fileName)) return '视频'
  if (isAudioFile(file.contentType, file.fileName)) return '音频'
  return '文档'
}

const getFileTypeTagType = (file: FileListItemDto): string => {
  if (isImageFile(file.contentType, file.fileName)) return 'success'
  if (isVideoFile(file.contentType, file.fileName)) return 'warning'
  if (isAudioFile(file.contentType, file.fileName)) return 'info'
  return 'default'
}

const formatDate = (dateString: string): string => {
  const date = new Date(dateString)
  return date.toLocaleDateString('zh-CN') + ' ' + date.toLocaleTimeString('zh-CN', { 
    hour: '2-digit', 
    minute: '2-digit' 
  })
}

const onImageError = (event: Event) => {
  const img = event.target as HTMLImageElement
  img.style.display = 'none'
}

/** 根据文件对象构建预览 URL，使用 API 内容代理端点以避免直接依赖 CDN 可达性 */
const getFilePreviewUrl = (file: FileListItemDto): string => {
  // 使用 API content 端点，后端直接代理内容（本地镜像优先，然后 OSS API）
  // 避免前端内嵌预览直接依赖 CDN URL 可达性，保证缩略图始终可见
  return buildFileContentUrl(file.id)
}

// 上传相关方法
const beforeUpload = (file: File) => {
  // 检查文件大小
  if (file.size > props.maxSize * 1024 * 1024) {
    ElMessage.error(`文件大小不能超过 ${props.maxSize}MB`)
    return false
  }
  
  // 检查文件类型
  if (props.accept && props.accept !== '*/*') {
    const acceptTypes = props.accept.split(',').map(t => t.trim())
    const fileType = file.type
    const fileName = file.name.toLowerCase()
    
    const isAccepted = acceptTypes.some(accept => {
      if (accept.startsWith('.')) {
        return fileName.endsWith(accept.toLowerCase())
      }
      if (accept.includes('*')) {
        const pattern = accept.replace('*', '.*')
        return new RegExp(pattern).test(fileType)
      }
      return fileType === accept
    })
    
    if (!isAccepted) {
      ElMessage.error('文件类型不符合要求')
      return false
    }
  }
  
  return true
}

const onUploadSuccess = (response: any, file: any, fileList: any) => {
  // 自定义上传方法已处理，这里仅作为回调占位
}

const onUploadError = (error: any, file: any, fileList: any) => {
  // 自定义上传方法已处理，这里仅作为回调占位
}

// 事件处理
const handleConfirm = () => {
  emit('confirm', selectedFiles.value)
  visible.value = false
}

const handleCancel = () => {
  emit('cancel')
  visible.value = false
}

const handleClosed = () => {
  selectedFiles.value = []
  currentPage.value = 1
  fileTypeFilter.value = ''
}

// 监听
watch(visible, async (newValue) => {
  if (newValue) {
    await fetchResourceConfig().catch(() => {})
    loadFiles()
  }
})

onMounted(async () => {
  await fetchResourceConfig().catch(() => {})
  if (visible.value) {
    loadFiles()
  }
})
</script>

<style scoped>
/* 对话框样式 */
.file-selector-dialog :deep(.el-dialog__body) {
  padding: 0;
}

.file-selector-dialog :deep(.el-dialog) {
  border-radius: 12px;
  overflow: hidden;
}

.file-selector-dialog :deep(.el-dialog__header) {
  padding: 1.25rem 1.5rem;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: var(--el-bg-color);
}

.file-selector-dialog :deep(.el-dialog__title) {
  font-size: 1.125rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.file-selector-container {
  height: 80vh;
  max-height: calc(90vh - 5vh - 60px); /* 90vh - top距离 - 对话框头部高度 */
  display: flex;
  flex-direction: column;
  background: var(--el-bg-color-page);
}

/* 工具栏样式 */
.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem 1.5rem;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: var(--el-bg-color);
  backdrop-filter: blur(10px);
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.toolbar-right {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

/* el-upload 内置文件列表浮动展示，不挤占工具栏布局；展开后显示进度条 */
.toolbar-right :deep(.file-selector-upload) {
  position: relative;
}
.toolbar-right :deep(.file-selector-upload .el-upload-list) {
  position: absolute;
  top: calc(100% + 6px);
  right: 0;
  width: 320px;
  max-height: 260px;
  overflow-y: auto;
  margin: 0;
  padding: 6px 8px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 4px;
  box-shadow: 0 4px 12px rgba(0,0,0,.08);
  z-index: 20;
}
.toolbar-right :deep(.file-selector-upload .el-upload-list:empty) {
  display: none;
}

/* 文件内容区域 */
.file-content {
  flex: 1;
  overflow: auto;
  padding: 1.5rem;
  background: var(--el-bg-color-page);
}

.file-content::-webkit-scrollbar {
  width: 8px;
  height: 8px;
}

.file-content::-webkit-scrollbar-track {
  background: var(--el-fill-color-lighter);
  border-radius: 4px;
}

.file-content::-webkit-scrollbar-thumb {
  background: var(--el-fill-color-dark);
  border-radius: 4px;
}

.file-content::-webkit-scrollbar-thumb:hover {
  background: var(--el-fill-color);
}

/* 加载状态 */
.loading-container {
  padding: 2rem;
}

/* 空状态 */
.empty-container {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  min-height: 400px;
}

/* 网格视图样式 */
.file-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 1.25rem;
  animation: fadeIn 0.3s ease-in-out;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.file-card {
  border: 2px solid var(--el-border-color-light);
  border-radius: 12px;
  overflow: hidden;
  cursor: pointer;
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  background: var(--el-bg-color);
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
  position: relative;
}

.file-card::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.05) 0%, rgba(147, 51, 234, 0.05) 100%);
  opacity: 0;
  transition: opacity 0.3s ease;
  pointer-events: none;
  z-index: 0;
}

.file-card:hover {
  border-color: var(--el-color-primary);
  box-shadow: 0 8px 24px rgba(59, 130, 246, 0.15), 0 4px 12px rgba(59, 130, 246, 0.1);
  transform: translateY(-4px);
}

.file-card:hover::before {
  opacity: 1;
}

.file-card.selected {
  border-color: var(--el-color-primary);
  box-shadow: 0 8px 24px rgba(59, 130, 246, 0.25), 0 4px 12px rgba(59, 130, 246, 0.15);
  transform: translateY(-2px);
}

.file-card.selected::before {
  opacity: 1;
}

.file-preview {
  position: relative;
  height: 140px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--el-fill-color-lighter);
  overflow: hidden;
}

.file-thumbnail {
  width: 100%;
  height: 100%;
  object-fit: cover;
  transition: transform 0.3s ease;
}

.file-card:hover .file-thumbnail {
  transform: scale(1.05);
}

/* 文件类型预览 */
.video-preview,
.audio-preview,
.file-preview-default {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  gap: 0.75rem;
  position: relative;
}

.video-preview {
  background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%);
  color: white;
}

.audio-preview {
  background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%);
  color: white;
}

.file-preview-default {
  background: linear-gradient(135deg, var(--el-fill-color) 0%, var(--el-fill-color-light) 100%);
}

.file-icon {
  font-size: 2.5rem;
  transition: transform 0.3s ease;
}

.file-card:hover .file-icon {
  transform: scale(1.1);
}

.video-icon {
  color: #fbbf24;
}

.audio-icon {
  color: #a78bfa;
}

.file-type-text {
  font-size: 0.8125rem;
  opacity: 0.9;
  font-weight: 500;
}

.file-extension {
  font-size: 0.8125rem;
  font-weight: 600;
  color: var(--el-text-color-primary);
  background: var(--el-bg-color);
  padding: 0.375rem 0.75rem;
  border-radius: 6px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
}

/* 选中状态覆盖层 */
.selection-overlay {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.9) 0%, rgba(147, 51, 234, 0.9) 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  animation: overlayFadeIn 0.2s ease-in-out;
}

@keyframes overlayFadeIn {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}

.selection-icon {
  font-size: 2.5rem;
  color: white;
  animation: checkBounce 0.3s ease-in-out;
}

@keyframes checkBounce {
  0% {
    transform: scale(0);
  }
  50% {
    transform: scale(1.2);
  }
  100% {
    transform: scale(1);
  }
}

/* 文件信息 */
.file-info {
  padding: 1rem;
  background: var(--el-bg-color);
  position: relative;
  z-index: 1;
}

.file-name {
  font-weight: 500;
  font-size: 0.875rem;
  color: var(--el-text-color-primary);
  margin-bottom: 0.5rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  line-height: 1.4;
}

.file-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
  font-size: 0.75rem;
  color: var(--el-text-color-secondary);
  gap: 0.5rem;
}

.file-size,
.file-date {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

/* 列表视图样式 */
.file-list {
  animation: fadeIn 0.3s ease-in-out;
}

.file-list :deep(.el-table) {
  border: none;
  background: transparent;
}

.file-list :deep(.el-table__header) {
  background: var(--el-fill-color-lighter);
}

.file-list :deep(.el-table th) {
  background: var(--el-fill-color-lighter);
  color: var(--el-text-color-primary);
  font-weight: 600;
}

.file-list :deep(.file-row) {
  cursor: pointer;
  transition: all 0.2s ease;
}

.file-list :deep(.file-row:hover) {
  background-color: var(--el-fill-color-light);
  transform: translateX(4px);
}

.file-list :deep(.file-row.selected) {
  background-color: var(--el-color-primary-light-9);
}

.file-name-cell {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.file-type-icon {
  font-size: 1.5rem;
  color: var(--el-text-color-secondary);
  transition: transform 0.2s ease;
}

.file-list :deep(.file-row:hover) .file-type-icon {
  transform: scale(1.1);
}

/* 分页样式 */
.pagination-container {
  padding: 1.25rem 1.5rem;
  border-top: 1px solid var(--el-border-color-lighter);
  background: var(--el-bg-color);
  display: flex;
  justify-content: center;
}

.pagination-container :deep(.el-pagination) {
  gap: 0.5rem;
}

/* 底部样式 */
.dialog-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.25rem 1.5rem;
  border-top: 1px solid var(--el-border-color-lighter);
  background: var(--el-bg-color);
}

.selected-info {
  font-size: 0.875rem;
  color: var(--el-text-color-secondary);
  font-weight: 500;
}

.selected-info span {
  color: var(--el-color-primary);
  font-weight: 600;
}

.dialog-actions {
  display: flex;
  gap: 0.75rem;
}

/* 响应式设计 */
@media (max-width: 768px) {
  .file-selector-dialog :deep(.el-dialog) {
    top: 3vh !important; /* 移动端距离顶部更近 */
  }

  .file-selector-container {
    height: 85vh;
    max-height: calc(94vh - 3vh - 60px); /* 移动端调整最大高度 */
  }

  .toolbar {
    flex-direction: column;
    gap: 1rem;
    padding: 1rem;
  }

  .toolbar-left,
  .toolbar-right {
    width: 100%;
    justify-content: space-between;
  }

  .file-content {
    padding: 1rem;
  }

  .file-grid {
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
    gap: 1rem;
  }

  .file-card {
    border-radius: 10px;
  }

  .file-preview {
    height: 120px;
  }

  .file-info {
    padding: 0.75rem;
  }

  .file-name {
    font-size: 0.8125rem;
  }

  .file-meta {
    font-size: 0.6875rem;
  }

  .pagination-container {
    padding: 1rem;
  }

  .pagination-container :deep(.el-pagination) {
    flex-wrap: wrap;
    justify-content: center;
  }

  .dialog-footer {
    flex-direction: column;
    gap: 1rem;
    padding: 1rem;
  }

  .selected-info {
    width: 100%;
    text-align: center;
  }

  .dialog-actions {
    width: 100%;
    justify-content: stretch;
  }

  .dialog-actions .el-button {
    flex: 1;
  }
}

@media (max-width: 480px) {
  .file-grid {
    grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
    gap: 0.75rem;
  }

  .file-preview {
    height: 100px;
  }

  .file-icon {
    font-size: 2rem;
  }

  .selection-icon {
    font-size: 2rem;
  }

  .file-info {
    padding: 0.5rem;
  }

  .file-name {
    font-size: 0.75rem;
  }

  .file-meta {
    font-size: 0.625rem;
    flex-direction: column;
    align-items: flex-start;
    gap: 0.25rem;
  }
}

/* 暗黑主题优化 - 使用 admin-dark 类避免影响前台 */
.admin-dark .file-card {
  background: var(--admin-surface);
  border-color: var(--admin-border);
}

.admin-dark .file-card:hover {
  border-color: var(--el-color-primary);
  box-shadow: 0 8px 24px rgba(59, 130, 246, 0.2), 0 4px 12px rgba(59, 130, 246, 0.15);
}

.admin-dark .file-preview {
  background: var(--admin-surface-2);
}

.admin-dark .file-info {
  background: var(--admin-surface);
}

.admin-dark .toolbar {
  background: var(--admin-surface);
  border-bottom-color: var(--admin-border);
}

.admin-dark .pagination-container {
  background: var(--admin-surface);
  border-top-color: var(--admin-border);
}

.admin-dark .dialog-footer {
  background: var(--admin-surface);
  border-top-color: var(--admin-border);
}

.admin-dark .file-extension {
  background: var(--admin-surface-2);
  color: var(--el-text-color-primary);
}

.admin-dark .file-list :deep(.file-row:hover) {
  background-color: var(--admin-menu-hover-bg);
}
</style>

