<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="notifications-page">
    <div class="page-layout">
      <!-- 左侧：通知列表 -->
      <div class="list-panel">
        <DataTable
          class="table-card"
          :data="rows"
          :loading="loading"
          :columns="columns"
          :pagination="pagination"
          :search-config="searchConfig"
          :compact-mode="true"
          :show-column-settings="true"
          :show-export="true"
          cache-key="noti-list"
          :action-column-width="100"
          :row-class-name="getRowClassName"
          @search="onSearch"
          @page-change="onPageChange"
          @size-change="onSizeChange"
          @row-click="onRowClick"
        >
          <!-- 页面标题 -->
          <template #header>
            <h2>通知管理</h2>
            <p>管理系统通知公告</p>
          </template>

          <!-- 页面操作按钮 -->
          <template #header-actions>
            <el-button v-permission="'/system/notify:add'" type="primary" @click="onCreate">
              <i class="bi bi-plus-lg" style="margin-right: 4px;"></i>
              新建通知
            </el-button>
          </template>

          <!-- 列自定义 -->
          <template #column-createdAt="{ row }">
            <span>{{ formatTime(row.createdAt) }}</span>
          </template>

          <!-- 标题列 - 可点击查看详情 -->
          <template #column-title="{ row }">
            <span class="title-link" @click.stop="onViewDetail(row)">{{ row.title }}</span>
          </template>

          <!-- 阅读进度列 -->
          <template #column-readRate="{ row }">
            <div class="read-rate-cell">
              <el-progress
                :percentage="getReadPercent(row)"
                :stroke-width="6"
                :show-text="false"
                :color="getReadPercent(row) >= 80 ? '#10b981' : getReadPercent(row) >= 50 ? '#f59e0b' : '#ef4444'"
              />
              <span class="read-rate-text">{{ row.readCount }}/{{ row.totalRecipients }}</span>
            </div>
          </template>

          <!-- 行操作 -->
          <template #actions="{ row }">
            <el-button v-permission="'/system/notify:delete'" class="row-action-btn" type="danger" link size="small" @click.stop="onDelete(row)">删除</el-button>
          </template>
        </DataTable>
      </div>

      <!-- 右侧：通知详情与统计面板 -->
      <div class="detail-panel">
        <!-- 空状态 -->
        <div v-if="!current" class="empty-detail">
          <div class="empty-icon">
            <i class="bi bi-bell"></i>
          </div>
          <p class="empty-text">选择一条通知查看详情</p>
          <p class="empty-hint">点击左侧列表中的通知，即可在此查看投递统计与收件人状态</p>
        </div>

        <!-- 详情内容 -->
        <div v-else class="detail-content">
          <!-- 通知信息 -->
          <div class="detail-header">
            <h3 class="detail-title">{{ current.title }}</h3>
            <div class="detail-meta">
              <span class="meta-item"><i class="bi bi-clock"></i> {{ formatTime(current.createdAt) }}</span>
              <el-tag size="small" type="success" effect="plain">已发送</el-tag>
            </div>
          </div>

          <!-- 统计卡片 -->
          <div class="stats-row">
            <div class="stat-card stat-total">
              <div class="stat-icon"><i class="bi bi-people-fill"></i></div>
              <div class="stat-body">
                <span class="stat-number">{{ stats?.totalRecipients ?? 0 }}</span>
                <span class="stat-label">接收人数</span>
              </div>
            </div>
            <div class="stat-card stat-read">
              <div class="stat-icon"><i class="bi bi-check2-all"></i></div>
              <div class="stat-body">
                <span class="stat-number">{{ stats?.readCount ?? 0 }}</span>
                <span class="stat-label">已读</span>
              </div>
            </div>
            <div class="stat-card stat-unread">
              <div class="stat-icon"><i class="bi bi-envelope"></i></div>
              <div class="stat-body">
                <span class="stat-number">{{ unreadCount }}</span>
                <span class="stat-label">未读</span>
              </div>
            </div>
          </div>

          <!-- 阅读进度 -->
          <div class="read-progress">
            <div class="progress-row">
              <span class="progress-label">阅读进度</span>
              <span class="progress-value" :class="{ good: readPercent >= 80, mid: readPercent >= 50 && readPercent < 80 }">{{ readPercent }}%</span>
            </div>
            <el-progress
              :percentage="readPercent"
              :stroke-width="8"
              :show-text="false"
              :color="readPercent >= 80 ? '#10b981' : readPercent >= 50 ? '#f59e0b' : '#ef4444'"
            />
          </div>

          <!-- 收件人列表 -->
          <div class="recipients-section">
            <div class="recipients-toolbar">
              <el-input
                v-model="recipientSearch"
                placeholder="搜索用户名..."
                clearable
                size="small"
                class="recipient-search"
              >
                <template #prefix><i class="bi bi-search"></i></template>
              </el-input>
              <el-radio-group v-model="recipientFilter" size="small" class="recipient-filter">
                <el-radio-button value="all">全部 ({{ mergedRecipients.length }})</el-radio-button>
                <el-radio-button value="read">已读 ({{ stats?.readCount ?? 0 }})</el-radio-button>
                <el-radio-button value="unread">未读 ({{ unreadCount }})</el-radio-button>
              </el-radio-group>
            </div>

            <el-scrollbar class="recipients-scroll" max-height="calc(100vh - 540px)">
              <div class="recipients-list">
                <div v-for="user in pagedRecipients" :key="user.id" class="recipient-item">
                  <div class="recipient-left">
                    <div class="recipient-avatar" :class="user.isRead ? 'is-read' : 'is-unread'">
                      {{ user.name.charAt(0) }}
                    </div>
                    <span class="recipient-name">{{ user.name }}</span>
                  </div>
                  <el-tag
                    :type="user.isRead ? 'success' : 'warning'"
                    size="small"
                    effect="light"
                    round
                  >
                    <i :class="user.isRead ? 'bi bi-check2-all' : 'bi bi-envelope'" style="margin-right: 3px;"></i>
                    {{ user.isRead ? '已读' : '未读' }}
                  </el-tag>
                </div>
                <div v-if="filteredRecipients.length === 0" class="empty-recipients">
                  <i class="bi bi-search"></i>
                  <span>暂无匹配用户</span>
                </div>
              </div>
            </el-scrollbar>

            <div v-if="filteredRecipients.length > 0" class="recipients-pagination">
              <el-pagination
                v-model:current-page="recipientPage"
                :page-size="recipientPageSize"
                :total="filteredRecipients.length"
                layout="total, prev, pager, next"
                small
                background
              />
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 通知详情弹窗 -->
    <el-dialog v-model="detailVisible" width="720px" top="5vh" :close-on-click-modal="true" class="detail-dialog">
      <template #header>
        <div class="detail-dlg-header">
          <i class="bi bi-envelope-open"></i>
          <span>通知详情</span>
        </div>
      </template>
      <div v-loading="detailLoading" class="detail-dlg-body">
        <template v-if="detailData">
          <!-- 标题与基本信息 -->
          <h3 class="detail-dlg-title">{{ detailData.title }}</h3>
          <div class="detail-dlg-meta">
            <span><i class="bi bi-clock"></i> {{ formatTime(detailData.createdAt) }}</span>
            <span><i class="bi bi-people"></i> {{ detailData.totalRecipients }} 人接收</span>
            <span><i class="bi bi-check2-all"></i> {{ detailData.readCount }} 人已读</span>
            <el-tag size="small" :type="detailData.type === 'system' ? 'primary' : 'info'" effect="plain">{{ detailData.type === 'system' ? '系统通知' : detailData.type }}</el-tag>
          </div>

          <!-- 正文内容 -->
          <div class="detail-dlg-content">
            <div v-if="detailData.content" class="content-text" v-html="renderContent(detailData.content)"></div>
            <div v-else class="content-empty">暂无正文内容</div>
          </div>

          <!-- 图片附件 -->
          <div v-if="detailImages.length" class="detail-dlg-section">
            <div class="section-label"><i class="bi bi-images"></i> 图片 ({{ detailImages.length }})</div>
            <div class="detail-images">
              <el-image
                v-for="img in detailImages"
                :key="img.id"
                :src="resolveResourcePath(img.fileUrl || '')"
                :preview-src-list="detailImages.map(i => resolveResourcePath(i.fileUrl || ''))"
                fit="cover"
                class="detail-image-item"
              />
            </div>
          </div>

          <!-- 文件附件 -->
          <div v-if="detailFiles.length" class="detail-dlg-section">
            <div class="section-label"><i class="bi bi-paperclip"></i> 附件 ({{ detailFiles.length }})</div>
            <div class="detail-files">
              <a
                v-for="f in detailFiles"
                :key="f.id"
                :href="resolveResourcePath(f.fileUrl || '')"
                target="_blank"
                class="detail-file-item"
              >
                <i class="bi bi-file-earmark"></i>
                <span class="file-name">{{ f.fileName }}</span>
                <span class="file-size">{{ formatFileSize(f.fileSize) }}</span>
                <i class="bi bi-download"></i>
              </a>
            </div>
          </div>

          <!-- 链接 -->
          <div v-if="detailData.links?.length" class="detail-dlg-section">
            <div class="section-label"><i class="bi bi-link-45deg"></i> 关联链接</div>
            <div class="detail-links">
              <div v-for="link in detailData.links" :key="link.id" class="detail-link-item">
                <el-tag size="small" :type="link.platform === 'web' ? 'primary' : link.platform === 'uniapp' ? 'success' : 'warning'" effect="plain">{{ getPlatformLabel(link.platform) }}</el-tag>
                <span class="link-title">{{ link.title }}</span>
                <span class="link-url">{{ link.url }}</span>
              </div>
            </div>
          </div>
        </template>
      </div>
    </el-dialog>

    <!-- 新建/编辑通知对话框 -->
    <el-dialog v-model="dialogVisible" width="1200px" top="5vh" :close-on-click-modal="false">
      <template #header><div class="dlg-header"><span>{{ dialogTitle }}</span><AdminLangSwitcher v-if="multiLangEnabled" /></div></template>
      <div class="dialog-layout">
        <!-- 左侧：通知内容 -->
        <div class="dialog-left">
          <div class="section-title">通知内容</div>
          <el-form :model="form" label-width="90px">
            <el-form-item label="标题" required>
              <LangInput v-if="multiLangEnabled" v-model="form.titleI18n" placeholder="请输入通知标题" />
              <el-input v-else v-model="form.title" maxlength="128" show-word-limit placeholder="请输入通知标题" />
            </el-form-item>
            <el-form-item label="内容" required>
              <el-input v-model="form.contentText" type="textarea" :rows="14" placeholder="请输入通知内容" />
            </el-form-item>
            <el-form-item label="图片">
              <div class="image-attachments">
                <div class="image-list">
                  <div v-for="(img, idx) in imageAttachments" :key="img.fileId" class="image-item">
                    <img :src="resolveResourcePath(img.fileUrl || '')" :alt="img.fileName" class="image-thumbnail" />
                    <div class="image-actions">
                      <el-icon class="action-icon" @click="removeImageAttachment(idx)"><Close /></el-icon>
                    </div>
                  </div>
                  <div v-if="imageAttachments.length < 9" class="image-add" @click="imageSelectorVisible = true">
                    <el-icon><Plus /></el-icon>
                  </div>
                </div>
                <div class="el-upload__tip">支持图片格式，最多 9 张</div>
              </div>
              <FileSelector
                v-model="imageSelectorVisible"
                title="选择图片"
                :multiple="true"
                accept="image/*"
                :max-size="20"
                @confirm="onImageSelectorConfirm"
              />
            </el-form-item>
            <el-form-item label="附件">
              <div class="file-attachments">
                <div v-for="(f, idx) in fileAttachments" :key="f.fileId" class="file-attachment-item">
                  <i class="bi bi-file-earmark"></i>
                  <span class="file-name">{{ f.fileName }}</span>
                  <span class="file-size">{{ formatFileSize(f.fileSize) }}</span>
                  <el-icon class="file-remove" @click="removeFileAttachment(idx)"><Close /></el-icon>
                </div>
                <el-button type="primary" plain size="small" @click="fileSelectorVisible = true">
                  <i class="bi bi-folder2-open" style="margin-right: 4px;"></i>选择文件
                </el-button>
                <div class="el-upload__tip">支持任意格式文件，单个不超过 100MB</div>
              </div>
              <FileSelector
                v-model="fileSelectorVisible"
                title="选择附件"
                :multiple="true"
                :max-size="100"
                @confirm="onFileSelectorConfirm"
              />
            </el-form-item>
            <el-form-item label="链接">
              <el-collapse accordion class="link-collapse">
                <el-collapse-item v-for="link in linkConfigs" :key="link.platform" :title="getPlatformLabel(link.platform)" :name="link.platform">
                  <el-form label-width="60px">
                    <el-form-item label="标题">
                      <el-input v-model="link.title" placeholder="链接标题" />
                    </el-form-item>
                    <el-form-item label="URL">
                      <el-input v-model="link.url" placeholder="跳转地址（如 /path/to/page 或 https://...）" />
                    </el-form-item>
                  </el-form>
                </el-collapse-item>
              </el-collapse>
            </el-form-item>
          </el-form>
        </div>

        <!-- 右侧：接收对象 -->
        <div class="dialog-right">
          <!-- 主送区块（必填） -->
          <div class="section-title">主送（必填）</div>
          <div class="recipient-block">
            <el-radio-group v-model="primaryGroup.mode" class="recipient-mode-group">
              <el-radio value="all">全体用户</el-radio>
              <el-radio value="users">按用户</el-radio>
              <el-radio value="roles">按角色</el-radio>
              <el-radio value="departments">按部门</el-radio>
            </el-radio-group>

            <div v-if="primaryGroup.mode === 'users'" class="selector-area">
              <el-select
                v-model="primaryGroup.ids"
                multiple
                filterable
                remote
                reserve-keyword
                clearable
                :remote-method="remoteSearchUsers"
                :loading="userLoading"
                placeholder="搜索并选择用户"
                style="width: 100%"
              >
                <el-option v-for="u in userOptions" :key="u.id" :label="u.displayName" :value="u.id" />
              </el-select>
            </div>
            <div v-if="primaryGroup.mode === 'roles'" class="selector-area">
              <el-tree
                ref="primaryRoleTreeRef"
                :data="roleTree"
                :props="{ label: 'name', children: 'children' }"
                node-key="id"
                show-checkbox
                :default-expand-all="false"
                class="tree-selector"
              />
            </div>
            <div v-if="primaryGroup.mode === 'departments'" class="selector-area">
              <el-tree
                ref="primaryDeptTreeRef"
                :data="deptTree"
                :props="{ label: 'name', children: 'children' }"
                node-key="id"
                show-checkbox
                :default-expand-all="false"
                class="tree-selector"
              />
            </div>
          </div>

          <!-- 知会区块（可选） -->
          <div class="cc-toggle">
            <el-switch v-model="ccEnabled" />
            <span class="cc-toggle-label">添加知会</span>
          </div>
          <div v-if="ccEnabled" class="recipient-block">
            <div class="section-title cc-title">知会</div>
            <el-radio-group v-model="ccGroup.mode" class="recipient-mode-group">
              <el-radio value="all">全体用户</el-radio>
              <el-radio value="users">按用户</el-radio>
              <el-radio value="roles">按角色</el-radio>
              <el-radio value="departments">按部门</el-radio>
            </el-radio-group>

            <div v-if="ccGroup.mode === 'users'" class="selector-area">
              <el-select
                v-model="ccGroup.ids"
                multiple
                filterable
                remote
                reserve-keyword
                clearable
                :remote-method="remoteSearchUsers"
                :loading="userLoading"
                placeholder="搜索并选择用户"
                style="width: 100%"
              >
                <el-option v-for="u in userOptions" :key="u.id" :label="u.displayName" :value="u.id" />
              </el-select>
            </div>
            <div v-if="ccGroup.mode === 'roles'" class="selector-area">
              <el-tree
                ref="ccRoleTreeRef"
                :data="roleTree"
                :props="{ label: 'name', children: 'children' }"
                node-key="id"
                show-checkbox
                :default-expand-all="false"
                class="tree-selector"
              />
            </div>
            <div v-if="ccGroup.mode === 'departments'" class="selector-area">
              <el-tree
                ref="ccDeptTreeRef"
                :data="deptTree"
                :props="{ label: 'name', children: 'children' }"
                node-key="id"
                show-checkbox
                :default-expand-all="false"
                class="tree-selector"
              />
            </div>
          </div>
        </div>
      </div>

      <template #footer>
        <el-button @click="dialogVisible=false">取消</el-button>
        <el-button v-permission="'/system/notify:add'" type="success" @click="saveAndPublish">发送消息</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import DataTable from '@/components/DataTable/index.vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Close } from '@element-plus/icons-vue'
