<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="gk-datatable">
    <!-- Unified Card -->
    <el-card shadow="never" class="dt-card" :class="{ 'compact-mode': compactMode }">
      <!-- Page Header (Title + Actions) -->
      <div v-if="$slots.header || $slots['header-actions']" class="dt-page-header">
        <div class="dt-page-title">
          <slot name="header">
            <h2>数据列表</h2>
          </slot>
        </div>
        <div class="dt-page-actions">
          <slot name="header-actions" />
        </div>
      </div>

      <!-- Integrated Toolbar with Search -->
      <div class="dt-toolbar">
        <div class="dt-toolbar-left">
          <!-- Simple Search Inline -->
          <div v-if="searchConfig && searchConfig.length" class="dt-inline-search">
            <template v-for="(field, idx) in simpleFields" :key="field.key">
              <div class="search-field-inline">
                <label v-if="field.label" class="search-label">{{ field.label }}</label>
                <template v-if="field.type === 'input'">
                  <el-input
                    v-model="internalSearch[field.key]"
                    :placeholder="field.placeholder"
                    :clearable="field.clearable !== false"
                    size="default"
                    :style="{ maxWidth: getFieldWidth(field), minWidth: '100px', width: '100%' }"
                  />
                </template>
                <template v-else-if="field.type === 'select'">
                  <el-select
                    :key="field.key + '_' + optionsVersion"
                    v-model="internalSearch[field.key]"
                    :multiple="!!field.multiple"
                    :placeholder="field.placeholder"
                    :clearable="field.clearable !== false"
                    size="default"
                    :style="{ maxWidth: getFieldWidth(field), minWidth: '100px', width: '100%' }"
                  >
                    <el-option
                      v-for="o in field.options || []"
                      :key="o.value"
                      :label="o.label"
                      :value="o.value"
                    />
                  </el-select>
                </template>
                <template v-else-if="field.type === 'remote-select'">
                  <el-select
                    v-model="internalSearch[field.key]"
                    :multiple="!!field.multiple"
                    :placeholder="field.placeholder"
                    :clearable="field.clearable !== false"
                    :filterable="field.filterable !== false"
                    remote
                    reserve-keyword
                    size="default"
                    :loading="!!remoteLoading[field.key]"
                    :style="{ maxWidth: getFieldWidth(field), minWidth: '100px', width: '100%' }"
                    :remote-method="(keyword) => runRemoteSearch(field, keyword)"
                    @visible-change="(visible) => handleRemoteVisible(field, visible)"
                    @clear="clearRemoteSearch(field)"
                    @change="ensureRemoteSelected(field)"
                  >
                    <el-option
                      v-for="o in getRemoteOptions(field)"
                      :key="o.value"
                      :label="o.label"
                      :value="o.value"
                    />
                  </el-select>
                </template>
                <template v-else-if="field.type === 'number'">
                  <el-input-number
                    v-model="internalSearch[field.key]"
                    size="default"
                    :style="{ maxWidth: getFieldWidth(field), minWidth: '100px', width: '100%' }"
                  />
                </template>
                <template v-else-if="field.type === 'date'">
                  <el-date-picker
                    v-model="internalSearch[field.key]"
                    type="date"
                    :placeholder="field.placeholder"
                    :clearable="field.clearable !== false"
                    size="default"
                    :style="{ maxWidth: getFieldWidth(field), minWidth: '100px', width: '100%' }"
                  />
                </template>
                <template v-else-if="field.type === 'daterange'">
                  <el-config-provider :locale="zhCn">
                    <el-date-picker
                      v-model="internalSearch[field.key]"
                      type="daterange"
                      start-placeholder="开始日期"
                      end-placeholder="结束日期"
                      format="YYYY-MM-DD HH:mm:ss"
                      value-format="YYYY-MM-DD HH:mm:ss"
                      :clearable="field.clearable !== false"
                      size="default"
                      :style="{ maxWidth: getFieldWidth(field), minWidth: '200px', width: '100%' }"
                    />
                  </el-config-provider>
                </template>
                <template v-else-if="field.type === 'tree'">
                  <el-tree-select
                    :key="field.key + '_' + optionsVersion"
                    v-model="internalSearch[field.key]"
                    :data="field.options || []"
                    node-key="value"
                    :props="{ label: 'label', value: 'value', children: 'children' }"
                    :multiple="!!field.multiple"
                    filterable
                    :default-expand-all="true"
                    :highlight-current="true"
                    :expand-on-click-node="false"
                    :check-strictly="true"
                    :placeholder="field.placeholder"
                    :clearable="field.clearable !== false"
                    size="default"
                    :style="{ maxWidth: getFieldWidth(field), minWidth: '100px', width: '100%' }"
                  />
                </template>
                <!-- 将按钮放在最后一个控件之后 -->
                <template v-if="idx === simpleFields.length - 1">
                  <div class="search-actions-inline">
                    <el-button type="primary" :icon="Search" size="default" :loading="loading" @click="onSearch">
                      搜索
                    </el-button>
                    <el-button :icon="RefreshLeft" size="default" @click="onReset">
                      重置
                    </el-button>
                    <el-button 
                      v-if="hasAdvancedFields"
                      :icon="isAdvancedSearchExpanded ? ArrowUp : ArrowDown" 
                      size="default"
                      @click="toggleAdvancedSearch">
                      {{ isAdvancedSearchExpanded ? '收起' : '高级' }}
                    </el-button>
                  </div>
                </template>
              </div>
            </template>
          </div>
        </div>

        <div class="dt-toolbar-right">
          <slot name="toolbar-right">
            <el-tooltip content="刷新数据" placement="top">
              <el-button 
                :icon="Refresh" 
                circle 
                size="small"
                @click="onSearch()" />
            </el-tooltip>
            <el-tooltip v-if="importConfig && (typeof importConfig === 'boolean' ? importConfig : importConfig.enabled !== false)" content="导入数据" placement="top">
              <el-button 
                :icon="Upload" 
                circle 
                size="small"
                @click="handleImport" />
            </el-tooltip>
            <el-tooltip v-if="showExport" content="导出数据" placement="top">
              <el-button 
                :icon="Download" 
                circle 
                size="small"
                @click="exportCurrent" />
            </el-tooltip>
            <el-tooltip v-if="printConfig && (typeof printConfig === 'boolean' ? printConfig : printConfig.enabled !== false)" content="打印数据" placement="top">
              <el-button 
                :icon="Printer" 
                circle 
                size="small"
                @click="handlePrintClick" />
            </el-tooltip>
            <ColumnSettings 
              v-if="showColumnSettings" 
              :columns="columns" 
              v-model:visibleKeys="visibleColumnKeys" />
          </slot>
        </div>
      </div>

      <!-- Advanced Search (Expanded Downwards) -->
      <transition name="expand-down">
        <div v-if="isAdvancedSearchExpanded && hasAdvancedFields" class="dt-advanced-search">
          <div class="advanced-search-header">
            <span class="advanced-search-title">
              <i class="bi bi-funnel"></i>
              高级筛选
            </span>
            <el-button 
              :icon="ArrowUp" 
              link 
              size="small"
              @click="toggleAdvancedSearch">
              收起
            </el-button>
          </div>
          <el-form :model="internalSearch" label-width="auto" size="default">
            <el-row :gutter="16">
              <template v-for="field in advancedFields" :key="field.key">
                <el-col :span="field.span || 8" :xs="24" :sm="12" :md="8" :lg="field.span || 8">
                  <el-form-item :label="field.label" class="advanced-search-item">
                    <template v-if="field.type === 'input'">
                      <el-input
                        v-model="internalSearch[field.key]"
                        :placeholder="field.placeholder"
                        :clearable="field.clearable !== false"
                        size="default"
                        style="width: 100%"
                      />
                    </template>
                    <template v-else-if="field.type === 'select'">
                      <el-select
                        :key="field.key + '_' + optionsVersion"
                        v-model="internalSearch[field.key]"
                        :multiple="!!field.multiple"
                        :placeholder="field.placeholder"
                        :clearable="field.clearable !== false"
                        size="default"
                        style="width: 100%"
                      >
                        <el-option
                          v-for="o in field.options || []"
                          :key="o.value"
                          :label="o.label"
                          :value="o.value"
                        />
                      </el-select>
                    </template>
                    <template v-else-if="field.type === 'remote-select'">
                      <el-select
                        v-model="internalSearch[field.key]"
                        :multiple="!!field.multiple"
                        :placeholder="field.placeholder"
                        :clearable="field.clearable !== false"
                        :filterable="field.filterable !== false"
                        remote
                        reserve-keyword
                        size="default"
                        style="width: 100%"
                        :loading="!!remoteLoading[field.key]"
                        :remote-method="(keyword) => runRemoteSearch(field, keyword)"
                        @visible-change="(visible) => handleRemoteVisible(field, visible)"
                        @clear="clearRemoteSearch(field)"
                        @change="ensureRemoteSelected(field)"
                      >
                        <el-option
                          v-for="o in getRemoteOptions(field)"
                          :key="o.value"
                          :label="o.label"
                          :value="o.value"
                        />
                      </el-select>
                    </template>
                    <template v-else-if="field.type === 'number'">
                      <el-input-number
                        v-model="internalSearch[field.key]"
                        size="default"
                        style="width: 100%"
                      />
                    </template>
                    <template v-else-if="field.type === 'date'">
                      <el-date-picker
                        v-model="internalSearch[field.key]"
                        type="date"
                        :placeholder="field.placeholder"
                        :clearable="field.clearable !== false"
                        size="default"
                        style="width: 100%"
                      />
                    </template>
                    <template v-else-if="field.type === 'daterange'">
                      <el-config-provider :locale="zhCn">
                        <el-date-picker
                          v-model="internalSearch[field.key]"
                          type="daterange"
                          start-placeholder="开始日期"
                          end-placeholder="结束日期"
                          format="YYYY-MM-DD HH:mm:ss"
                          value-format="YYYY-MM-DD HH:mm:ss"
                          :clearable="field.clearable !== false"
                          size="default"
                          style="width: 100%"
                        />
                      </el-config-provider>
                    </template>
                    <template v-else-if="field.type === 'tree'">
                      <el-tree-select
                        :key="field.key + '_' + optionsVersion"
                        v-model="internalSearch[field.key]"
                        :data="field.options || []"
                        node-key="value"
                        :props="{ label: 'label', value: 'value', children: 'children' }"
                        :multiple="!!field.multiple"
                        filterable
                        :default-expand-all="true"
                        :highlight-current="true"
                        :expand-on-click-node="false"
                        :check-strictly="true"
                        :placeholder="field.placeholder"
                        :clearable="field.clearable !== false"
                        size="default"
                        style="width: 100%"
                      />
                    </template>
                  </el-form-item>
                </el-col>
              </template>
              <slot name="search-extra" />
            </el-row>
          </el-form>
        </div>
      </transition>

      <!-- Batch Actions (if any) -->
      <transition name="fade-slide">
        <div v-if="batchActions?.length && selection.length" class="dt-batch-actions-bar">
          <div class="batch-actions-wrapper">
            <span class="selection-badge">已选 {{ selection.length }} 项</span>
            <el-button 
              v-for="action in batchActions" 
              :key="action.label"
              :type="action.type || 'primary'" 
              :icon="action.icon"
              size="small"
              @click="emitBatch(action.label)">
              {{ action.label }}
            </el-button>
          </div>
        </div>
      </transition>


      <!-- Table -->
      <div class="dt-table-wrapper">
        <component :is="tableComponent"
                   ref="tableRef"
                   v-loading="loading"
                   class="dt-table"
                   :data="data"
                   :height="tableHeight"
                   :row-key="rowKey"
                   :tree-props="treeProps"
                   :default-expand-all="defaultExpandAll"
                   :lazy="isLazyLoad"
                   :load="loadTreeNode"
                   :row-class-name="rowClassName"
                   stripe
                   @selection-change="onSelectionChange"
                   @row-click="(row: any)=>$emit('row-click', row)"
                   @sort-change="onSortChange">
          <el-table-column v-if="showSelection" type="selection" width="48" fixed="left" />
          <el-table-column v-if="showIndex" type="index" width="60" label="#" align="center" fixed="left" />

          <template v-for="col in visibleColumns" :key="col.prop">
            <el-table-column
              :prop="col.prop"
              :label="col.label"
              :width="col.width"
              :min-width="col.minWidth"
              :align="col.align"
              :sortable="col.sortable ? (col.sortable === true ? 'custom' : col.sortable) : false"
              show-overflow-tooltip>
              <template #default="scope">
                <slot :name="`column-${col.prop}`" v-bind="scope">
                  <component :is="cellRenderer(col, scope)" />
                </slot>
              </template>
            </el-table-column>
          </template>

          <el-table-column
            v-if="hasActions"
            :width="actionColumnWidth"
            label="操作"
            fixed="right"
            align="center"
            class-name="dt-action-column"
            label-class-name="dt-action-column-header"
          >
            <template #default="{ row, $index }">
              <div class="dt-actions-wrapper">
                <slot name="actions" :row="row" :$index="$index">
                  <TableActions :actions="actions" :row="row" :permission-checker="props.permissionChecker as any" @action="(a: any)=>$emit('action-click', a, row)" />
                </slot>
              </div>
            </template>
          </el-table-column>

          <template #empty>
            <slot name="empty">
              <div class="dt-empty">
                <svg class="empty-icon" viewBox="0 0 64 41" xmlns="http://www.w3.org/2000/svg">
                  <g transform="translate(0 1)" fill="none" fill-rule="evenodd">
                    <ellipse fill="#f5f5f5" cx="32" cy="33" rx="32" ry="7"/>
                    <g fill-rule="nonzero" stroke="#d9d9d9">
                      <path d="M55 12.76L44.854 1.258C44.367.474 43.656 0 42.907 0H21.093c-.749 0-1.46.474-1.947 1.257L9 12.761V22h46v-9.24z"/>
                      <path d="M41.613 15.931c0-1.605.994-2.93 2.227-2.931H55v18.137C55 33.26 53.68 35 52.05 35h-40.1C10.32 35 9 33.259 9 31.137V13h11.16c1.233 0 2.227 1.323 2.227 2.928v.022c0 1.605 1.005 2.901 2.237 2.901h14.752c1.232 0 2.237-1.308 2.237-2.913v-.007z" fill="#fafafa"/>
                    </g>
                  </g>
                </svg>
                <p class="empty-text">暂无数据</p>
              </div>
            </slot>
          </template>
        </component>
      </div>

      <!-- Pagination -->
      <div v-if="pagination" class="dt-pagination" :class="{ 'dt-pagination-small': paginationSize === 'small' }">
        <el-config-provider :locale="zhCn">
          <el-pagination
            v-model:current-page="internalPage"
            v-model:page-size="internalPageSize"
            :page-sizes="pageSizes"
            :total="Number(pagination.total) || 0"
            :background="true"
            :small="paginationSize === 'small'"
            prev-text="上一页"
            next-text="下一页"
            :layout="paginationLayout"
            :hide-on-single-page="false"
            @current-change="(p: any)=>$emit('page-change', p)"
            @size-change="(s: any)=>$emit('size-change', s)"
          />
        </el-config-provider>
      </div>
    </el-card>

    <!-- Import Preview Dialog -->
    <el-dialog
      v-model="importDialogVisible"
      title="导入预览"
      width="80%"
      :close-on-click-modal="false">
      <div v-loading="importLoading">
        <el-alert
          type="info"
          :closable="false"
          style="margin-bottom: 16px;">
          <template #title>
            共 {{ importPreviewData.length }} 条数据，请确认后导入
          </template>
        </el-alert>
        <el-table
          :data="importPreviewData.slice(0, 100)"
          max-height="400"
          border
          stripe>
          <el-table-column
            v-for="col in columns"
            :key="col.prop"
            :prop="col.prop"
            :label="col.label"
            show-overflow-tooltip />
        </el-table>
        <div v-if="importPreviewData.length > 100" style="text-align: center; padding: 12px; color: #909399;">
          仅显示前 100 条数据预览
        </div>
      </div>
      <template #footer>
        <el-button @click="cancelImport">取消</el-button>
        <el-button @click="downloadTemplate" v-if="importConfig && typeof importConfig !== 'boolean' && importConfig.templateUrl">
          下载模板
        </el-button>
        <el-button type="primary" @click="confirmImport" :loading="importLoading">
          确认导入
        </el-button>
      </template>
    </el-dialog>

    <!-- Print Preview Dialog -->
    <el-dialog
      v-model="printDialogVisible"
      title="打印预览"
      width="80%"
      :close-on-click-modal="false">
      <div v-loading="printLoading">
        <el-alert
          type="info"
          :closable="false"
          style="margin-bottom: 16px;">
          <template #title>
            共 {{ printPreviewData.length }} 条数据，点击确认打印将打开打印预览窗口
          </template>
        </el-alert>
        <el-table
          :data="printPreviewData.slice(0, 100)"
          max-height="400"
          border
          stripe>
          <el-table-column
            v-for="col in visibleColumns"
            :key="col.prop"
            :prop="col.prop"
            :label="col.label"
            show-overflow-tooltip />
        </el-table>
        <div v-if="printPreviewData.length > 100" style="text-align: center; padding: 12px; color: #909399;">
          仅显示前 100 条数据预览
        </div>
      </div>
      <template #footer>
        <el-button @click="cancelPrint">取消</el-button>
        <el-button type="primary" @click="confirmPrint" :loading="printLoading">
          确认打印
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch, onMounted, h, useSlots } from 'vue'
// @ts-ignore locale import for Chinese
import zhCn from 'element-plus/es/locale/lang/zh-cn'
import { Refresh, Download, Upload, Printer, Search, RefreshLeft, ArrowUp, ArrowDown } from '@element-plus/icons-vue'
// @ts-ignore - Vue SFC type shim is provided globally
import TableActions from './components/TableActions.vue'
// @ts-ignore - Vue SFC type shim is provided globally
import ColumnSettings from './components/ColumnSettings.vue'
// @ts-ignore - local type resolution in linter context
import type { DataTableProps, ColumnConfig, SearchFieldConfig } from './types.ts'
import { useImport } from './hooks/useImport'
import { usePrint } from './hooks/usePrint'

