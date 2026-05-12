<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="wang-editor-container" :style="containerStyle">
    <Toolbar
      :editor="editorRef"
      :defaultConfig="toolbarConfig"
      :mode="mode"
      class="wang-editor-toolbar"
    />
    <Editor
      :defaultConfig="editorConfig"
      :mode="mode"
      :model-value="modelValue"
      :style="editorStyle"
      class="wang-editor-content"
      @onCreated="handleCreated"
      @onChange="handleChange"
    />

    <!-- 图片选择器 -->
    <FileSelector
      v-model="imageSelectorVisible"
      title="选择图片"
      :multiple="true"
      accept="image/*"
      :data-scope="fileDataScope"
      @confirm="onImageSelected"
    />

    <!-- 视频选择器 -->
    <FileSelector
      v-model="videoSelectorVisible"
      title="选择视频"
      :multiple="false"
      accept="video/*"
      :data-scope="fileDataScope"
      @confirm="onVideoSelected"
    />

    <!-- 附件选择器 -->
    <FileSelector
      v-model="fileSelectorVisible"
      title="选择附件"
      :multiple="true"
      :data-scope="fileDataScope"
      @confirm="onFileSelected"
    />

    <!-- 统一媒体插入对话框 -->
    <el-dialog
      v-model="mediaDialogVisible"
      :title="mediaDialogTitle"
      width="760px"
      append-to-body
      destroy-on-close
      @closed="onMediaDialogClosed"
    >
      <div class="media-dialog-body">
        <div class="media-url-panel">
          <div class="panel-title">资源地址</div>
          <el-input
            v-model="mediaUrlText"
            type="textarea"
            :rows="8"
            resize="none"
            placeholder="每行一个 http://、https:// 地址；也可以从右侧附件库选择后自动填充"
          />
        </div>

        <div class="media-picker-panel">
          <div class="panel-title">系统附件</div>
          <el-button type="primary" plain @click="onMediaDialogSelectFile">
            <el-icon><Folder /></el-icon>
            打开附件管理器
          </el-button>
          <div class="picker-tip">
            支持单选和多选，选择后会自动写入左侧地址列表。
          </div>
          <div v-if="currentMediaItems.length > 0" class="selected-summary">
            已准备插入 {{ currentMediaItems.length }} 项
          </div>
        </div>
      </div>
      <template #footer>
        <el-button @click="mediaDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="onMediaDialogConfirm">插入</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, watch, shallowRef, ref, onBeforeUnmount, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import { Folder } from '@element-plus/icons-vue'
import { Editor, Toolbar } from '@wangeditor/editor-for-vue'
import { Boot } from '@wangeditor/editor'
import type { IButtonMenu, IDomEditor } from '@wangeditor/editor'
import type { EditorAdapterProps, EditorAdapterExposed } from './editor-adapter'
import FileSelector from './FileSelector.vue'
import { type FileListItemDto } from '../api/files'
import { resolveFileUrl } from '@/utils/resourceUrl'
import { useAuthStore } from '../stores/auth'
import {
  buildMediaInsertHtml,
  isSafeMediaUrl,
  normalizeMediaItems,
  type MediaInsertItem,
} from '@/utils/editorMediaInsert'
import '@wangeditor/editor/dist/css/style.css'

// ============================================================
// 模块级：注册自定义 insertAttachment 菜单（全局只注册一次）
// ============================================================

/** editor 实例 → 组件回调映射，支持多编辑器并存 */
const editorMediaCallbackMap = new WeakMap<IDomEditor, (type: 'image' | 'video' | 'file') => void>()

// 回形针 SVG 图标
const attachmentIconSvg = '<svg viewBox="0 0 1024 1024" width="1em" height="1em"><path d="M859.2 169.6c-87.2-87.2-228.8-87.2-316 0L199.2 513.6c-62.4 62.4-62.4 163.2 0 225.6 62.4 62.4 163.2 62.4 225.6 0l272-272c37.6-37.6 37.6-97.6 0-135.2-37.6-37.6-97.6-37.6-135.2 0L357.6 536c-12.8 12.8-12.8 32 0 44.8 12.8 12.8 32 12.8 44.8 0l204-204c12.8-12.8 32-12.8 44.8 0 12.8 12.8 12.8 32 0 44.8l-272 272c-37.6 37.6-97.6 37.6-135.2 0-37.6-37.6-37.6-97.6 0-135.2L588 214.4c62.4-62.4 163.2-62.4 225.6 0 62.4 62.4 62.4 163.2 0 225.6L497.6 756c-12.8 12.8-12.8 32 0 44.8 12.8 12.8 32 12.8 44.8 0l316-316c87.2-87.2 87.2-228 0.8-315.2z" fill="currentColor"/></svg>'

