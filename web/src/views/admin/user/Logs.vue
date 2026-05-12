<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
    <div class="page-container">


        <!-- 日志列表（使用通用 DataTable 组件） -->
        <DataTable
            :data="tableData"
            :loading="loading"
            :columns="columns"
            :pagination="dtPagination"
            :search-config="searchConfig"
            :compact-mode="true"
            :default-expand-search="false"
            cache-key="my-logs"
            @search="onDtSearch"
            @page-change="onPageChange"
            @size-change="onSizeChange"
        >
            <template #header>
                <h2>我的日志</h2>
                <p>查看您的操作记录和活动日志</p>
            </template>

            <template #column-action="{ row }">
                <el-tag :type="getActionTagType(row.action)" size="small">{{ getActionText(row.action) }}</el-tag>
            </template>

            <template #actions="{ row }">
                <el-button type="primary" link size="small" @click="handleViewDetail(row)">详情</el-button>
            </template>
        </DataTable>
    </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { Search, Refresh } from '@element-plus/icons-vue'
import { getMyOpLogs, type OpLogItem } from '../../../api/user'
// 使用相对路径引入通用 DataTable 组件
// 注意：相对当前文件 web/src/views/admin/user/Logs.vue
import DataTable from '../../../components/DataTable/index.vue'

// DataTable 内部使用的分页对象
const dtPagination = reactive({ page: 1, pageSize: 20, total: 0 })

// 列配置
const columns = [
    { prop: 'action', label: '操作类型', width: 120, slot: 'column-action' },
    { prop: 'module', label: '模块', width: 120 },
    { prop: 'description', label: '操作描述', minWidth: 200 },
    { prop: 'ip', label: 'IP地址', width: 140 },
    { prop: 'userAgent', label: '用户代理', minWidth: 200 },
    { prop: 'createdAt', label: '操作时间', width: 160 }
]

// 搜索配置：标题 + 时间范围（下发为统一 filter JSON 到后端）
const searchConfig = [
    { key: 'module', label: '模块', type: 'input' as const, simple: true, placeholder: '请输入模块名称' },
    { key: 'dateRange', label: '时间范围', type: 'daterange' as const, simple: true }
]

// 最近一次筛选条件（前端过滤使用）
const lastFilters = ref<Record<string, any>>({})

// 表格数据（真实数据）
type LogRow = {
    id: string
    action: string
    module: string
    description: string
    ip?: string
    userAgent?: string
    createdAt: string
    // 供前端搜索用（后端如返回，则会被填充）
    userName?: string
    phone?: string
    email?: string
}
const tableData = ref<LogRow[]>([])

const loading = ref(false)

// 获取操作类型标签类型
function getActionTagType(action: string) {
    switch (action) {
        case 'login': return 'success'
        case 'logout': return 'info'
        case 'create': return 'primary'
        case 'update': return 'warning'
        case 'delete': return 'danger'
        default: return ''
    }
}

// 获取操作类型文本
function getActionText(action: string) {
    switch (action) {
        case 'login': return '登录'
        case 'logout': return '登出'
        case 'create': return '创建'
        case 'update': return '更新'
        case 'delete': return '删除'
        default: return action
    }
}

// 映射后端日志项到表格展示项
function mapLogItem(item: OpLogItem) {
    const createdAt = item.createdAt ? new Date(item.createdAt).toLocaleString('zh-CN') : ''
    const moduleName = item.moduleCN || item.resource || ''
    const description = item.featureCN || `${item.action}${item.resource ? ' - ' + item.resource : ''}`
    return {
        id: item.id,
        action: item.action,
        module: moduleName,
        description,
        ip: item.ip,
        userAgent: item.userAgent,
        createdAt,
        // 透传后端可能包含的用户名/邮箱/手机号字段，便于前端过滤
        userName: (item as any).userName,
        phone: (item as any).phone,
        email: (item as any).email
    }
}

