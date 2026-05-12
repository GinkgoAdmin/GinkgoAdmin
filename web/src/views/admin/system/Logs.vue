<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="logs-page">
    <div class="logs-overview">
      <div class="overview-card is-total">
        <span class="overview-label">当前筛选总数</span>
        <strong class="overview-value">{{ pagination.total }}</strong>
        <span class="overview-desc">系统操作日志总量会随筛选条件实时变化</span>
      </div>
      <div class="overview-card is-success">
        <span class="overview-label">本页正常日志</span>
        <strong class="overview-value">{{ pageSummary.normal }}</strong>
        <span class="overview-desc">快速确认稳定请求与成功操作</span>
      </div>
      <div class="overview-card is-danger">
        <span class="overview-label">本页错误日志</span>
        <strong class="overview-value">{{ pageSummary.error }}</strong>
        <span class="overview-desc">异常记录会使用高亮颜色提醒处理</span>
      </div>
    </div>

    <DataTable
      :data="rows"
      :loading="loading"
      :columns="columns"
      :pagination="pagination"
      :compact-mode="true"
      :show-column-settings="true"
      :show-export="true"
      cache-key="system-op-logs"
      :search-config="searchConfig"
      :row-class-name="getRowClassName"
      @search="onSearch"
      @page-change="onPageChange"
      @size-change="onSizeChange"
      @row-click="openDetail"
    >
      <template #header>
        <h2>操作日志</h2>
        <p>按模块、功能、类型快速筛选系统操作记录，异常项会醒目高亮。</p>
      </template>

      <template #header-actions>
        <el-button @click="refresh">
          <i class="bi bi-arrow-clockwise" style="margin-right: 4px;"></i>刷新
        </el-button>
      </template>

      <template #column-displayName="{ row }">
        <div class="user-cell">
          <span class="user-name">{{ row.displayName || row.userName || '系统用户' }}</span>
          <span class="user-meta">
            {{ row.userName || '匿名' }}
            <template v-if="row.ip"> · {{ row.ip }}</template>
          </span>
        </div>
      </template>

      <template #column-createdAt="{ row }">
        <div class="time-cell">
          <span>{{ formatLogTime(row.createdAt) }}</span>
          <span class="time-meta" v-if="row.elapsedMs != null">耗时 {{ row.elapsedMs }} ms</span>
        </div>
      </template>

      <template #column-moduleCN="{ row }">
        <div class="module-cell">
          <span class="module-title">{{ row.moduleCN || '未归类模块' }}</span>
          <span class="module-meta">{{ row.featureCN || '未标注功能' }}</span>
        </div>
      </template>

      <template #column-featureCN="{ row }">
        <div class="feature-cell">
          <span class="feature-title">{{ row.featureCN || '未标注功能' }}</span>
          <span class="feature-preview">{{ buildLogPreviewText(row) }}</span>
        </div>
      </template>

      <template #column-result="{ row }">
        <div class="result-cell">
          <span class="result-dot" :class="getLogResultMeta(row.result).dotClass"></span>
          <el-tag :type="getLogResultMeta(row.result).tagType" effect="light" round>
            {{ getLogResultMeta(row.result).label }}
          </el-tag>
        </div>
      </template>

      <template #column-action="{ row }">
        <el-tag :type="getMethodTagType(row.action)" effect="plain" class="method-tag">
          {{ row.action || 'N/A' }}
        </el-tag>
      </template>

      <template #column-resource="{ row }">
        <div class="resource-cell">
          <code>{{ row.resource || '-' }}</code>
        </div>
      </template>

      <template #column-preview="{ row }">
        <button v-permission="'/system/logs:detail'" class="preview-button" type="button" @click.stop="openDetail(row)">
          <span class="preview-text">{{ buildLogPreviewText(row) }}</span>
          <span class="preview-link">查看更多</span>
        </button>
      </template>

      <template #actions="{ row }">
        <el-button v-permission="'/system/logs:detail'" type="primary" link size="small" @click.stop="openDetail(row)">详情</el-button>
      </template>
    </DataTable>

    <el-drawer
      v-model="detailVisible"
      title="日志详情"
      size="680px"
      destroy-on-close
      class="log-detail-drawer"
    >
      <template v-if="selectedLog">
        <div class="detail-header">
          <div class="detail-title-group">
            <div class="detail-title">
              {{ selectedLog.moduleCN || '未归类模块' }}
              <span>/</span>
              {{ selectedLog.featureCN || '未标注功能' }}
            </div>
            <div class="detail-subtitle">{{ buildLogPreviewText(selectedLog) }}</div>
          </div>
          <el-tag :type="getLogResultMeta(selectedLog.result).tagType" effect="dark" round>
            {{ getLogResultMeta(selectedLog.result).label }}
          </el-tag>
        </div>

        <div class="detail-grid">
          <div class="detail-item">
            <span class="detail-label">操作人</span>
            <span class="detail-value">{{ selectedLog.displayName || selectedLog.userName || '系统用户' }}</span>
          </div>
          <div class="detail-item">
            <span class="detail-label">账号</span>
            <span class="detail-value">{{ selectedLog.userName || '-' }}</span>
          </div>
          <div class="detail-item">
            <span class="detail-label">操作时间</span>
            <span class="detail-value">{{ formatLogTime(selectedLog.createdAt) }}</span>
          </div>
          <div class="detail-item">
            <span class="detail-label">结果原值</span>
            <span class="detail-value">{{ selectedLog.result || '-' }}</span>
          </div>
          <div class="detail-item">
            <span class="detail-label">请求方式</span>
            <span class="detail-value">{{ selectedLog.action || '-' }}</span>
          </div>
          <div class="detail-item">
            <span class="detail-label">耗时</span>
            <span class="detail-value">{{ selectedLog.elapsedMs != null ? `${selectedLog.elapsedMs} ms` : '-' }}</span>
          </div>
          <div class="detail-item detail-item-full">
            <span class="detail-label">请求资源</span>
            <code class="detail-code">{{ selectedLog.resource || '-' }}</code>
          </div>
          <div class="detail-item">
            <span class="detail-label">IP</span>
            <span class="detail-value">{{ selectedLog.ip || '-' }}</span>
          </div>
          <div class="detail-item">
            <span class="detail-label">手机号</span>
            <span class="detail-value">{{ selectedLog.phone || '-' }}</span>
          </div>
          <div class="detail-item detail-item-full">
            <span class="detail-label">邮箱</span>
            <span class="detail-value">{{ selectedLog.email || '-' }}</span>
          </div>
          <div class="detail-item detail-item-full">
            <span class="detail-label">UserAgent</span>
            <div class="detail-block">{{ selectedLog.userAgent || '-' }}</div>
          </div>
          <div class="detail-item detail-item-full" v-if="selectedLog.reviewCN">
            <span class="detail-label">审计摘要</span>
            <div class="detail-block">{{ selectedLog.reviewCN }}</div>
          </div>
          <div class="detail-item detail-item-full" v-if="formattedDataJson">
            <span class="detail-label">附加数据</span>
            <pre class="detail-json">{{ formattedDataJson }}</pre>
          </div>
        </div>
      </template>
    </el-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import DataTable from '../../../components/DataTable/index.vue'
