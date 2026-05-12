<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="scheduled-tasks-page">
    <!-- 概览卡片 -->
    <div class="tasks-overview">
      <div class="overview-card is-total">
        <span class="overview-label">任务总数</span>
        <strong class="overview-value">{{ taskSummary.total }}</strong>
        <span class="overview-desc">系统已注册的定时任务</span>
      </div>
      <div class="overview-card is-success">
        <span class="overview-label">已启用</span>
        <strong class="overview-value">{{ taskSummary.enabled }}</strong>
        <span class="overview-desc">正常调度中的任务</span>
      </div>
      <div class="overview-card is-danger">
        <span class="overview-label">最近失败</span>
        <strong class="overview-value">{{ taskSummary.failed }}</strong>
        <span class="overview-desc">上次执行失败的任务</span>
      </div>
    </div>

    <!-- 任务列表 -->
    <DataTable
      :data="rows"
      :loading="loading"
      :columns="columns"
      :compact-mode="true"
      :show-column-settings="true"
      cache-key="system-scheduled-tasks"
      :search-config="searchConfig"
      :row-class-name="getRowClassName"
      @search="onSearch"
    >
      <template #header>
        <h2>定时任务</h2>
        <p>管理系统定时任务，支持启禁用、修改调度频率、手动触发和查看执行日志。</p>
      </template>

      <template #header-actions>
        <el-button type="primary" @click="openCreate">
          <i class="bi bi-plus-circle" style="margin-right: 4px;"></i>新增任务
        </el-button>
        <el-button @click="refresh">
          <i class="bi bi-arrow-clockwise" style="margin-right: 4px;"></i>刷新
        </el-button>
      </template>

      <template #column-displayName="{ row }">
        <div class="task-name-cell">
          <span class="task-name">{{ row.displayName }}</span>
          <span class="task-key">{{ row.taskKey }}</span>
        </div>
      </template>

      <template #column-group="{ row }">
        <el-tag v-if="row.group" type="info" effect="plain" round size="small">
          {{ row.group }}
        </el-tag>
        <span v-else class="text-muted">-</span>
      </template>

      <template #column-cronExpression="{ row }">
        <div class="cron-cell">
          <span class="cron-human">{{ cronToHuman(row.cronExpression) }}</span>
          <code class="cron-code">{{ row.cronExpression }}</code>
        </div>
      </template>

      <template #column-executionTarget="{ row }">
        <div class="execution-cell">
          <el-tag size="small" type="warning" effect="plain">{{ row.executionType || '内置方法' }}</el-tag>
          <div class="execution-target">{{ row.executionTarget || row.taskKey }}</div>
        </div>
      </template>

      <template #column-isEnabled="{ row }">
        <el-switch
          :model-value="row.isEnabled"
          @change="(val: boolean) => onToggleEnabled(row, val)"
          :loading="row._switching"
          size="small"
        />
      </template>

      <template #column-lastRunAt="{ row }">
        <span v-if="row.lastRunAt">{{ formatTime(row.lastRunAt) }}</span>
        <span v-else class="text-muted">从未执行</span>
      </template>

      <template #column-nextRunAt="{ row }">
        <span v-if="row.nextRunAt">{{ formatTime(row.nextRunAt) }}</span>
        <span v-else class="text-muted">-</span>
      </template>

      <template #column-lastResult="{ row }">
        <el-tag
          v-if="row.lastResult"
          :type="getResultTagType(row.lastResult)"
          effect="light"
          round
          size="small"
        >
          {{ getResultLabel(row.lastResult) }}
        </el-tag>
        <span v-else class="text-muted">-</span>
      </template>

      <template #actions="{ row }">
        <el-button type="primary" link size="small" @click.stop="onTrigger(row)">
          <i class="bi bi-play-circle" style="margin-right: 2px;"></i>执行
        </el-button>
        <el-button type="warning" link size="small" @click.stop="openEdit(row)">
          <i class="bi bi-pencil" style="margin-right: 2px;"></i>编辑
        </el-button>
        <el-button type="info" link size="small" @click.stop="openLogs(row)">
          <i class="bi bi-journal-text" style="margin-right: 2px;"></i>日志
        </el-button>
        <el-button v-if="row.definitionType === 'Dynamic'" type="danger" link size="small" @click.stop="onDelete(row)">
          <i class="bi bi-trash" style="margin-right: 2px;"></i>删除
        </el-button>
      </template>
    </DataTable>

    <!-- 编辑对话框 -->
    <el-dialog
      v-model="editVisible"
      title="编辑定时任务"
      width="520px"
      destroy-on-close
    >
      <el-form v-if="editForm" label-width="100px" @submit.prevent="onSaveEdit">
        <el-form-item label="任务名称">
          <el-input :model-value="editForm.displayName" disabled />
        </el-form-item>
        <el-form-item label="分组">
          <el-input :model-value="editForm.group || '-'" disabled />
        </el-form-item>
        <el-form-item label="执行内容">
          <div class="execution-panel">
            <el-tag size="small" type="warning" effect="plain">{{ editForm.executionType || '内置方法' }}</el-tag>
            <div class="execution-panel-text">{{ editForm.executionTarget || editForm.taskKey }}</div>
          </div>
        </el-form-item>
        <el-form-item label="执行时间">
          <ScheduledTaskCronBuilder v-model="editForm.cronExpression" />
          <div class="cron-hint">请直接选择“每天 / 每周 / 每月 / 按间隔”，无需手工记忆 Cron。</div>
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="editForm.description" type="textarea" :rows="3" placeholder="任务描述（可选）" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="editForm.isEnabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editVisible = false">取消</el-button>
        <el-button type="primary" :loading="editSaving" @click="onSaveEdit">保存</el-button>
      </template>
    </el-dialog>

    <!-- 新增任务对话框 -->
    <el-dialog
      v-model="createVisible"
      title="新增定时任务"
      width="680px"
      destroy-on-close
      top="5vh"
    >
      <!-- 步骤条 -->
      <el-steps :active="createStep" finish-status="success" simple style="margin-bottom: 24px;">
        <el-step title="选择类型" />
        <el-step title="配置内容" />
        <el-step title="设置时间" />
      </el-steps>

      <!-- 步骤 1：选择执行提供器 -->
      <div v-if="createStep === 0" class="create-step">
        <div class="provider-cards">
          <div
            v-for="p in providers"
            :key="p.sourceKey"
            class="provider-card"
            :class="{ active: createForm.executionSource === p.sourceKey }"
            @click="createForm.executionSource = p.sourceKey"
          >
            <i :class="p.icon || 'bi bi-gear'" class="provider-icon"></i>
            <div class="provider-info">
              <strong>{{ p.displayName }}</strong>
              <span>{{ p.description }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- 步骤 2：配置执行内容 -->
      <div v-if="createStep === 1" class="create-step">
        <el-form label-width="110px">
          <template v-for="field in currentFormFields" :key="field.name">
            <el-form-item
              v-if="isFieldVisible(field)"
              :label="field.label"
              :required="field.required"
            >
              <!-- action-picker：能力选择器 -->
              <template v-if="field.type === 'action-picker'">
                <el-select
                  v-model="configFormData[field.name]"
                  filterable
                  :placeholder="field.placeholder || '请选择能力'"
                  style="width: 100%;"
                  @change="onActionSelected"
                >
                  <el-option-group
                    v-for="group in actionGroups"
                    :key="group.label"
                    :label="group.label"
                  >
                    <el-option
                      v-for="action in group.items"
                      :key="action.actionKey"
                      :value="action.actionKey"
                      :label="action.displayName"
                    >
                      <span>{{ action.displayName }}</span>
                      <span style="float: right; color: var(--el-text-color-placeholder); font-size: 12px;">{{ action.source }}</span>
                    </el-option>
                  </el-option-group>
                </el-select>
                <div v-if="selectedActionDesc" class="field-hint">{{ selectedActionDesc }}</div>
              </template>
              <!-- select -->
              <template v-else-if="field.type === 'select'">
                <el-select
                  v-model="configFormData[field.name]"
                  :multiple="field.multiple"
                  :placeholder="field.placeholder || '请选择'"
                  clearable
                  style="width: 100%;"
                >
                  <el-option
                    v-for="opt in field.options || []"
                    :key="opt.value"
                    :value="opt.value"
                    :label="opt.label"
                  />
                </el-select>
              </template>
              <!-- switch -->
              <template v-else-if="field.type === 'switch'">
                <el-switch v-model="configFormData[field.name]" />
              </template>
              <!-- number -->
              <template v-else-if="field.type === 'number'">
                <el-input-number
                  v-model="configFormData[field.name]"
                  :min="field.minValue ?? undefined"
                  :max="field.maxValue ?? undefined"
                  controls-position="right"
                />
              </template>
              <!-- textarea / code-editor -->
              <template v-else-if="field.type === 'textarea' || field.type === 'code-editor'">
                <el-input
                  v-model="configFormData[field.name]"
                  type="textarea"
                  :rows="field.rows || 4"
                  :placeholder="field.placeholder || ''"
                  style="font-family: 'Cascadia Code', 'Fira Code', monospace;"
                />
              </template>
              <!-- json-editor -->
              <template v-else-if="field.type === 'json-editor'">
                <el-input
                  v-model="configFormData[field.name]"
                  type="textarea"
                  :rows="field.rows || 3"
                  :placeholder="field.placeholder || '{}'"
                  style="font-family: 'Cascadia Code', 'Fira Code', monospace;"
                />
              </template>
              <!-- input (default) -->
              <template v-else>
                <el-input
                  v-model="configFormData[field.name]"
                  :placeholder="field.placeholder || ''"
                />
              </template>
              <div v-if="field.description" class="field-hint">{{ field.description }}</div>
            </el-form-item>
          </template>

          <!-- 测试按钮 -->
          <el-form-item v-if="currentProvider?.supportsTest" label=" ">
            <el-button :loading="testLoading" @click="onTestExecute">
              <i class="bi bi-lightning" style="margin-right: 4px;"></i>测试执行
            </el-button>
            <span v-if="testResult" :style="{ color: testResult.success ? 'var(--el-color-success)' : 'var(--el-color-danger)', marginLeft: '12px', fontSize: '13px' }">
              {{ testResult.message }}
            </span>
          </el-form-item>
        </el-form>
      </div>

      <!-- 步骤 3：设置时间 -->
      <div v-if="createStep === 2" class="create-step">
        <el-form label-width="110px">
          <el-form-item label="任务名称" required>
            <el-input v-model="createForm.displayName" placeholder="请输入任务名称" />
          </el-form-item>
          <el-form-item label="分组">
            <el-input v-model="createForm.group" placeholder="如：系统维护（可选）" />
          </el-form-item>
          <el-form-item label="执行时间" required>
            <ScheduledTaskCronBuilder v-model="createForm.cronExpression" />
          </el-form-item>
          <el-form-item label="描述">
            <el-input v-model="createForm.description" type="textarea" :rows="2" placeholder="任务描述（可选）" />
          </el-form-item>
          <el-form-item label="立即启用">
            <el-switch v-model="createForm.isEnabled" />
          </el-form-item>
        </el-form>
      </div>

      <template #footer>
        <div class="create-footer">
          <el-button v-if="createStep > 0" @click="createStep--">上一步</el-button>
          <el-button v-if="createStep < 2" type="primary" :disabled="!canNextStep" @click="createStep++">下一步</el-button>
          <el-button v-if="createStep === 2" type="primary" :loading="createSaving" @click="onSaveCreate">创建任务</el-button>
          <el-button @click="createVisible = false">取消</el-button>
        </div>
      </template>
    </el-dialog>

    <!-- 执行日志（居中弹窗） -->
    <el-dialog
      v-model="logDialogVisible"
      :title="`执行日志 — ${logDialogTaskName}`"
      width="1100px"
      top="5vh"
      destroy-on-close
      class="task-log-dialog"
    >
      <div class="log-toolbar">
        <el-button size="small" @click="loadLogs">
          <i class="bi bi-arrow-clockwise" style="margin-right: 4px;"></i>刷新
        </el-button>
        <span class="log-toolbar-tip">点击"详情"可查看任务执行过程中的完整输出（含成功路径下的提示与警告）。</span>
      </div>
      <el-table :data="logRows" v-loading="logLoading" stripe size="small" style="width: 100%">
        <el-table-column prop="startedAt" label="开始时间" width="170" :formatter="(row: any) => formatTime(row.startedAt)" />
        <el-table-column prop="finishedAt" label="结束时间" width="170" :formatter="(row: any) => row.finishedAt ? formatTime(row.finishedAt) : '-'" />
        <el-table-column prop="success" label="结果" width="80" align="center">
          <template #default="{ row }">
            <el-tag :type="row.success ? 'success' : 'danger'" effect="light" round size="small">
              {{ row.success ? '成功' : '失败' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="elapsedMs" label="耗时" width="90" align="right">
          <template #default="{ row }">
            {{ row.elapsedMs != null ? `${row.elapsedMs} ms` : '-' }}
          </template>
        </el-table-column>
        <el-table-column prop="triggerType" label="触发方式" width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="row.triggerType === 'Manual' ? 'warning' : 'info'" effect="plain" size="small">
              {{ row.triggerType === 'Manual' ? '手动' : '自动' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="结果摘要" min-width="240" show-overflow-tooltip>
          <template #default="{ row }">
            <span :class="{ 'log-summary-error': !row.success }">
              {{ buildLogSummary(row) || '-' }}
            </span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="90" align="center" fixed="right">
          <template #default="{ row }">
            <el-button type="primary" link size="small" @click="openLogDetail(row)">详情</el-button>
          </template>
        </el-table-column>
      </el-table>
      <div class="log-pagination" v-if="logPagination.total > 0">
        <el-pagination
          v-model:current-page="logPagination.page"
          v-model:page-size="logPagination.pageSize"
          :total="Number(logPagination.total) || 0"
          :page-sizes="[10, 20, 50]"
          layout="total, sizes, prev, pager, next"
          small
          @current-change="loadLogs"
          @size-change="() => { logPagination.page = 1; loadLogs() }"
        />
      </div>
    </el-dialog>

    <!-- 单条日志详情弹窗 -->
    <el-dialog
      v-model="logDetailVisible"
      title="执行详情"
      width="900px"
      top="6vh"
      destroy-on-close
      append-to-body
      class="task-log-detail-dialog"
    >
      <div v-if="logDetailRow" class="log-detail-content">
        <div class="log-detail-meta">
          <div class="log-detail-meta-row">
            <span class="meta-key">任务</span>
            <span class="meta-val">{{ logDialogTaskName }}（{{ logDetailRow.taskKey }}）</span>
          </div>
          <div class="log-detail-meta-row">
            <span class="meta-key">触发方式</span>
            <el-tag :type="logDetailRow.triggerType === 'Manual' ? 'warning' : 'info'" effect="plain" size="small">
              {{ logDetailRow.triggerType === 'Manual' ? '手动' : '自动' }}
            </el-tag>
          </div>
          <div class="log-detail-meta-row">
            <span class="meta-key">开始 / 结束</span>
            <span class="meta-val">
              {{ formatTime(logDetailRow.startedAt) }}
              <span class="meta-sep">→</span>
              {{ logDetailRow.finishedAt ? formatTime(logDetailRow.finishedAt) : '-' }}
              <span class="meta-elapsed" v-if="logDetailRow.elapsedMs != null">耗时 {{ logDetailRow.elapsedMs }} ms</span>
            </span>
          </div>
          <div class="log-detail-meta-row">
            <span class="meta-key">结果</span>
            <el-tag :type="logDetailRow.success ? 'success' : 'danger'" effect="light" size="small" round>
              {{ logDetailRow.success ? '成功' : '失败' }}
            </el-tag>
          </div>
        </div>

        <div v-if="logDetailRow.errorMessage" class="log-detail-section log-detail-error">
          <div class="log-detail-section-title">错误信息</div>
          <pre class="log-detail-pre">{{ logDetailRow.errorMessage }}</pre>
        </div>

        <div class="log-detail-section">
          <div class="log-detail-section-title">执行过程输出（{{ parsedOutput.length }} 条）</div>
          <div v-if="parsedOutput.length === 0" class="log-detail-empty">该次执行未产生过程输出。</div>
          <ul v-else class="log-detail-output-list">
            <li
              v-for="(entry, idx) in parsedOutput"
              :key="idx"
              :class="['log-output-item', `log-output-${(entry.level || 'info').toLowerCase()}`]"
            >
              <span class="log-output-level">{{ entry.level }}</span>
              <span class="log-output-time">{{ entry.at }}</span>
              <span class="log-output-msg">{{ entry.message }}</span>
            </li>
          </ul>
        </div>

        <div v-if="parsedException" class="log-detail-section log-detail-error">
          <div class="log-detail-section-title">异常堆栈</div>
          <pre class="log-detail-pre">{{ parsedException }}</pre>
        </div>

        <details class="log-detail-raw">
          <summary>原始 JSON（DetailsJson）</summary>
          <pre class="log-detail-pre">{{ logDetailRow.detailsJson || '(空)' }}</pre>
        </details>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import DataTable from '@/components/DataTable/index.vue'
import ScheduledTaskCronBuilder from './ScheduledTaskCronBuilder.vue'
import type { SearchFieldConfig } from '@/components/DataTable/types'
import {
  getScheduledTasks,
  updateScheduledTask,
  triggerScheduledTask,
  getScheduledTaskLogs,
  createDynamicTask,
  deleteDynamicTask,
  getExecutionProviders,
  getInvocableActions,
  testExecute,
  type ScheduledTaskItem,
  type ScheduledTaskLogItem,
  type ExecutionProviderInfo,
  type ExecutionFormField,
  type InvocableActionInfo
} from '@/api/scheduledTasks'
import { cronToHuman } from './scheduled-task-cron'

interface TaskRow extends ScheduledTaskItem {
  _switching?: boolean
}

const loading = ref(false)
const rows = ref<TaskRow[]>([])
const currentKeyword = ref('')

// 概览统计
const taskSummary = computed(() => {
  const total = rows.value.length
  const enabled = rows.value.filter(r => r.isEnabled).length
  const failed = rows.value.filter(r => r.lastResult === 'Failed').length
  return { total, enabled, failed }
})

// 表格列配置
const columns = [
  { prop: 'displayName', label: '任务名称', minWidth: 200, slot: 'column-displayName' },
  { prop: 'group', label: '分组', width: 160, slot: 'column-group', align: 'center' as const },
  { prop: 'executionTarget', label: '执行内容', minWidth: 360, slot: 'column-executionTarget' },
  { prop: 'cronExpression', label: '执行时间', minWidth: 220, slot: 'column-cronExpression' },
  { prop: 'isEnabled', label: '状态', width: 80, slot: 'column-isEnabled', align: 'center' as const },
  { prop: 'lastRunAt', label: '上次执行', width: 180, slot: 'column-lastRunAt' },
  { prop: 'nextRunAt', label: '下次执行', width: 180, slot: 'column-nextRunAt' },
  { prop: 'lastResult', label: '上次结果', width: 100, slot: 'column-lastResult', align: 'center' as const }
]

// 搜索配置
const searchConfig = computed<SearchFieldConfig[]>(() => {
  const groups = [...new Set(rows.value.map(r => r.group).filter(Boolean))]
  return [
    { key: 'keyword', label: '关键词', type: 'input', placeholder: '任务名称 / TaskKey', simple: true, width: 200 },
    {
      key: 'group',
      label: '分组',
      type: 'select',
      options: groups.map(g => ({ label: g!, value: g! })),
      placeholder: '选择分组',
      simple: true,
      clearable: true,
      width: 140
    }
  ]
})

// ===== 加载 =====
async function load() {
  loading.value = true
  try {
    const res = await getScheduledTasks()
    const data = (res as any)?.data ?? res
    let items = data?.items ?? data ?? []
    if (!Array.isArray(items)) items = []
    rows.value = items.map((item: ScheduledTaskItem) => ({ ...item, _switching: false }))
  } catch (e: any) {
    ElMessage.error('加载定时任务失败: ' + (e?.message || '未知错误'))
  } finally {
    loading.value = false
  }
}

function refresh() { load() }

function onSearch(params: Record<string, any>) {
  currentKeyword.value = params.keyword || ''
  // 前端筛选（数据量小，无需分页）
  load()
}

// ===== 启禁用 =====
async function onToggleEnabled(row: TaskRow, val: boolean) {
  row._switching = true
  try {
    await updateScheduledTask(row.taskKey, {
      isEnabled: val,
      cronExpression: row.cronExpression,
      description: row.description
    })
    row.isEnabled = val
    ElMessage.success(val ? '已启用' : '已禁用')
  } catch (e: any) {
    ElMessage.error('操作失败: ' + (e?.message || '未知错误'))
  } finally {
    row._switching = false
  }
}

// ===== 手动触发 =====
async function onTrigger(row: TaskRow) {
  try {
    await ElMessageBox.confirm(
      `确定要手动执行任务「${row.displayName}」吗？`,
      '手动触发',
      { type: 'warning', confirmButtonText: '执行', cancelButtonText: '取消' }
    )
    ElMessage.info('正在执行...')
    await triggerScheduledTask(row.taskKey)
    ElMessage.success('任务已执行完成')
    await load()
  } catch (e: any) {
    if (e !== 'cancel' && e?.message !== 'cancel') {
      ElMessage.error('执行失败: ' + (e?.message || '未知错误'))
    }
  }
}

// ===== 编辑 =====
const editVisible = ref(false)
const editSaving = ref(false)
const editForm = ref<{
  taskKey: string
  displayName: string
  group: string | null
  executionType: string | null
  executionTarget: string | null
  cronExpression: string
  description: string | null
  isEnabled: boolean
} | null>(null)

function openEdit(row: TaskRow) {
  editForm.value = {
    taskKey: row.taskKey,
    displayName: row.displayName,
    group: row.group,
    executionType: row.executionType,
    executionTarget: row.executionTarget,
    cronExpression: row.cronExpression,
    description: row.description,
    isEnabled: row.isEnabled
  }
  editVisible.value = true
}

async function onSaveEdit() {
  if (!editForm.value) return
  if (!editForm.value.cronExpression?.trim()) {
    ElMessage.warning('Cron 表达式不能为空')
    return
  }
  editSaving.value = true
  try {
    await updateScheduledTask(editForm.value.taskKey, {
      isEnabled: editForm.value.isEnabled,
      cronExpression: editForm.value.cronExpression.trim(),
      description: editForm.value.description
    })
    ElMessage.success('保存成功')
    editVisible.value = false
    await load()
  } catch (e: any) {
    ElMessage.error('保存失败: ' + (e?.message || '未知错误'))
  } finally {
    editSaving.value = false
  }
}

// ===== 执行日志 =====
const logDialogVisible = ref(false)
const logDialogTaskKey = ref('')
const logDialogTaskName = ref('')
const logLoading = ref(false)
const logRows = ref<ScheduledTaskLogItem[]>([])
const logPagination = ref({ total: 0, page: 1, pageSize: 20 })

// 单条日志详情
const logDetailVisible = ref(false)
const logDetailRow = ref<ScheduledTaskLogItem | null>(null)

interface ParsedOutputEntry { level: string; at: string; message: string }

const parsedOutput = computed<ParsedOutputEntry[]>(() => {
  const raw = logDetailRow.value?.detailsJson
  if (!raw) return []
  try {
    const obj = JSON.parse(raw)
    const arr = Array.isArray(obj?.output) ? obj.output : []
    return arr.map((e: any) => ({
      level: String(e?.level || 'Info'),
      at: String(e?.at || ''),
      message: String(e?.message || '')
    }))
  } catch {
    return []
  }
})

const parsedException = computed<string>(() => {
  const raw = logDetailRow.value?.detailsJson
  if (!raw) return ''
  try {
    const obj = JSON.parse(raw)
    if (!obj?.exception) return ''
    const ex = obj.exception
    return [ex.type, ex.message, ex.stackTrace].filter(Boolean).join('\n')
  } catch {
    return ''
  }
})

function buildLogSummary(row: ScheduledTaskLogItem): string {
  // 失败时优先显示 errorMessage；成功时优先解析 detailsJson 中最后一条 Result/Info
  if (!row.success && row.errorMessage) {
    return row.errorMessage.length > 120 ? row.errorMessage.slice(0, 120) + '...' : row.errorMessage
  }
  if (row.detailsJson) {
    try {
      const obj = JSON.parse(row.detailsJson)
      const arr: any[] = Array.isArray(obj?.output) ? obj.output : []
      // 先找 Result，没有则取最后一条
      const result = [...arr].reverse().find(e => String(e?.level).toLowerCase() === 'result')
      const last = arr[arr.length - 1]
      const msg = (result?.message || last?.message || '').toString()
      return msg.length > 120 ? msg.slice(0, 120) + '...' : msg
    } catch {
      // ignore
    }
  }
  return row.errorMessage || ''
}

function openLogs(row: TaskRow) {
  logDialogTaskKey.value = row.taskKey
  logDialogTaskName.value = row.displayName
  logPagination.value = { total: 0, page: 1, pageSize: 20 }
  logRows.value = []
  logDialogVisible.value = true
  loadLogs()
}

function openLogDetail(row: ScheduledTaskLogItem) {
  logDetailRow.value = row
  logDetailVisible.value = true
}

async function loadLogs() {
  logLoading.value = true
  try {
    const res = await getScheduledTaskLogs(
      logDialogTaskKey.value,
      logPagination.value.page,
      logPagination.value.pageSize
    )
    const data = (res as any)?.data ?? res
    logRows.value = data?.items ?? []
    logPagination.value.total = Number(data?.total) || 0
  } catch (e: any) {
    ElMessage.error('加载日志失败: ' + (e?.message || '未知错误'))
  } finally {
    logLoading.value = false
  }
}

// ===== 工具函数 =====
function formatTime(val: string | null | undefined): string {
  if (!val) return '-'
  try {
    const d = new Date(val)
    if (isNaN(d.getTime())) return val
    const pad = (n: number) => String(n).padStart(2, '0')
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
  } catch {
    return val
  }
}

function getResultTagType(result: string | null): '' | 'success' | 'warning' | 'danger' | 'info' {
  switch (result) {
    case 'Success': return 'success'
    case 'Failed': return 'danger'
    case 'Running': return 'warning'
    default: return 'info'
  }
}

function getResultLabel(result: string | null): string {
  switch (result) {
    case 'Success': return '成功'
    case 'Failed': return '失败'
    case 'Running': return '执行中'
    default: return result || '-'
  }
}

function getRowClassName({ row }: { row: TaskRow; rowIndex: number }) {
  if (row.lastResult === 'Failed') return 'task-row-failed'
  if (!row.isEnabled) return 'task-row-disabled'
  return ''
}

// ===== 删除动态任务 =====
async function onDelete(row: TaskRow) {
  try {
    await ElMessageBox.confirm(
      `确定要删除任务「${row.displayName}」吗？删除后不可恢复。`,
      '删除确认',
      { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消', confirmButtonClass: 'el-button--danger' }
    )
    await deleteDynamicTask(row.taskKey)
    ElMessage.success('删除成功')
    await load()
  } catch (e: any) {
    if (e !== 'cancel' && e?.message !== 'cancel') {
      ElMessage.error('删除失败: ' + (e?.message || '未知错误'))
    }
  }
}

// ===== 新增任务 =====
const createVisible = ref(false)
const createStep = ref(0)
const createSaving = ref(false)
const providers = ref<ExecutionProviderInfo[]>([])
const actions = ref<InvocableActionInfo[]>([])
const configFormData = ref<Record<string, any>>({})
const testLoading = ref(false)
const testResult = ref<{ success: boolean; message: string | null } | null>(null)

const createForm = ref({
  displayName: '',
  group: '',
  cronExpression: '0 0 * * *',
  description: '',
  isEnabled: true,
  executionSource: ''
})

// 当前选中的提供器
const currentProvider = computed(() =>
  providers.value.find(p => p.sourceKey === createForm.value.executionSource)
)

// 当前表单字段
const currentFormFields = computed<ExecutionFormField[]>(() =>
  currentProvider.value?.formDefinition?.fields ?? []
)

// 能力分组列表
const actionGroups = computed(() => {
  const map = new Map<string, InvocableActionInfo[]>()
  for (const a of actions.value) {
    const key = a.source || a.category || '未分类'
    if (!map.has(key)) map.set(key, [])
    map.get(key)!.push(a)
  }
  return Array.from(map.entries()).map(([label, items]) => ({ label, items }))
})

// 选中能力的描述
const selectedActionDesc = computed(() => {
  const key = configFormData.value['actionKey']
  if (!key) return ''
  const action = actions.value.find(a => a.actionKey === key)
  return action?.description || ''
})

// 是否可以下一步
const canNextStep = computed(() => {
  if (createStep.value === 0) return !!createForm.value.executionSource
  if (createStep.value === 1) {
    // 检查必填字段
    for (const field of currentFormFields.value) {
      if (field.required && !configFormData.value[field.name]) return false
    }
    return true
  }
  return true
})

function isFieldVisible(field: ExecutionFormField): boolean {
  if (!field.dependsOn) return true
  return !!configFormData.value[field.dependsOn]
}

function onActionSelected() {
  // 选中能力后自动填写任务名称
  const key = configFormData.value['actionKey']
  if (!key) return
  const action = actions.value.find(a => a.actionKey === key)
  if (action && !createForm.value.displayName) {
    createForm.value.displayName = `定时执行-${action.displayName}`
  }
}

async function openCreate() {
  createStep.value = 0
  createForm.value = {
    displayName: '',
    group: '',
    cronExpression: '0 0 * * *',
    description: '',
    isEnabled: true,
    executionSource: ''
  }
  configFormData.value = {}
  testResult.value = null
  createVisible.value = true

  // 加载提供器和能力列表
  try {
    const [pRes, aRes] = await Promise.all([getExecutionProviders(), getInvocableActions()])
    providers.value = ((pRes as any)?.data ?? pRes)?.items ?? []
    actions.value = ((aRes as any)?.data ?? aRes)?.items ?? []
  } catch (e: any) {
    ElMessage.error('加载执行提供器失败: ' + (e?.message || ''))
  }
}

async function onTestExecute() {
  testLoading.value = true
  testResult.value = null
  try {
    const configJson = JSON.stringify(configFormData.value)
    const res = await testExecute(createForm.value.executionSource, configJson)
    const data = (res as any)?.data ?? res
    testResult.value = { success: data?.success, message: data?.message || ('测试' + (data?.success ? '成功' : '失败')) }
  } catch (e: any) {
    testResult.value = { success: false, message: e?.message || '测试失败' }
  } finally {
    testLoading.value = false
  }
}

async function onSaveCreate() {
  if (!createForm.value.displayName?.trim()) {
    ElMessage.warning('任务名称不能为空')
    return
  }
  if (!createForm.value.cronExpression?.trim()) {
    ElMessage.warning('Cron 表达式不能为空')
    return
  }
  createSaving.value = true
  try {
    await createDynamicTask({
      displayName: createForm.value.displayName.trim(),
      group: createForm.value.group?.trim() || null,
      cronExpression: createForm.value.cronExpression.trim(),
      description: createForm.value.description?.trim() || null,
      isEnabled: createForm.value.isEnabled,
      executionSource: createForm.value.executionSource,
      configJson: JSON.stringify(configFormData.value)
    })
    ElMessage.success('任务创建成功')
    createVisible.value = false
    await load()
  } catch (e: any) {
    ElMessage.error('创建失败: ' + (e?.message || '未知错误'))
  } finally {
    createSaving.value = false
  }
}

onMounted(() => load())
</script>

<style scoped>
.scheduled-tasks-page {
  padding: 0;
}

.tasks-overview {
  display: flex;
  gap: 16px;
  margin-bottom: 16px;
}

.overview-card {
  flex: 1;
  padding: 16px 20px;
  border-radius: 8px;
  background: var(--el-bg-color);
  border: 1px solid var(--el-border-color-lighter);
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.overview-card.is-total { border-left: 3px solid var(--el-color-primary); }
.overview-card.is-success { border-left: 3px solid var(--el-color-success); }
.overview-card.is-danger { border-left: 3px solid var(--el-color-danger); }

.overview-label {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.overview-value {
  font-size: 28px;
  font-weight: 600;
  line-height: 1.2;
  color: var(--el-text-color-primary);
}

.overview-desc {
  font-size: 11px;
  color: var(--el-text-color-placeholder);
}

.task-name-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.task-name {
  font-weight: 500;
  color: var(--el-text-color-primary);
}

.task-key {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  font-family: monospace;
}

.cron-code {
  font-size: 12px;
  padding: 2px 6px;
  background: var(--el-fill-color-light);
  border-radius: 4px;
  color: var(--el-text-color-regular);
  font-family: 'Cascadia Code', 'Fira Code', monospace;
}

.cron-cell {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.cron-human {
  font-size: 12px;
  color: var(--el-text-color-primary);
}

.execution-cell {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.execution-target {
  font-size: 12px;
  line-height: 1.6;
  color: var(--el-text-color-regular);
  word-break: break-all;
}

.execution-panel {
  display: flex;
  flex-direction: column;
  gap: 8px;
  width: 100%;
}

.execution-panel-text {
  padding: 10px 12px;
  border-radius: 6px;
  background: var(--el-fill-color-light);
  color: var(--el-text-color-regular);
  line-height: 1.7;
  word-break: break-all;
}

.cron-hint {
  font-size: 11px;
  color: var(--el-text-color-placeholder);
  margin-top: 4px;
}

.text-muted {
  color: var(--el-text-color-placeholder);
  font-size: 12px;
}

/* 日志弹窗 */
.log-toolbar {
  margin-bottom: 12px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.log-toolbar-tip {
  font-size: 12px;
  color: var(--el-text-color-secondary, #909399);
  flex: 1;
  text-align: right;
}

.log-pagination {
  margin-top: 12px;
  display: flex;
  justify-content: flex-end;
}

.log-summary-error {
  color: var(--el-color-danger, #f56c6c);
}

/* 日志详情弹窗 */
.log-detail-content {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.log-detail-meta {
  display: grid;
  grid-template-columns: max-content 1fr;
  row-gap: 8px;
  column-gap: 14px;
  padding: 12px 14px;
  background: var(--el-fill-color-lighter, #f5f7fa);
  border-radius: 6px;
}

.log-detail-meta-row {
  display: contents;
}

.log-detail-meta-row .meta-key {
  color: var(--el-text-color-secondary, #909399);
  font-size: 13px;
}

.log-detail-meta-row .meta-val {
  color: var(--el-text-color-primary);
  font-size: 13px;
}

.log-detail-meta .meta-sep {
  margin: 0 6px;
  color: var(--el-text-color-secondary);
}

.log-detail-meta .meta-elapsed {
  margin-left: 12px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.log-detail-section-title {
  font-weight: 600;
  margin-bottom: 8px;
  color: var(--el-text-color-primary);
  font-size: 14px;
}

.log-detail-error .log-detail-section-title {
  color: var(--el-color-danger, #f56c6c);
}

.log-detail-pre {
  margin: 0;
  padding: 10px 12px;
  background: var(--el-fill-color-light, #f2f3f5);
  border-radius: 4px;
  font-family: ui-monospace, "JetBrains Mono", "Fira Code", Consolas, monospace;
  font-size: 12.5px;
  white-space: pre-wrap;
  word-break: break-word;
  max-height: 280px;
  overflow: auto;
}

.log-detail-empty {
  padding: 14px;
  text-align: center;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-lighter);
  border-radius: 4px;
  font-size: 13px;
}

.log-detail-output-list {
  list-style: none;
  margin: 0;
  padding: 0;
  border: 1px solid var(--el-border-color-lighter, #ebeef5);
  border-radius: 6px;
  max-height: 360px;
  overflow: auto;
}

.log-output-item {
  display: grid;
  grid-template-columns: 70px 170px 1fr;
  gap: 12px;
  padding: 8px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter, #ebeef5);
  font-size: 12.5px;
  align-items: start;
}

.log-output-item:last-child {
  border-bottom: 0;
}

.log-output-level {
  font-weight: 600;
  text-align: center;
  border-radius: 3px;
  padding: 1px 6px;
  font-size: 11.5px;
  height: fit-content;
}

.log-output-info .log-output-level { background: #e8f4ff; color: #409eff; }
.log-output-warn .log-output-level { background: #fff4e6; color: #e6a23c; }
.log-output-error .log-output-level { background: #fef0f0; color: #f56c6c; }
.log-output-result .log-output-level { background: #ecf5e6; color: #67c23a; }

.log-output-time {
  color: var(--el-text-color-secondary);
  font-family: ui-monospace, monospace;
}

.log-output-msg {
  color: var(--el-text-color-primary);
  word-break: break-word;
  white-space: pre-wrap;
}

.log-detail-raw {
  margin-top: 4px;
}

.log-detail-raw summary {
  cursor: pointer;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  padding: 4px 0;
}

/* 新增任务弹窗 */
.create-step {
  min-height: 200px;
}

.provider-cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 12px;
}

.provider-card {
  display: flex;
  align-items: flex-start;
  gap: 14px;
  padding: 16px;
  border: 2px solid var(--el-border-color-lighter);
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.2s;
  background: var(--el-bg-color);
}

.provider-card:hover {
  border-color: var(--el-color-primary-light-3);
  background: var(--el-color-primary-light-9);
}

.provider-card.active {
  border-color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  box-shadow: 0 0 0 1px var(--el-color-primary-light-5);
}

.provider-icon {
  font-size: 28px;
  color: var(--el-color-primary);
  flex-shrink: 0;
  margin-top: 2px;
}

.provider-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.provider-info strong {
  font-size: 14px;
  color: var(--el-text-color-primary);
}

.provider-info span {
  font-size: 12px;
  color: var(--el-text-color-secondary);
  line-height: 1.5;
}

.field-hint {
  font-size: 11px;
  color: var(--el-text-color-placeholder);
  margin-top: 4px;
  line-height: 1.5;
}

.create-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

/* 行样式 */
:deep(.task-row-failed) {
  background-color: var(--el-color-danger-light-9) !important;
}

:deep(.task-row-disabled) {
  opacity: 0.6;
}
</style>
