import http from './http'
import { API_BASE_URL } from '../config/env'

export interface FileListItemDto {
  id: string
  fileName: string
  contentType?: string
  size: number
  storageProvider?: string
  url?: string
  downloadUrl?: string
  type?: string
  createdAt: string
  createdBy?: string
}

export interface FileDetailDto {
  id: string
  fileName: string
  contentType?: string
  size: number
  hash?: string
  storageProvider: string
  storagePath: string
  url?: string
  downloadUrl?: string
  ownerId?: string
  tags?: string
  version: number
  type?: string
  departmentId?: string
  createdAt: string
  createdBy?: string
}

export interface PageRequest {
  page: number
  pageSize: number
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

/**
 * 获取文件列表（分页）
 * @param params.dataScope 数据范围: Self(仅本人) / Dept(本部门) / DeptAndChildren(本部门及子部门) / All(全部)
 */
export async function getFiles(params: PageRequest & { type?: string; dataScope?: string }): Promise<PagedResult<FileListItemDto>> {
  return await http.get('/v1/files', { params })
}

/**
 * 获取文件详情
 */
export async function getFileDetail(id: string): Promise<FileDetailDto> {
  return await http.get(`/v1/files/${id}`)
}

/**
 * 批量上传文件（一次性 multipart 请求）。
 * @param onProgress 可选进度回调，接收 0-100 整数百分比。
 *   注意：批量模式下回调反映的是整个 multipart 请求的总进度，无法精确到单个文件。
 *   如果希望按单文件分别展示进度 + 完成状态，请改用 uploadFile() 串行调用。
 */
export async function uploadFiles(
  files: File[],
  type?: string,
  tags?: string,
  onProgress?: (percent: number) => void
): Promise<string[]> {
  const formData = new FormData()

  files.forEach(file => {
    formData.append('files', file)
  })

  if (type) {
    formData.append('type', type)
  }

  if (tags) {
    formData.append('tags', tags)
  }

  return await http.post('/v1/files/upload', formData, {
    headers: {
      'Content-Type': 'multipart/form-data'
    },
    onUploadProgress: (e: any) => {
      if (!onProgress) return
      const total = e?.total || e?.loaded || 0
      if (!total) return
      const percent = Math.min(100, Math.round((e.loaded / total) * 100))
      onProgress(percent)
    }
  })
}

/**
 * 上传单个文件（带进度回调），返回新建文件 ID。
 * 用于需要按文件粒度展示「上传中 / 已完成 + 进度条」的交互场景。
 */
export async function uploadFile(
  file: File,
  type?: string,
  tags?: string,
  onProgress?: (percent: number) => void
): Promise<string> {
  const ids = await uploadFiles([file], type, tags, onProgress)
  if (!ids || ids.length === 0) throw new Error('上传未返回文件 ID')
  return ids[0]
}

/**
 * 删除文件
 */
export async function deleteFile(id: string): Promise<void> {
  await http.delete(`/v1/files/${id}`)
}

/**
 * 从 localStorage 获取当前登录用户的 auth token。
 * @deprecated 不再需要在文件 URL 中附加 token。文件通过 /uploads/ 静态路径直接访问。
 */
function getAuthToken(): string {
  try {
    return localStorage.getItem('web_user_token') || localStorage.getItem('auth-token') || ''
  } catch {
    return ''
  }
}

/**
 * @deprecated 不再需要在 URL 中附加 token。文件通过 /uploads/ 静态路径直接访问。
 */
function appendToken(url: string): string {
  const token = getAuthToken()
  return token ? `${url}?access_token=${encodeURIComponent(token)}` : url
}

/**
 * @deprecated 请使用 resolveResourcePath(path) 代替。文件已通过 /uploads/ 静态路径直接访问，无需 ID 查询。
 */
export function buildFileContentUrl(id: string): string {
  return appendToken(`${API_BASE_URL}/v1/files/${id}/content`)
}

/**
 * 构建文件下载URL（带鉴权 token）。
 * 仅用于触发浏览器下载，不适用于 img/video 等内联预览。
 */
export function buildFileDownloadUrl(id: string): string {
  return appendToken(`${API_BASE_URL}/v1/files/${id}/download`)
}

/**
 * @deprecated GrantKey 公开授权机制已移除。文件通过 /uploads/ 静态路径直接访问。
 */
export function buildPublicUrl(grantKey: string): string {
  return `${API_BASE_URL}/v1/files/public/${grantKey}`
}

/**
 * @deprecated Ticket 签名票据机制已移除。文件通过 /uploads/ 静态路径直接访问。
 */
export async function signTicketUrl(id: string, minutes = 10): Promise<string> {
  console.warn('[files] signTicketUrl 已废弃，请使用 resolveResourcePath 替代')
  return ''
}

/**
 * 格式化文件大小
 */
export function formatFileSize(size: number): string {
  if (size < 1024) return `${size} B`
  if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KB`
  if (size < 1024 * 1024 * 1024) return `${(size / (1024 * 1024)).toFixed(1)} MB`
  return `${(size / (1024 * 1024 * 1024)).toFixed(1)} GB`
}

/**
 * 判断是否为图片文件
 */
export function isImageFile(contentType?: string, fileName?: string): boolean {
  if (contentType && contentType.startsWith('image/')) return true
  if (fileName) {
    const ext = fileName.toLowerCase().split('.').pop()
    return ['jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp', 'svg'].includes(ext || '')
  }
  return false
}

/**
 * 判断是否为视频文件
 */
export function isVideoFile(contentType?: string, fileName?: string): boolean {
  if (contentType && contentType.startsWith('video/')) return true
  if (fileName) {
    const ext = fileName.toLowerCase().split('.').pop()
    return ['mp4', 'webm', 'ogg', 'avi', 'mov', 'wmv', 'flv'].includes(ext || '')
  }
  return false
}

/**
 * 判断是否为音频文件
 */
export function isAudioFile(contentType?: string, fileName?: string): boolean {
  if (contentType && contentType.startsWith('audio/')) return true
  if (fileName) {
    const ext = fileName.toLowerCase().split('.').pop()
    return ['mp3', 'wav', 'ogg', 'aac', 'flac', 'm4a'].includes(ext || '')
  }
  return false
}

// 来自 file.ts 的类型和函数 (合并)

export interface FileListItem {
  id: string
  fileName: string
  contentType: string
  size: number
  type: string
  url?: string
  downloadUrl?: string
  storageProvider?: string
  createdAt: string
  createdBy?: string
  userName?: string | null
  displayName?: string | null
}

export interface FileDetail extends FileListItem {
  storagePath?: string
  tags?: string
  updatedAt?: string
}

/**
 * 获取文件列表（分页，带高级筛选）
 */
export async function getFilesFiltered(page = 1, pageSize = 20, type?: string, filter?: { userName?: string; dateRange?: [string, string] }): Promise<PagedResult<FileListItem>> {
  const params: any = { page, pageSize }
  if (type && type.trim()) params.type = type.trim()

  if (filter) {
    const payload: any = {
      userName: (filter.userName || '').trim() || undefined,
      dateRange: (Array.isArray(filter.dateRange) && filter.dateRange.length === 2 && filter.dateRange[0] && filter.dateRange[1]) ? [filter.dateRange[0], filter.dateRange[1]] : undefined
    }
    params.filter = JSON.stringify(payload)
  }

  return await http.get<any, PagedResult<FileListItem>>('/v1/files', { params })
}

/**
 * @deprecated 请使用 resolveResourcePath(path) 代替。
 */
export function buildFileUrl(id: string, download = false): string {
  const base = String(API_BASE_URL).replace(/\/$/, '')
  const endpoint = download ? 'download' : 'content'
  const url = `${base}/v1/files/${id}/${endpoint}`
  return appendToken(url)
}

/**
 * 批量迁移文件到目标存储区块
 */
export async function batchMoveFiles(ids: string[], targetProvider: string): Promise<void> {
  await http.post('/v1/files/batch-move', { ids, targetProvider })
}

/**
 * 批量删除文件
 */
export async function batchDeleteFiles(ids: string[]): Promise<void> {
  await http.post('/v1/files/batch-delete', { ids })
}
