<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="files-page">
    <DataTable
      :data="rows"
      :loading="loading"
      :columns="columns"
      :pagination="pagination"
      :compact-mode="true"
      :show-column-settings="true"
      :show-export="true"
      :search-config="searchConfig"
      cache-key="files-list"
      :action-column-width="200"
      :show-selection="true"
      @search="onSearch"
      @page-change="onPageChange"
      @size-change="onSizeChange"
      @selection-change="onSelectionChange"
    >
      <template #header>
        <h2>文件管理</h2>
        <p>管理系统上传的文件</p>
      </template>

      <template #header-actions>
        <el-select v-model="currentType" placeholder="文件分类" style="width: 160px; margin-right: 12px" @change="onTypeChange">
          <el-option label="全部" value="" />
          <el-option v-for="item in fileTypes" :key="item.value" :label="`${item.label}(${item.value})`" :value="item.value" />
        </el-select>
        <el-button v-permission="'/system/files:move'" :disabled="selectedRows.length === 0" @click="showBatchMoveDialog = true">
          <i class="bi bi-arrow-left-right" style="margin-right: 4px;"></i>批量迁移 ({{ selectedRows.length }})
        </el-button>
        <el-button v-permission="'/system/files:batchdelete'" :disabled="selectedRows.length === 0" type="danger" @click="onBatchDelete">
          <i class="bi bi-trash" style="margin-right: 4px;"></i>批量删除 ({{ selectedRows.length }})
        </el-button>
        <el-button @click="refresh">
          <i class="bi bi-arrow-clockwise" style="margin-right: 4px;"></i>刷新
        </el-button>
        <el-button type="primary" @click="showUploadDialog = true">
          <i class="bi bi-cloud-upload" style="margin-right: 4px;"></i>上传管理
        </el-button>
      </template>

      <template #column-preview="{ row }">
        <div class="file-preview" :title="'点击在新标签页打开附件完整地址'" @click="openInNewTab(row)" style="cursor: pointer">
          <el-image
            v-if="isImageFile(row.fileName)"
            :src="buildFileUrl(row.id)"
            fit="cover"
            style="width: 80px; height: 50px; border-radius: 4px; pointer-events: none"
          />
          <div v-else-if="isVideoFile(row.fileName)" class="file-icon file-icon--video">
            <i class="bi bi-camera-video"></i>
          </div>
          <div v-else-if="isAudioFile(row.fileName)" class="file-icon file-icon--audio">
            <i class="bi bi-music-note-beamed"></i>
          </div>
          <div v-else-if="isPdfFile(row.fileName)" class="file-icon file-icon--pdf">
            <i class="bi bi-file-earmark-pdf"></i>
          </div>
          <div v-else class="file-icon">
            <i class="bi bi-file-earmark"></i>
          </div>
        </div>
      </template>

      <template #column-fileName="{ row }">
        <a class="file-name-link" :title="'点击在新标签页打开附件完整地址'" @click.prevent="openInNewTab(row)" href="javascript:void(0)">{{ row.fileName }}</a>
      </template>

      <template #column-storageProvider="{ row }">
        <el-tag :type="getStorageTagType(row.storageProvider)" size="small" effect="plain">
          {{ getStorageLabel(row.storageProvider) }}
        </el-tag>
      </template>

      <template #column-size="{ row }">
        <span>{{ formatSize(row.size) }}</span>
      </template>

      <template #column-createdAt="{ row }">
        <span>{{ formatTime(row.createdAt) }}</span>
      </template>

      <template #actions="{ row }">
        <el-button size="small" link @click="onPreview(row)">预览</el-button>
        <el-button v-permission="'/system/files:download'" size="small" link @click="onDownload(row)">下载</el-button>
        <el-button v-permission="'/system/files:delete'" size="small" type="danger" link @click="onDelete(row)">删除</el-button>
      </template>
    </DataTable>

    <!-- 文件预览对话框 -->
    <el-dialog v-model="showPreviewDialog" :title="previewFile?.fileName || '文件预览'" width="800px" :close-on-click-modal="true" destroy-on-close>
      <div class="preview-container">
        <!-- 图片预览 -->
        <div v-if="previewFile && isImageFile(previewFile.fileName)" class="preview-image">
          <el-image
            :src="buildFileUrl(previewFile.id)"
            fit="contain"
            :preview-src-list="[buildFileUrl(previewFile.id)]"
            style="max-width: 100%; max-height: 60vh"
          />
        </div>
        <!-- 视频预览 -->
        <div v-else-if="previewFile && isVideoFile(previewFile.fileName)" class="preview-video">
          <video controls style="max-width: 100%; max-height: 60vh" :src="buildFileUrl(previewFile.id)">
            您的浏览器不支持视频播放
          </video>
        </div>
        <!-- 音频预览 -->
        <div v-else-if="previewFile && isAudioFile(previewFile.fileName)" class="preview-audio">
          <div class="audio-icon"><i class="bi bi-music-note-beamed"></i></div>
          <p class="audio-filename">{{ previewFile.fileName }}</p>
          <audio controls style="width: 100%" :src="buildFileUrl(previewFile.id)">
            您的浏览器不支持音频播放
          </audio>
        </div>
        <!-- PDF 预览 -->
        <div v-else-if="previewFile && isPdfFile(previewFile.fileName)" class="preview-pdf">
          <iframe :src="buildFileUrl(previewFile.id)" style="width: 100%; height: 60vh; border: none"></iframe>
        </div>
        <!-- 其他文件类型 -->
        <div v-else class="preview-unsupported">
          <div class="unsupported-icon"><i class="bi bi-file-earmark"></i></div>
          <p>该文件类型不支持在线预览</p>
          <el-button type="primary" @click="onDownload(previewFile!)">下载文件</el-button>
        </div>
      </div>
      <template #footer>
        <div class="preview-info">
          <span>大小：{{ previewFile ? formatSize(previewFile.size) : '' }}</span>
          <span style="margin-left: 16px">类型：{{ previewFile?.contentType || '未知' }}</span>
          <span style="margin-left: 16px">存储：{{ previewFile ? getStorageLabel(previewFile.storageProvider) : '' }}</span>
        </div>
        <div v-if="previewFile" class="preview-address">
          <span class="preview-address-label">完整地址：</span>
          <el-input
            :model-value="getAbsoluteUrl(previewFile)"
            readonly
            size="small"
            class="preview-address-input"
          >
            <template #append>
              <el-button @click="copyAddress(previewFile)">
                <i class="bi bi-clipboard" style="margin-right: 4px"></i>复制地址
              </el-button>
            </template>
          </el-input>
          <el-button link type="primary" style="margin-left: 8px" @click="openInNewTab(previewFile)">
            <i class="bi bi-box-arrow-up-right" style="margin-right: 4px"></i>新标签页打开
          </el-button>
        </div>
      </template>
    </el-dialog>

    <!-- 上传对话框 -->
    <el-dialog v-model="showUploadDialog" title="上传文件" width="560px" :close-on-click-modal="false">
      <el-form label-width="90px">
        <el-form-item label="文件分类">
          <el-select v-model="uploadType" placeholder="请选择分类" style="width: 100%">
            <el-option label="默认" value="default" />
            <el-option v-for="item in fileTypes" :key="item.value" :label="`${item.label}(${item.value})`" :value="item.value" />
          </el-select>
        </el-form-item>
        <el-form-item label="选择文件">
          <el-upload
            ref="uploadRef"
            :auto-upload="false"
            :on-change="handleFileChange"
            :on-remove="handleFileRemove"
            :file-list="uploadFileList"
            :disabled="uploading"
            multiple
            drag
            style="width: 100%"
          >
            <div class="upload-area">
              <i class="bi bi-cloud-upload"></i>
              <div class="upload-text">点击或拖拽文件到此处上传</div>
              <div class="upload-hint">支持多文件上传</div>
            </div>
          </el-upload>
        </el-form-item>
        <el-form-item v-if="uploadProgress.length > 0" label="上传进度">
          <div class="upload-progress-list">
            <div
              v-for="item in uploadProgress"
              :key="item.uid"
              class="upload-progress-item"
              :class="`is-${item.status}`"
            >
              <div class="upload-progress-row">
                <span class="upload-progress-name" :title="item.name">{{ item.name }}</span>
                <span class="upload-progress-status">
                  <template v-if="item.status === 'pending'">
                    <i class="bi bi-hourglass"></i> 等待中
                  </template>
                  <template v-else-if="item.status === 'uploading'">
                    <i class="bi bi-arrow-up-circle"></i> 上传中 {{ item.percent }}%
                  </template>
                  <template v-else-if="item.status === 'done'">
                    <i class="bi bi-check-circle-fill"></i> 已完成
                  </template>
                  <template v-else-if="item.status === 'error'">
                    <i class="bi bi-x-circle-fill"></i> 失败<span v-if="item.error" class="upload-progress-error" :title="item.error">：{{ item.error }}</span>
                  </template>
                </span>
              </div>
              <el-progress
                :percentage="item.percent"
                :status="item.status === 'done' ? 'success' : (item.status === 'error' ? 'exception' : '')"
                :stroke-width="6"
                :show-text="false"
              />
            </div>
          </div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="uploading" @click="closeUploadDialog">取消</el-button>
        <el-button type="primary" :loading="uploading" @click="submitUpload">上传</el-button>
      </template>
    </el-dialog>

    <!-- 批量迁移对话框 -->
    <el-dialog v-model="showBatchMoveDialog" title="批量迁移存储区块" width="480px" :close-on-click-modal="false">
      <el-form label-width="120px">
        <el-form-item label="已选择文件">
          <span>{{ selectedRows.length }} 个文件</span>
        </el-form-item>
        <el-form-item label="当前存储区块">
          <div class="selected-providers">
            <el-tag v-for="p in selectedProviders" :key="p" :type="getStorageTagType(p)" size="small" style="margin-right: 6px">
              {{ getStorageLabel(p) }}
            </el-tag>
          </div>
        </el-form-item>
        <el-form-item label="目标存储区块">
          <el-select v-model="targetProvider" placeholder="请选择目标存储" style="width: 100%">
            <el-option label="本地存储" value="Local" />
            <el-option label="OSS 云存储" value="OssLib" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showBatchMoveDialog = false">取消</el-button>
        <el-button type="primary" :loading="batchMoving" @click="submitBatchMove">确认迁移</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import DataTable from '../../../components/DataTable/index.vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { UploadUserFile, UploadFile, UploadInstance } from 'element-plus'