import DOMPurify from 'dompurify'
import LangInput from '@/components/framework/LangInput.vue'
import AdminLangSwitcher from '@/components/framework/AdminLangSwitcher.vue'
import { getDefaultLang, useMultiLangEnabled } from '@/utils/lang'
import type { SearchFieldConfig } from '@/components/DataTable/types'
import type { PagedResult } from '@/api/message'
import { createMessage, getAdminMessageList, getAdminMessageStats, getAdminMessageDetail, deleteAdminMessageBatch, type CreateMessageInput, type RecipientGroup, type CreateAttachmentInput, type CreateLinkInput, type AdminMessageListItem, type AdminMessageStats, type AdminMessageDetail } from '@/api/message'
import { getUsers } from '@/api/users'
import { getRoleTree } from '@/api/role'
import { getDepartmentsTree } from '@/api/department'
import { formatFileSize } from '@/api/files'
import { resolveResourcePath } from '@/utils/resourceUrl'
import FileSelector from '@/components/FileSelector.vue'
import type { FileListItemDto } from '@/api/files'

const loading = ref(false)
const rows = ref<AdminMessageListItem[]>([])
const pagination = ref({ total: 0, page: 1, pageSize: 20, pageSizes: [10, 20, 50, 100] })
const current = ref<AdminMessageListItem | null>(null)
const stats = ref<AdminMessageStats | null>(null)

