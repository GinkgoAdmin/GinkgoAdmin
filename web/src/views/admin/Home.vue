<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="dashboard-container">
    <!-- 欢迎区域 -->
    <div class="welcome-section">
      <div class="welcome-card">
        <div class="welcome-content">
          <div class="welcome-avatar">
            <el-avatar :size="64" class="user-avatar">
              {{ (auth.userName || '用户').charAt(0).toUpperCase() }}
            </el-avatar>
          </div>
          <div class="welcome-info">
            <h1>欢迎回来，{{ auth.userName }}！</h1>
            <p>今天是 {{ currentDate }}，{{ getGreeting() }}</p>
            <div class="welcome-stats">
              <span class="stat-item">
                <el-icon><Clock /></el-icon>
                今日操作：{{ dashStats.todayLogs }} 次
              </span>
            </div>
          </div>
        </div>
        <div class="welcome-actions">
          <el-button v-permission="'/dashboard:view-data'" type="primary" :icon="DataAnalysis" @click="goToAnalytics">
            查看数据
          </el-button>
        </div>
      </div>
    </div>

    <!-- 统计卡片 -->
    <div class="stats-section">
      <div class="stats-grid">
        <div class="stat-card stat-card-primary">
          <div class="stat-icon">
            <el-icon size="24"><User /></el-icon>
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ dashStats.totalUsers }}</div>
            <div class="stat-label">总用户数</div>
            <div class="stat-trend positive" v-if="dashStats.recentNewUsers > 0">
              <el-icon><ArrowUp /></el-icon>
              <span>近7天+{{ dashStats.recentNewUsers }}</span>
            </div>
          </div>
        </div>

        <div class="stat-card stat-card-success">
          <div class="stat-icon">
            <el-icon size="24"><Box /></el-icon>
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ dashStats.totalModules }}</div>
            <div class="stat-label">已安装插件</div>
          </div>
        </div>

        <div class="stat-card stat-card-warning">
          <div class="stat-icon">
            <el-icon size="24"><Document /></el-icon>
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ dashStats.todayLogs }}</div>
            <div class="stat-label">今日操作</div>
            <div :class="['stat-trend', logTrendDir >= 0 ? 'positive' : 'negative']" v-if="dashStats.yesterdayLogs > 0">
              <el-icon><component :is="logTrendDir >= 0 ? ArrowUp : ArrowDown" /></el-icon>
              <span>{{ logTrendDir >= 0 ? '+' : '' }}{{ logTrendPct }}%</span>
            </div>
          </div>
        </div>

        <div class="stat-card stat-card-info">
          <div class="stat-icon">
            <el-icon size="24"><Files /></el-icon>
          </div>
          <div class="stat-content">
            <div class="stat-value">{{ dashStats.totalFiles }}</div>
            <div class="stat-label">文件总数</div>
          </div>
        </div>
      </div>
    </div>

    <!-- 主要内容区域 -->
    <div class="main-content-section">
      <el-row :gutter="24">
        <!-- 左侧：图表和数据 -->
        <el-col :xs="24" :lg="16">
          <div class="content-card chart-card">
            <div class="card-header">
              <h3>操作日志趋势</h3>
              <div class="card-actions">
                <el-button-group size="small">
                  <el-button v-permission="'/dashboard:trend:7'" :type="chartPeriod === 7 ? 'primary' : ''" @click="switchPeriod(7)">7天</el-button>
                  <el-button v-permission="'/dashboard:trend:30'" :type="chartPeriod === 30 ? 'primary' : ''" @click="switchPeriod(30)">30天</el-button>
                  <el-button v-permission="'/dashboard:trend:90'" :type="chartPeriod === 90 ? 'primary' : ''" @click="switchPeriod(90)">90天</el-button>
                </el-button-group>
              </div>
            </div>
            <div class="chart-container" ref="chartRef"></div>
          </div>

          <!-- 快速操作 -->
          <div class="content-card quick-actions-card">
            <div class="card-header">
              <h3>快速操作</h3>
            </div>
            <div class="quick-actions-grid">
              <router-link :to="`${adminBasePath}/system/users`" class="quick-action-item">
                <div class="action-icon">
                  <el-icon><User /></el-icon>
                </div>
                <div class="action-content">
                  <div class="action-title">用户管理</div>
                  <div class="action-desc">{{ dashStats.totalUsers }} 个用户</div>
                </div>
              </router-link>

              <router-link :to="`${adminBasePath}/system/roles`" class="quick-action-item">
                <div class="action-icon action-icon-green">
                  <el-icon><UserFilled /></el-icon>
                </div>
                <div class="action-content">
                  <div class="action-title">角色管理</div>
                  <div class="action-desc">{{ dashStats.totalRoles }} 个角色</div>
                </div>
              </router-link>

              <router-link :to="`${adminBasePath}/system/departments`" class="quick-action-item">
                <div class="action-icon action-icon-orange">
                  <el-icon><OfficeBuilding /></el-icon>
                </div>
                <div class="action-content">
                  <div class="action-title">部门管理</div>
                  <div class="action-desc">{{ dashStats.totalDepts }} 个部门</div>
                </div>
              </router-link>

              <router-link :to="`${adminBasePath}/system/logs`" class="quick-action-item">
                <div class="action-icon action-icon-purple">
                  <el-icon><Document /></el-icon>
                </div>
                <div class="action-content">
                  <div class="action-title">日志管理</div>
                  <div class="action-desc">今日 {{ dashStats.todayLogs }} 条</div>
                </div>
              </router-link>
            </div>
          </div>
        </el-col>

        <!-- 右侧：系统信息和通知 -->
        <el-col :xs="24" :lg="8">
          <!-- 系统概况 -->
          <div class="content-card system-status-card">
            <div class="card-header">
              <h3>系统概况</h3>
              <el-tag type="success" size="small">运行正常</el-tag>
            </div>
            <div class="system-status-list">
              <div class="status-item">
                <div class="status-label">用户总数</div>
                <div class="status-value"><span class="status-number">{{ dashStats.totalUsers }}</span></div>
              </div>
              <div class="status-item">
                <div class="status-label">角色总数</div>
                <div class="status-value"><span class="status-number">{{ dashStats.totalRoles }}</span></div>
              </div>
              <div class="status-item">
                <div class="status-label">部门总数</div>
                <div class="status-value"><span class="status-number">{{ dashStats.totalDepts }}</span></div>
              </div>
              <div class="status-item">
                <div class="status-label">文件总数</div>
                <div class="status-value"><span class="status-number">{{ dashStats.totalFiles }}</span></div>
              </div>
              <div class="status-item">
                <div class="status-label">已安装插件</div>
                <div class="status-value"><span class="status-number">{{ dashStats.totalModules }}</span></div>
              </div>
              <div class="status-item">
                <div class="status-label">通知总数</div>
                <div class="status-value"><span class="status-number">{{ dashStats.totalNotifications }}</span></div>
              </div>
            </div>
          </div>

          <!-- 最近活动 -->
          <div class="content-card activity-card">
            <div class="card-header">
              <h3>最近活动</h3>
            </div>
            <div class="activity-list" v-loading="activitiesLoading">
              <template v-if="recentActivities.length > 0">
                <div class="activity-item" v-for="activity in recentActivities" :key="activity.id">
                  <div class="activity-avatar">
                    <el-avatar :size="32">{{ (activity.userName || '?').charAt(0) }}</el-avatar>
                  </div>
                  <div class="activity-content">
                    <div class="activity-text">
                      <span class="activity-user">{{ activity.userName || '未知用户' }}</span>
                      {{ formatActivityText(activity) }}
                    </div>
                    <div class="activity-time">{{ formatRelativeTime(activity.createdAt) }}</div>
                  </div>
                </div>
              </template>
              <el-empty v-else description="暂无活动记录" :image-size="60" />
            </div>
          </div>
        </el-col>
      </el-row>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, nextTick, watch, shallowRef } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import {
  Clock, DataAnalysis, User, UserFilled, Document,
  ArrowUp, ArrowDown, Files, Box, OfficeBuilding
} from '@element-plus/icons-vue'
import {
  getDashboardStats, getLogTrend, getRecentActivities,
  type DashboardStats, type TrendItem, type RecentActivity
} from '../../api/dashboard'
import { adminBasePath } from '../../config/admin'
import * as echarts from 'echarts'

