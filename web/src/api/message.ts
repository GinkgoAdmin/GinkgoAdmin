import http from './http'

// ---- 类型定义 ----

/** 消息列表项 */
export interface MessageListItem {
  id: string
  title: string
  summary?: string
  type: string
  isRead: boolean
  createdAt: string
  deliveryRole: string // 'primary' | 'cc'
}

/** 消息附件 DTO */
export interface MessageAttachmentDto {
  id: string
  fileId: string
  fileName: string
  fileSize: number
  attachmentType: string // 'image' | 'file'
  fileUrl?: string // 文件相对路径（来自 SysFile.Url）
}

/** 消息链接 DTO */
export interface MessageLinkDto {
  id: string
  title: string
  platform: string // 'web' | 'wpf' | 'uniapp'
  url: string
}

/** 消息详情 */
export interface MessageDetail extends MessageListItem {
  content?: string
  readAt?: string
  attachments: MessageAttachmentDto[]
  links: MessageLinkDto[]
}

/** 分页结果 */
export interface PagedResult<T> {
  total: number
  page: number
  pageSize: number
  items: T[]
}

/** 接收对象组 */
export interface RecipientGroup {
  mode: 'all' | 'users' | 'roles' | 'departments'
  ids?: string[]
}

/** 附件创建输入 */
export interface CreateAttachmentInput {
  fileId: string
  fileName: string
  fileSize: number
  attachmentType: string
}

/** 链接创建输入 */
export interface CreateLinkInput {
  title: string
  platform: string
  url: string
}

/** 消息创建表单 */
export interface CreateMessageInput {
  title: string
  titleI18n?: string | null
  summary?: string
  content?: string
  type: string
  primary: RecipientGroup
  cc?: RecipientGroup | null
  attachments?: CreateAttachmentInput[]
  links?: CreateLinkInput[]
}

// ---- API 调用 ----

/** 获取消息列表（分页） */
export async function getMessageList(params: {
  pageIndex?: number
  pageSize?: number
  isRead?: boolean
  deliveryRole?: string
}): Promise<PagedResult<MessageListItem>> {
  return await http.get<any, PagedResult<MessageListItem>>('/message/list', { params })
}

/** 获取消息详情（自动传入 platform=web） */
export async function getMessageDetail(id: string): Promise<MessageDetail> {
  return await http.get<any, MessageDetail>(`/message/${id}`, { params: { platform: 'web' } })
}

/** 创建消息 */
export async function createMessage(input: CreateMessageInput): Promise<void> {
  await http.post('/message', input)
}

/** 标记消息为已读 */
export async function markMessageAsRead(id: string): Promise<void> {
  await http.put(`/message/${id}/read`)
}

/** 标记所有消息为已读 */
export async function markAllMessagesAsRead(): Promise<void> {
  await http.put('/message/read-all')
}

/** 获取未读消息数量 */
export async function getUnreadMessageCount(): Promise<number> {
  return await http.get<any, number>('/message/unread-count')
}

// ---- 管理端 API ----

/** 管理端消息列表项 */
export interface AdminMessageListItem {
  title: string
  createdAt: string
  totalRecipients: number
  readCount: number
  status: string
}

/** 管理端消息统计 */
export interface AdminMessageStats {
  totalRecipients: number
  deliveredCount: number
  readCount: number
  deliveredUsers: Array<{ id: string; name: string }>
  unreadUsers: Array<{ id: string; name: string }>
  readUsers: Array<{ id: string; name: string }>
}

/** 管理端消息详情 */
export interface AdminMessageDetail {
  title: string
  createdAt: string
  content?: string
  summary?: string
  type: string
  totalRecipients: number
  readCount: number
  attachments: MessageAttachmentDto[]
  links: MessageLinkDto[]
}

/** 管理端：获取已发送消息列表 */
export async function getAdminMessageList(params: {
  page?: number
  pageSize?: number
  title?: string
  startDate?: string
  endDate?: string
}): Promise<PagedResult<AdminMessageListItem>> {
  return await http.get<any, PagedResult<AdminMessageListItem>>('/message/admin/list', { params })
}

/** 管理端：获取消息投递统计 */
export async function getAdminMessageStats(title: string, createdAt: string): Promise<AdminMessageStats> {
  return await http.get<any, AdminMessageStats>('/message/admin/stats', { params: { title, createdAt } })
}

/** 管理端：获取消息批次详情（含正文、附件和链接） */
export async function getAdminMessageDetail(title: string, createdAt: string): Promise<AdminMessageDetail> {
  return await http.get<any, AdminMessageDetail>('/message/admin/detail', { params: { title, createdAt } })
}

/** 管理端：删除一批消息 */
export async function deleteAdminMessageBatch(title: string, createdAt: string): Promise<void> {
  await http.delete('/message/admin/batch', { params: { title, createdAt } })
}
