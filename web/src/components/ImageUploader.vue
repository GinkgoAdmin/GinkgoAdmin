<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="image-uploader">
    <!-- 已选图片列表 -->
    <div class="image-list" ref="imageListRef">
      <div 
        v-for="(file, index) in fileList" 
        :key="file.uid" 
        class="image-item"
        :class="{ 'is-avatar': index === 0 && showAvatarBadge }"
        draggable="true"
        @dragstart="handleDragStart($event, index)"
        @dragover.prevent="handleDragOver($event, index)"
        @drop="handleDrop($event, index)"
        @dragend="handleDragEnd"
      >
        <img :src="file.url" :alt="file.name" class="image-thumbnail" />
        <div v-if="index === 0 && showAvatarBadge" class="avatar-badge">头像</div>
        <div class="image-actions">
          <el-icon class="action-icon" @click="handlePreview(file)"><ZoomIn /></el-icon>
          <el-icon v-if="!disabled" class="action-icon" @click="handleRemove(index)"><Delete /></el-icon>
        </div>
        <div v-if="draggable && !disabled" class="drag-hint">
          <el-icon><Rank /></el-icon>
        </div>
      </div>
      
      <!-- 添加按钮 -->
      <div 
        v-if="!disabled && fileList.length < limit" 
        class="image-add"
        @click="openFileSelector"
      >
        <el-icon><Plus /></el-icon>
      </div>
    </div>

    <!-- 提示文字 -->
    <div class="el-upload__tip">
      支持 {{ acceptTip }} 格式，单张不超过 {{ computedMaxSizeMB }}MB，最多 {{ limit }} 张
      <span v-if="draggable && showAvatarBadge">（拖拽调整顺序，第一张为头像）</span>
      <span v-else-if="draggable">（可拖拽调整顺序）</span>
    </div>

    <!-- 图片预览 -->
    <el-dialog v-model="previewVisible" title="图片预览" width="600px">
      <img :src="previewUrl" style="width: 100%" alt="预览图片" />
    </el-dialog>

    <!-- 文件选择器（附件库） -->
    <FileSelector
      v-model="fileSelectorVisible"
      title="选择图片"
      :multiple="limit > 1"
      accept="image/*"
      :max-size="computedMaxSizeMB"
      @confirm="onFilesSelected"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * 多图片选择组件
 * 点击加号打开附件库，支持从已上传的图片中选择或上传新图片
 * 支持拖拽排序，第一张默认为头像
 * 
 * 使用示例:
 * <ImageUploader v-model="photos" :limit="9" :draggable="true" :show-avatar-badge="true" />
 * 
 * 数据格式:
 * "/uploads/2026/02/06/xxx.jpg,/uploads/2026/02/07/yyy.png" (逗号分隔的相对URL路径)
 */
import { ref, watch, computed, onMounted } from 'vue'
import { Plus, Delete, ZoomIn, Rank } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { type FileListItemDto } from '@/api/files'
import http from '@/api/http'
import FileSelector from '@/components/FileSelector.vue'
import { resolveFileUrl, fetchResourceConfig } from '@/utils/resourceUrl'

interface FileItem {
  uid: number
  name: string
  url: string
  response?: string  // 相对URL路径（如 /uploads/2026/02/06/xxx.jpg）
}