import type { SearchFieldConfig } from '../../../components/DataTable/types'
import { getOpLogs, type AdminLogFilter, type AdminOpLogItem, type PagedResult } from '../../../api/system'
import {
  buildLogFilterOptions,
  buildLogPreviewText,
  formatLogTime,
  getLogResultMeta,
  safeFormatJson,
  type SystemLogRow
} from './logs.utils'

type ViewLogRow = SystemLogRow

const loading = ref(false)
const rows = ref<ViewLogRow[]>([])
const detailVisible = ref(false)
const selectedLog = ref<ViewLogRow | null>(null)
const pagination = ref({ total: 0, page: 1, pageSize: 20, pageSizes: [10, 20, 50, 100] })
const currentFilter = ref<AdminLogFilter>({})

const columns = [
  { prop: 'displayName', label: '操作人', minWidth: 150, slot: 'column-displayName' },
  { prop: 'createdAt', label: '时间', width: 190, slot: 'column-createdAt' },
  { prop: 'moduleCN', label: '模块', minWidth: 170, slot: 'column-moduleCN' },
  { prop: 'featureCN', label: '功能', minWidth: 220, slot: 'column-featureCN' },
  { prop: 'result', label: '类型', width: 110, slot: 'column-result', align: 'center' as const },
  { prop: 'action', label: '请求', width: 100, slot: 'column-action', align: 'center' as const },
  { prop: 'resource', label: '资源', minWidth: 240, slot: 'column-resource' },
  { prop: 'preview', label: '内容摘要', minWidth: 260, slot: 'column-preview' }
]