import type { SearchFieldConfig } from '../../../components/DataTable/types'
import type { FileListItem, PagedResult } from '../../../api/files'
import { getFilesFiltered as getFiles, uploadFile, deleteFile, buildFileUrl, batchMoveFiles, batchDeleteFiles } from '../../../api/files'
import { getDictionaryItems } from '../../../api/dictionary'
import { resolveFileUrl, fetchResourceConfig } from '@/utils/resourceUrl'

const loading = ref(false)
const rows = ref<FileListItem[]>([])
const pagination = ref({ total: 0, page: 1, pageSize: 20, pageSizes: [10, 20, 50, 100] })
const currentType = ref('')
const currentFilter = ref<{ userName?: string; dateRange?: [string, string] }>({})
const fileTypes = ref<Array<{ label: string; value: string }>>([])
const selectedRows = ref<FileListItem[]>([])

// 预览相关
const showPreviewDialog = ref(false)
const previewFile = ref<FileListItem | null>(null)

const showUploadDialog = ref(false)
const uploadType = ref('default')
const uploadFileList = ref<UploadUserFile[]>([])
const uploadRef = ref<UploadInstance>()
const uploading = ref(false)

/** 单文件上传进度项。status: pending未开始 / uploading上传中 / done已完成 / error失败 */
interface UploadProgressItem {
  uid: number
  name: string
  size: number
  percent: number
  status: 'pending' | 'uploading' | 'done' | 'error'
  error?: string
}
const uploadProgress = ref<UploadProgressItem[]>([])

