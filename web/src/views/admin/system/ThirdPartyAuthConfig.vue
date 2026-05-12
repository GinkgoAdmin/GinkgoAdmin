<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="third-auth-admin">
    <!-- 统计卡片 -->
    <div class="stats-row">
      <div class="stat-card">
        <div class="stat-icon binding"><i class="bi bi-link-45deg"></i></div>
        <div class="stat-info">
          <span class="stat-value">{{ stats.totalBindings }}</span>
          <span class="stat-label">绑定总数</span>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon users"><i class="bi bi-people"></i></div>
        <div class="stat-info">
          <span class="stat-value">{{ stats.totalUsers }}</span>
          <span class="stat-label">绑定用户数</span>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon totp"><i class="bi bi-shield-check"></i></div>
        <div class="stat-info">
          <span class="stat-value">{{ totpStats.enabled }}</span>
          <span class="stat-label">已启用验证器</span>
        </div>
      </div>
      <div class="stat-card">
        <div class="stat-icon provider"><i class="bi bi-grid"></i></div>
        <div class="stat-info">
          <span class="stat-value">{{ stats.providers?.length || 0 }}</span>
          <span class="stat-label">接入平台数</span>
        </div>
      </div>
    </div>

    <!-- Tab切换 -->
    <el-card class="main-card">
      <el-tabs v-model="activeTab">
        <!-- ===== 第三方登录绑定 ===== -->
        <el-tab-pane label="第三方登录绑定" name="bindings">
          <div class="tab-toolbar">
            <div class="toolbar-left">
              <el-input v-model="bindingSearch.keyword" placeholder="搜索用户名/第三方昵称/邮箱" clearable style="width: 260px;" @keyup.enter="loadBindings" @clear="loadBindings">
                <template #prefix><i class="bi bi-search"></i></template>
              </el-input>
              <el-select v-model="bindingSearch.provider" placeholder="全部平台" clearable style="width: 150px;" @change="loadBindings">
                <el-option v-for="p in providerOptions" :key="p.value" :label="p.label" :value="p.value" />
              </el-select>
              <el-button @click="loadBindings" :loading="bindingLoading">
                <i class="bi bi-arrow-clockwise" style="margin-right: 4px;"></i>刷新
              </el-button>
            </div>
            <div class="toolbar-right">
              <el-button v-permission="'/system/third-auth:binding:batchdelete'" v-if="selectedBindingIds.length > 0" type="danger" size="small" @click="handleBatchDeleteBindings">
                <i class="bi bi-trash" style="margin-right: 4px;"></i>批量删除 ({{ selectedBindingIds.length }})
              </el-button>
            </div>
          </div>

          <el-table :data="bindings" v-loading="bindingLoading" stripe @selection-change="onBindingSelectionChange" style="width: 100%;">
            <el-table-column type="selection" width="42" />
            <el-table-column label="用户" min-width="160">
              <template #default="{ row }">
                <div class="user-cell">
                  <span class="user-name">{{ row.userDisplayName }}</span>
                  <span class="user-account">@{{ row.userName }}</span>
                </div>
              </template>
            </el-table-column>
            <el-table-column label="平台" width="120">
              <template #default="{ row }">
                <el-tag :type="getProviderTagType(row.provider)" size="small" effect="plain">
                  {{ getProviderLabel(row.provider) }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="providerDisplayName" label="第三方昵称" min-width="140" show-overflow-tooltip />
            <el-table-column prop="providerKey" label="第三方标识" min-width="160" show-overflow-tooltip />
            <el-table-column prop="email" label="邮箱" min-width="160" show-overflow-tooltip>
              <template #default="{ row }">{{ row.email || '-' }}</template>
            </el-table-column>
            <el-table-column label="头像" width="65" align="center">
              <template #default="{ row }">
                <el-avatar v-if="row.avatarUrl" :src="row.avatarUrl" :size="28" />
                <span v-else>-</span>
              </template>
            </el-table-column>
            <el-table-column label="令牌" width="80" align="center">
              <template #default="{ row }">
                <el-tag v-if="row.hasAccessToken" type="success" size="small" effect="plain">有效</el-tag>
                <el-tag v-else type="info" size="small" effect="plain">无</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="绑定时间" width="160">
              <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
            </el-table-column>
            <el-table-column label="操作" width="130" fixed="right">
              <template #default="{ row }">
                <el-button v-permission="'/system/third-auth:binding:detail'" type="primary" link size="small" @click="handleViewBinding(row)">
                  <i class="bi bi-eye" style="margin-right: 2px;"></i>详情
                </el-button>
                <el-button v-permission="'/system/third-auth:binding:delete'" type="danger" link size="small" @click="handleDeleteBinding(row)">
                  <i class="bi bi-trash" style="margin-right: 2px;"></i>删除
                </el-button>
              </template>
            </el-table-column>
          </el-table>

          <div class="pagination-wrapper">
            <el-pagination
              v-model:current-page="bindingPage.page"
              v-model:page-size="bindingPage.pageSize"
              :total="bindingPage.total"
              :page-sizes="[10, 20, 50, 100]"
              layout="total, sizes, prev, pager, next, jumper"
              @current-change="loadBindings"
              @size-change="() => { bindingPage.page = 1; loadBindings() }"
            />
          </div>
        </el-tab-pane>

        <!-- ===== TOTP验证器管理 ===== -->
        <el-tab-pane label="TOTP验证器" name="totp">
          <div class="tab-toolbar">
            <div class="toolbar-left">
              <el-input v-model="totpSearch.keyword" placeholder="搜索用户名" clearable style="width: 260px;" @keyup.enter="loadTotpList" @clear="loadTotpList">
                <template #prefix><i class="bi bi-search"></i></template>
              </el-input>
              <el-select v-model="totpSearch.isEnabled" placeholder="全部状态" clearable style="width: 130px;" @change="loadTotpList">
                <el-option label="已启用" :value="true" />
                <el-option label="已禁用" :value="false" />
              </el-select>
              <el-button @click="loadTotpList" :loading="totpLoading">
                <i class="bi bi-arrow-clockwise" style="margin-right: 4px;"></i>刷新
              </el-button>
            </div>
          </div>

          <el-table :data="totpList" v-loading="totpLoading" stripe style="width: 100%;">
            <el-table-column label="用户" min-width="180">
              <template #default="{ row }">
                <div class="user-cell">
                  <span class="user-name">{{ row.userDisplayName }}</span>
                  <span class="user-account">@{{ row.userName }}</span>
                </div>
              </template>
            </el-table-column>
            <el-table-column label="状态" width="100" align="center">
              <template #default="{ row }">
                <el-tag :type="row.isEnabled ? 'success' : 'info'" size="small">
                  {{ row.isEnabled ? '已启用' : '已禁用' }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="恢复码" width="100" align="center">
              <template #default="{ row }">
                <el-tag v-if="row.hasRecoveryCodes" type="success" size="small" effect="plain">已生成</el-tag>
                <span v-else style="color: #9ca3af;">无</span>
              </template>
            </el-table-column>
            <el-table-column label="创建时间" width="160">
              <template #default="{ row }">{{ formatDateTime(row.createdAt) }}</template>
            </el-table-column>
            <el-table-column label="更新时间" width="160">
              <template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template>
            </el-table-column>
            <el-table-column label="操作" width="160" fixed="right">
              <template #default="{ row }">
                <el-button v-permission="'/system/third-auth:totp:disable'" v-if="row.isEnabled" type="warning" link size="small" @click="handleDisableTotp(row)">
                  <i class="bi bi-pause-circle" style="margin-right: 2px;"></i>禁用
                </el-button>
                <el-button v-permission="'/system/third-auth:totp:delete'" type="danger" link size="small" @click="handleDeleteTotp(row)">
                  <i class="bi bi-trash" style="margin-right: 2px;"></i>删除
                </el-button>
              </template>
            </el-table-column>
          </el-table>

          <div class="pagination-wrapper">
            <el-pagination
              v-model:current-page="totpPage.page"
              v-model:page-size="totpPage.pageSize"
              :total="totpPage.total"
              :page-sizes="[10, 20, 50, 100]"
              layout="total, sizes, prev, pager, next, jumper"
              @current-change="loadTotpList"
              @size-change="() => { totpPage.page = 1; loadTotpList() }"
            />
          </div>
        </el-tab-pane>
      </el-tabs>
    </el-card>

    <!-- 绑定详情对话框 -->
    <el-dialog v-model="showDetailDialog" title="绑定详情" width="600px" destroy-on-close>
      <el-descriptions v-if="bindingDetail" :column="2" border>
        <el-descriptions-item label="用户名">{{ bindingDetail.userName || '-' }}</el-descriptions-item>
        <el-descriptions-item label="平台">
          <el-tag :type="getProviderTagType(bindingDetail.provider)" size="small">{{ getProviderLabel(bindingDetail.provider) }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="第三方昵称">{{ bindingDetail.providerDisplayName || '-' }}</el-descriptions-item>
        <el-descriptions-item label="第三方标识">{{ bindingDetail.providerKey || '-' }}</el-descriptions-item>
        <el-descriptions-item label="邮箱" :span="2">{{ bindingDetail.email || '-' }}</el-descriptions-item>
        <el-descriptions-item label="头像" :span="2">
          <el-avatar v-if="bindingDetail.avatarUrl" :src="bindingDetail.avatarUrl" :size="48" />
          <span v-else>无</span>
        </el-descriptions-item>
        <el-descriptions-item label="Access Token">
          <el-tag :type="bindingDetail.hasAccessToken ? 'success' : 'info'" size="small">{{ bindingDetail.hasAccessToken ? '已存储' : '无' }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="Refresh Token">
          <el-tag :type="bindingDetail.hasRefreshToken ? 'success' : 'info'" size="small">{{ bindingDetail.hasRefreshToken ? '已存储' : '无' }}</el-tag>
        </el-descriptions-item>
        <el-descriptions-item label="令牌过期时间" :span="2">{{ bindingDetail.tokenExpiresAt ? formatDateTime(bindingDetail.tokenExpiresAt) : '无' }}</el-descriptions-item>
        <el-descriptions-item label="绑定时间">{{ formatDateTime(bindingDetail.createdAt) }}</el-descriptions-item>
        <el-descriptions-item label="更新时间">{{ formatDateTime(bindingDetail.updatedAt) }}</el-descriptions-item>
      </el-descriptions>
      <!-- 原始数据 -->
      <div v-if="bindingDetail?.rawProfile" class="raw-profile-section">
        <div class="raw-profile-header">
          <span>原始用户信息 (JSON)</span>
          <el-button size="small" @click="copyRawProfile">复制</el-button>
        </div>
        <pre class="raw-profile-content">{{ formatJson(bindingDetail.rawProfile) }}</pre>
      </div>
      <template #footer>
        <el-button @click="showDetailDialog = false">关闭</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import http from '@/api/http'

const API_BASE = '/v1/external-auth-admin'

// ==================== 状态 ====================
const activeTab = ref('bindings')

// 统计数据
const stats = ref<any>({ totalBindings: 0, totalUsers: 0, providers: [] })
const totpStats = ref<any>({ total: 0, enabled: 0, disabled: 0 })

// 绑定管理
const bindings = ref<any[]>([])
const bindingLoading = ref(false)
const bindingSearch = reactive({ keyword: '', provider: '' })
const bindingPage = reactive({ page: 1, pageSize: 20, total: 0 })
const selectedBindingIds = ref<string[]>([])

// TOTP管理
const totpList = ref<any[]>([])
const totpLoading = ref(false)
const totpSearch = reactive({ keyword: '', isEnabled: undefined as boolean | undefined })
const totpPage = reactive({ page: 1, pageSize: 20, total: 0 })

// 详情
const showDetailDialog = ref(false)
const bindingDetail = ref<any>(null)

// 平台选项
const providerOptions = [
  { value: 'wechat', label: '微信' },
  { value: 'wecom', label: '企业微信' },
  { value: 'feishu', label: '飞书' },
  { value: 'qq', label: 'QQ' },
  { value: 'dingtalk', label: '钉钉' },
  { value: 'apple', label: 'Apple' },
  { value: 'github', label: 'GitHub' },
  { value: 'google', label: 'Google' },
  { value: 'microsoft', label: 'Microsoft' },
  { value: 'alipay', label: '支付宝' },
  { value: 'douyin', label: '抖音' },
  { value: 'discord', label: 'Discord' },
  { value: 'steam', label: 'Steam' },
]

// 平台颜色映射
const providerTagTypeMap: Record<string, string> = {
  wechat: 'success', wecom: 'success', github: '', google: 'warning',
  microsoft: 'primary', apple: '', qq: 'primary', feishu: 'primary',
  dingtalk: 'primary', alipay: 'primary', douyin: 'danger',
  discord: 'primary', steam: ''
}

// ==================== 方法 ====================

function getProviderLabel(code: string): string {
  return providerOptions.find(p => p.value === code)?.label || code
}

function getProviderTagType(code: string): any {
  return providerTagTypeMap[code] || 'info'
}

function formatDateTime(dateStr?: string): string {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleString('zh-CN', {
    year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit'
  })
}

function formatJson(text: string): string {
  try {
    return JSON.stringify(JSON.parse(text), null, 2)
  } catch {
    return text
  }
}

async function copyRawProfile() {
  try {
    await navigator.clipboard.writeText(bindingDetail.value?.rawProfile || '')
    ElMessage.success('已复制到剪贴板')
  } catch { ElMessage.error('复制失败') }
}

// ===== 加载绑定列表 =====
async function loadBindings() {
  bindingLoading.value = true
  try {
    const params: any = { page: bindingPage.page, pageSize: bindingPage.pageSize }
    if (bindingSearch.keyword) params.keyword = bindingSearch.keyword
    if (bindingSearch.provider) params.provider = bindingSearch.provider
    const res = await http.get(`${API_BASE}/bindings`, { params })
    bindings.value = res.items || []
    bindingPage.total = res.total || 0
  } catch (e: any) {
    ElMessage.error(`加载失败: ${e.message || '未知错误'}`)
  } finally { bindingLoading.value = false }
}

// ===== 加载统计 =====
async function loadStats() {
  try {
    stats.value = await http.get(`${API_BASE}/bindings/stats`)
  } catch { /* 忽略 */ }
  try {
    totpStats.value = await http.get(`${API_BASE}/totp/stats`)
  } catch { /* 忽略 */ }
}

// ===== 查看绑定详情 =====
async function handleViewBinding(row: any) {
  try {
    const detail = await http.get(`${API_BASE}/bindings/${row.id}`)
    // 附加用户名信息
    detail.userName = row.userName
    bindingDetail.value = detail
    showDetailDialog.value = true
  } catch (e: any) {
    ElMessage.error(`获取详情失败: ${e.message || '未知错误'}`)
  }
}

// ===== 删除绑定 =====
async function handleDeleteBinding(row: any) {
  try {
    await ElMessageBox.confirm(
      `确定要删除用户 "${row.userDisplayName}" 的 ${getProviderLabel(row.provider)} 绑定吗？`,
      '确认删除', { type: 'warning', confirmButtonText: '确认删除', cancelButtonText: '取消' }
    )
    await http.delete(`${API_BASE}/bindings/${row.id}`)
    ElMessage.success('删除成功')
    loadBindings()
    loadStats()
  } catch (e: any) {
    if (e !== 'cancel') ElMessage.error(e.message || '删除失败')
  }
}

// ===== 批量删除绑定 =====
async function handleBatchDeleteBindings() {
  try {
    await ElMessageBox.confirm(
      `确定要删除选中的 ${selectedBindingIds.value.length} 条绑定记录吗？`,
      '确认批量删除', { type: 'warning' }
    )
    await http.post(`${API_BASE}/bindings/batch-delete`, selectedBindingIds.value)
    ElMessage.success('批量删除成功')
    selectedBindingIds.value = []
    loadBindings()
    loadStats()
  } catch (e: any) {
    if (e !== 'cancel') ElMessage.error(e.message || '删除失败')
  }
}

function onBindingSelectionChange(selection: any[]) {
  selectedBindingIds.value = selection.map(r => r.id)
}

// ===== TOTP列表 =====
async function loadTotpList() {
  totpLoading.value = true
  try {
    const params: any = { page: totpPage.page, pageSize: totpPage.pageSize }
    if (totpSearch.keyword) params.keyword = totpSearch.keyword
    if (totpSearch.isEnabled !== undefined && totpSearch.isEnabled !== null) params.isEnabled = totpSearch.isEnabled
    const res = await http.get(`${API_BASE}/totp`, { params })
    totpList.value = res.items || []
    totpPage.total = res.total || 0
  } catch (e: any) {
    ElMessage.error(`加载失败: ${e.message || '未知错误'}`)
  } finally { totpLoading.value = false }
}

// ===== 禁用TOTP =====
async function handleDisableTotp(row: any) {
  try {
    await ElMessageBox.confirm(
      `确定要禁用用户 "${row.userDisplayName}" 的TOTP验证器吗？禁用后用户登录将不再需要两步验证。`,
      '确认禁用', { type: 'warning' }
    )
    await http.post(`${API_BASE}/totp/${row.id}/disable`)
    ElMessage.success('已禁用')
    row.isEnabled = false
    loadStats()
  } catch (e: any) {
    if (e !== 'cancel') ElMessage.error(e.message || '操作失败')
  }
}

// ===== 删除TOTP =====
async function handleDeleteTotp(row: any) {
  try {
    await ElMessageBox.confirm(
      `确定要删除用户 "${row.userDisplayName}" 的TOTP验证器记录吗？删除后用户需重新设置两步验证。`,
      '确认删除', { type: 'warning' }
    )
    await http.delete(`${API_BASE}/totp/${row.id}`)
    ElMessage.success('删除成功')
    loadTotpList()
    loadStats()
  } catch (e: any) {
    if (e !== 'cancel') ElMessage.error(e.message || '删除失败')
  }
}

// ==================== 初始化 ====================
onMounted(() => {
  loadBindings()
  loadTotpList()
  loadStats()
})
</script>

<style scoped>
.third-auth-admin {
  padding: 24px;
  background: var(--el-bg-color-page);
  min-height: 100vh;
  animation: fadeIn 0.3s ease-out;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

/* 统计卡片行 */
.stats-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 20px;
}

.stat-card {
  background: #fff;
  border-radius: 12px;
  padding: 18px 20px;
  display: flex;
  align-items: center;
  gap: 16px;
  box-shadow: 0 1px 3px rgba(0,0,0,0.05);
  transition: all 0.2s ease;
}

.stat-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(0,0,0,0.08);
}

.admin-dark .stat-card {
  background: #1f2937;
  box-shadow: 0 1px 3px rgba(0,0,0,0.2);
}

.stat-icon {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.stat-icon i { font-size: 22px; color: #fff; }
.stat-icon.binding { background: linear-gradient(135deg, #3b82f6, #2563eb); }
.stat-icon.users { background: linear-gradient(135deg, #22c55e, #16a34a); }
.stat-icon.totp { background: linear-gradient(135deg, #a855f7, #7c3aed); }
.stat-icon.provider { background: linear-gradient(135deg, #f59e0b, #d97706); }

.stat-info {
  display: flex;
  flex-direction: column;
}

.stat-value {
  font-size: 24px;
  font-weight: 700;
  color: #1f2937;
  line-height: 1.2;
}

.admin-dark .stat-value { color: #f1f5f9; }

.stat-label {
  font-size: 13px;
  color: #6b7280;
  margin-top: 2px;
}

.admin-dark .stat-label { color: #9ca3af; }

/* 主卡片 */
.main-card {
  border-radius: 12px;
}

/* 工具栏 */
.tab-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  flex-wrap: wrap;
  gap: 10px;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

/* 用户单元格 */
.user-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.user-name {
  font-weight: 500;
  color: #1f2937;
  font-size: 13px;
}

.admin-dark .user-name { color: #f1f5f9; }

.user-account {
  font-size: 12px;
  color: #9ca3af;
}

/* 分页 */
.pagination-wrapper {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
  padding-top: 12px;
  border-top: 1px solid #e5e7eb;
}

.admin-dark .pagination-wrapper { border-top-color: #374151; }

/* 原始数据区域 */
.raw-profile-section {
  margin-top: 16px;
}

.raw-profile-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
  font-size: 13px;
  font-weight: 500;
  color: #374151;
}

.admin-dark .raw-profile-header { color: #d1d5db; }

.raw-profile-content {
  background: #f9fafb;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  padding: 12px;
  font-size: 12px;
  font-family: 'Consolas', 'Monaco', monospace;
  max-height: 300px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
  color: #374151;
  margin: 0;
}

.admin-dark .raw-profile-content {
  background: #1f2937;
  border-color: #374151;
  color: #d1d5db;
}

/* 对话框优化 */
:deep(.el-dialog) { border-radius: 12px; overflow: hidden; }
:deep(.el-dialog__header) {
  background: linear-gradient(to right, #f9fafb 0%, #ffffff 100%);
  border-bottom: 1px solid #e5e7eb;
  padding: 16px 20px;
  margin: 0;
}
.admin-dark :deep(.el-dialog__header) {
  background: linear-gradient(to right, #1f2937, #1a2332);
  border-bottom-color: #374151;
}

/* 响应式 */
@media (max-width: 768px) {
  .stats-row { grid-template-columns: repeat(2, 1fr); }
  .tab-toolbar { flex-direction: column; align-items: stretch; }
  .toolbar-left { flex-direction: column; }
}
</style>