const router = useRouter()
const auth = useAuthStore()

// 深色模式检测
const isDark = computed(() => auth.theme === 'dark')

// === 统计数据 ===
const dashStats = ref<DashboardStats>({
  totalUsers: 0, totalRoles: 0, totalDepts: 0, totalFiles: 0,
  totalModules: 0, todayLogs: 0, yesterdayLogs: 0,
  recentNewUsers: 0, totalNotifications: 0
})

// 日志趋势方向和百分比
const logTrendDir = computed(() => dashStats.value.todayLogs - dashStats.value.yesterdayLogs)
const logTrendPct = computed(() => {
  const y = dashStats.value.yesterdayLogs
  if (y === 0) return dashStats.value.todayLogs > 0 ? 100 : 0
  return Math.round(((dashStats.value.todayLogs - y) / y) * 100)
})

// 当前日期
const currentDate = computed(() => {
  const now = new Date()
  return now.toLocaleDateString('zh-CN', {
    year: 'numeric', month: 'long', day: 'numeric', weekday: 'long'
  })
})

// === 图表 ===
const chartPeriod = ref(7)
const chartRef = ref<HTMLDivElement>()
const logTrendData = ref<TrendItem[]>([])
let chartInstance: echarts.ECharts | null = null

function renderChart() {
  if (!chartRef.value) return
  // 深色模式切换时重新创建实例
  if (chartInstance) {
    chartInstance.dispose()
    chartInstance = null
  }
  chartInstance = echarts.init(chartRef.value, isDark.value ? 'dark' : undefined)
  const data = logTrendData.value
  const dark = isDark.value
  chartInstance.setOption({
    backgroundColor: 'transparent',
    tooltip: {
      trigger: 'axis',
      backgroundColor: dark ? 'rgba(30,41,59,0.95)' : 'rgba(0,0,0,0.75)',
      borderColor: dark ? '#475569' : 'transparent',
      borderWidth: dark ? 1 : 0,
      textStyle: { color: '#fff', fontSize: 13 }
    },
    grid: { left: 48, right: 20, top: 20, bottom: 30 },
    xAxis: {
      type: 'category',
      data: data.map(d => d.date),
      axisLine: { lineStyle: { color: dark ? '#475569' : '#e5e7eb' } },
      axisLabel: { color: dark ? '#94a3b8' : '#6b7280', fontSize: 12 },
      axisTick: { lineStyle: { color: dark ? '#475569' : '#e5e7eb' } }
    },
    yAxis: {
      type: 'value',
      splitLine: { lineStyle: { type: 'dashed', color: dark ? '#334155' : '#f3f4f6' } },
      axisLabel: { color: dark ? '#94a3b8' : '#6b7280', fontSize: 12 }
    },
    series: [{
      type: 'line',
      data: data.map(d => d.count),
      smooth: true,
      symbol: 'circle',
      symbolSize: 7,
      lineStyle: { width: 3, color: new echarts.graphic.LinearGradient(0, 0, 1, 0, [
        { offset: 0, color: dark ? '#818cf8' : '#667eea' },
        { offset: 1, color: dark ? '#a78bfa' : '#764ba2' }
      ]) },
      itemStyle: { color: dark ? '#818cf8' : '#667eea', borderWidth: 2, borderColor: dark ? '#1e293b' : '#fff' },
      areaStyle: { color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
        { offset: 0, color: dark ? 'rgba(129,140,248,0.3)' : 'rgba(102,126,234,0.35)' },
        { offset: 1, color: dark ? 'rgba(129,140,248,0.02)' : 'rgba(102,126,234,0.02)' }
      ]) }
    }]
  }, true)
}