// 收件人列表状态
const recipientSearch = ref('')
const recipientFilter = ref<'all' | 'read' | 'unread'>('all')
const recipientPage = ref(1)
const recipientPageSize = 20

// 通知详情弹窗状态
const detailVisible = ref(false)
const detailLoading = ref(false)
const detailData = ref<AdminMessageDetail | null>(null)

const detailImages = computed(() => detailData.value?.attachments?.filter(a => a.attachmentType === 'image') || [])
const detailFiles = computed(() => detailData.value?.attachments?.filter(a => a.attachmentType === 'file') || [])

const columns = [
  { prop: 'title', label: '标题', minWidth: 280, slot: 'column-title' },
  { prop: 'createdAt', label: '发送时间', width: 170, slot: 'column-createdAt' },
  { prop: 'totalRecipients', label: '接收人数', width: 100, align: 'center' },
  { prop: 'readRate', label: '阅读进度', width: 160, slot: 'column-readRate' },
]

const searchConfig: SearchFieldConfig[] = [
  { key: 'title', label: '标题', type: 'input', placeholder: '输入标题', simple: true, width: 180 },
  { key: 'dateRange', label: '发布时间', type: 'daterange', simple: true, width: 280 }
]

const dialogVisible = ref(false)
const dialogTitle = ref('新建通知')
const form = ref<{ title: string; contentText: string; titleI18n: string }>({ title: '', contentText: '', titleI18n: '' })
const multiLangEnabled = useMultiLangEnabled()

