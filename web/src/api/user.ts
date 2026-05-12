import http from './http'
import { API_BASE_URL } from '../config/env'

// 用户信息接口
export interface UserInfo {
  id: string
  userName: string
  displayName: string
  avatar?: string
  introduction?: string
  email?: string
  phone?: string
  enabled: boolean
  createdAt: string
}

// 修改密码输入
export interface ChangePasswordInput {
  oldPassword: string
  newPassword: string
}

// 更新个人资料输入
export interface UpdateProfileInput {
  displayName: string
  avatar?: string
  introduction?: string
  email?: string
  phone?: string
}

// 通知信息
export interface NotificationItem {
  id: string
  title: string
  content: string
  type: string
  isRead: boolean
  createdAt: string
}

// 与后端返回结构对齐的原始通知项（例如 publishedAt）
interface RawNotificationItem {
  id: string
  title: string
  publishedAt?: string
  isRead: boolean
  // 兼容后端可能已有的字段
  content?: string
  type?: string
  createdAt?: string
}

// 通知详情（我的）
export interface MyNotificationDetail {
  id: string
  title: string
  contentType: number
  contentText?: string | null
  contentHtml?: string | null
  createdAt: string
  isRead: boolean
}

export interface AttachmentDto {
  fileId: string
  name?: string
  contentType?: string
  size?: number
  fileUrl?: string // 文件相对路径（来自 SysFile.Url）
}

// 获取当前用户信息
export async function getCurrentUser(): Promise<UserInfo> {
  return await http.get<any, UserInfo>('/v1/users/me')
}

// 更新个人资料
export async function updateProfile(input: UpdateProfileInput): Promise<void> {
  await http.put('/v1/users/me', input)
}

// 修改密码
export async function changePassword(input: ChangePasswordInput): Promise<void> {
  await http.post('/v1/users/me/password', input)
}

// 获取我的通知列表（使用新消息 API）
export async function getMyNotifications(filter?: { title?: string; dateRange?: [string, string] }): Promise<NotificationItem[]> {
  const params: any = { pageIndex: 1, pageSize: 50 }
  const res = await http.get<any, any>('/message/list', { params })
  const items = res?.items || res || []
  return (Array.isArray(items) ? items : []).map((n: any) => ({
    id: String(n.id),
    title: n.title || '',
    content: n.summary || n.content || '',
    type: n.type || 'info',
    isRead: !!n.isRead,
    createdAt: n.createdAt || new Date().toISOString()
  }))
}

// 获取未读通知数量（使用新消息 API）
export async function getUnreadNotificationCount(): Promise<number> {
  return await http.get<any, number>('/message/unread-count')
}

// 标记通知为已读（使用新消息 API）
export async function markNotificationAsRead(notificationId: string): Promise<void> {
  await http.put(`/message/${notificationId}/read`)
}

// 获取通知详情（使用新消息 API）
export async function getNotificationDetail(notificationId: string): Promise<NotificationItem> {
  const n = await http.get<any, any>(`/message/${notificationId}`, { params: { platform: 'web' } })
  return {
    id: String(n.id),
    title: n.title || '',
    content: n.content || n.summary || '',
    type: n.type || 'info',
    isRead: !!n.isRead,
    createdAt: n.createdAt || new Date().toISOString()
  }
}

// 获取"我的"通知详情（使用新消息 API，适配 MessageDetailDto 返回结构）
export async function getMyNotificationDetail(notificationId: string): Promise<MyNotificationDetail> {
  const d = await http.get<any, any>(`/message/${notificationId}`, { params: { platform: 'web' } })
  return {
    id: String(d.id),
    title: d.title || '',
    contentType: 1,
    contentText: d.content ?? d.summary ?? null,
    contentHtml: null,
    createdAt: d.createdAt || new Date().toISOString(),
    isRead: !!d.isRead
  }
}

// 获取通知附件列表
export async function getNotificationAttachments(notificationId: string): Promise<AttachmentDto[]> {
  return await http.get<any, AttachmentDto[]>(`/v1/notifications/${notificationId}/attachments`)
}

// 构建附件下载地址（用于直接打开/下载）
export function buildAttachmentDownloadUrl(notificationId: string, fileId: string): string {
  const base = String(API_BASE_URL).replace(/\/$/, '')
  // 尝试从本地获取 token，确保新窗口下载也能鉴权
  let token = ''
  try {
    const fromAdmin = localStorage.getItem('auth-token') || ''
    const fromWeb = localStorage.getItem('web_user_token') || ''
    token = fromWeb || fromAdmin
  } catch {}
  const url = `${base}/v1/notifications/${notificationId}/attachments/${fileId}/download`
  return token ? `${url}?access_token=${encodeURIComponent(token)}` : url
}

/**
 * @deprecated 已废弃，文件通过 /uploads/ 静态路径直接访问，无需鉴权 URL
 */
export function buildFileContentUrl(fileId: string): string {
  console.warn('[deprecated] buildFileContentUrl 已废弃，请使用 resolveResourcePath 或 resolveFileUrl')
  return ''
}

// 操作日志（个人）
export interface OpLogItem {
  id: string
  action: string
  resource?: string
  moduleCN?: string
  featureCN?: string
  reviewCN?: string
  ip?: string
  userAgent?: string
  createdAt: string
  userId?: string
  userName?: string | null
  displayName?: string | null
  email?: string | null
  phone?: string | null
}

export interface PagedResult<T> {
  total: number
  page: number
  pageSize: number
  items: T[]
}

// 获取“我的”操作日志（分页）
export async function getMyOpLogs(page = 1, pageSize = 20, filter?: { module?: string; keyword?: string; dateRange?: [string, string] } | any): Promise<PagedResult<OpLogItem>> {
  const params: any = { page, pageSize }
  if (filter) {
    const payload: any = {
      module: (filter.module || '').trim() || undefined,
      keyword: (filter.keyword || '').trim() || undefined,
      dateRange: (Array.isArray(filter.dateRange) && filter.dateRange.length === 2 && filter.dateRange[0] && filter.dateRange[1]) ? [filter.dateRange[0], filter.dateRange[1]] : undefined
    }
    params.filter = JSON.stringify(payload)
  }
  return await http.get<any, PagedResult<OpLogItem>>('/v1/logs/my', { params })
}

// 新增：Web 个人中心聚合数据
export interface WebUserCenterData {
  loginDays: number
  lastLoginTime: string | null
  operationCount: number
  favoriteCount: number
  recentActivities: Array<{
    id: string
    action: string
    resource: string
    module?: string
    feature?: string
    review?: string
    createdAt: string
  }>
}

export async function getWebUserCenter(): Promise<WebUserCenterData> {
  return await http.get<any, WebUserCenterData>('/web/user/center')
}