const searchConfig = computed<SearchFieldConfig[]>(() => {
  const filterOptions = buildLogFilterOptions(rows.value)

  return [
    { key: 'module', label: '模块', type: 'input', placeholder: '输入模块名称', simple: true, width: 150 },
    {
      key: 'feature',
      label: '功能',
      type: filterOptions.features.length > 0 ? 'select' : 'input',
      options: filterOptions.features,
      filterable: true,
      placeholder: filterOptions.features.length > 0 ? '选择功能' : '输入功能名称',
      simple: true,
      width: 180
    },
    {
      key: 'type',
      label: '类型',
      type: 'select',
      options: [
        { label: '正常', value: 'normal' },
        { label: '错误', value: 'error' }
      ],
      placeholder: '选择类型',
      simple: true,
      width: 120
    },
    { key: 'keyword', label: '关键词', type: 'input', placeholder: '用户/资源/摘要', simple: true, width: 180 },
    { key: 'dateRange', label: '日期范围', type: 'daterange', simple: true, width: 360 }
  ]
})

const pageSummary = computed(() => {
  return rows.value.reduce(
    (acc, row) => {
      const kind = getLogResultMeta(row.result).kind
      if (kind === 'normal') acc.normal += 1
      if (kind === 'error') acc.error += 1
      return acc
    },
    { normal: 0, error: 0 }
  )
})

const formattedDataJson = computed(() => safeFormatJson(selectedLog.value?.dataJson))

function mapLogItem(item: AdminOpLogItem): ViewLogRow {
  return {
    id: String(item.id),
    action: item.action || '',
    resource: item.resource || '',
    moduleCN: item.moduleCN || '未归类模块',
    featureCN: item.featureCN || '未标注功能',
    result: item.result || '',
    reviewCN: item.reviewCN || '',
    dataJson: item.dataJson || '',
    createdAt: item.createdAt,
    userName: item.userName ?? null,
    displayName: item.displayName ?? null,
    email: item.email ?? null,
    phone: item.phone ?? null,
    ip: item.ip,
    userAgent: item.userAgent,
    elapsedMs: item.elapsedMs ?? null
  }
}

function getMethodTagType(action?: string) {
  switch (String(action || '').toUpperCase()) {
    case 'GET':
      return 'info'
    case 'POST':
      return 'primary'
    case 'PUT':
    case 'PATCH':
      return 'warning'
    case 'DELETE':
      return 'danger'
    default:
      return undefined
  }
}

function getRowClassName({ row }: { row: ViewLogRow; rowIndex: number }) {
  const kind = getLogResultMeta(row.result).kind
  if (kind === 'error') return 'log-row-error'
  if (kind === 'normal') return 'log-row-success'
  return ''
}