const props = withDefaults(defineProps<{
  modelValue?: string  // 逗号分隔的文件ID字符串
  limit?: number
  maxSizeMB?: number   // 如果不传，从系统配置读取
  accept?: string      // 如果不传，从系统配置读取图片格式
  disabled?: boolean
  draggable?: boolean  // 是否支持拖拽排序
  showAvatarBadge?: boolean  // 是否显示头像标记（第一张）
}>(), {
  limit: 9,
  disabled: false,
  draggable: true,
  showAvatarBadge: false
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
  (e: 'change', value: string): void
}>()

const fileList = ref<FileItem[]>([])
const previewVisible = ref(false)
const previewUrl = ref('')
const fileSelectorVisible = ref(false)
const imageListRef = ref<HTMLElement>()

// 拖拽状态
const dragIndex = ref<number | null>(null)
const dragOverIndex = ref<number | null>(null)

// 系统配置
const systemMaxSizeMB = ref(20)
const systemAllowedExtensions = ref('.jpg,.png,.gif,.jpeg')

// 计算实际使用的配置
const computedMaxSizeMB = computed(() => props.maxSizeMB ?? systemMaxSizeMB.value)

const acceptTip = computed(() => {
  const exts = systemAllowedExtensions.value.split(',')
    .map(e => e.trim().replace('.', ''))
    .filter(e => ['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp'].includes(e.toLowerCase()))
  return exts.length > 0 ? exts.join('/') : 'jpg/png/gif'
})

// 加载系统配置
async function loadSystemConfig() {
  try {
    const list = await http.get<any, Array<{ key: string; value?: string }>>('/v1/settings')
    const map = new Map<string, string>((list || []).map(it => [it.key, it.value ?? '']))
    
    const maxSize = map.get('Upload.MaxSizeMB')
    if (maxSize) {
      systemMaxSizeMB.value = parseInt(maxSize) || 20
    }
    
    const allowedExts = map.get('Upload.AllowedExtensions')
    if (allowedExts) {
      systemAllowedExtensions.value = allowedExts
    }
  } catch (error) {
    // silently ignored - using defaults
  }
}

// 监听外部值变化，解析为文件列表
watch(() => props.modelValue, (newVal) => {
  if (!newVal || typeof newVal !== 'string') {
    fileList.value = []
    return
  }
  
  // 解析逗号分隔的相对URL路径
  const urls = newVal.split(',').filter(url => url.trim())
  
  // 转换为 FileItem 格式
  fileList.value = urls.map((url, index) => {
    const relativeUrl = url.trim()
    // 根据路径特征判断 storageProvider 并构建完整显示 URL
    const isLocal = relativeUrl.startsWith('/uploads/') || relativeUrl.startsWith('/api/')
    const displayUrl = resolveFileUrl({
      url: relativeUrl,
      storageProvider: isLocal ? 'Local' : 'OssLibFileStorage'
    })
    
    return {
      uid: Date.now() + index,
      name: `图片${index + 1}`,
      url: displayUrl,
      response: relativeUrl  // 存储相对路径
    }
  })
}, { immediate: true })

// 拖拽开始
function handleDragStart(e: DragEvent, index: number) {
  if (!props.draggable || props.disabled) return
  dragIndex.value = index
  if (e.dataTransfer) {
    e.dataTransfer.effectAllowed = 'move'
  }
  // 添加拖拽样式
  const target = e.target as HTMLElement
  target.classList.add('dragging')
}

// 拖拽经过
function handleDragOver(e: DragEvent, index: number) {
  if (!props.draggable || props.disabled || dragIndex.value === null) return
  e.preventDefault()
  dragOverIndex.value = index
}

// 放置
function handleDrop(e: DragEvent, index: number) {
  if (!props.draggable || props.disabled || dragIndex.value === null) return
  e.preventDefault()
  
  const fromIndex = dragIndex.value
  const toIndex = index
  
  if (fromIndex !== toIndex) {
    // 移动元素
    const item = fileList.value.splice(fromIndex, 1)[0]
    fileList.value.splice(toIndex, 0, item)
    emitValue()
  }
  
  dragIndex.value = null
  dragOverIndex.value = null
}

// 拖拽结束
function handleDragEnd(e: DragEvent) {
  const target = e.target as HTMLElement
  target.classList.remove('dragging')
  dragIndex.value = null
  dragOverIndex.value = null
}

// 打开文件选择器
function openFileSelector() {
  fileSelectorVisible.value = true
}

// 从附件库选择图片
function onFilesSelected(files: FileListItemDto[]) {
  if (files.length === 0) return
  
  // 检查是否超出限制
  const currentCount = fileList.value.length
  const remainingSlots = props.limit - currentCount
  
  if (remainingSlots <= 0) {
    ElMessage.warning(`最多只能选择 ${props.limit} 张图片`)
    return
  }
  
  // 只添加剩余可用数量的图片
  const filesToAdd = files.slice(0, remainingSlots)
  
  // 添加选中的文件到列表
  filesToAdd.forEach((file, index) => {
    // 从完整URL中提取相对路径
    const relativeUrl = extractRelativeUrl(file.url || '')
    if (!relativeUrl) {
      return
    }
    
    // 检查是否已存在
    const exists = fileList.value.some(f => f.response === relativeUrl)
    if (!exists) {
      fileList.value.push({
        uid: Date.now() + index + Math.random() * 1000,
        name: file.fileName || `图片${currentCount + index + 1}`,
        url: file.url || resolveFileUrl({ id: file.id, url: file.url, storageProvider: file.storageProvider }),
        response: relativeUrl  // 存储相对路径
      })
    }
  })
  
  emitValue()
  ElMessage.success(`已添加 ${filesToAdd.length} 张图片`)
}

// 从完整URL中提取相对路径
function extractRelativeUrl(fullUrl: string): string {
  if (!fullUrl) return ''
  
  // 如果已经是相对路径，直接返回
  if (fullUrl.startsWith('/')) {
    return fullUrl
  }
  
  // 从完整URL中提取路径部分
  try {
    const url = new URL(fullUrl)
    return url.pathname  // 返回路径部分，如 /uploads/2026/02/06/xxx.jpg
  } catch {
    // 如果解析失败，尝试简单提取
    const match = fullUrl.match(/\/uploads\/.*$/)
    return match ? match[0] : fullUrl
  }
}

// 删除图片
function handleRemove(index: number) {
  fileList.value.splice(index, 1)
  emitValue()
}

// 预览图片
function handlePreview(file: FileItem) {
  previewUrl.value = file.url
  previewVisible.value = true
}

// 发送值变化
function emitValue() {
  const ids = fileList.value
    .map(file => file.response)
    .filter(id => id)
  
  const value = ids.join(',')
  emit('update:modelValue', value)
  emit('change', value)
}

onMounted(async () => {
  await fetchResourceConfig().catch(() => {})
  loadSystemConfig()
})
</script>

<style scoped>
.image-uploader {
  width: 100%;
}

.image-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.image-item {
  position: relative;
  width: 100px;
  height: 100px;
  border-radius: 6px;
  overflow: hidden;
  border: 1px solid var(--el-border-color);
  cursor: grab;
  transition: transform 0.2s, box-shadow 0.2s;
}

.image-item:active {
  cursor: grabbing;
}

.image-item.dragging {
  opacity: 0.5;
  transform: scale(1.05);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
}

.image-item.is-avatar {
  border: 2px solid var(--el-color-primary);
}

.avatar-badge {
  position: absolute;
  top: 0;
  left: 0;
  background: var(--el-color-primary);
  color: #fff;
  font-size: 10px;
  padding: 2px 6px;
  border-radius: 0 0 6px 0;
}

.image-thumbnail {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.image-actions {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  opacity: 0;
  transition: opacity 0.2s;
}

.image-item:hover .image-actions {
  opacity: 1;
}

.action-icon {
  color: #fff;
  font-size: 18px;
  cursor: pointer;
  transition: transform 0.2s;
}

.action-icon:hover {
  transform: scale(1.2);
}

.drag-hint {
  position: absolute;
  bottom: 4px;
  right: 4px;
  color: rgba(255, 255, 255, 0.7);
  font-size: 14px;
  opacity: 0;
  transition: opacity 0.2s;
}

.image-item:hover .drag-hint {
  opacity: 1;
}

.image-add {
  width: 100px;
  height: 100px;
  border: 1px dashed var(--el-border-color);
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
  background: var(--el-fill-color-lighter);
}

.image-add:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}

.image-add .el-icon {
  font-size: 24px;
  color: var(--el-text-color-placeholder);
}

.image-add:hover .el-icon {
  color: var(--el-color-primary);
}

.el-upload__tip {
  color: var(--el-text-color-placeholder);
  font-size: 12px;
  margin-top: 8px;
}
</style>