async function switchPeriod(days: number) {
  chartPeriod.value = days
  await loadLogTrend()
}

// === 最近活动 ===
const recentActivities = ref<RecentActivity[]>([])
const activitiesLoading = ref(false)

function formatActivityText(a: RecentActivity): string {
  if (a.reviewCN) return a.reviewCN
  const parts: string[] = []
  if (a.moduleCN) parts.push(a.moduleCN)
  if (a.featureCN) parts.push(a.featureCN)
  if (parts.length > 0) return parts.join(' - ')
  return `${a.action} ${a.resource}`
}

function formatRelativeTime(dateStr: string): string {
  const date = new Date(dateStr)
  const now = new Date()
  const diffMs = now.getTime() - date.getTime()
  const diffMin = Math.floor(diffMs / 60000)
  if (diffMin < 1) return '刚刚'
  if (diffMin < 60) return `${diffMin}分钟前`
  const diffHr = Math.floor(diffMin / 60)
  if (diffHr < 24) return `${diffHr}小时前`
  const diffDay = Math.floor(diffHr / 24)
  if (diffDay < 30) return `${diffDay}天前`
  return date.toLocaleDateString('zh-CN')
}

// === 获取问候语 ===
function getGreeting() {
  const hour = new Date().getHours()
  if (hour < 6) return '夜深了，注意休息'
  if (hour < 9) return '早上好'
  if (hour < 12) return '上午好'
  if (hour < 14) return '中午好'
  if (hour < 18) return '下午好'
  if (hour < 22) return '晚上好'
  return '夜深了，注意休息'
}

