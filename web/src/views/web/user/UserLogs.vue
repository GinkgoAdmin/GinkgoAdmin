<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="user-logs">
    <div class="container">
      <div class="user-center-layout">
        <!-- 侧边导航 -->
        <WebUserSidebar :user-info="userInfo" @logout="handleLogout" />

        <!-- 主要内容区域 -->
        <main class="main-content">
          <div class="content-header">
            <h1>{{ t('logs_title') }}</h1>
            <p>{{ t('logs_subtitle') }}</p>
          </div>

          <!-- 筛选工具栏 -->
          <div class="filter-toolbar">
            <div class="filter-left">
              <el-select v-model="filterType" :placeholder="t('logs_type')" clearable style="width: 150px">
                <el-option :label="t('logs_all')" value="" />
                <el-option :label="t('logs_login')" value="login" />
                <el-option :label="t('logs_operation')" value="operation" />
                <el-option :label="t('logs_setting')" value="setting" />
                <el-option :label="t('logs_security')" value="security" />
              </el-select>

              <el-date-picker
                v-model="dateRange"
                type="daterange"
                range-separator="~"
                :start-placeholder="t('logs_start_date')"
                :end-placeholder="t('logs_end_date')"
                format="YYYY-MM-DD"
                value-format="YYYY-MM-DD"
                style="width: 240px"
              />
            </div>

            <div class="filter-right">
              <el-button @click="handleRefresh" :loading="loading">
                <el-icon><Refresh /></el-icon>
                {{ t('logs_refresh') }}
              </el-button>
              <el-button @click="handleExport">
                <el-icon><Download /></el-icon>
                {{ t('logs_export') }}
              </el-button>
            </div>
          </div>

          <!-- 日志列表 -->
          <div class="logs-container">
            <div v-if="loading" class="loading-container">
              <el-skeleton :rows="5" animated />
            </div>

            <div v-else-if="paginatedLogs.length === 0" class="empty-container">
              <el-empty :description="t('logs_empty')" />
            </div>

            <div v-else class="logs-list">
              <div v-for="log in paginatedLogs" :key="log.id" class="log-item">
                <div class="log-icon">
                  <el-icon :class="getLogIconClass(log.type)">
                    <component :is="getLogIcon(log.type)" />
                  </el-icon>
                </div>

                <div class="log-content">
                  <div class="log-header">
                    <h3 class="log-title">{{ log.title }}</h3>
                    <span class="log-time">{{ formatTime(log.time) }}</span>
                  </div>

                  <p class="log-description">{{ log.description }}</p>

                  <div class="log-meta">
                    <span class="log-type-badge" :class="'badge-' + log.type">{{ getLogTypeText(log.type) }}</span>
                    <span class="log-ip" v-if="log.ip">
                      <el-icon class="meta-icon"><Location /></el-icon>
                      IP: {{ log.ip }}
                    </span>
                    <span class="log-device" v-if="log.device">{{ log.device }}</span>
                  </div>
                </div>

                <div class="log-status">
                  <el-tag :type="getLogStatusType(log.status)" size="small" effect="light" round>
                    {{ getLogStatusText(log.status) }}
                  </el-tag>
                </div>
              </div>
            </div>

            <!-- 分页 -->
            <div v-if="filteredLogs.length > 0" class="pagination-container">
              <el-pagination
                v-model:current-page="currentPage"
                v-model:page-size="pageSize"
                :page-sizes="[10, 20, 50, 100]"
                :total="totalCount"
                layout="total, sizes, prev, pager, next, jumper"
                background
              />
            </div>
          </div>
        </main>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  House, User, Document, SwitchButton, Refresh, Download,
  UserFilled, Setting, Shield, Operation, Location
} from '@element-plus/icons-vue'
import { useWebAuthStore } from '../../../stores/webAuth'
import { getMyOpLogs, type OpLogItem } from '../../../api/user'
import WebUserSidebar from './components/WebUserSidebar.vue'
import { t } from '@/utils/lang'