// 批量迁移相关
const showBatchMoveDialog = ref(false)
const targetProvider = ref('')
const batchMoving = ref(false)

const columns = [
  { prop: 'displayName', label: '上传用户', minWidth: 120 },
  { prop: 'preview', label: '预览', width: 100, slot: 'column-preview' },
  { prop: 'fileName', label: '文件名', minWidth: 220, slot: 'column-fileName' },
  { prop: 'contentType', label: '类型', minWidth: 140 },
  { prop: 'storageProvider', label: '存储区块', width: 110, slot: 'column-storageProvider' },
  { prop: 'size', label: '大小', width: 100, slot: 'column-size' },
  { prop: 'type', label: '分类', width: 100 },
  { prop: 'createdAt', label: '上传时间', width: 170, slot: 'column-createdAt' }
]

const searchConfig: SearchFieldConfig[] = [
  { key: 'userName', label: '用户', type: 'input', placeholder: '输入用户名', simple: true, width: 140 },
  { key: 'dateRange', label: '上传时间', type: 'daterange', simple: true, width: 360 }
]

// 选中行的存储提供者种类
const selectedProviders = computed(() => {
  const set = new Set<string>()
  selectedRows.value.forEach(r => set.add(r.storageProvider || 'Local'))
  return Array.from(set)
})