async function load() {
  loading.value = true
  try {
    const response: PagedResult<AdminOpLogItem> = await getOpLogs(
      pagination.value.page,
      pagination.value.pageSize,
      currentFilter.value
    )
    rows.value = Array.isArray(response.items) ? response.items.map(mapLogItem) : []
    pagination.value.total = Number(response.total || 0)
  } catch {
    rows.value = []
    pagination.value.total = 0
    ElMessage.error('加载日志失败')
  } finally {
    loading.value = false
  }
}

function onSearch(payload: { filters?: Record<string, any>; page?: number; pageSize?: number }) {
  const filters = payload?.filters || {}
  currentFilter.value = {}

  if (filters.module) currentFilter.value.module = String(filters.module || '').trim()
  if (filters.feature) currentFilter.value.feature = String(filters.feature || '').trim()
  if (filters.type) currentFilter.value.type = String(filters.type || '').trim()
  if (filters.keyword) currentFilter.value.keyword = String(filters.keyword || '').trim()
  if (Array.isArray(filters.dateRange) && filters.dateRange[0] && filters.dateRange[1]) {
    currentFilter.value.dateRange = [filters.dateRange[0], filters.dateRange[1]]
  }

  pagination.value.page = payload?.page ? Number(payload.page) || 1 : 1
  if (payload?.pageSize) pagination.value.pageSize = Number(payload.pageSize) || 20
  load()
}

function onPageChange(page: number) {
  pagination.value.page = page
  load()
}

function onSizeChange(size: number) {
  pagination.value.pageSize = size
  pagination.value.page = 1
  load()
}

function refresh() {
  load()
}

function openDetail(row: ViewLogRow) {
  selectedLog.value = row
  detailVisible.value = true
}

onMounted(load)
</script>

<style scoped>
.logs-page {
  padding: 24px;
  background:
    radial-gradient(circle at top left, rgba(59, 130, 246, 0.08), transparent 26%),
    radial-gradient(circle at top right, rgba(16, 185, 129, 0.08), transparent 24%),
    var(--el-bg-color-page);
  min-height: 100vh;
}

.logs-overview {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
  margin-bottom: 18px;
}

.overview-card {
  position: relative;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 18px 20px;
  border-radius: 18px;
  border: 1px solid rgba(148, 163, 184, 0.18);
  background: rgba(255, 255, 255, 0.86);
  box-shadow: 0 14px 32px rgba(15, 23, 42, 0.06);
  backdrop-filter: blur(10px);
}

.overview-card::after {
  content: '';
  position: absolute;
  inset: auto -24px -24px auto;
  width: 96px;
  height: 96px;
  border-radius: 999px;
  opacity: 0.12;
}

.overview-card.is-total::after {
  background: #3b82f6;
}

.overview-card.is-success::after {
  background: #10b981;
}

.overview-card.is-danger::after {
  background: #ef4444;
}

.overview-label {
  font-size: 13px;
  font-weight: 600;
  color: #64748b;
}

.overview-value {
  font-size: 30px;
  line-height: 1;
  font-weight: 700;
  color: #0f172a;
}

.overview-desc {
  font-size: 13px;
  color: #94a3b8;
}