const props = withDefaults(defineProps<DataTableProps>(), {
  showColumnSettings: true,
  showExport: true,
  printConfig: true,
  paginationSize: 'default',
})
const emit = defineEmits<{
  (e: 'search', params: Record<string, any>): void
  (e: 'page-change', page: number): void
  (e: 'size-change', size: number): void
  (e: 'sort-change', sort: { prop?: string; order?: string }): void
  (e: 'selection-change', selection: any[]): void
  (e: 'row-click', row: any): void
  (e: 'action-click', action: string, row: any): void
  (e: 'batch-action', action: string, selection: any[]): void
}>()

const tableRef = ref()
const selection = ref<any[]>([])
const internalSearch = ref<Record<string, any>>({})
const internalPage = computed({ get:()=> props.pagination?.page || 1, set:(v:number)=>emit('page-change', v) })
const internalPageSize = computed({ get:()=> props.pagination?.pageSize || 20, set:(v:number)=>emit('size-change', v) })
const pageSizes = computed(()=> props.pagination?.pageSizes || [10,20,50,100])
const paginationSize = computed(()=> props.paginationSize || 'default')
const paginationLayout = computed(()=> {
  if (paginationSize.value === 'small') {
    return 'total, prev, pager, next'
  }
  return 'total, sizes, prev, pager, next, jumper'
})