class InsertAttachmentMenu implements IButtonMenu {
  title: string
  iconSvg: string
  tag: string

  constructor() {
    this.title = '插入附件'
    this.iconSvg = attachmentIconSvg
    this.tag = 'button'
  }

  getValue(_editor: IDomEditor): string | boolean {
    return ''
  }

  isActive(_editor: IDomEditor): boolean {
    return false
  }

  isDisabled(editor: IDomEditor): boolean {
    return editor.isDisabled()
  }

  exec(editor: IDomEditor, _value: string | boolean): void {
    if (this.isDisabled(editor)) return
    const callback = editorMediaCallbackMap.get(editor)
    if (callback) callback('file')
  }
}

// 防止 HMR 或多次加载时重复注册
// 通过全局标记避免 Vite HMR 模块重新评估时重复调用 Boot.registerMenu 导致报错
const _globalKey = '__ginkgo_attachment_menu_registered__'
if (!(window as any)[_globalKey]) {
  try {
    Boot.registerMenu({
      key: 'insertAttachment',
      factory() {
        return new InsertAttachmentMenu()
      }
    })
    ;(window as any)[_globalKey] = true
  } catch (_e) {
    // 已注册则静默忽略
  }
}

// ============================================================
// 工具栏预设
// ============================================================

const TOOLBAR_PRESETS: Record<string, Record<string, any>> = {
  minimal: {
    toolbarKeys: [
      'bold', 'italic', 'underline', '|',
      'bulletedList', 'numberedList', '|',
      'insertLink'
    ]
  },
  basic: {
    toolbarKeys: [
      'headerSelect', 'bold', 'italic', 'underline', 'through', '|',
      'color', 'bgColor', '|',
      'bulletedList', 'numberedList', '|',
      'insertLink', 'uploadImage', 'uploadVideo', 'insertAttachment', '|',
      'codeBlock', 'undo', 'redo'
    ]
  },
  full: {
    // 使用显式 toolbarKeys 列表确保 uploadImage/uploadVideo/insertAttachment 明确出现
    toolbarKeys: [
      'headerSelect', '|',
      'blockquote', 'bold', 'italic', 'underline', 'through', 'sub', 'sup', 'clearStyle', '|',
      'color', 'bgColor', '|',
      'fontSize', 'fontFamily', 'lineHeight', '|',
      'bulletedList', 'numberedList', 'todo', '|',
      'justifyLeft', 'justifyRight', 'justifyCenter', '|',
      'indent', 'delIndent', '|',
      'emotion', 'insertLink', 'uploadImage', 'uploadVideo', 'insertAttachment', '|',
      'insertTable', 'codeBlock', 'divider', '|',
      'undo', 'redo', '|',
      'fullScreen'
    ]
  }
}