// recipient dual-block state
const primaryGroup = ref<{ mode: string; ids: string[] }>({ mode: 'all', ids: [] })
const ccEnabled = ref(false)
const ccGroup = ref<{ mode: string; ids: string[] }>({ mode: 'all', ids: [] })
const userOptions = ref<Array<{ id: string; displayName: string }>>([])
const userLoading = ref(false)
const roleTree = ref<any[]>([])
const deptTree = ref<any[]>([])
const primaryRoleTreeRef = ref()
const primaryDeptTreeRef = ref()
const ccRoleTreeRef = ref()
const ccDeptTreeRef = ref()
const deptScope = ref<'DeptOnly'|'DeptWithChildren'>('DeptOnly')

// 链接配置状态
const linkConfigs = ref<Array<{ platform: string; title: string; url: string }>>([
  { platform: 'web', title: '', url: '' },
  { platform: 'wpf', title: '', url: '' },
  { platform: 'uniapp', title: '', url: '' }
])

// 附件状态
const imageSelectorVisible = ref(false)
const imageAttachments = ref<Array<{ fileId: string; fileName: string; fileSize: number; fileUrl?: string }>>([])
const fileAttachments = ref<Array<{ fileId: string; fileName: string; fileSize: number; fileUrl?: string }>>([])
const fileSelectorVisible = ref(false)

function formatTime(v?: string | null) { 
  if(!v) return '-'
  try { return new Date(v).toLocaleString() } catch { return String(v) } 
}

function getReadPercent(row: AdminMessageListItem) {
  return row.totalRecipients > 0 ? Math.round(row.readCount / row.totalRecipients * 100) : 0
}

/** 净化 HTML 内容并保留安全标签，防止存储型 XSS */
function renderContent(text: string): string {
  return DOMPurify.sanitize(text)
}

/** 查看通知详情 */
async function onViewDetail(row: AdminMessageListItem) {
  detailVisible.value = true
  detailLoading.value = true
  detailData.value = null
  try {
    detailData.value = await getAdminMessageDetail(row.title, row.createdAt)
  } catch {
    ElMessage.error('加载通知详情失败')
  } finally {
    detailLoading.value = false
  }
}

function getRowClassName({ row }: { row: AdminMessageListItem }) {
  return current.value?.title === row.title && current.value?.createdAt === row.createdAt ? 'selected-row' : ''
}

async function load() {
  loading.value = true
  try {
    const res = await getAdminMessageList({ page: pagination.value.page, pageSize: pagination.value.pageSize })
    rows.value = Array.isArray(res.items) ? res.items : []
    pagination.value.total = Number(res.total || 0)
  } catch { rows.value = []; ElMessage.error('加载失败') } finally { loading.value = false }
}

function onSearch(payload: { filters?: Record<string, any>; page?: number; pageSize?: number }) {
  const f = payload?.filters || {}
  pagination.value.page = payload?.page || 1
  pagination.value.pageSize = payload?.pageSize || pagination.value.pageSize
  loadWithFilter({ title: f.title, dateRange: f.dateRange })
}

async function loadWithFilter(filter?: { title?: string; dateRange?: [string, string] }) {
  loading.value = true
  try {
    const params: any = { page: pagination.value.page, pageSize: pagination.value.pageSize }
    if (filter?.title) params.title = filter.title
    if (filter?.dateRange?.[0]) params.startDate = filter.dateRange[0]
    if (filter?.dateRange?.[1]) params.endDate = filter.dateRange[1]
    const res = await getAdminMessageList(params)
    rows.value = Array.isArray(res.items) ? res.items : []
    pagination.value.total = Number(res.total || 0)
  } catch { rows.value = []; ElMessage.error('加载失败') } finally { loading.value = false }
}

function onPageChange(p: number) { pagination.value.page = p; load() }
function onSizeChange(s: number) { pagination.value.pageSize = s; pagination.value.page = 1; load() }

async function onRowClick(row: AdminMessageListItem) {
  current.value = row
  recipientSearch.value = ''
  recipientFilter.value = 'all'
  recipientPage.value = 1
  try { 
    stats.value = await getAdminMessageStats(row.title, row.createdAt)
  } catch {
    stats.value = null
  }
}

// ---- 收件人列表计算属性 ----
const mergedRecipients = computed(() => {
  if (!stats.value) return []
  const readIdSet = new Set((stats.value.readUsers || []).map(u => u.id))
  return (stats.value.deliveredUsers || []).map(u => ({
    id: u.id,
    name: u.name,
    isRead: readIdSet.has(u.id)
  }))
})