function goToAnalytics() {
  router.push(`${adminBasePath}/dashboard`)
}

// === 数据加载 ===
async function loadStats() {
  try {
    dashStats.value = await getDashboardStats()
  } catch (e) { console.error('[Dashboard] loadStats failed', e) }
}

async function loadLogTrend() {
  try {
    logTrendData.value = await getLogTrend(chartPeriod.value)
    await nextTick()
    renderChart()
  } catch (e) { console.error('[Dashboard] loadLogTrend failed', e) }
}

async function loadActivities() {
  activitiesLoading.value = true
  try {
    recentActivities.value = await getRecentActivities(8)
  } catch (e) { console.error('[Dashboard] loadActivities failed', e) }
  finally { activitiesLoading.value = false }
}

// resize 处理
function handleResize() { chartInstance?.resize() }

onMounted(async () => {
  await Promise.all([loadStats(), loadLogTrend(), loadActivities()])
  window.addEventListener('resize', handleResize)
})

// 深色模式切换时重新渲染图表
watch(isDark, () => {
  if (logTrendData.value.length > 0) {
    nextTick(() => renderChart())
  }
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', handleResize)
  chartInstance?.dispose()
  chartInstance = null
})
</script>

<style scoped>
.dashboard-container {
  padding: 0;
}

/* 欢迎区域 */
.welcome-section {
  margin-bottom: 24px;
}

.welcome-card {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  border-radius: 16px;
  padding: 32px;
  color: white;
  display: flex;
  justify-content: space-between;
  align-items: center;
  position: relative;
  overflow: hidden;
}

.welcome-card::before {
  content: '';
  position: absolute;
  top: 0;
  right: 0;
  width: 200px;
  height: 200px;
  background: radial-gradient(circle, rgba(255,255,255,0.1) 0%, transparent 70%);
  border-radius: 50%;
  transform: translate(50%, -50%);
}