const router = useRouter()
const webAuth = useWebAuthStore()
const userInfo = computed(() => webAuth.userInfo)
const loading = ref(false)
const filterType = ref('')
const dateRange = ref<[string, string] | null>(null)
const currentPage = ref(1)
const pageSize = ref(20)

// 来自服务端的日志数据（当前页）
const logs = ref<any[]>([])
const totalCount = ref(0)

function mapToUi(item: OpLogItem) {
  const action = (item.action || '').toLowerCase()
  const type = (action === 'login' || action === 'logout')
    ? 'login'
    : (action === 'update' || action === 'create' || action === 'delete') ? 'operation' : 'operation'
  const title = item.featureCN || item.moduleCN || item.resource || action || t('logs_title')
  const description = item.moduleCN || item.resource || ''
  const time = item.createdAt
  const ip = item.ip
  const device = item.userAgent || ''
  const status = 'success'
  const review = item.reviewCN
  let tt = title
  let dd = description
  if (review) {
    const parts = review.split('-')
    if (parts.length >= 2) {
      tt = parts[1]
      dd = review
    }
  }


  return { id: item.id, type, title: tt, description: dd, time, ip, device, status }
}

async function loadLogs() {
  loading.value = true
  try {
    const resp = await getMyOpLogs(currentPage.value, pageSize.value)
    totalCount.value = resp.total || 0
    logs.value = (resp.items || []).map(mapToUi)
  } catch (e) {
    ElMessage.error(t('logs_load_fail'))
  } finally {
    loading.value = false
  }
}

// 筛选后的日志
const filteredLogs = computed(() => {
  let result = logs.value

  // 按类型筛选
  if (filterType.value) {
    result = result.filter(log => log.type === filterType.value)
  }

  // 按日期范围筛选
  if (dateRange.value && dateRange.value.length === 2) {
    const [startDate, endDate] = dateRange.value
    result = result.filter(log => {
      const logDate = new Date(log.time).toISOString().split('T')[0]
      return logDate >= startDate && logDate <= endDate
    })
  }

  return result.sort((a, b) => new Date(b.time).getTime() - new Date(a.time).getTime())
})

// 分页后的日志（此处 logs 已是当前页数据，直接返回）
const paginatedLogs = computed(() => filteredLogs.value)

// 获取日志图标
const getLogIcon = (type: string) => {
  switch (type) {
    case 'login':
      return 'UserFilled'
    case 'operation':
      return 'Operation'
    case 'setting':
      return 'Setting'
    case 'security':
      return 'Shield'
    default:
      return 'Document'
  }
}

// 获取日志图标样式类
const getLogIconClass = (type: string) => {
  switch (type) {
    case 'login':
      return 'icon-primary'
    case 'operation':
      return 'icon-success'
    case 'setting':
      return 'icon-info'
    case 'security':
      return 'icon-warning'
    default:
      return 'icon-default'
  }
}

// 获取日志类型文本
const getLogTypeText = (type: string) => {
  switch (type) {
    case 'login':
      return t('logs_login')
    case 'operation':
      return t('logs_operation')
    case 'setting':
      return t('logs_setting')
    case 'security':
      return t('logs_security')
    default:
      return t('logs_other')
  }
}

// 获取日志状态类型
const getLogStatusType = (status: string) => {
  switch (status) {
    case 'success':
      return 'success'
    case 'failed':
      return 'danger'
    case 'warning':
      return 'warning'
    default:
      return 'info'
  }
}

// 获取日志状态文本
const getLogStatusText = (status: string) => {
  switch (status) {
    case 'success':
      return t('logs_success')
    case 'failed':
      return t('logs_failed')
    case 'warning':
      return t('logs_warning')
    default:
      return t('logs_unknown')
  }
}

// 格式化时间
const formatTime = (timeStr: string) => {
  const time = new Date(timeStr)
  const now = new Date()
  const diff = now.getTime() - time.getTime()

  const minutes = Math.floor(diff / (1000 * 60))
  const hours = Math.floor(diff / (1000 * 60 * 60))
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))

  if (minutes < 60) {
    return t('time_min_ago', { n: minutes })
  } else if (hours < 24) {
    return t('time_hour_ago', { n: hours })
  } else if (days < 7) {
    return t('time_day_ago', { n: days })
  } else {
    return time.toLocaleString('zh-CN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    })
  }
}