const slots = useSlots()
const showSelection = computed(()=> props.showSelection !== false)
const showIndex = computed(()=> !!props.showIndex)
const hasActions = computed(()=> (Array.isArray(props.actions) && props.actions.length > 0) || !!slots.actions)
const maxActionsPerRow = 5
const actionButtonBaseWidth = 64
const actionColumnBasePadding = 56
// 操作列默认固定在最右侧，并至少保证 2-5 个按钮可单行展示，超过 5 个再自动换到下一行。
const actionColumnWidth = computed(() => {
  const actionCount = Math.max(1, Math.min(maxActionsPerRow, props.actions?.length || (slots.actions ? maxActionsPerRow : 1)))
  const recommendedWidth = actionCount * actionButtonBaseWidth + actionColumnBasePadding
  const customWidth = Number.parseInt(String((props as any).actionColumnWidth ?? ''), 10)
  if (Number.isFinite(customWidth)) {
    return Math.max(customWidth, recommendedWidth)
  }
  return recommendedWidth
})

const visibleColumnKeys = ref<string[]>([])
const visibleColumns = computed<ColumnConfig[]>(() => {
  const cols: ColumnConfig[] = (props.columns || []) as ColumnConfig[]
  if (!visibleColumnKeys.value.length) return cols
  return cols.filter((c: ColumnConfig) => visibleColumnKeys.value.includes(c.prop))
})

const tableComponent = computed(()=> 'el-table')
const tableHeight = computed(()=> undefined)
const rowKey = computed(()=> (props.rowKey || 'id') as string)

// 搜索/排序综合查询参数
const sortState = ref<{ prop?: string; order?: string }>({})

function buildQueryPayload() {
  // 过滤掉空值/空字符串/空数组
  const filters: Record<string, any> = {}
  Object.entries(internalSearch.value || {}).forEach(([k, v]) => {
    if (v === undefined || v === null) return
    if (typeof v === 'string' && v.trim() === '') return
    if (Array.isArray(v) && v.length === 0) return
    filters[k] = v
  })

  return {
    filters,
    page: internalPage.value,
    pageSize: internalPageSize.value,
    sortProp: sortState.value.prop,
    sortOrder: sortState.value.order,
  }
}

// ==================== 搜索字段处理 ====================
const simpleFields = computed(() => props.searchConfig?.filter(f => f.simple === true) || [])
const advancedFields = computed(() => props.searchConfig?.filter(f => f.simple !== true) || [])
const hasAdvancedFields = computed(() => advancedFields.value.length > 0)

// When async options update, force select/tree-select to re-render once
const optionsVersion = ref(0)
const remoteOptions = ref<Record<string, any[]>>({})
const remoteLoading = ref<Record<string, boolean>>({})

