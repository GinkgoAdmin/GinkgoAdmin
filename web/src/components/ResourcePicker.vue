<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="resource-picker" :class="{ 'is-disabled': disabled, 'is-multiple': mode === 'multiple' }">
    <!-- 单选模式 -->
    <template v-if="mode === 'single'">
      <div class="rp-single">
        <el-input
          :model-value="modelValue"
          :placeholder="computedPlaceholder"
          :disabled="disabled"
          clearable
          @update:model-value="onInputChange"
          @clear="onClear"
        >
          <template #append>
            <el-button :disabled="disabled" @click="openSelector">
              <el-icon><Folder /></el-icon>
              <span class="rp-btn-text">附件库</span>
            </el-button>
          </template>
        </el-input>
        <!-- 图片预览 -->
        <div v-if="previewable && isImage && displayUrl" class="rp-preview">
          <img :src="displayUrl" :alt="placeholder || '预览'" @error="onPreviewError" />
        </div>
      </div>
    </template>

    <!-- 多选模式 -->
    <template v-else>
      <div class="rp-multiple">
        <div class="rp-file-list">
          <div
            v-for="(item, index) in fileItems"
            :key="item.uid"
            class="rp-file-item"
            :class="{ 'is-image': isImage }"
          >
            <!-- 图片缩略图 -->
            <img v-if="isImage" :src="item.displayUrl" :alt="item.name" class="rp-thumb" @error="onThumbError" />
            <!-- 非图片文件名 -->
            <span v-else class="rp-file-name" :title="item.path">{{ item.name }}</span>
            <!-- 删除按钮 -->
            <el-icon v-if="!disabled" class="rp-remove" @click="removeItem(index)"><Close /></el-icon>
          </div>
          <!-- 添加按钮 -->
          <div
            v-if="!disabled && fileItems.length < limit"
            class="rp-add-btn"
            @click="openSelector"
          >
            <el-icon><Plus /></el-icon>
          </div>
        </div>
        <div class="rp-tip">
          已选 {{ fileItems.length }} / {{ limit }}
          <span v-if="!disabled">，点击 + 从附件库选择</span>
        </div>
      </div>
    </template>

    <!-- 附件库选择器 -->
    <FileSelector
      v-model="selectorVisible"
      :title="selectorTitle"
      :multiple="mode === 'multiple'"
      :accept="accept"
      :data-scope="fileDataScope"
      @confirm="onFilesConfirmed"
    />
  </div>
</template>

<script setup lang="ts">
/**
 * 统一资源选择器组件
 *
 * 功能：
 * - 单选模式：输入框 + 附件库按钮 + 图片预览（适用于 LOGO、头像、封面等）
 * - 多选模式：文件列表 + 添加按钮（适用于图集、附件列表等）
 * - 支持直接输入 HTTP URL（外部资源）
 * - 附件库选择时自动提取相对路径存储
 * - 展示时通过 resolveResourcePath 动态拼接完整 URL
 *
 * 数据格式：
 * - 单选：字符串（相对路径或完整 HTTP URL）
 * - 多选：逗号分隔的字符串
 *
 * 使用示例：
 * <ResourcePicker v-model="form.logoUrl" accept="image/*" />
 * <ResourcePicker v-model="form.images" mode="multiple" accept="image/*" :limit="9" />
 * <ResourcePicker v-model="form.filePath" accept="all" placeholder="选择文件" />
 */
import { ref, computed, watch } from 'vue'
import { Folder, Close, Plus } from '@element-plus/icons-vue'
import FileSelector from './FileSelector.vue'
import type { FileListItemDto } from '../api/files'
import { resolveResourcePath } from '../utils/resourceUrl'
import { useAuthStore } from '../stores/auth'

interface FileItem {
  uid: number
  path: string      // 存储路径（相对路径或完整 URL）
  displayUrl: string // 展示用的完整 URL
  name: string       // 文件名
}