// 刷新日志
const handleRefresh = async () => {
  await loadLogs()
  ElMessage.success(t('logs_refreshed'))
}

// 导出日志：按当前筛选条件批量拉取后台数据，前端生成 CSV 直接触发浏览器下载，
// 不依赖后端额外导出接口；UTF-8 BOM 保证 Excel 中文不乱码，字段统一双引号包裹并转义。
const exporting = ref(false)
const handleExport = async () => {
  if (exporting.value) return
  exporting.value = true
  ElMessage.info(t('logs_exporting'))
  try {
    // 一次性拉取较大页码以覆盖个人用户的常规日志量；超大量时仍由后端分页保护
    const filter: any = {}
    if (dateRange.value && dateRange.value.length === 2) {
      filter.dateRange = dateRange.value
    }
    const resp = await getMyOpLogs(1, 2000, Object.keys(filter).length ? filter : undefined)
    let items = (resp.items || []).map(mapToUi)
    // 前端按当前类型筛选条件再次过滤，保证导出与界面所见一致
    if (filterType.value) {
      items = items.filter(log => log.type === filterType.value)
    }
    if (items.length === 0) {
      ElMessage.warning(t('logs_export_empty'))
      return
    }

    // 表头使用稳定的中文常量，避免 t() 在缺失 key 时回退为 key 本身导致表头出现英文键名
    const headers = ['标题', '类型', '状态', 'IP', '设备', '时间']
    const escape = (raw: any) => {
      const v = raw === null || raw === undefined ? '' : String(raw)
      // RFC 4180：含逗号 / 引号 / 换行的字段使用双引号包裹，内部双引号转义为两个
      return /[",\n\r]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v
    }
    const rows = items.map(log => [
      log.title,
      getLogTypeText(log.type),
      getLogStatusText(log.status),
      log.ip || '',
      log.device || '',
      log.time
    ])
    const csv = [headers, ...rows]
      .map(row => row.map(escape).join(','))
      .join('\r\n')

    // UTF-8 BOM，避免 Excel 用 GBK 解码导致中文乱码
    const blob = new Blob(['\uFEFF' + csv], { type: 'text/csv;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    const ts = new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-')
    a.href = url
    a.download = `user-logs-${ts}.csv`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)

    ElMessage.success(t('logs_export_success', { n: items.length }))
  } catch (e) {
    ElMessage.error(t('logs_export_fail'))
  } finally {
    exporting.value = false
  }
}

// 处理退出登录
const handleLogout = async () => {
  try {
    await ElMessageBox.confirm(t('confirm_logout'), t('tip'), {
      confirmButtonText: t('confirm'),
      cancelButtonText: t('cancel'),
      type: 'warning'
    })

    webAuth.logout()
    ElMessage.success(t('logged_out'))
    router.push('/web')
  } catch (e) {
    // 用户取消退出
  }
}

onMounted(async () => {
  webAuth.initFromStorage()
  if (!webAuth.isAuthenticated) {
    router.push('/web/login')
    return
  }
  await loadLogs()
})

watch([currentPage, pageSize], async () => {
  if (!webAuth.isAuthenticated) return
  await loadLogs()
})
</script>

<style scoped>
.user-logs {
  min-height: calc(100vh - 4rem - 200px);
  background: #f8fafc;
  padding: 2rem 0;
}

.container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 0 1rem;
}

.user-center-layout {
  display: grid;
  grid-template-columns: 280px 1fr;
  gap: 2rem;
  align-items: start;
}

/* 主要内容区域 - 统一白色卡片 */
.main-content {
  background: white;
  border-radius: 16px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.06);
  padding: 2rem;
}

.content-header {
  margin-bottom: 1.5rem;
}

.content-header h1 {
  font-size: 1.75rem;
  font-weight: 700;
  color: #1e293b;
  margin: 0 0 0.5rem 0;
}