const props = withDefaults(defineProps<EditorAdapterProps>(), {
  modelValue: '',
  placeholder: '请输入内容...',
  disabled: false,
  readonly: false,
  height: 300,
  toolbar: 'full',
  config: () => ({}),
  editorId: ''
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
  'editor-ready': [editor: any]
  'editor-change': [value: string, editor: any]
}>()

const editorRef = shallowRef<IDomEditor | null>(null)
let isUpdatingContent = false
let isFirstChange = true

// FileSelector 状态
const imageSelectorVisible = ref(false)
const videoSelectorVisible = ref(false)
const fileSelectorVisible = ref(false)

// ============================================================
// 统一媒体插入对话框
// ============================================================
const mediaDialogVisible = ref(false)
const mediaForm = ref<{
  type: 'image' | 'video' | 'file'
  url: string
  alt: string
  text: string
}>({ type: 'image', url: '', alt: '', text: '' })
const mediaUrlText = ref('')
const selectedMediaItems = ref<MediaInsertItem[]>([])

const mediaDialogTitle = computed(() => {
  const map: Record<string, string> = {
    image: '插入图片',
    video: '插入视频',
    file: '插入附件'
  }
  return map[mediaForm.value.type] || '插入资源'
})

const currentMediaItems = computed(() => {
  const selectedMap = new Map(selectedMediaItems.value.map(item => [item.url, item]))
  return normalizeMediaItems(mediaUrlText.value).map(item => selectedMap.get(item.url) || item)
})

/** 打开统一媒体插入对话框 */
function openMediaDialog(type: 'image' | 'video' | 'file') {
  mediaForm.value = { type, url: '', alt: '', text: '' }
  mediaUrlText.value = ''
  selectedMediaItems.value = []
  mediaDialogVisible.value = true
}

/** 对话框内点击"从附件库选择" */
function onMediaDialogSelectFile() {
  const { type } = mediaForm.value
  if (type === 'image') {
    imageSelectorVisible.value = true
  } else if (type === 'video') {
    videoSelectorVisible.value = true
  } else {
    fileSelectorVisible.value = true
  }
}

/** 对话框关闭时的清理 */
function onMediaDialogClosed() {
  mediaForm.value = { type: 'image', url: '', alt: '', text: '' }
  mediaUrlText.value = ''
  selectedMediaItems.value = []
}

/** 确认插入媒体资源 */
function onMediaDialogConfirm() {
  const editor = editorRef.value
  if (!editor) return
  const items = currentMediaItems.value
  if (items.length === 0) {
    ElMessage.warning('请输入资源地址或从附件库选择')
    return
  }

  const invalidItem = items.find(item => !isSafeMediaUrl(item.url))
  if (invalidItem) {
    ElMessage.warning(`资源地址不合法：${invalidItem.url}`)
    return
  }

  const html = buildMediaInsertHtml(mediaForm.value.type, items)
  if (!html) {
    ElMessage.warning('没有可插入的资源')
    return
  }
  editor.dangerouslyInsertHtml(html)
  mediaDialogVisible.value = false
}

// 权限检查：ADMIN 角色不受数据范围限制，可查看所有附件
// 非 ADMIN 用户由服务端根据 DataPermission.DefaultScope 配置过滤
const auth = useAuthStore()
const isAdmin = computed(() => auth.roles.includes('ADMIN'))
// ADMIN 传空值让服务端不做限制；非 ADMIN 不传 dataScope，由服务端读取系统配置决定
const fileDataScope = computed(() => isAdmin.value ? 'All' : '')

const mode = computed(() => 'default')

const toolbarConfig = computed(() => {
  const preset = props.toolbar && TOOLBAR_PRESETS[props.toolbar]
    ? props.toolbar
    : 'full'
  return { ...TOOLBAR_PRESETS[preset] }
})

/** 构建文件的最优访问 URL */
function getFileUrl(file: FileListItemDto): string {
  return resolveFileUrl({
    id: file.id,
    url: file.url,
    storageProvider: file.storageProvider
  })
}

/** 图片选择回调 */
function onImageSelected(files: FileListItemDto[]) {
  const editor = editorRef.value
  if (!editor) return
  if (mediaDialogVisible.value && mediaForm.value.type === 'image') {
    appendSelectedFilesToDialog(files)
    return
  }
  editor.dangerouslyInsertHtml(buildMediaInsertHtml('image', files.map(file => ({
    url: getFileUrl(file),
    name: file.fileName,
    alt: file.fileName
  }))))
  mediaDialogVisible.value = false
}

/** 视频选择回调 */
function onVideoSelected(files: FileListItemDto[]) {
  const editor = editorRef.value
  if (!editor || files.length === 0) return
  if (mediaDialogVisible.value && mediaForm.value.type === 'video') {
    appendSelectedFilesToDialog(files)
    return
  }
  editor.dangerouslyInsertHtml(buildMediaInsertHtml('video', files.map(file => ({
    url: getFileUrl(file),
    name: file.fileName
  }))))
  mediaDialogVisible.value = false
}

/** 附件选择回调 */
function onFileSelected(files: FileListItemDto[]) {
  const editor = editorRef.value
  if (!editor) return
  if (mediaDialogVisible.value && mediaForm.value.type === 'file') {
    appendSelectedFilesToDialog(files)
    return
  }
  editor.dangerouslyInsertHtml(buildMediaInsertHtml('file', files.map(file => ({
    url: getFileUrl(file),
    name: file.fileName,
    text: file.fileName
  }))))
  mediaDialogVisible.value = false
}

function appendSelectedFilesToDialog(files: FileListItemDto[]) {
  const currentUrls = normalizeMediaItems(mediaUrlText.value).map(item => item.url)
  const nextUrls = [...currentUrls]

  for (const file of files) {
    const url = getFileUrl(file)
    if (!url || nextUrls.includes(url)) continue
    nextUrls.push(url)
    selectedMediaItems.value.push({
      url,
      name: file.fileName,
      alt: file.fileName,
      text: file.fileName,
    })
  }

  mediaUrlText.value = nextUrls.join('\n')
}

const editorConfig = computed(() => ({
  placeholder: props.placeholder,
  readOnly: props.disabled || props.readonly,
  MENU_CONF: {
    // 拦截图片上传，改为打开统一媒体对话框
    uploadImage: {
      customBrowseAndUpload: (_insertFn: any) => {
        console.log('[WangEditor] uploadImage.customBrowseAndUpload triggered')
        openMediaDialog('image')
      }
    },
    // 拦截视频上传，改为打开统一媒体对话框
    uploadVideo: {
      customBrowseAndUpload: (_insertFn: any) => {
        console.log('[WangEditor] uploadVideo.customBrowseAndUpload triggered')
        openMediaDialog('video')
      }
    }
  },
  ...props.config
}))

const containerStyle = computed(() => ({
  border: '1px solid #ccc',
  borderRadius: '4px',
  overflow: 'hidden'
}))

const editorStyle = computed(() => {
  const h = typeof props.height === 'number' ? `${props.height}px` : props.height
  return { height: h, overflowY: 'auto' as const }
})

function handleCreated(editor: IDomEditor) {
  editorRef.value = editor
  // 注册 editor → 组件回调映射，供自定义菜单使用
  editorMediaCallbackMap.set(editor, openMediaDialog)
  console.log('[WangEditor] editor created, toolbar preset=', props.toolbar, 'toolbarConfig=', toolbarConfig.value, 'editorConfig.MENU_CONF=', (editorConfig.value as any).MENU_CONF)
  // 编辑器创建后，如果父组件已有内容（如编辑模式下 loadTemplate 先完成），主动设置
  // 使用 nextTick 确保在 @wangeditor 内部初始化完成后再设置
  if (props.modelValue) {
    nextTick(() => {
      isUpdatingContent = true
      editor.setHtml(props.modelValue)
      isUpdatingContent = false
    })
  }
  emit('editor-ready', editor)
}

function handleChange(editor: IDomEditor) {
  if (isUpdatingContent) return
  // 跳过编辑器创建时触发的首次 onChange（此时内容为空的 <p><br></p>，会覆盖父组件已有的 modelValue）
  if (isFirstChange) {
    isFirstChange = false
    return
  }
  const html = editor.getHtml()
  emit('update:modelValue', html)
  emit('editor-change', html, editor)
}

watch(
  () => [props.disabled, props.readonly],
  ([disabled, readonly]) => {
    const editor = editorRef.value
    if (!editor) return
    if (disabled || readonly) {
      editor.disable()
    } else {
      editor.enable()
    }
  }
)

watch(
  () => props.modelValue,
  (newVal) => {
    const editor = editorRef.value
    if (!editor) return
    const currentHtml = editor.getHtml()
    if (newVal !== currentHtml) {
      isUpdatingContent = true
      editor.setHtml(newVal || '')
      isUpdatingContent = false
    }
  }
)

onBeforeUnmount(() => {
  const editor = editorRef.value
  if (editor) {
    editorMediaCallbackMap.delete(editor)
    try {
      editor.destroy()
    } catch (_e) {
      // 静默处理销毁异常
    }
  }
})

const getHTML = (): string => editorRef.value?.getHtml() ?? ''
const getText = (): string => editorRef.value?.getText() ?? ''
const isEmpty = (): boolean => {
  if (!editorRef.value) return true
  return editorRef.value.isEmpty()
}
const reset = (): void => { editorRef.value?.setHtml('') }
const focus = (): void => { editorRef.value?.focus() }
const blur = (): void => { editorRef.value?.blur() }

defineExpose<EditorAdapterExposed>({
  getHTML, getText, isEmpty, reset, focus, blur
})
</script>

<style scoped>
.wang-editor-container {
  width: 100%;
}
.wang-editor-toolbar {
  border-bottom: 1px solid #ccc;
}
.wang-editor-content {
  width: 100%;
}

/* 媒体预览区域 */
.media-preview {
  max-width: 200px;
  max-height: 150px;
  overflow: hidden;
  border: 1px solid var(--el-border-color, #dcdfe6);
  border-radius: 4px;
  background: var(--el-fill-color-lighter, #fafafa);
}
.media-preview img {
  max-width: 100%;
  max-height: 150px;
  display: block;
  object-fit: contain;
}
</style>