const filteredRecipients = computed(() => {
  let list = mergedRecipients.value
  if (recipientFilter.value === 'read') {
    list = list.filter(u => u.isRead)
  } else if (recipientFilter.value === 'unread') {
    list = list.filter(u => !u.isRead)
  }
  if (recipientSearch.value.trim()) {
    const kw = recipientSearch.value.trim().toLowerCase()
    list = list.filter(u => u.name.toLowerCase().includes(kw))
  }
  return list
})

const pagedRecipients = computed(() => {
  const start = (recipientPage.value - 1) * recipientPageSize
  return filteredRecipients.value.slice(start, start + recipientPageSize)
})

const unreadCount = computed(() => {
  return (stats.value?.totalRecipients ?? 0) - (stats.value?.readCount ?? 0)
})

const readPercent = computed(() => {
  const total = stats.value?.totalRecipients ?? 0
  if (total === 0) return 0
  return Math.round((stats.value?.readCount ?? 0) / total * 100)
})

watch([recipientSearch, recipientFilter], () => {
  recipientPage.value = 1
})

async function onEdit(row: AdminMessageListItem) {
  // 新消息系统不支持编辑已发送的消息
  ElMessage.info('已发送的消息不支持编辑')
}

function onCreate() { 
  form.value = { title: '', contentText: '', titleI18n: '' }
  primaryGroup.value = { mode: 'all', ids: [] }
  ccEnabled.value = false
  ccGroup.value = { mode: 'all', ids: [] }
  deptScope.value = 'DeptOnly'
  imageAttachments.value = []
  fileAttachments.value = []
  linkConfigs.value = [
    { platform: 'web', title: '', url: '' },
    { platform: 'wpf', title: '', url: '' },
    { platform: 'uniapp', title: '', url: '' }
  ]
  current.value = null
  stats.value = null
  dialogTitle.value = '新建通知'
  dialogVisible.value = true 
}

// 构建新消息创建输入
function buildCreateInput(): { primary: RecipientGroup; cc?: RecipientGroup } {
  const primary: RecipientGroup = {
    mode: primaryGroup.value.mode as RecipientGroup['mode'],
    ids: primaryGroup.value.mode === 'all' ? undefined : [...primaryGroup.value.ids]
  }
  if (primaryGroup.value.mode === 'roles') {
    primary.ids = (primaryRoleTreeRef.value?.getCheckedKeys?.() || []).map(String)
  }
  if (primaryGroup.value.mode === 'departments') {
    primary.ids = (primaryDeptTreeRef.value?.getCheckedKeys?.() || []).map(String)
  }

  let cc: RecipientGroup | undefined
  if (ccEnabled.value) {
    cc = {
      mode: ccGroup.value.mode as RecipientGroup['mode'],
      ids: ccGroup.value.mode === 'all' ? undefined : [...ccGroup.value.ids]
    }
    if (ccGroup.value.mode === 'roles') {
      cc.ids = (ccRoleTreeRef.value?.getCheckedKeys?.() || []).map(String)
    }
    if (ccGroup.value.mode === 'departments') {
      cc.ids = (ccDeptTreeRef.value?.getCheckedKeys?.() || []).map(String)
    }
  }

  return { primary, cc }
}

// ---- 附件处理 ----

/** 图片选择器确认回调 */
function onImageSelectorConfirm(files: FileListItemDto[]) {
  const remaining = 9 - imageAttachments.value.length
  files.slice(0, remaining).forEach(file => {
    const exists = imageAttachments.value.some(a => a.fileId === file.id)
    if (!exists) {
      imageAttachments.value.push({
        fileId: file.id,
        fileName: file.fileName,
        fileSize: file.size,
        fileUrl: file.url
      })
    }
  })
}

/** 移除图片附件 */
function removeImageAttachment(index: number) {
  imageAttachments.value.splice(index, 1)
}

/** FileSelector 确认回调 - 添加文件附件 */
function onFileSelectorConfirm(files: FileListItemDto[]) {
  files.forEach(file => {
    const exists = fileAttachments.value.some(a => a.fileId === file.id)
    if (!exists) {
      fileAttachments.value.push({
        fileId: file.id,
        fileName: file.fileName,
        fileSize: file.size
      })
    }
  })
}

/** 移除文件附件 */
function removeFileAttachment(index: number) {
  fileAttachments.value.splice(index, 1)
}

/** 获取当前所有附件（供 Task 8.4 提交时使用） */
function getAttachments(): CreateAttachmentInput[] {
  const imgs: CreateAttachmentInput[] = imageAttachments.value.map(a => ({
    fileId: a.fileId,
    fileName: a.fileName,
    fileSize: a.fileSize,
    attachmentType: 'image'
  }))
  const docs: CreateAttachmentInput[] = fileAttachments.value.map(a => ({
    fileId: a.fileId,
    fileName: a.fileName,
    fileSize: a.fileSize,
    attachmentType: 'file'
  }))
  return [...imgs, ...docs]
}

/** 获取平台标签 */
function getPlatformLabel(platform: string) {
  switch (platform) {
    case 'web': return 'WEB 端'
    case 'wpf': return 'WPF 端'
    case 'uniapp': return 'UniApp 端'
    default: return platform
  }
}

/** 获取当前所有链接配置（供 Task 8.4 提交时使用） */
function getLinks(): CreateLinkInput[] {
  return linkConfigs.value
    .filter(l => l.title.trim() && l.url.trim())
    .map(l => ({ title: l.title.trim(), platform: l.platform, url: l.url.trim() }))
}

async function save() {
  // 新消息系统不支持草稿，直接发送
  await saveAndPublish()
}