async function loadFileTypes() {
  try {
    const cats = await import('../../../api/dictionary').then(m => m.getDictionaryCategories())
    const fileCat = cats.find((c: any) => c.code === 'file')
    if (fileCat) {
      const items = await getDictionaryItems(fileCat.id)
      fileTypes.value = items.map((it: any) => ({ label: it.itemValue, value: it.itemKey }))
    }
  } catch {}
}

async function load() {
  loading.value = true
  try {
    const res: PagedResult<FileListItem> = await getFiles(pagination.value.page, pagination.value.pageSize, currentType.value, currentFilter.value)
    rows.value = Array.isArray(res.items) ? res.items : []
    pagination.value.total = Number(res.total || 0)
  } catch {
    rows.value = []
    ElMessage.error('加载文件失败')
  } finally {
    loading.value = false
  }
}

function onSearch(payload: { filters?: Record<string, any>; page?: number; pageSize?: number }) {
  const f = payload?.filters || {}
  currentFilter.value = {}
  if (f.userName) currentFilter.value.userName = String(f.userName || '').trim()
  if (Array.isArray(f.dateRange) && f.dateRange[0] && f.dateRange[1]) {
    currentFilter.value.dateRange = [f.dateRange[0], f.dateRange[1]]
  }
  if (payload?.page) pagination.value.page = Number(payload.page) || 1
  if (payload?.pageSize) pagination.value.pageSize = Number(payload.pageSize) || 20
  load()
}

function onTypeChange() { pagination.value.page = 1; load() }
function onPageChange(p: number) { pagination.value.page = p; load() }
function onSizeChange(s: number) { pagination.value.pageSize = s; pagination.value.page = 1; load() }
function refresh() { load() }
function onSelectionChange(selection: FileListItem[]) { selectedRows.value = selection }

function isImageFile(fileName: string): boolean {
  const ext = fileName.toLowerCase().split('.').pop() || ''
  return ['jpg', 'jpeg', 'png', 'gif', 'bmp', 'webp', 'svg'].includes(ext)
}

function isVideoFile(fileName: string): boolean {
  const ext = fileName.toLowerCase().split('.').pop() || ''
  return ['mp4', 'webm', 'ogg', 'avi', 'mov', 'wmv', 'flv', 'mkv'].includes(ext)
}

function isAudioFile(fileName: string): boolean {
  const ext = fileName.toLowerCase().split('.').pop() || ''
  return ['mp3', 'wav', 'ogg', 'aac', 'flac', 'm4a', 'wma'].includes(ext)
}

function isPdfFile(fileName: string): boolean {
  const ext = fileName.toLowerCase().split('.').pop() || ''
  return ext === 'pdf'
}