// DataTable 统一搜索事件：将 keyword 传到后端；后端进行用户维度过滤
function onDtSearch(params: any) {
    dtPagination.page = 1
    lastFilters.value = params?.filters || {}
    loadData()
}

// 查看详情
function handleViewDetail(row: any) {
    ElMessage.info(`查看日志详情: ${row.description}`)
}

function onSizeChange(size: number) {
    dtPagination.pageSize = size
    dtPagination.page = 1
    loadData()
}

function onPageChange(page: number) {
    dtPagination.page = page
    loadData()
}

// 加载数据
async function loadData() {
    loading.value = true
    try {
        // 通过 filter JSON 传参：{ title, dateRange: [from, to] }
        const filters = { ...lastFilters.value }
        const kw = (filters.module || '').toString().trim()
        const range = Array.isArray(filters.dateRange) && filters.dateRange.length === 2 ? filters.dateRange : undefined
        const resp = await getMyOpLogs(dtPagination.page, dtPagination.pageSize, {
            title: undefined as any, // 兼容旧签名，不使用
            // 新后端需解析 module 字段；为保持 API 函数签名，传入 filter JSON 时包含 module
            dateRange: range as any
        })
        dtPagination.total = resp.total
        // 映射数据（如需前端筛选，可在此对 resp.items 进行过滤）
        let items = resp.items.map(mapLogItem)
        // 本地兜底过滤（后端已支持则尽量依赖后端）
        if (kw) items = items.filter(x => (x.module || '').includes(kw))
        if (range) {
            const [start, end] = range
            const s = new Date(start).getTime(); const e = new Date(end).getTime()
            items = items.filter(x => { const t = x.createdAt ? new Date(x.createdAt).getTime() : 0; return t>=s && t<=e })
        }
        tableData.value = items
    } catch (e) {
        tableData.value = []
    } finally {
        loading.value = false
    }
}

onMounted(() => {
    loadData()
})
</script>

<style scoped>
.page-container {
    padding: 0;
}

.page-header {
    margin-bottom: 24px;
}

.page-title h1 {
    font-size: 24px;
    font-weight: 600;
    color: #1f2937;
    margin: 0 0 4px 0;
}

.page-title p {
    font-size: 14px;
    color: #6b7280;
    margin: 0;
}

.search-section {
    margin-bottom: 24px;
}

.search-card {
    border-radius: 12px;
    border: 1px solid #e5e7eb;
}

.search-card :deep(.el-card__body) {
    padding: 20px;
}

.search-form {
    margin: 0;
}

.search-form :deep(.el-form-item) {
    margin-bottom: 0;
    margin-right: 24px;
}

.table-section {
    margin-bottom: 24px;
}

.table-card {
    border-radius: 12px;
    border: 1px solid #e5e7eb;
}

.table-card :deep(.el-card__body) {
    padding: 0;
}

.data-table {
    border-radius: 12px;
}

.data-table :deep(.el-table__header) {
    background: #f9fafb;
}

.data-table :deep(.el-table__header th) {
    background: #f9fafb;
    color: #374151;
    font-weight: 600;
    border-bottom: 1px solid #e5e7eb;
}

.data-table :deep(.el-table__body tr:hover) {
    background: #f9fafb;
}

.pagination-container {
    padding: 20px;
    display: flex;
    justify-content: flex-end;
    border-top: 1px solid #e5e7eb;
}

/* 深色模式 */
.dark .page-title h1 {
    color: #f9fafb;
}

.dark .page-title p {
    color: #9ca3af;
}

.dark .search-card,
.dark .table-card {
    background: #1f2937;
    border-color: #374151;
}

.dark .data-table :deep(.el-table__header th) {
    background: #374151;
    color: #e5e7eb;
    border-bottom-color: #4b5563;
}

.dark .data-table :deep(.el-table__body tr:hover) {
    background: #374151;
}

.dark .pagination-container {
    border-top-color: #4b5563;
}
</style>