async function saveAndPublish() {
  // 多语言开启时从 JSON 提取默认语言值填充 title
  let title = form.value.title?.trim() || ''
  if (multiLangEnabled.value && form.value.titleI18n) {
    try { const obj = JSON.parse(form.value.titleI18n); title = obj[getDefaultLang()] || obj['zh-CN'] || Object.values(obj).find((v: any) => v?.trim()) as string || title } catch {}
  }
  if (!title) { ElMessage.warning('请输入标题'); return }
  if (!form.value.contentText?.trim()) { ElMessage.warning('请输入内容'); return }

  const { primary, cc } = buildCreateInput()
  if (primary.mode !== 'all' && (!primary.ids || primary.ids.length === 0)) {
    ElMessage.warning('请选择主送接收对象')
    return
  }

  const input: CreateMessageInput = {
    title: title,
    titleI18n: form.value.titleI18n || null,
    summary: form.value.contentText?.substring(0, 200) ?? undefined,
    content: form.value.contentText ?? undefined,
    type: 'system',
    primary,
    cc: cc || undefined,
    attachments: getAttachments(),
    links: getLinks()
  }

  try {
    await createMessage(input)
    ElMessage.success('发送成功')
    dialogVisible.value = false
    await load()
  } catch (e: any) {
    ElMessage.error(e?.message || '发送失败')
  }
}

async function onDelete(row: AdminMessageListItem) { 
  try { 
    await ElMessageBox.confirm(`确定删除「${row.title}」?`, '提示', { type: 'warning' })
    await deleteAdminMessageBatch(row.title, row.createdAt)
    ElMessage.success('已删除')
    if (current.value?.title === row.title && current.value?.createdAt === row.createdAt) {
      current.value = null
      stats.value = null
    }
    await load() 
  } catch {} 
}

onMounted(load)

// remote user search
async function remoteSearchUsers(keyword: string) {
  userLoading.value = true
  try {
    const res = await getUsers({ page: 1, pageSize: 20, filters: { keyword } } as any)
    userOptions.value = (res.items || []).map((u:any)=>({ id: u.id, displayName: u.displayName || u.userName }))
  } catch { userOptions.value = [] } finally { userLoading.value = false }
}

// preload role/dept tree when dialog opens
watch(dialogVisible, async (v) => {
  if (v) {
    try { roleTree.value = await getRoleTree() } catch { roleTree.value = [] }
    try { deptTree.value = await getDepartmentsTree() } catch { deptTree.value = [] }
  }
})
</script>

<style scoped>
/* ==================== 页面容器 ==================== */
.notifications-page {
  padding: 24px;
  background: var(--el-bg-color-page);
  min-height: 100vh;
  animation: fadeIn 0.3s ease-out;
}

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: translateY(0); }
}

/* ==================== 页面布局：左侧列表 + 右侧详情 ==================== */
.page-layout {
  display: grid;
  grid-template-columns: 1fr 440px;
  gap: 24px;
  align-items: start;
}

.list-panel {
  min-width: 0;
}

/* ==================== 阅读进度列 ==================== */
.read-rate-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.read-rate-cell :deep(.el-progress) {
  flex: 1;
}

.read-rate-text {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  white-space: nowrap;
  min-width: 40px;
  text-align: right;
}

/* ==================== 详情面板 ==================== */
.detail-panel {
  position: sticky;
  top: 24px;
  background: var(--el-bg-color);
  border-radius: 12px;
  border: 1px solid var(--el-border-color-lighter);
  overflow: hidden;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.04);
  transition: box-shadow 0.3s;
}

.detail-panel:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.06);
}

.admin-dark .detail-panel {
  background: #1f2937;
  border-color: #374151;
}

/* ---- 空状态 ---- */
.empty-detail {
  padding: 60px 32px;
  text-align: center;
}