// Helper: 获取搜索字段宽度样式
function getFieldWidth(field: SearchFieldConfig): string {
  if (field.width) {
    return typeof field.width === 'number' ? `${field.width}px` : String(field.width)
  }
  // 默认宽度
  if (field.type === 'daterange') return '280px'
  if (field.type === 'number') return '140px'
  return '180px'
}

function getRemoteOptions(field: SearchFieldConfig) {
  return remoteOptions.value[field.key] || field.options || []
}

async function runRemoteSearch(field: SearchFieldConfig, keyword = '') {
  if (!field.remoteMethod) {
    return
  }
  remoteLoading.value[field.key] = true
  try {
    const options = await field.remoteMethod(keyword, internalSearch.value || {})
    remoteOptions.value[field.key] = Array.isArray(options) ? options : field.options || []
  } catch {
    remoteOptions.value[field.key] = field.options || []
  } finally {
    remoteLoading.value[field.key] = false
  }
}

function handleRemoteVisible(field: SearchFieldConfig, visible: boolean) {
  if (!visible || getRemoteOptions(field).length > 0) {
    return
  }
  void runRemoteSearch(field, '')
}

function clearRemoteSearch(field: SearchFieldConfig) {
  if (!remoteOptions.value[field.key]?.length && field.options?.length) {
    remoteOptions.value[field.key] = field.options
  }
}

function ensureRemoteSelected(field: SearchFieldConfig) {
  const currentValue = internalSearch.value?.[field.key]
  if (currentValue == null || currentValue === '') {
    return
  }
  const exists = getRemoteOptions(field).some(option => option.value === currentValue)
  if (exists) {
    return
  }
  remoteOptions.value[field.key] = [
    ...getRemoteOptions(field),
    {
      label: String(currentValue),
      value: currentValue
    }
  ]
}

watch(() => props.searchConfig, () => {
  optionsVersion.value++
  remoteOptions.value = {}
  remoteLoading.value = {}
}, { deep: true })

const storageKey = 'dt-advanced-search-expanded'
const isAdvancedSearchExpanded = ref(!!props.defaultExpandSearch)

onMounted(() => {
  // 默认按 props 控制初始状态（默认为收起）。不再读取历史缓存以避免首次进入即展开。
  localStorage.setItem(storageKey, String(isAdvancedSearchExpanded.value))
})

watch(isAdvancedSearchExpanded, (val) => {
  localStorage.setItem(storageKey, String(val))
})

function toggleAdvancedSearch() {
  isAdvancedSearchExpanded.value = !isAdvancedSearchExpanded.value
}

// renderField removed: using native template controls for better interactivity

// ==================== 树形表格配置 ====================
const isTreeTable = computed(() => !!props.treeConfig)

const treeProps = computed(() => {
  if (!props.treeConfig) return undefined
  if (typeof props.treeConfig === 'boolean') {
    return { children: 'children', hasChildren: 'hasChildren' }
  }
  return {
    children: props.treeConfig.children || 'children',
    hasChildren: props.treeConfig.hasChildren || 'hasChildren'
  }
})

const defaultExpandAll = computed(() => {
  if (!props.treeConfig || typeof props.treeConfig === 'boolean') return false
  return props.treeConfig.expandAll || false
})

const isLazyLoad = computed(() => {
  if (!props.treeConfig || typeof props.treeConfig === 'boolean') return false
  return props.treeConfig.lazy || false
})

function loadTreeNode(row: any, treeNode: any, resolve: (data: any[]) => void) {
  if (props.treeConfig && typeof props.treeConfig !== 'boolean' && props.treeConfig.load) {
    props.treeConfig.load(row, treeNode, resolve)
  } else {
    resolve([])
  }
}

function onSelectionChange(val: any[]) {
  selection.value = val
  emit('selection-change', val)
}

function onSortChange(e: any) {
  sortState.value = { prop: e.prop, order: e.order }
  emit('sort-change', { prop: e.prop, order: e.order })
  // 触发综合查询
  emit('search', buildQueryPayload())
}

function onSearch(params?: Record<string, any>) {
  if (params && typeof params === 'object') {
    internalSearch.value = { ...internalSearch.value, ...params }
  }
  emit('search', buildQueryPayload())
}

function onReset() {
  internalSearch.value = {}
  emit('page-change', 1)
  emit('search', buildQueryPayload())
}

function exportCurrent() {
  try {
    // Simple CSV export of current page
    const rows = props.data || []
    const cols = visibleColumns.value
    const header = cols.map((c: ColumnConfig) => `"${c.label}"`).join(',')
    const body = rows.map((r: any) => cols.map((c: ColumnConfig) => `"${(r as any)[c.prop] ?? ''}"`).join(',')).join('\n')
    const csv = header + '\n' + body
    const bom = '\ufeff'
    const blob = new Blob([bom + csv], { type: 'text/csv;charset=utf-8;' })
    const link = document.createElement('a')
    link.href = URL.createObjectURL(blob)
    link.download = 'export.csv'
    link.click()
    URL.revokeObjectURL(link.href)
  } catch {}
}

// ==================== Excel 导入和打印功能 ====================
const {
  importDialogVisible,
  importLoading,
  importPreviewData,
  importFileList,
  handleFileChange,
  confirmImport,
  cancelImport,
  downloadTemplate
} = useImport(props.importConfig)

const {
  printDialogVisible,
  printLoading,
  printPreviewData,
  handlePrint,
  confirmPrint,
  cancelPrint
} = usePrint(props.printConfig, props.columns, props.data)

function handleImport() {
  // 触发文件选择
  const fileInput = document.createElement('input')
  fileInput.type = 'file'
  fileInput.accept = '.xlsx,.xls,.csv'
  fileInput.onchange = (e: any) => {
    const file = e.target?.files?.[0]
    if (file) {
      handleFileChange({ raw: file })
    }
  }
  fileInput.click()
}

function handlePrintClick() {
  handlePrint(props.data)
}

function emitBatch(action: string) {
  emit('batch-action', action, selection.value)
}

onMounted(() => {
  // initialize visible columns from cache if provided
  if (props.cacheKey && props.showColumnSettings) {
    const raw = localStorage.getItem(props.cacheKey + ':columns')
    if (raw) {
      try { 
        visibleColumnKeys.value = JSON.parse(raw) 
      } catch {
        // ignore parse errors
      }
    }
  }
})

watch(visibleColumnKeys, (val) => {
  if (props.cacheKey && props.showColumnSettings) {
    localStorage.setItem(props.cacheKey + ':columns', JSON.stringify(val))
  }
}, { deep: true })

function cellRenderer(col: ColumnConfig, scope: any) {
  const value = scope.row?.[col.prop]
  if (col.slot) return { render: () => null }
  if (col.formatter) {
    try { return { render: () => col.formatter!(scope.row, col, value, scope.$index) } } catch {}
  }
  // basic types
  if (col.type === 'tag') {
    return {
      render() { return h('span', { class: 'dt-tag' }, String(value ?? '')) }
    }
  }
  return {
    render() { return h('span', String(value ?? '')) }
  }
}

defineExpose({ tableRef, selection })
</script>

<style scoped>
/* ==================== 主容器 ==================== */
.gk-datatable {
  display: flex;
  flex-direction: column;
  animation: fadeIn 0.3s ease-out;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* ==================== 卡片容器 ==================== */
.dt-card {
  border-radius: 8px;
  border: 1px solid #e5e7eb;
  overflow: visible;
  transition: all 0.3s ease;
}

/* 移除 el-card 默认的 body padding */
.dt-card :deep(.el-card__body) {
  padding: 0;
}

.dt-card.compact-mode {
  border-radius: 6px;
}

.dt-card:hover {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.06);
}

