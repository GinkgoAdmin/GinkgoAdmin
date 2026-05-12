import http from './http'

export interface DashboardStats {
  totalUsers: number
  totalRoles: number
  totalDepts: number
  totalFiles: number
  totalModules: number
  todayLogs: number
  yesterdayLogs: number
  recentNewUsers: number
  totalNotifications: number
}

export interface TrendItem {
  date: string
  count: number
}

export interface RecentActivity {
  id: string
  action: string
  resource: string
  moduleCN?: string
  featureCN?: string
  reviewCN?: string
  userName?: string
  createdAt: string
}

/** 获取首页统计概览 */
export async function getDashboardStats(): Promise<DashboardStats> {
  return await http.get<any, DashboardStats>('/v1/dashboard/stats')
}

/** 获取操作日志趋势 */
export async function getLogTrend(days = 7): Promise<TrendItem[]> {
  return await http.get<any, TrendItem[]>('/v1/dashboard/log-trend', { params: { days } })
}

/** 获取用户注册趋势 */
export async function getUserTrend(days = 7): Promise<TrendItem[]> {
  return await http.get<any, TrendItem[]>('/v1/dashboard/user-trend', { params: { days } })
}

/** 获取最近操作活动 */
export async function getRecentActivities(limit = 10): Promise<RecentActivity[]> {
  return await http.get<any, RecentActivity[]>('/v1/dashboard/recent-activities', { params: { limit } })
}