.user-cell,
.time-cell,
.module-cell,
.feature-cell {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.user-name,
.module-title,
.feature-title {
  font-weight: 600;
  color: #0f172a;
}

.user-meta,
.time-meta,
.module-meta,
.feature-preview {
  font-size: 12px;
  color: #64748b;
}

.result-cell {
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.result-dot {
  width: 8px;
  height: 8px;
  border-radius: 999px;
  box-shadow: 0 0 0 4px rgba(148, 163, 184, 0.16);
}

.result-dot.is-success {
  background: #10b981;
  box-shadow: 0 0 0 4px rgba(16, 185, 129, 0.14);
}

.result-dot.is-danger {
  background: #ef4444;
  box-shadow: 0 0 0 4px rgba(239, 68, 68, 0.14);
}

.result-dot.is-warning {
  background: #f59e0b;
  box-shadow: 0 0 0 4px rgba(245, 158, 11, 0.14);
}

.result-dot.is-info {
  background: #3b82f6;
  box-shadow: 0 0 0 4px rgba(59, 130, 246, 0.14);
}

.method-tag {
  min-width: 64px;
  justify-content: center;
  font-weight: 600;
}

.resource-cell code,
.detail-code {
  display: inline-block;
  width: 100%;
  padding: 6px 10px;
  border-radius: 10px;
  background: rgba(15, 23, 42, 0.06);
  color: #1e293b;
  font-size: 12px;
  line-height: 1.5;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.preview-button {
  width: 100%;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 6px;
  padding: 0;
  border: none;
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.preview-text {
  color: #334155;
  line-height: 1.5;
}

.preview-link {
  font-size: 12px;
  font-weight: 600;
  color: #2563eb;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
  margin-bottom: 20px;
  padding: 18px;
  border-radius: 18px;
  background: linear-gradient(135deg, rgba(59, 130, 246, 0.1), rgba(15, 23, 42, 0.04));
}

.detail-title-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.detail-title {
  font-size: 18px;
  font-weight: 700;
  color: #0f172a;
}

.detail-title span {
  margin: 0 6px;
  color: #94a3b8;
}

.detail-subtitle {
  color: #475569;
  line-height: 1.6;
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 14px;
}

.detail-item {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 14px;
  border-radius: 14px;
  background: rgba(248, 250, 252, 0.96);
  border: 1px solid rgba(226, 232, 240, 0.92);
}

.detail-item-full {
  grid-column: 1 / -1;
}

.detail-label {
  font-size: 12px;
  font-weight: 600;
  color: #64748b;
}

.detail-value,
.detail-block {
  color: #0f172a;
  line-height: 1.7;
  word-break: break-all;
}

.detail-json {
  margin: 0;
  padding: 14px;
  border-radius: 14px;
  background: #0f172a;
  color: #e2e8f0;
  font-size: 12px;
  line-height: 1.7;
  overflow-x: auto;
}

:deep(.log-row-error > td) {
  background: rgba(254, 242, 242, 0.68) !important;
}

:deep(.log-row-success > td) {
  background: rgba(240, 253, 244, 0.55) !important;
}

.admin-dark .overview-card {
  background: rgba(15, 23, 42, 0.82);
  border-color: rgba(71, 85, 105, 0.46);
  box-shadow: 0 16px 36px rgba(2, 6, 23, 0.36);
}

.admin-dark .overview-label,
.admin-dark .user-meta,
.admin-dark .time-meta,
.admin-dark .module-meta,
.admin-dark .feature-preview,
.admin-dark .detail-label,
.admin-dark .overview-desc {
  color: #94a3b8;
}

.admin-dark .overview-value,
.admin-dark .user-name,
.admin-dark .module-title,
.admin-dark .feature-title,
.admin-dark .detail-title,
.admin-dark .detail-value,
.admin-dark .detail-block,
.admin-dark .preview-text {
  color: #e2e8f0;
}

.admin-dark .resource-cell code,
.admin-dark .detail-code {
  background: rgba(30, 41, 59, 0.95);
  color: #cbd5e1;
}

.admin-dark .detail-header {
  background: linear-gradient(135deg, rgba(30, 64, 175, 0.28), rgba(15, 23, 42, 0.28));
}

.admin-dark .detail-item {
  background: rgba(15, 23, 42, 0.9);
  border-color: rgba(51, 65, 85, 0.82);
}

.admin-dark :deep(.log-row-error > td) {
  background: rgba(69, 10, 10, 0.42) !important;
}

.admin-dark :deep(.log-row-success > td) {
  background: rgba(5, 46, 22, 0.28) !important;
}

@media (max-width: 1280px) {
  .logs-overview {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 768px) {
  .logs-page {
    padding: 16px;
  }

  .logs-overview,
  .detail-grid {
    grid-template-columns: 1fr;
  }

  .detail-header {
    flex-direction: column;
  }
}
</style>