.empty-icon {
  width: 72px;
  height: 72px;
  margin: 0 auto 16px;
  border-radius: 50%;
  background: linear-gradient(135deg, #eff6ff, #dbeafe);
  display: flex;
  align-items: center;
  justify-content: center;
}

.admin-dark .empty-icon {
  background: linear-gradient(135deg, #1e3a5f, #1e293b);
}

.empty-icon i {
  font-size: 32px;
  color: #3b82f6;
}

.admin-dark .empty-icon i {
  color: #60a5fa;
}

.empty-text {
  font-size: 15px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin: 0 0 6px;
}

.empty-hint {
  font-size: 13px;
  color: var(--el-text-color-secondary);
  margin: 0;
  line-height: 1.5;
}

/* ---- 详情内容 ---- */
.detail-content {
  padding: 20px;
}

.detail-header {
  padding-bottom: 16px;
  margin-bottom: 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.admin-dark .detail-header {
  border-bottom-color: #374151;
}

.detail-title {
  font-size: 16px;
  font-weight: 700;
  color: var(--el-text-color-primary);
  margin: 0 0 10px;
  line-height: 1.4;
  word-break: break-all;
}

.detail-meta {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.meta-item {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.meta-item i {
  font-size: 14px;
}

/* ---- 统计卡片行 ---- */
.stats-row {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 10px;
  margin-bottom: 16px;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px;
  border-radius: 10px;
  background: var(--el-fill-color-lighter);
  transition: transform 0.2s, box-shadow 0.2s;
}

.stat-card:hover {
  transform: translateY(-1px);
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}

.admin-dark .stat-card {
  background: #111827;
}

.stat-icon {
  width: 36px;
  height: 36px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.stat-icon i {
  font-size: 16px;
  color: #fff;
}

.stat-total .stat-icon {
  background: linear-gradient(135deg, #3b82f6, #2563eb);
}

.stat-read .stat-icon {
  background: linear-gradient(135deg, #10b981, #059669);
}

.stat-unread .stat-icon {
  background: linear-gradient(135deg, #f59e0b, #d97706);
}

.stat-body {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.stat-number {
  font-size: 18px;
  font-weight: 700;
  color: var(--el-text-color-primary);
  line-height: 1.2;
}

.stat-label {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  margin-top: 2px;
}

/* ---- 阅读进度条 ---- */
.read-progress {
  margin-bottom: 18px;
  padding: 12px 14px;
  background: var(--el-fill-color-lighter);
  border-radius: 8px;
}

.admin-dark .read-progress {
  background: #111827;
}

.progress-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.progress-label {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-primary);
}

.progress-value {
  font-size: 14px;
  font-weight: 700;
  color: #ef4444;
}

.progress-value.mid {
  color: #f59e0b;
}

.progress-value.good {
  color: #10b981;
}

/* ---- 收件人区域 ---- */
.recipients-section {
  border-top: 1px solid var(--el-border-color-lighter);
  padding-top: 16px;
}

.admin-dark .recipients-section {
  border-top-color: #374151;
}

.recipients-toolbar {
  display: flex;
  flex-direction: column;
  gap: 10px;
  margin-bottom: 12px;
}

.recipient-search {
  width: 100%;
}

.recipient-search :deep(.el-input__wrapper) {
  border-radius: 8px;
}

.recipient-filter {
  width: 100%;
}

.recipient-filter :deep(.el-radio-button__inner) {
  width: 100%;
  text-align: center;
  font-size: 12px;
  padding: 6px 0;
}

.recipient-filter :deep(.el-radio-group) {
  width: 100%;
  display: flex;
}

.recipient-filter :deep(.el-radio-button) {
  flex: 1;
}

/* ---- 收件人列表 ---- */
.recipients-scroll {
  margin-bottom: 12px;
}

.recipients-list {
  display: flex;
  flex-direction: column;
}

.recipient-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 10px;
  border-radius: 8px;
  transition: background 0.15s;
}

.recipient-item:hover {
  background: var(--el-fill-color-lighter);
}

.admin-dark .recipient-item:hover {
  background: #111827;
}

.recipient-left {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}

.recipient-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 13px;
  font-weight: 600;
  color: #fff;
  flex-shrink: 0;
  letter-spacing: 0;
}

.recipient-avatar.is-read {
  background: linear-gradient(135deg, #10b981, #059669);
}

.recipient-avatar.is-unread {
  background: linear-gradient(135deg, #94a3b8, #64748b);
}

.recipient-name {
  font-size: 13px;
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.empty-recipients {
  text-align: center;
  padding: 24px 0;
  color: var(--el-text-color-secondary);
  font-size: 13px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 6px;
}

.empty-recipients i {
  font-size: 24px;
  opacity: 0.4;
}

.recipients-pagination {
  display: flex;
  justify-content: center;
  padding-top: 8px;
  border-top: 1px solid var(--el-border-color-lighter);
}

.admin-dark .recipients-pagination {
  border-top-color: #374151;
}

.recipients-pagination :deep(.el-pagination) {
  flex-wrap: wrap;
  justify-content: center;
}

/* ==================== 选中行高亮 ==================== */
:deep(.selected-row) {
  background-color: rgba(59, 130, 246, 0.08) !important;
}

.admin-dark :deep(.selected-row) {
  background-color: rgba(59, 130, 246, 0.15) !important;
}

:deep(.selected-row:hover > td) {
  background-color: rgba(59, 130, 246, 0.12) !important;
}

.admin-dark :deep(.selected-row:hover > td) {
  background-color: rgba(59, 130, 246, 0.2) !important;
}

/* ==================== 对话框布局 ==================== */
.dialog-layout {
  display: grid;
  grid-template-columns: 1fr 380px;
  gap: 24px;
  min-height: 500px;
}

.dialog-left,
.dialog-right {
  display: flex;
  flex-direction: column;
}

.section-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-bottom: 16px;
  padding-bottom: 8px;
  border-bottom: 2px solid #3b82f6;
}

.tree-selector {
  max-height: 200px;
  overflow-y: auto;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  padding: 8px;
}

.admin-dark .tree-selector {
  border-color: #374151;
  background: #111827;
}

.dept-selector {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.dept-scope-row {
  padding: 8px 0;
}

/* ==================== 接收对象双区块 ==================== */
.recipient-block {
  background: var(--el-fill-color-lighter);
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  padding: 16px;
  margin-bottom: 16px;
}

.admin-dark .recipient-block {
  background: #111827;
  border-color: #374151;
}

.recipient-mode-group {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 4px;
}

.selector-area {
  margin-top: 12px;
}

.cc-toggle {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 12px;
  padding: 8px 0;
}

.cc-toggle-label {
  font-size: 14px;
  font-weight: 500;
  color: var(--el-text-color-regular);
}

.cc-title {
  margin-top: 0;
}

/* ==================== 链接配置折叠面板 ==================== */
.link-collapse {
  width: 100%;
}

.link-collapse :deep(.el-collapse-item__header) {
  font-size: 13px;
  font-weight: 500;
}

/* ==================== 对话框优化 ==================== */
:deep(.el-dialog) {
  border-radius: 12px;
  overflow: hidden;
}

:deep(.el-dialog__header) {
  background: linear-gradient(to right, var(--el-fill-color-lighter) 0%, var(--el-bg-color) 100%);
  border-bottom: 1px solid var(--el-border-color-lighter);
  padding: 20px 24px;
  margin: 0;
}

:deep(.el-dialog__title) {
  font-size: 18px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

:deep(.el-dialog__body) {
  padding: 24px;
}

:deep(.el-dialog__footer) {
  padding: 16px 24px;
  border-top: 1px solid var(--el-border-color-lighter);
}

/* ==================== 表单优化 ==================== */
:deep(.el-form-item__label) {
  font-weight: 500;
}

:deep(.el-input__wrapper),
:deep(.el-select .el-input__wrapper),
:deep(.el-tree-select .el-input__wrapper) {
  border-radius: 8px;
  transition: all 0.2s ease;
}

:deep(.el-input__wrapper:hover),
:deep(.el-select .el-input__wrapper:hover) {
  border-color: #3b82f6;
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.1);
}

:deep(.el-input__wrapper.is-focus),
:deep(.el-select .el-input__wrapper.is-focus) {
  border-color: #3b82f6;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.15);
}

/* ==================== 行操作按钮 ==================== */
.row-action-btn {
  padding: 4px 8px;
}

.admin-dark .row-action-btn {
  color: #cbd5e1 !important;
}

.admin-dark .row-action-btn.el-button--primary {
  color: #90caf9 !important;
}

.admin-dark .row-action-btn.el-button--success {
  color: #4ade80 !important;
}

.admin-dark .row-action-btn.el-button--danger {
  color: #f87171 !important;
}

.admin-dark .row-action-btn:hover {
  background: rgba(255, 255, 255, 0.06) !important;
}

/* ==================== 响应式布局 ==================== */
@media (max-width: 1200px) {
  .page-layout {
    grid-template-columns: 1fr;
  }

  .detail-panel {
    position: static;
  }
}

@media (max-width: 900px) {
  .dialog-layout {
    grid-template-columns: 1fr;
  }

  .dialog-right {
    border-top: 1px solid var(--el-border-color-lighter);
    padding-top: 16px;
  }
}

@media (max-width: 768px) {
  .notifications-page {
    padding: 16px;
  }

  :deep(.el-dialog) {
    width: 95% !important;
    margin: 10px auto;
  }

  :deep(.el-dialog__body) {
    padding: 16px;
  }

  .stats-row {
    grid-template-columns: 1fr;
  }
}

/* ==================== 加载动画 ==================== */
:deep(.el-loading-spinner) {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
}

:deep(.el-loading-spinner .circular) {
  width: 48px;
  height: 48px;
}

:deep(.el-loading-text) {
  font-size: 14px;
  font-weight: 500;
  color: #3b82f6;
}

.admin-dark :deep(.el-loading-text) {
  color: #60a5fa;
}

/* ==================== 图片附件列表 ==================== */
.image-attachments {
  width: 100%;
}

.image-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.image-item {
  position: relative;
  width: 100px;
  height: 100px;
  border-radius: 6px;
  overflow: hidden;
  border: 1px solid var(--el-border-color);
}

.image-item .image-thumbnail {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.image-item .image-actions {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  transition: opacity 0.2s;
}

.image-item:hover .image-actions {
  opacity: 1;
}

.image-item .action-icon {
  color: #fff;
  font-size: 18px;
  cursor: pointer;
}

.image-add {
  width: 100px;
  height: 100px;
  border: 1px dashed var(--el-border-color);
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
  background: var(--el-fill-color-lighter);
}

.image-add:hover {
  border-color: var(--el-color-primary);
  color: var(--el-color-primary);
}

.image-add .el-icon {
  font-size: 24px;
  color: var(--el-text-color-placeholder);
}

.image-add:hover .el-icon {
  color: var(--el-color-primary);
}

/* ==================== 文件附件列表 ==================== */
.file-attachments {
  width: 100%;
}

.file-attachment-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  margin-bottom: 8px;
  background: var(--el-fill-color-lighter);
  border-radius: 6px;
  font-size: 13px;
}

.file-attachment-item .file-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--el-text-color-primary);
}

.file-attachment-item .file-size {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  flex-shrink: 0;
}

.file-attachment-item .file-remove {
  cursor: pointer;
  color: var(--el-text-color-placeholder);
  flex-shrink: 0;
  transition: color 0.2s;
}

.file-attachment-item .file-remove:hover {
  color: var(--el-color-danger);
}

/* 对话框标题+语言切换器 */
.dlg-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}
.dlg-header span {
  font-size: 18px;
  font-weight: 600;
}

/* ==================== 标题链接 ==================== */
.title-link {
  color: var(--el-color-primary);
  cursor: pointer;
  font-weight: 500;
  transition: color 0.15s;
}

.title-link:hover {
  color: var(--el-color-primary-light-3);
  text-decoration: underline;
}

/* ==================== 通知详情弹窗 ==================== */
.detail-dlg-header {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 17px;
  font-weight: 600;
}

.detail-dlg-header i {
  color: var(--el-color-primary);
  font-size: 20px;
}

.detail-dlg-body {
  min-height: 200px;
}

.detail-dlg-title {
  font-size: 18px;
  font-weight: 700;
  color: var(--el-text-color-primary);
  margin: 0 0 12px;
  line-height: 1.5;
}

.detail-dlg-meta {
  display: flex;
  align-items: center;
  gap: 16px;
  flex-wrap: wrap;
  margin-bottom: 20px;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.detail-dlg-meta > span {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.detail-dlg-meta > span i {
  font-size: 14px;
}

.detail-dlg-content {
  margin-bottom: 20px;
}

.content-text {
  font-size: 14px;
  line-height: 1.8;
  color: var(--el-text-color-primary);
  background: var(--el-fill-color-lighter);
  border-radius: 8px;
  padding: 16px 20px;
  word-break: break-word;
  max-height: 360px;
  overflow-y: auto;
}

.content-empty {
  text-align: center;
  padding: 24px;
  color: var(--el-text-color-placeholder);
  font-size: 13px;
}

.detail-dlg-section {
  margin-bottom: 16px;
}

.section-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-bottom: 10px;
}

.section-label i {
  color: var(--el-color-primary);
  font-size: 15px;
}

/* 图片网格 */
.detail-images {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.detail-image-item {
  width: 100px;
  height: 100px;
  border-radius: 6px;
  overflow: hidden;
  border: 1px solid var(--el-border-color-lighter);
  cursor: pointer;
}

/* 文件列表 */
.detail-files {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.detail-file-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: var(--el-fill-color-lighter);
  border-radius: 6px;
  text-decoration: none;
  color: var(--el-text-color-primary);
  transition: background 0.15s, box-shadow 0.15s;
}

.detail-file-item:hover {
  background: var(--el-fill-color);
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.06);
}

.detail-file-item .file-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
}

.detail-file-item .file-size {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  flex-shrink: 0;
}

.detail-file-item .bi-download {
  color: var(--el-color-primary);
  opacity: 0;
  transition: opacity 0.15s;
}

.detail-file-item:hover .bi-download {
  opacity: 1;
}

/* 链接列表 */
.detail-links {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.detail-link-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: var(--el-fill-color-lighter);
  border-radius: 6px;
}

.detail-link-item .link-title {
  font-size: 13px;
  font-weight: 500;
  color: var(--el-text-color-primary);
}

.detail-link-item .link-url {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  flex: 1;
}

/* 弹窗样式优化 */
.detail-dialog :deep(.el-dialog) {
  border-radius: 12px;
}

.detail-dialog :deep(.el-dialog__header) {
  background: linear-gradient(to right, var(--el-fill-color-lighter) 0%, var(--el-bg-color) 100%);
  border-bottom: 1px solid var(--el-border-color-lighter);
  padding: 18px 24px;
  margin: 0;
}

.detail-dialog :deep(.el-dialog__body) {
  padding: 24px;
  max-height: 70vh;
  overflow-y: auto;
}
</style>