.admin-dark .dt-card {
  background: #1f2937;
  border-color: #374151;
}

.admin-dark .dt-card:hover {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
}

/* ==================== 页面标题（内嵌在卡片内） ==================== */
.dt-page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid #e5e7eb;
}

.compact-mode .dt-page-header {
  padding: 12px 16px;
}

.admin-dark .dt-page-header {
  border-bottom-color: #374151;
}

.dt-page-title h2 {
  font-size: 18px;
  font-weight: 600;
  color: #1f2937;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
  line-height: 1.4;
}

.dt-page-title h2::before {
  content: '';
  width: 3px;
  height: 18px;
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  border-radius: 2px;
}

.admin-dark .dt-page-title h2 {
  color: #f9fafb;
}

.admin-dark .dt-page-title h2::before {
  background: linear-gradient(135deg, #60a5fa 0%, #3b82f6 100%);
}

.dt-page-title p {
  font-size: 13px;
  color: #6b7280;
  margin: 4px 0 0 11px;
}

.admin-dark .dt-page-title p {
  color: #9ca3af;
}

.dt-page-actions {
  display: flex;
  gap: 12px;
  align-items: center;
}

/* ==================== 高级搜索区（向下展开） ==================== */
.dt-advanced-search {
  background: #f9fafb;
  border-top: 1px solid #e5e7eb;
  border-bottom: 1px solid #e5e7eb;
  padding: 16px 20px;
}

.compact-mode .dt-advanced-search {
  padding: 12px 16px;
}

.admin-dark .dt-advanced-search {
  background: #111827;
  border-top-color: #374151;
  border-bottom-color: #374151;
}

.advanced-search-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
  padding-bottom: 12px;
  border-bottom: 1px solid #e5e7eb;
}

.admin-dark .advanced-search-header {
  border-bottom-color: #374151;
}

.advanced-search-title {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  font-size: 15px;
  font-weight: 600;
  color: #374151;
}

.admin-dark .advanced-search-title {
  color: #e5e7eb;
}

.advanced-search-title i {
  font-size: 16px;
  color: #3b82f6;
}

.admin-dark .advanced-search-title i {
  color: #60a5fa;
}

.advanced-search-item {
  margin-bottom: 16px;
}

.advanced-search-item :deep(.el-form-item__label) {
  font-size: 14px;
  font-weight: 600;
  color: #374151;
}

.admin-dark .advanced-search-item :deep(.el-form-item__label) {
  color: #e5e7eb;
}

/* 使高级搜索的标题与控件垂直对齐 */
.dt-advanced-search :deep(.el-form-item) {
  align-items: center;
}

.dt-advanced-search :deep(label),
.dt-inline-search :deep(label) {
  margin-bottom: 0 !important; /* 覆盖 _reboot.scss 的全局 label margin-bottom */
}

.dt-advanced-search :deep(.el-form-item__label-wrap),
.dt-advanced-search :deep(.el-form-item__label) {
  display: inline-flex;
  align-items: center;
  height: 36px;
  line-height: 36px;
  padding: 0;
}

.dt-advanced-search :deep(.el-form-item__content) {
  display: flex;
  align-items: center;
  min-height: 36px;
}

/* 统一控件最小高度，保证与标题对齐 */
.dt-advanced-search :deep(.el-input__wrapper),
.dt-advanced-search :deep(.el-select .el-input__wrapper),
.dt-advanced-search :deep(.el-date-editor .el-input__wrapper),
.dt-advanced-search :deep(.el-tree-select .el-input__wrapper),
.dt-advanced-search :deep(.el-input-number .el-input__wrapper) {
  min-height: 36px;
}

/* 高级搜索输入框美化 */
.dt-advanced-search :deep(.el-input__wrapper) {
  border-radius: 6px;
  border: 1px solid #d1d5db;
  background: #fff;
  transition: all 0.2s ease;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
}