.content-header p {
  color: #64748b;
  margin: 0;
  font-size: 1rem;
}

/* 筛选工具栏 */
.filter-toolbar {
  padding: 1rem 1.25rem;
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  background: #f8fafc;
  border-radius: 12px;
  margin-bottom: 1.5rem;
  border: 1px solid #f1f5f9;
}

.filter-left {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.filter-right {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

/* 日志容器 */
.logs-container {
  border-radius: 12px;
  overflow: hidden;
}

.loading-container,
.empty-container {
  padding: 2rem;
}

.logs-list {
  display: flex;
  flex-direction: column;
}

.log-item {
  display: flex;
  align-items: flex-start;
  gap: 1rem;
  padding: 1.25rem 0;
  border-bottom: 1px solid #f1f5f9;
  transition: all 0.2s ease;
}

.log-item:hover {
  background: #fafbfc;
  margin: 0 -1rem;
  padding-left: 1rem;
  padding-right: 1rem;
  border-radius: 10px;
}

.log-item:last-child {
  border-bottom: none;
}

.log-icon {
  width: 38px;
  height: 38px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1rem;
  flex-shrink: 0;
}

.icon-primary {
  background: linear-gradient(135deg, #eff6ff 0%, #dbeafe 100%);
  color: #3b82f6;
}

.icon-success {
  background: linear-gradient(135deg, #f0fdf4 0%, #dcfce7 100%);
  color: #22c55e;
}

.icon-info {
  background: linear-gradient(135deg, #ecfeff 0%, #cffafe 100%);
  color: #06b6d4;
}

.icon-warning {
  background: linear-gradient(135deg, #fff7ed 0%, #ffedd5 100%);
  color: #f97316;
}

.icon-default {
  background: #f1f5f9;
  color: #64748b;
}

.log-content {
  flex: 1;
  min-width: 0;
}

.log-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 0.35rem;
  gap: 1rem;
}

.log-title {
  font-size: .95rem;
  font-weight: 600;
  color: #1e293b;
  margin: 0;
}

.log-time {
  color: #94a3b8;
  font-size: 0.8rem;
  white-space: nowrap;
}

.log-description {
  color: #64748b;
  margin: 0 0 0.5rem 0;
  font-size: 0.85rem;
  line-height: 1.5;
}

.log-meta {
  display: flex;
  align-items: center;
  gap: .75rem;
  font-size: 0.75rem;
  color: #94a3b8;
}

.meta-icon {
  font-size: .7rem;
  margin-right: .15rem;
}

/* 日志类型彩色小标签 */
.log-type-badge {
  font-size: .7rem;
  font-weight: 600;
  padding: .15rem .5rem;
  border-radius: 6px;
}
.badge-login { background: #eff6ff; color: #3b82f6; }
.badge-operation { background: #f0fdf4; color: #22c55e; }
.badge-setting { background: #ecfeff; color: #06b6d4; }
.badge-security { background: #fff7ed; color: #f97316; }

.log-status {
  flex-shrink: 0;
}

/* 分页 */
.pagination-container {
  padding: 1.5rem 0 0;
  border-top: 1px solid #f1f5f9;
  display: flex;
  justify-content: center;
}

/* 响应式设计 */
@media (max-width: 1024px) {
  .user-center-layout {
    grid-template-columns: 1fr;
    gap: 1.5rem;
  }
}

@media (max-width: 768px) {
  .user-logs {
    padding: 1rem 0;
  }

  .main-content {
    padding: 1.5rem;
  }

  .filter-toolbar {
    flex-direction: column;
    align-items: stretch;
    gap: 1rem;
  }

  .filter-left {
    flex-direction: column;
    align-items: stretch;
  }

  .log-item {
    padding: 1rem 0;
  }

  .log-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.5rem;
  }

  .log-meta {
    flex-direction: column;
    align-items: flex-start;
    gap: 0.25rem;
  }
}

@media (max-width: 480px) {
  .log-item {
    flex-direction: column;
    gap: 0.75rem;
  }

  .log-icon {
    align-self: flex-start;
  }
}
</style>