.welcome-content {
  display: flex;
  align-items: center;
  gap: 20px;
  flex: 1;
}

.welcome-avatar .user-avatar {
  background: rgba(255, 255, 255, 0.2);
  color: white;
  font-weight: 600;
  font-size: 24px;
  border: 3px solid rgba(255, 255, 255, 0.3);
}

.welcome-info h1 {
  font-size: 28px;
  font-weight: 600;
  margin: 0 0 8px 0;
}

.welcome-info p {
  font-size: 16px;
  opacity: 0.9;
  margin: 0 0 12px 0;
}

.welcome-stats {
  display: flex;
  gap: 24px;
}

.stat-item {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  opacity: 0.8;
}

.welcome-actions {
  z-index: 1;
}

/* 统计卡片 */
.stats-section {
  margin-bottom: 24px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 20px;
}

.stat-card {
  background: white;
  border-radius: 12px;
  padding: 24px;
  border: 1px solid #e5e7eb;
  display: flex;
  align-items: center;
  gap: 16px;
  transition: all 0.2s ease;
  position: relative;
  overflow: hidden;
}

.stat-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 25px rgba(0, 0, 0, 0.1);
}

.stat-card::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 4px;
}

.stat-card-primary::before { background: linear-gradient(90deg, #3b82f6, #2563eb); }
.stat-card-success::before { background: linear-gradient(90deg, #10b981, #059669); }
.stat-card-warning::before { background: linear-gradient(90deg, #f59e0b, #d97706); }
.stat-card-info::before { background: linear-gradient(90deg, #8b5cf6, #7c3aed); }

.stat-icon {
  width: 56px;
  height: 56px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
}

.stat-card-primary .stat-icon { background: linear-gradient(135deg, #3b82f6, #2563eb); }
.stat-card-success .stat-icon { background: linear-gradient(135deg, #10b981, #059669); }
.stat-card-warning .stat-icon { background: linear-gradient(135deg, #f59e0b, #d97706); }
.stat-card-info .stat-icon { background: linear-gradient(135deg, #8b5cf6, #7c3aed); }

.stat-content { flex: 1; }
.stat-value { font-size: 28px; font-weight: 700; color: #1f2937; line-height: 1.2; margin-bottom: 4px; }
.stat-label { font-size: 14px; color: #6b7280; margin-bottom: 8px; }
.stat-trend { display: flex; align-items: center; gap: 4px; font-size: 12px; font-weight: 600; }
.stat-trend.positive { color: #059669; }
.stat-trend.negative { color: #dc2626; }

/* 内容卡片 */
.main-content-section { margin-bottom: 24px; }

.content-card {
  background: white;
  border-radius: 12px;
  border: 1px solid #e5e7eb;
  margin-bottom: 24px;
  overflow: hidden;
}

.card-header {
  padding: 20px 24px;
  border-bottom: 1px solid #f3f4f6;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.card-header h3 { font-size: 18px; font-weight: 600; color: #1f2937; margin: 0; }

/* 图表卡片 */
.chart-container { padding: 16px 24px 24px; height: 300px; }

/* 快速操作 */
.quick-actions-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
  padding: 24px;
}

.quick-action-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
  text-decoration: none;
  color: inherit;
  transition: all 0.2s ease;
}

.quick-action-item:hover {
  border-color: #3b82f6;
  background: #f8fafc;
  transform: translateY(-1px);
}

.action-icon {
  width: 40px; height: 40px;
  background: linear-gradient(135deg, #3b82f6, #2563eb);
  border-radius: 8px;
  display: flex; align-items: center; justify-content: center;
  color: white;
  flex-shrink: 0;
}
.action-icon-green { background: linear-gradient(135deg, #10b981, #059669); }
.action-icon-orange { background: linear-gradient(135deg, #f59e0b, #d97706); }
.action-icon-purple { background: linear-gradient(135deg, #8b5cf6, #7c3aed); }

.action-title { font-size: 14px; font-weight: 600; color: #1f2937; margin-bottom: 2px; }
.action-desc { font-size: 12px; color: #6b7280; }

/* 系统状态 */
.system-status-list { padding: 16px 24px; }

.status-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 0;
  border-bottom: 1px solid #f9fafb;
}
.status-item:last-child { border-bottom: none; }
.status-label { font-size: 14px; color: #6b7280; }
.status-number { font-size: 16px; font-weight: 700; color: #1f2937; }

/* 活动列表 */
.activity-list { padding: 16px 24px; max-height: 380px; overflow-y: auto; }

.activity-item {
  display: flex;
  gap: 12px;
  padding: 10px 0;
  border-bottom: 1px solid #f9fafb;
}
.activity-item:last-child { border-bottom: none; }

.activity-content { flex: 1; min-width: 0; }
.activity-text { font-size: 13px; color: #374151; line-height: 1.5; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.activity-user { font-weight: 600; color: #1f2937; margin-right: 4px; }
.activity-time { font-size: 12px; color: #9ca3af; margin-top: 2px; }

/* ==================== 深色模式 (admin-dark) ==================== */

/* 欢迎卡片 */
.admin-dark .welcome-card {
  background: linear-gradient(135deg, #4338ca 0%, #6d28d9 100%);
}

/* 统计卡片 & 内容卡片 */
.admin-dark .stat-card,
.admin-dark .content-card {
  background: #1e293b;
  border-color: #334155;
}

.admin-dark .stat-card:hover {
  box-shadow: 0 8px 25px rgba(0, 0, 0, 0.35);
}

/* 卡片头部 */
.admin-dark .card-header {
  border-bottom-color: #334155;
}
.admin-dark .card-header h3 {
  color: #f1f5f9;
}

/* 统计数值 */
.admin-dark .stat-value {
  color: #f1f5f9;
}
.admin-dark .stat-label {
  color: #94a3b8;
}
.admin-dark .stat-trend.positive {
  color: #34d399;
}
.admin-dark .stat-trend.negative {
  color: #f87171;
}

/* 快速操作 */
.admin-dark .quick-action-item {
  border-color: #334155;
  background: #1e293b;
}
.admin-dark .quick-action-item:hover {
  background: #334155;
  border-color: #6366f1;
  box-shadow: 0 4px 12px rgba(99, 102, 241, 0.15);
}
.admin-dark .action-title {
  color: #f1f5f9;
}
.admin-dark .action-desc {
  color: #94a3b8;
}

/* 系统概况 */
.admin-dark .status-item {
  border-bottom-color: #334155;
}
.admin-dark .status-label {
  color: #94a3b8;
}
.admin-dark .status-number {
  color: #e2e8f0;
}

/* 活动列表 */
.admin-dark .activity-item {
  border-bottom-color: #334155;
}
.admin-dark .activity-text {
  color: #cbd5e1;
}
.admin-dark .activity-user {
  color: #f1f5f9;
}
.admin-dark .activity-time {
  color: #64748b;
}

/* Element Plus 组件在深色下的调整 */
.admin-dark .el-tag--success {
  --el-tag-bg-color: rgba(52, 211, 153, 0.15);
  --el-tag-border-color: rgba(52, 211, 153, 0.3);
  --el-tag-text-color: #34d399;
}
.admin-dark .el-progress :deep(.el-progress-bar__outer) {
  background-color: #334155;
}
.admin-dark .el-empty :deep(.el-empty__description p) {
  color: #64748b;
}

/* 响应式设计 */
@media (max-width: 768px) {
  .welcome-card {
    flex-direction: column;
    text-align: center;
    gap: 20px;
  }
  .welcome-content {
    flex-direction: column;
    text-align: center;
  }
  .stats-grid { grid-template-columns: 1fr; }
  .quick-actions-grid { grid-template-columns: 1fr; }
}
</style>
