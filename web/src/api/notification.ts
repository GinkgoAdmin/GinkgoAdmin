import http from './http'

export interface NotificationListItem {
  id: string
  title: string
  status: string
  publishedAt?: string | null
  createdAt?: string
}

export interface NotificationDetail extends NotificationListItem {
  contentType?: number
  contentText?: string | null
  contentHtml?: string | null
}

export interface PagedResult<T> {
  total: number
  page: number
  pageSize: number
  items: T[]
}

export interface NotificationStats {
  id: string
  totalRecipients: number
  deliveredCount: number
  readCount: number
  deliveredUsers: Array<{ id: string; name: string }>
  unreadUsers: Array<{ id: string; name: string }>
  readUsers: Array<{ id: string; name: string }>
}

export interface SaveTargetsInput {
  all?: boolean
  userIds?: string[]
  roleIds?: string[]
  departmentIds?: string[]
  deptScope?: 'DeptOnly' | 'DeptWithChildren'
}

export async function getNotifications(page=1, pageSize=20, filter?: { title?: string; dateRange?: [string, string] }): Promise<PagedResult<NotificationListItem>> {
  const params: any = { page, pageSize }
  if (filter) {
    const payload: any = {
      title: (filter.title || '').trim() || undefined,
      dateRange: (Array.isArray(filter.dateRange) && filter.dateRange.length===2 && filter.dateRange[0] && filter.dateRange[1]) ? [filter.dateRange[0], filter.dateRange[1]] : undefined
    }
    params.filter = JSON.stringify(payload)
  }
  const res = await http.get<any, PagedResult<NotificationListItem> | { data: PagedResult<NotificationListItem> }>('/v1/notifications', { params })
  return (res as any)?.data ?? (res as PagedResult<NotificationListItem>)
}

export async function getNotificationDetail(id: string): Promise<NotificationDetail> {
  const res = await http.get<any, NotificationDetail | { data: NotificationDetail }>(`/v1/notifications/${id}`)
  return (res as any)?.data ?? (res as NotificationDetail)
}

export interface AudienceSeed {
  targetType: number // 1:User 2:Role 3:Dept 4:All
  targetValue: string
}

export async function createNotification(input: { 
  title: string
  contentType: number
  contentText?: string
  contentHtml?: string
  audience?: AudienceSeed[]
}): Promise<string> {
  return await http.post<any, string>('/v1/notifications', input)
}

export async function updateNotification(id: string, input: { 
  title: string
  contentType: number
  contentText?: string
  contentHtml?: string
  audience?: AudienceSeed[]
}): Promise<void> {
  await http.put(`/v1/notifications/${id}`, input)
}

export async function publishNotification(id: string): Promise<void> {
  await http.post(`/v1/notifications/${id}/publish`)
}

export async function deleteNotification(id: string): Promise<void> {
  await http.delete(`/v1/notifications/${id}`)
}

export async function getNotificationStats(id: string): Promise<NotificationStats> {
  return await http.get<any, NotificationStats>(`/v1/notifications/${id}/stats`)
}

export async function saveNotificationTargets(id: string, input: SaveTargetsInput): Promise<void> {
  await http.post(`/v1/notifications/${id}/targets`, input)
}

// 标记“我的通知”为已读（进入详情时可调用）
export async function markMyNotificationRead(id: string): Promise<void> {
  await http.post(`/v1/notifications/my/${id}/read`)
}