.dt-advanced-search :deep(.el-input__wrapper:hover) {
  border-color: #3b82f6;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

.dt-advanced-search :deep(.el-input__wrapper.is-focus) {
  border-color: #3b82f6;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.15);
  background: #eff6ff;
}

.admin-dark .dt-advanced-search :deep(.el-input__wrapper) {
  background: #1f2937;
  border-color: #4b5563;
}

.admin-dark .dt-advanced-search :deep(.el-input__wrapper:hover) {
  border-color: #60a5fa;
  box-shadow: 0 0 0 3px rgba(96, 165, 250, 0.15);
}

.admin-dark .dt-advanced-search :deep(.el-input__wrapper.is-focus) {
  border-color: #60a5fa;
  box-shadow: 0 0 0 3px rgba(96, 165, 250, 0.2);
  background: #1e3a5f;
}

.dt-advanced-search :deep(.el-input__inner) {
  color: #1f2937;
  font-size: 14px;
}

.admin-dark .dt-advanced-search :deep(.el-input__inner) {
  color: #f9fafb;
}

.dt-advanced-search :deep(.el-select .el-input__wrapper),
.dt-advanced-search :deep(.el-date-editor .el-input__wrapper),
.dt-advanced-search :deep(.el-tree-select .el-input__wrapper),
.dt-advanced-search :deep(.el-input-number .el-input__wrapper) {
  border-radius: 6px;
}

/* 向下展开动画 */
.expand-down-enter-active,
.expand-down-leave-active {
  transition: all 0.35s cubic-bezier(0.4, 0, 0.2, 1);
  overflow: hidden;
}

.expand-down-enter-from,
.expand-down-leave-to {
  max-height: 0;
  opacity: 0;
  padding-top: 0;
  padding-bottom: 0;
}

.expand-down-enter-to,
.expand-down-leave-from {
  max-height: 800px;
  opacity: 1;
}

/* 批量操作栏 */
.dt-batch-actions-bar {
  padding: 12px 20px;
  background: linear-gradient(135deg, #eff6ff 0%, #dbeafe 100%);
  border-bottom: 1px solid #bfdbfe;
}

.admin-dark .dt-batch-actions-bar {
  background: linear-gradient(135deg, #1e3a5f 0%, #1e40af 100%);
  border-bottom-color: #3b82f6;
}

/* ==================== 工具栏 ==================== */
.dt-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: #fff;
  border-bottom: 1px solid #e5e7eb;
  min-height: 56px;
  flex-wrap: wrap;
  gap: 12px;
}

.compact-mode .dt-toolbar {
  padding: 10px 14px;
  min-height: 50px;
}

.admin-dark .dt-toolbar {
  background: #1f2937;
  border-bottom-color: #283344;
}

.dt-toolbar-left,
.dt-toolbar-right {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.dt-toolbar-left {
  flex: 1;
  min-width: 0;
}

/* ==================== 内联搜索 ==================== */
.dt-inline-search {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
  flex: 1;
}

.search-field-inline {
  display: flex;
  align-items: center;
  gap: 8px;
  background: #fff;
  padding: 4px 10px;
  height: 36px;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
  flex: 1 1 auto;
  min-width: 0;
  transition: all 0.2s ease;
}

.search-field-inline:hover {
  border-color: #3b82f6;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

.search-field-inline:focus-within {
  border-color: #3b82f6;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.15);
  background: #eff6ff;
}

.admin-dark .search-field-inline {
  background: #1f2937;
  border-color: #374151;
}

.admin-dark .search-field-inline:hover {
  border-color: #60a5fa;
  box-shadow: 0 0 0 3px rgba(96, 165, 250, 0.15);
}

.admin-dark .search-field-inline:focus-within {
  border-color: #60a5fa;
  box-shadow: 0 0 0 3px rgba(96, 165, 250, 0.2);
  background: #1e3a5f;
}


.search-label {
  display: inline-flex;
  align-items: center;
  height: 28px;
  line-height: 28px;
  font-size: 14px;
  font-weight: 600;
  color: #6b7280;
  white-space: nowrap;
  margin-right: 6px;
}

.admin-dark .search-label {
  color: #9ca3af;
}

.search-actions-inline {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.search-actions-inline :deep(.el-button) {
  height: 32px;
  display: inline-flex;
  align-items: center;
}

.dt-inline-search :deep(.el-input),
.dt-inline-search :deep(.el-select),
.dt-inline-search :deep(.el-date-editor),
.dt-inline-search :deep(.el-input-number),
.dt-inline-search :deep(.el-tree-select) {
  flex-shrink: 1;
  min-width: 0;
}

/* 统一控件高度以便与标签垂直对齐 */
.dt-inline-search :deep(.el-input__wrapper),
.dt-inline-search :deep(.el-select .el-input__wrapper),
.dt-inline-search :deep(.el-date-editor .el-input__wrapper),
.dt-inline-search :deep(.el-input-number .el-input__wrapper),
.dt-inline-search :deep(.el-tree-select .el-input__wrapper) {
  min-height: 28px;
}

/* 美化内联搜索控件 */
.search-field-inline :deep(.el-input__wrapper) {
  border: none !important;
  box-shadow: none !important;
  background: transparent !important;
  padding: 0 !important;
}

.search-field-inline :deep(.el-input__inner) {
  font-size: 14px;
  color: #1f2937;
  background: transparent;
}

.admin-dark .search-field-inline :deep(.el-input__inner) {
  color: #f9fafb;
}

.search-field-inline :deep(.el-select .el-input__wrapper) {
  border: none !important;
  box-shadow: none !important;
  background: transparent !important;
}

.search-field-inline :deep(.el-select .el-input__inner) {
  font-weight: 500;
}

.search-field-inline :deep(.el-date-editor .el-input__wrapper) {
  border: none !important;
  box-shadow: none !important;
  background: transparent !important;
}

.search-field-inline :deep(.el-tree-select .el-input__wrapper) {
  border: none !important;
  box-shadow: none !important;
  background: transparent !important;
}

.search-field-inline :deep(.el-input-number .el-input__wrapper) {
  border: none !important;
  box-shadow: none !important;
  background: transparent !important;
}

.dt-inline-search :deep(.el-input__wrapper),
.dt-inline-search :deep(.el-select .el-input__wrapper),
.dt-inline-search :deep(.el-date-editor .el-input__wrapper),
.dt-inline-search :deep(.el-input-number .el-input__wrapper),
.dt-inline-search :deep(.el-tree-select .el-input__wrapper) {
  border-radius: 6px;
  border-color: #d1d5db;
  transition: all 0.2s ease;
  box-shadow: none;
}

.admin-dark .dt-inline-search :deep(.el-input__wrapper),
.admin-dark .dt-inline-search :deep(.el-select .el-input__wrapper),
.admin-dark .dt-inline-search :deep(.el-date-editor .el-input__wrapper),
.admin-dark .dt-inline-search :deep(.el-input-number .el-input__wrapper),
.admin-dark .dt-inline-search :deep(.el-tree-select .el-input__wrapper) {
  background: #374151;
  border-color: #4b5563;
}

.dt-inline-search :deep(.el-input__wrapper:hover),
.dt-inline-search :deep(.el-select .el-input__wrapper:hover),
.dt-inline-search :deep(.el-date-editor .el-input__wrapper:hover),
.dt-inline-search :deep(.el-input-number .el-input__wrapper:hover),
.dt-inline-search :deep(.el-tree-select .el-input__wrapper:hover) {
  border-color: #3b82f6;
}

.dt-inline-search :deep(.el-input__wrapper.is-focus),
.dt-inline-search :deep(.el-select .el-input__wrapper.is-focus),
.dt-inline-search :deep(.el-date-editor .el-input__wrapper.is-focus),
.dt-inline-search :deep(.el-input-number .el-input__wrapper.is-focus),
.dt-inline-search :deep(.el-tree-select .el-input__wrapper.is-focus) {
  border-color: #3b82f6;
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.1);
}

.search-actions-inline :deep(.el-button) {
  border-radius: 6px;
  font-weight: 500;
  transition: all 0.2s ease;
}

.search-actions-inline :deep(.el-button--primary) {
  background: #3b82f6;
  border-color: #3b82f6;
}

.search-actions-inline :deep(.el-button--primary:hover) {
  background: #2563eb;
  border-color: #2563eb;
  transform: translateY(-1px);
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.3);
}

.search-actions-inline :deep(.el-button:not(.el-button--primary)) {
  border-color: #d1d5db;
  color: #6b7280;
}

.admin-dark .search-actions-inline :deep(.el-button:not(.el-button--primary)) {
  background: #374151;
  border-color: #4b5563;
  color: #9ca3af;
}

.search-actions-inline :deep(.el-button:not(.el-button--primary):hover) {
  color: #3b82f6;
  border-color: #3b82f6;
}

.admin-dark .search-actions-inline :deep(.el-button:not(.el-button--primary):hover) {
  background: #4b5563;
  border-color: #60a5fa;
  color: #60a5fa;
}

.dt-toolbar-left {
  flex: 1;
  min-width: 0;
}

/* 批量操作区域 */
.batch-actions-wrapper {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 16px;
  background: linear-gradient(135deg, #eff6ff 0%, #dbeafe 100%);
  border-radius: 8px;
  border: 1px solid #bfdbfe;
  animation: slideIn 0.3s ease-out;
}

.admin-dark .batch-actions-wrapper {
  background: linear-gradient(135deg, #1e3a5f 0%, #1e40af 100%);
  border-color: #3b82f6;
}

@keyframes slideIn {
  from {
    opacity: 0;
    transform: translateX(-20px);
  }
  to {
    opacity: 1;
    transform: translateX(0);
  }
}

.selection-badge {
  font-size: 14px;
  font-weight: 600;
  color: #1e40af;
  padding: 4px 12px;
  background: #ffffff;
  border-radius: 6px;
  white-space: nowrap;
}

.admin-dark .selection-badge {
  color: #93c5fd;
  background: #1e3a8a;
}

/* Fade slide 过渡 */
.fade-slide-enter-active,
.fade-slide-leave-active {
  transition: all 0.3s ease;
}

.fade-slide-enter-from {
  opacity: 0;
  transform: translateX(-20px);
}

.fade-slide-leave-to {
  opacity: 0;
  transform: translateX(20px);
}

/* ==================== 表格区域 ==================== */
.dt-table-wrapper {
  margin: 4px 0;
  position: relative;
  overflow: visible;
}

.compact-mode .dt-table-wrapper {
  margin: 2px 0;
}

.dt-table {
  width: 100%;
  font-size: 14px;
  --el-table-border-color: #f1f5f9;
  --el-table-row-hover-bg-color: #f8fafc;
}

.admin-dark .dt-table {
  --el-table-border-color: #283344;
  --el-table-row-hover-bg-color: #253245;
}

.dt-table :deep(.el-table__header-wrapper),
.dt-table :deep(.el-table__footer-wrapper) {
  overflow-x: hidden;
}

.dt-table :deep(.el-table__body-wrapper) {
  overflow-x: overlay;
  scrollbar-width: thin;
  scrollbar-color: #cbd5e1 transparent;
}

.admin-dark .dt-table :deep(.el-table__body-wrapper) {
  scrollbar-color: #4b5563 transparent;
}

.dt-table :deep(.el-table__body-wrapper)::-webkit-scrollbar {
  height: 6px;
}

.dt-table :deep(.el-table__body-wrapper)::-webkit-scrollbar-thumb {
  background: #cbd5e1;
  border-radius: 3px;
}

.dt-table :deep(.el-table__body-wrapper)::-webkit-scrollbar-thumb:hover {
  background: #94a3b8;
}

.dt-table :deep(.el-table__body-wrapper)::-webkit-scrollbar-track {
  background: transparent;
}

.admin-dark .dt-table :deep(.el-table__body-wrapper)::-webkit-scrollbar-thumb {
  background: #4b5563;
}

.admin-dark .dt-table :deep(.el-table__body-wrapper)::-webkit-scrollbar-thumb:hover {
  background: #6b7280;
}

/* 表格样式增强 */
.dt-table :deep(.el-table__header) {
  font-weight: 600;
}

.dt-table :deep(.el-table__header th) {
  background: #f8fafc !important;
  color: #475569;
  font-size: 13px;
  font-weight: 600;
  padding: 14px 0;
  border-bottom: 1px solid #e2e8f0;
  letter-spacing: 0.02em;
}

.admin-dark .dt-table :deep(.el-table__header th) {
  background: #1e2736 !important;
  color: #8b9bb5;
  font-size: 13px;
  font-weight: 600;
  letter-spacing: 0.02em;
  text-transform: none;
  border-bottom: 1px solid #283344;
}

.dt-table :deep(.el-table__body tr) {
  transition: background-color 0.2s ease;
}

.dt-table :deep(.el-table__body tr:hover) > td {
  background: #f8fafc !important;
}

.admin-dark .dt-table :deep(.el-table__body tr:hover) > td {
  background: #253245 !important;
  box-shadow: none;
}

.dt-table :deep(.el-table__body td) {
  padding: 13px 0;
  color: #1e293b;
  border-bottom: 1px solid #f1f5f9;
}

/* 表格单元格内容内边距优化 */
.dt-table :deep(.el-table__cell .cell) {
  padding-left: 16px;
  padding-right: 16px;
}

/* 第一列额外左边距，避免内容贴边 */
.dt-table :deep(.el-table__header th:first-child .cell),
.dt-table :deep(.el-table__body td:first-child .cell) {
  padding-left: 20px;
}

.dt-table :deep(.el-table__fixed-right) {
  box-shadow: -6px 0 16px rgba(15, 23, 42, 0.08);
}

.dt-table :deep(.el-table__fixed-right th),
.dt-table :deep(.el-table__fixed-right td),
.dt-table :deep(.el-table__fixed-right-patch) {
  background: #ffffff !important;
}

/* Element Plus 2.x sticky 固定列背景保持一致 */
.dt-table :deep(th.el-table-fixed-column--right),
.dt-table :deep(td.el-table-fixed-column--right) {
  background: #ffffff !important;
  z-index: 1;
}

.dt-table :deep(.el-table__header th.el-table-fixed-column--right) {
  background: #f8fafc !important;
}

.dt-table :deep(.el-table__body tr:hover > td.el-table-fixed-column--right) {
  background: var(--el-table-row-hover-bg-color, #f8fafc) !important;
}

.dt-table :deep(.el-table__body tr.el-table__row--striped td.el-table-fixed-column--right) {
  background: #fafbfd !important;
}

.admin-dark .dt-table :deep(th.el-table-fixed-column--right),
.admin-dark .dt-table :deep(td.el-table-fixed-column--right) {
  background: #1f2937 !important;
}

.admin-dark .dt-table :deep(.el-table__header th.el-table-fixed-column--right) {
  background: #1e2736 !important;
}

.admin-dark .dt-table :deep(.el-table__body tr:hover > td.el-table-fixed-column--right) {
  background: #253245 !important;
}

.admin-dark .dt-table :deep(.el-table__body tr.el-table__row--striped td.el-table-fixed-column--right) {
  background: #222d3b !important;
}

.admin-dark .dt-table :deep(.el-table__body td) {
  color: #c8ced8;
  border-bottom-color: #283344;
}

.admin-dark .dt-table :deep(.el-table__fixed-right) {
  box-shadow: -6px 0 16px rgba(15, 23, 42, 0.35);
}

.admin-dark .dt-table :deep(.el-table__fixed-right th),
.admin-dark .dt-table :deep(.el-table__fixed-right td),
.admin-dark .dt-table :deep(.el-table__fixed-right-patch) {
  background: #1f2937 !important;
}

/* 斑马纹 */
.dt-table :deep(.el-table__body tr.el-table__row--striped) {
  background: #fafbfc;
}

.admin-dark .dt-table :deep(.el-table__body tr.el-table__row--striped) {
  background: #222d3b;
}

.admin-dark .dt-table :deep(.el-table__body tr.el-table__row--striped:hover) {
  background: #253245 !important;
}

/* 选中行 */
.dt-table :deep(.el-table__body tr.current-row) {
  background: #eff6ff !important;
}

.admin-dark .dt-table :deep(.el-table__body tr.current-row) {
  background: #243856 !important;
}

/* 排序图标 */
.dt-table :deep(.el-table__column-filter-trigger),
.dt-table :deep(.caret-wrapper) {
  color: #9ca3af;
}

.dt-table :deep(.ascending),
.dt-table :deep(.descending) {
  color: #3b82f6;
}

/* ==================== 树形表格样式 ==================== */
/* 树形展开图标 */
.dt-table :deep(.el-table__expand-icon) {
  width: 20px;
  height: 20px;
  line-height: 20px;
  border-radius: 4px;
  color: #6b7280;
  transition: all 0.2s ease;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  margin-right: 8px;
}

.dt-table :deep(.el-table__expand-icon:hover) {
  background: #f3f4f6;
  color: #3b82f6;
}

.dt-table :deep(.el-table__expand-icon.el-table__expand-icon--expanded) {
  transform: rotate(90deg);
}

.admin-dark .dt-table :deep(.el-table__expand-icon) {
  color: #9ca3af;
}

.admin-dark .dt-table :deep(.el-table__expand-icon:hover) {
  background: #374151;
  color: #60a5fa;
}

/* 树形缩进 */
.dt-table :deep(.el-table__placeholder) {
  display: inline-block;
  width: 18px;
  height: 18px;
}

/* 树形行样式 */
.dt-table :deep(.el-table__row--level-1) {
  background: rgba(59, 130, 246, 0.02);
}

.dt-table :deep(.el-table__row--level-2) {
  background: rgba(59, 130, 246, 0.04);
}

.dt-table :deep(.el-table__row--level-3) {
  background: rgba(59, 130, 246, 0.06);
}

.admin-dark .dt-table :deep(.el-table__row--level-1) {
  background: rgba(96, 165, 250, 0.03);
}

.admin-dark .dt-table :deep(.el-table__row--level-2) {
  background: rgba(96, 165, 250, 0.05);
}

.admin-dark .dt-table :deep(.el-table__row--level-3) {
  background: rgba(96, 165, 250, 0.07);
}

/* 树形加载图标 */
.dt-table :deep(.el-table__expand-icon .el-icon-loading) {
  animation: rotating 2s linear infinite;
  color: #3b82f6;
}

@keyframes rotating {
  from {
    transform: rotate(0deg);
  }
  to {
    transform: rotate(360deg);
  }
}

.admin-dark .dt-table :deep(.el-table__expand-icon .el-icon-loading) {
  color: #60a5fa;
}

/* ==================== 空状态 ==================== */
.dt-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 60px 20px;
  color: #9ca3af;
}

.empty-icon {
  width: 64px;
  height: 41px;
  margin-bottom: 16px;
  opacity: 0.8;
}

.empty-text {
  font-size: 14px;
  color: #9ca3af;
  margin: 0;
}

.admin-dark .empty-text {
  color: #6b7280;
}

/* ==================== 分页 ==================== */
.dt-pagination {
  display: flex;
  justify-content: flex-end;
  padding: 12px 16px;
  border-top: 1px solid #f3f4f6;
  margin-top: 0;
}

.compact-mode .dt-pagination {
  padding: 8px 12px;
}

.admin-dark .dt-pagination {
  border-top-color: #283344;
}

.dt-pagination :deep(.el-pagination) {
  gap: 8px;
}

/* 小分页样式 */
.dt-pagination-small {
  padding: 8px 12px !important;
}

.dt-pagination-small :deep(.el-pagination) {
  gap: 4px;
}

.dt-pagination-small :deep(.el-pagination .btn-prev),
.dt-pagination-small :deep(.el-pagination .btn-next) {
  min-width: 24px !important;
  height: 24px !important;
  line-height: 22px !important;
  padding: 0 8px !important;
  font-size: 12px;
}

.dt-pagination-small :deep(.el-pager li) {
  min-width: 24px !important;
  height: 24px !important;
  line-height: 22px !important;
  font-size: 12px;
  margin: 0 2px !important;
}

.dt-pagination-small :deep(.el-pagination__total),
.dt-pagination-small :deep(.el-pagination__jump) {
  font-size: 12px;
}

.dt-pagination-small :deep(.el-pagination__total) {
  margin-right: 8px;
}

.dt-pagination :deep(.el-pagination .btn-prev),
.dt-pagination :deep(.el-pagination .btn-next) {
  background: #ffffff;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  transition: all 0.2s ease;
  padding: 0 12px;
  min-width: 32px;
  height: 32px;
}

.admin-dark .dt-pagination :deep(.el-pagination .btn-prev),
.admin-dark .dt-pagination :deep(.el-pagination .btn-next) {
  background: #374151;
  border-color: #4b5563;
  color: #e5e7eb;
}

.dt-pagination :deep(.el-pagination .btn-prev:hover),
.dt-pagination :deep(.el-pagination .btn-next:hover) {
  color: #3b82f6;
  border-color: #3b82f6;
  transform: translateY(-1px);
}

.dt-pagination :deep(.el-pager li) {
  background: #ffffff;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  transition: all 0.2s ease;
  min-width: 32px;
  height: 32px;
  line-height: 30px;
  margin: 0 4px;
}

.admin-dark .dt-pagination :deep(.el-pager li) {
  background: #374151;
  border-color: #4b5563;
  color: #e5e7eb;
}

.dt-pagination :deep(.el-pager li:hover) {
  color: #3b82f6;
  border-color: #3b82f6;
  transform: translateY(-1px);
}

.dt-pagination :deep(.el-pager li.is-active) {
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  color: #ffffff;
  border-color: transparent;
  font-weight: 600;
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.3);
}

.admin-dark .dt-pagination :deep(.el-pager li.is-active) {
  background: linear-gradient(135deg, #60a5fa 0%, #3b82f6 100%);
}

/* 每页条数选择器 */
.dt-pagination :deep(.el-pagination__sizes) {
  margin-right: 12px;
}

.dt-pagination :deep(.el-select .el-input__wrapper) {
  border-radius: 8px;
  border-color: #e5e7eb;
  transition: all 0.2s ease;
}

.admin-dark .dt-pagination :deep(.el-select .el-input__wrapper) {
  background: #374151;
  border-color: #4b5563;
}

.dt-pagination :deep(.el-select .el-input__wrapper:hover) {
  border-color: #3b82f6;
}

/* 跳转输入框 */
.dt-pagination :deep(.el-pagination__jump) {
  margin-left: 12px;
}

.dt-pagination :deep(.el-pagination__editor.el-input .el-input__wrapper) {
  border-radius: 8px;
  border-color: #e5e7eb;
  transition: all 0.2s ease;
}

.admin-dark .dt-pagination :deep(.el-pagination__editor.el-input .el-input__wrapper) {
  background: #374151;
  border-color: #4b5563;
}

.dt-pagination :deep(.el-pagination__editor.el-input .el-input__wrapper:hover) {
  border-color: #3b82f6;
}

/* ==================== 响应式 ==================== */
@media (max-width: 768px) {
  .dt-toolbar {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
    padding: 12px;
  }

  .dt-toolbar-left,
  .dt-toolbar-right {
    width: 100%;
    justify-content: space-between;
  }

  .batch-actions-wrapper {
    flex-direction: column;
    align-items: stretch;
  }

  .dt-pagination {
    justify-content: center;
    padding: 12px;
  }

  .dt-pagination :deep(.el-pagination) {
    flex-wrap: wrap;
    justify-content: center;
  }
}

/* ==================== 工具按钮增强 ==================== */
.dt-toolbar-right :deep(.el-button.is-circle) {
  width: 36px;
  height: 36px;
  padding: 0;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
  background: #ffffff;
  color: #6b7280;
  transition: all 0.2s ease;
}

.admin-dark .dt-toolbar-right :deep(.el-button.is-circle) {
  background: #374151;
  border-color: #4b5563;
  color: #9ca3af;
}

.dt-toolbar-right :deep(.el-button.is-circle:hover) {
  color: #3b82f6;
  border-color: #3b82f6;
  background: #eff6ff;
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(59, 130, 246, 0.2);
}

.admin-dark .dt-toolbar-right :deep(.el-button.is-circle:hover) {
  background: #4b5563;
  border-color: #60a5fa;
  color: #60a5fa;
}

/* ==================== 操作列按钮容器 ==================== */
.dt-actions-wrapper {
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 100%;
  padding: 4px 0;
  overflow: visible;
}

.dt-actions-wrapper :deep(.el-button) {
  margin: 0 !important;
}

.dt-table :deep(.dt-action-column .cell),
.dt-table :deep(.dt-action-column-header .cell) {
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 100%;
}

/* 操作列 link 按钮在深色模式下的文字颜色修正 */
.admin-dark .dt-actions-wrapper :deep(.el-button.is-link) {
  --el-button-text-color: #93c5fd;
}

.admin-dark .dt-actions-wrapper :deep(.el-button--primary.is-link) {
  --el-button-text-color: #93c5fd;
  --el-button-hover-text-color: #bfdbfe;
}

.admin-dark .dt-actions-wrapper :deep(.el-button--success.is-link) {
  --el-button-text-color: #86efac;
  --el-button-hover-text-color: #bbf7d0;
}

.admin-dark .dt-actions-wrapper :deep(.el-button--danger.is-link) {
  --el-button-text-color: #fca5a5;
  --el-button-hover-text-color: #fecaca;
}

.admin-dark .dt-actions-wrapper :deep(.el-button--warning.is-link) {
  --el-button-text-color: #fcd34d;
  --el-button-hover-text-color: #fde68a;
}

.admin-dark .dt-actions-wrapper :deep(.el-button--info.is-link) {
  --el-button-text-color: #d1d5db;
  --el-button-hover-text-color: #e5e7eb;
}
</style>