const props = withDefaults(defineProps<{
  /** 绑定值。单选为字符串，多选为逗号分隔字符串 */
  modelValue?: string
  /** 选择模式 */
  mode?: 'single' | 'multiple'
  /** MIME 类型过滤，如 'image/*'、'video/*'、'*\/*' */
  accept?: string
  /** 多选时最大数量限制 */
  limit?: number
  /** 是否禁用 */
  disabled?: boolean
  /** 输入框占位文字 */
  placeholder?: string
  /** 是否显示图片预览（仅图片类型有效） */
  previewable?: boolean
  /** 选择器弹窗标题 */
  title?: string
}>(), {
  modelValue: '',
  mode: 'single',
  accept: '*/*',
  limit: 9,
  disabled: false,
  placeholder: '',
  previewable: true,
  title: ''
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
  'change': [value: string]
}>()

// 附件库可见状态
const selectorVisible = ref(false)

// 权限控制：ADMIN 角色可查看所有附件
const auth = useAuthStore()
const isAdmin = computed(() => auth.roles.includes('ADMIN'))
const fileDataScope = computed(() => isAdmin.value ? 'All' : '')

/** 判断当前 accept 是否为图片类型 */
const isImage = computed(() => {
  return props.accept === 'image/*' || props.accept.startsWith('image/')
})

/** 选择器弹窗标题 */
const selectorTitle = computed(() => {
  if (props.title) return props.title
  if (isImage.value) return '选择图片'
  if (props.accept === 'video/*') return '选择视频'
  if (props.accept === 'audio/*') return '选择音频'
  return '选择文件'
})

/** 单选模式的占位文字 */
const computedPlaceholder = computed(() => {
  if (props.placeholder) return props.placeholder
  if (isImage.value) return '输入图片地址或从附件库选择'
  if (props.accept === 'video/*') return '输入视频地址或从附件库选择'
  return '输入文件地址或从附件库选择'
})

/** 单选模式下用于预览的完整 URL */
const displayUrl = computed(() => {
  return resolveResourcePath(props.modelValue || '')
})

// ================================================================
// 多选模式数据处理
// ================================================================

const fileItems = ref<FileItem[]>([])

/** 从逗号分隔的字符串解析为文件列表 */
function parseItems(value: string): FileItem[] {
  if (!value || typeof value !== 'string') return []
  return value.split(',').filter(s => s.trim()).map((path, index) => ({
    uid: Date.now() + index + Math.random() * 10000,
    path: path.trim(),
    displayUrl: resolveResourcePath(path.trim()),
    name: extractFileName(path.trim())
  }))
}

/** 从路径中提取文件名 */
function extractFileName(path: string): string {
  if (!path) return ''
  // 去掉查询参数
  const cleanPath = path.split('?')[0]
  const parts = cleanPath.split('/')
  return parts[parts.length - 1] || path
}

// 监听外部值变更
watch(() => props.modelValue, (newVal) => {
  if (props.mode === 'multiple') {
    fileItems.value = parseItems(newVal || '')
  }
}, { immediate: true })

// ================================================================
// 事件处理
// ================================================================

/** 单选模式输入框变更（支持手动输入 URL） */
function onInputChange(val: string) {
  emitValue(val)
}

/** 清空值 */
function onClear() {
  emitValue('')
}

/** 打开附件库选择器 */
function openSelector() {
  selectorVisible.value = true
}

/** 附件库选择确认 */
function onFilesConfirmed(files: FileListItemDto[]) {
  if (!files || files.length === 0) return

  if (props.mode === 'single') {
    // 单选：取第一个文件的相对路径
    const file = files[0]
    const path = file.url || ''
    emitValue(path)
  } else {
    // 多选：追加到现有列表
    const currentPaths = fileItems.value.map(item => item.path)
    const remaining = props.limit - fileItems.value.length
    const newFiles = files.slice(0, remaining)

    newFiles.forEach(file => {
      const path = file.url || ''
      if (path && !currentPaths.includes(path)) {
        fileItems.value.push({
          uid: Date.now() + Math.random() * 10000,
          path,
          displayUrl: resolveResourcePath(path),
          name: file.fileName || extractFileName(path)
        })
      }
    })
    emitMultipleValue()
  }
}

/** 多选模式删除某项 */
function removeItem(index: number) {
  fileItems.value.splice(index, 1)
  emitMultipleValue()
}

/** 发射单选值 */
function emitValue(val: string) {
  emit('update:modelValue', val)
  emit('change', val)
}

/** 发射多选值（逗号分隔） */
function emitMultipleValue() {
  const val = fileItems.value.map(item => item.path).join(',')
  emit('update:modelValue', val)
  emit('change', val)
}

/** 预览图片加载失败 */
function onPreviewError(e: Event) {
  const img = e.target as HTMLImageElement
  img.style.display = 'none'
}

/** 缩略图加载失败 */
function onThumbError(e: Event) {
  const img = e.target as HTMLImageElement
  img.src = ''
  img.alt = '加载失败'
}
</script>

<style scoped>
.resource-picker {
  width: 100%;
}

/* 单选模式 */
.rp-single {
  width: 100%;
}
.rp-btn-text {
  margin-left: 4px;
}
.rp-preview {
  margin-top: 8px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 80px;
  max-width: 240px;
  min-height: 60px;
  max-height: 180px;
  padding: 8px;
  border: 1px solid var(--el-border-color, #dcdfe6);
  border-radius: 6px;
  background: var(--el-fill-color-lighter, #f5f7fa);
  overflow: hidden;
}
.rp-preview img {
  max-width: 100%;
  max-height: 162px;  /* max-height 180 - padding 8*2 */
  display: block;
  object-fit: contain;
  border-radius: 3px;
}

/* 多选模式 */
.rp-multiple {
  width: 100%;
}
.rp-file-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.rp-file-item {
  position: relative;
  border: 1px solid var(--el-border-color, #dcdfe6);
  border-radius: 6px;
  overflow: hidden;
  background: var(--el-fill-color-lighter, #f5f7fa);
}
.rp-file-item.is-image {
  width: 88px;
  height: 88px;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 4px;
  box-sizing: border-box;
}
.rp-file-item:not(.is-image) {
  padding: 6px 28px 6px 10px;
  max-width: 200px;
}
.rp-thumb {
  max-width: 80px;
  max-height: 80px;
  width: auto;
  height: auto;
  object-fit: contain;
  display: block;
  border-radius: 2px;
}
.rp-file-name {
  font-size: 12px;
  color: var(--el-text-color-regular);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  display: block;
  max-width: 160px;
}
.rp-remove {
  position: absolute;
  top: 3px;
  right: 3px;
  cursor: pointer;
  font-size: 13px;
  color: #fff;
  background: rgba(0, 0, 0, 0.45);
  border-radius: 50%;
  padding: 2px;
  transition: background 0.2s;
  z-index: 1;
}
.rp-remove:hover {
  background: rgba(220, 38, 38, 0.85);
}
.rp-add-btn {
  width: 88px;
  height: 88px;
  border: 1px dashed var(--el-border-color, #dcdfe6);
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  font-size: 24px;
  color: var(--el-text-color-placeholder);
  transition: border-color 0.2s, color 0.2s;
}
.rp-add-btn:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}
.rp-tip {
  margin-top: 4px;
  font-size: 12px;
  color: var(--el-text-color-placeholder);
}

/* 禁用态 */
.is-disabled .rp-add-btn {
  cursor: not-allowed;
  opacity: 0.5;
}
.is-disabled .rp-add-btn:hover {
  border-color: var(--el-border-color);
  color: var(--el-text-color-placeholder);
}
</style>