/** 获取存储区块显示标签 */
function getStorageLabel(provider?: string): string {
  if (!provider) return '本地'
  const p = provider.toLowerCase()
  if (p.includes('qiniu')) return '七牛云'
  if (p.includes('aliyun') || p.includes('ali')) return '阿里云'
  if (p.includes('tencent') || p.includes('cos')) return '腾讯云'
  if (p.includes('minio')) return 'MinIO'
  if (p.includes('s3') || p.includes('aws')) return 'AWS S3'
  if (p.includes('oss')) return 'OSS 云存储'
  if (p.includes('local')) return '本地'
  return provider
}

/** 获取存储标签颜色 */
function getStorageTagType(provider?: string): '' | 'success' | 'warning' | 'danger' | 'info' {
  if (!provider) return 'info'
  const p = provider.toLowerCase()
  if (p.includes('local')) return 'info'
  if (p.includes('qiniu')) return 'success'
  if (p.includes('aliyun') || p.includes('ali')) return 'warning'
  if (p.includes('tencent') || p.includes('cos')) return ''
  if (p.includes('oss')) return 'success'
  return 'info'
}

/** 根据 storageProvider 构建最优预览 URL */
function getPreviewUrl(row: FileListItem): string {
  if (row.url) {
    const resolved = resolveFileUrl({
      id: row.id,
      url: row.url,
      storageProvider: row.storageProvider
    })
    // 解析成功：返回绝对 URL 或以 / 开头的有效路径
    if (resolved && (resolved.startsWith('http') || resolved.startsWith('/'))) {
      return resolved
    }
    // 解析失败（原始相对路径），降级到 API 内容端点
    return buildFileUrl(row.id, false)
  }
  return buildFileUrl(row.id, false)
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`
}

function formatTime(v?: string) {
  if (!v) return ''
  try { return new Date(v).toLocaleString() } catch { return v }
}

function onPreview(row: FileListItem) {
  previewFile.value = row
  showPreviewDialog.value = true
}

/** 将 getPreviewUrl 的结果规范成可分享的完整 URL（带 origin） */
function getAbsoluteUrl(row: FileListItem): string {
  const url = getPreviewUrl(row)
  if (!url) return ''
  if (/^https?:\/\//i.test(url)) return url
  // 相对路径（如 /uploads/2026/...）补 origin
  try { return new URL(url, window.location.origin).href } catch { return url }
}

/** 在新标签页打开附件完整地址 */
function openInNewTab(row: FileListItem) {
  const url = getAbsoluteUrl(row)
  if (!url) {
    ElMessage.warning('无法获取附件地址')
    return
  }
  window.open(url, '_blank', 'noopener,noreferrer')
}

/** 复制附件完整地址到剪贴板 */
async function copyAddress(row: FileListItem) {
  const url = getAbsoluteUrl(row)
  if (!url) {
    ElMessage.warning('无法获取附件地址')
    return
  }
  try {
    if (navigator.clipboard && window.isSecureContext) {
      await navigator.clipboard.writeText(url)
    } else {
      // 兜底：通过隐藏 textarea + execCommand 复制（HTTP 等非安全上下文）
      const ta = document.createElement('textarea')
      ta.value = url
      ta.style.position = 'fixed'
      ta.style.opacity = '0'
      document.body.appendChild(ta)
      ta.select()
      document.execCommand('copy')
      document.body.removeChild(ta)
    }
    ElMessage.success('地址已复制到剪贴板')
  } catch {
    ElMessage.error('复制失败，请手动选中地址复制')
  }
}

function onDownload(row: FileListItem) {
  const link = document.createElement('a')
  link.href = buildFileUrl(row.id, true)
  link.download = row.fileName
  link.target = '_blank'
  link.click()
}

async function onDelete(row: FileListItem) {
  try {
    await ElMessageBox.confirm(`确定删除文件「${row.fileName}」?`, '提示', { type: 'warning' })
    await deleteFile(row.id)
    ElMessage.success('删除成功')
    await load()
  } catch {}
}

async function onBatchDelete() {
  if (selectedRows.value.length === 0) return
  try {
    await ElMessageBox.confirm(`确定删除选中的 ${selectedRows.value.length} 个文件？此操作不可恢复。`, '批量删除', { type: 'warning' })
    const ids = selectedRows.value.map(r => r.id)
    await batchDeleteFiles(ids)
    ElMessage.success(`成功删除 ${ids.length} 个文件`)
    selectedRows.value = []
    await load()
  } catch {}
}

async function submitBatchMove() {
  if (!targetProvider.value) { ElMessage.warning('请选择目标存储区块'); return }
  if (selectedRows.value.length === 0) return
  batchMoving.value = true
  try {
    const ids = selectedRows.value.map(r => r.id)
    await batchMoveFiles(ids, targetProvider.value)
    ElMessage.success(`成功提交 ${ids.length} 个文件的迁移任务`)
    showBatchMoveDialog.value = false
    targetProvider.value = ''
    selectedRows.value = []
    await load()
  } catch (e: any) {
    ElMessage.error(e?.message || '批量迁移失败')
  } finally {
    batchMoving.value = false
  }
}

function handleFileChange(file: UploadFile, fileList: UploadUserFile[]) { uploadFileList.value = fileList }
function handleFileRemove(file: UploadFile, fileList: UploadUserFile[]) { uploadFileList.value = fileList }

/** 关闭上传对话框：上传中不允许关闭，避免中途中断请求造成进度与列表不一致 */
function closeUploadDialog() {
  if (uploading.value) return
  showUploadDialog.value = false
  uploadFileList.value = []
  uploadProgress.value = []
  uploadRef.value?.clearFiles()
}

/**
 * 提交上传：按文件串行上传，每个文件单独走 /v1/files/upload，
 * 实时刷新该文件的进度条与完成状态。
 * 任何一个文件失败不会中断后续文件，最后统一提示成功 / 失败数量。
 */
async function submitUpload() {
  const files = (uploadFileList.value || []).map((f: any) => f.raw).filter((f: any) => f) as File[]
  if (files.length === 0) { ElMessage.warning('请选择文件'); return }
  uploading.value = true
  // 初始化进度面板
  uploadProgress.value = files.map((f, idx) => ({
    uid: Date.now() + idx,
    name: f.name,
    size: f.size,
    percent: 0,
    status: 'pending'
  }))
  let successCount = 0
  let failCount = 0
  for (let i = 0; i < files.length; i++) {
    const item = uploadProgress.value[i]
    item.status = 'uploading'
    try {
      await uploadFile(files[i], uploadType.value, undefined, (percent) => {
        // 99% 作为上传完成但后端还未返回的过渡状态，成功后置为 100%
        if (item.status !== 'uploading') return
        item.percent = Math.min(99, percent)
      })
      item.percent = 100
      item.status = 'done'
      successCount++
    } catch (e: any) {
      item.status = 'error'
      item.error = e?.message || '上传失败'
      failCount++
    }
  }
  uploading.value = false
  if (failCount === 0) {
    ElMessage.success(`全部 ${successCount} 个文件上传成功`)
    // 全部成功后自动关闭对话框；给用户一点反馈时间
    setTimeout(() => {
      showUploadDialog.value = false
      uploadFileList.value = []
      uploadProgress.value = []
      uploadRef.value?.clearFiles()
    }, 600)
  } else if (successCount > 0) {
    ElMessage.warning(`完成：成功 ${successCount} 个，失败 ${failCount} 个`)
  } else {
    ElMessage.error(`全部 ${failCount} 个文件上传失败`)
  }
  await load()
}

onMounted(async () => { await fetchResourceConfig().catch(() => {}); await loadFileTypes(); await load() })
</script>

<style scoped>
.files-page {
  padding: 24px;
  background: var(--el-bg-color-page);
  min-height: 100vh;
}

.file-preview {
  display: flex;
  align-items: center;
  justify-content: center;
}

.file-icon {
  width: 80px;
  height: 50px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f3f4f6;
  border-radius: 4px;
}

.file-icon i {
  font-size: 24px;
  color: #6b7280;
}

.file-icon--video { background: #ede9fe; }
.file-icon--video i { color: #7c3aed; }
.file-icon--audio { background: #fef3c7; }
.file-icon--audio i { color: #d97706; }
.file-icon--pdf { background: #fee2e2; }
.file-icon--pdf i { color: #dc2626; }

.admin-dark .file-icon { background: #374151; }
.admin-dark .file-icon--video { background: #312e81; }
.admin-dark .file-icon--audio { background: #451a03; }
.admin-dark .file-icon--pdf { background: #450a0a; }

/* 预览对话框 */
.preview-container {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 200px;
}

.preview-image,
.preview-video,
.preview-pdf {
  width: 100%;
  text-align: center;
}

.preview-audio {
  width: 100%;
  text-align: center;
  padding: 40px 0;
}

.audio-icon i {
  font-size: 64px;
  color: #d97706;
}

.audio-filename {
  margin: 16px 0;
  font-size: 14px;
  color: var(--el-text-color-regular);
}

.preview-unsupported {
  text-align: center;
  padding: 40px 0;
}

.unsupported-icon i {
  font-size: 64px;
  color: #9ca3af;
}

.preview-unsupported p {
  margin: 16px 0;
  color: var(--el-text-color-secondary);
}

.preview-info {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.preview-address {
  display: flex;
  align-items: center;
  margin-top: 10px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.preview-address-label {
  flex: 0 0 auto;
  margin-right: 6px;
  white-space: nowrap;
}

.preview-address-input {
  flex: 1 1 auto;
  min-width: 0;
}

.file-name-link {
  color: var(--el-color-primary);
  text-decoration: none;
  cursor: pointer;
  word-break: break-all;
}

.file-name-link:hover {
  text-decoration: underline;
}

.selected-providers {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.upload-area {
  padding: 32px 20px;
  text-align: center;
}

.upload-progress-list {
  width: 100%;
  max-height: 220px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.upload-progress-item {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 4px;
  padding: 8px 10px;
  background: var(--el-fill-color-blank);
  transition: border-color .2s, background-color .2s;
}

.upload-progress-item.is-done {
  border-color: var(--el-color-success-light-5);
  background: var(--el-color-success-light-9);
}

.upload-progress-item.is-error {
  border-color: var(--el-color-danger-light-5);
  background: var(--el-color-danger-light-9);
}

.upload-progress-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  font-size: 12px;
  margin-bottom: 6px;
}

.upload-progress-name {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--el-text-color-primary);
}

.upload-progress-status {
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  gap: 4px;
  color: var(--el-text-color-regular);
}

.upload-progress-item.is-done .upload-progress-status {
  color: var(--el-color-success);
}

.upload-progress-item.is-error .upload-progress-status {
  color: var(--el-color-danger);
}

.upload-progress-error {
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.upload-area i {
  font-size: 40px;
  color: #3b82f6;
}

.upload-text {
  margin-top: 12px;
  font-size: 14px;
  color: #1f2937;
}

.upload-hint {
  margin-top: 6px;
  font-size: 12px;
  color: #6b7280;
}

.admin-dark .upload-text { color: #e5e7eb; }
.admin-dark .upload-hint { color: #9ca3af; }

/* 对话框 */
:deep(.el-dialog) { border-radius: 12px; }

:deep(.el-dialog__header) {
  background: linear-gradient(to right, #f9fafb 0%, #ffffff 100%);
  border-bottom: 1px solid #e5e7eb;
  padding: 20px 24px;
  margin: 0;
}

.admin-dark :deep(.el-dialog__header) {
  background: linear-gradient(to right, #1f2937 0%, #1a2332 100%);
  border-bottom-color: #374151;
}

:deep(.el-dialog__title) { font-size: 18px; font-weight: 600; color: #1f2937; }
.admin-dark :deep(.el-dialog__title) { color: #f9fafb; }

:deep(.el-dialog__body) { padding: 24px; }

:deep(.el-dialog__footer) {
  padding: 16px 24px;
  border-top: 1px solid #f3f4f6;
}

.admin-dark :deep(.el-dialog__footer) { border-top-color: #374151; }

@media (max-width: 768px) {
  .files-page { padding: 16px; }
}
</style>
