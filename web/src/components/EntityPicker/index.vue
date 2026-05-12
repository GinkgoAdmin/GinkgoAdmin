<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="entity-picker" :class="{ 'is-disabled': disabled, 'is-readonly': readonly }">
    <el-popover
      ref="popoverRef"
      :visible="popoverVisible"
      placement="bottom-start"
      :width="popoverWidth"
      trigger="click"
      :disabled="disabled || readonly"
      :popper-options="{ modifiers: [{ name: 'offset', options: { offset: [0, 4] } }] }"
      @show="onPopoverShow"
    >
      <template #reference>
        <div
          class="picker-trigger"
          :class="{ 'is-focus': popoverVisible, 'is-disabled': disabled, 'is-readonly': readonly }"
          ref="triggerRef"
          @click="handleTriggerClick"
        >
          <!-- 多选标签 -->
          <template v-if="multiple && selectedItems.length">
            <el-tag
              v-for="item in selectedItems"
              :key="getItemValue(item)"
              :closable="!disabled && !readonly"
              size="small"
              class="picker-tag"
              @close="removeItem(item)"
            >
              {{ getItemLabel(item) }}
            </el-tag>
          </template>
          <!-- 单选显示文本 -->
          <span v-else-if="!multiple && displayLabel" class="picker-label">{{ displayLabel }}</span>
          <!-- 占位符 -->
          <span v-else class="picker-placeholder">{{ placeholder || '请选择' }}</span>
          <!-- 清除按钮 -->
          <span
            v-if="clearable && hasValue && !disabled && !readonly"
            class="picker-clear"
            @click.stop="handleClear"
          >
            <el-icon><Close /></el-icon>
          </span>
          <span v-else class="picker-arrow" :class="{ 'is-reverse': popoverVisible }">
            <el-icon><ArrowDown /></el-icon>
          </span>
        </div>
      </template>

      <!-- 弹出面板内容 -->
      <div class="picker-panel">
        <div class="picker-search">
          <el-input
            ref="searchInputRef"
            v-model="keyword"
            :placeholder="searchPlaceholder || '输入关键词搜索...'"
            clearable
            size="small"
            :prefix-icon="Search"
            @input="onSearchInput"
          />
        </div>
        <div class="picker-list" v-loading="loading">
          <div
            v-for="item in listData"
            :key="getItemValue(item)"
            class="picker-item"
            :class="{ 'is-selected': isSelected(item) }"
            @click="handleSelect(item)"
          >
            <span class="item-label">{{ getItemLabel(item) }}</span>
            <span class="item-value">{{ getItemValue(item) }}</span>
            <el-icon v-if="isSelected(item)" class="item-check"><Check /></el-icon>
          </div>
          <el-empty v-if="!loading && !listData.length" description="暂无数据" :image-size="48" />
        </div>
        <div class="picker-footer" v-if="total > internalPageSize">
          <el-pagination
            v-model:current-page="currentPage"
            :page-size="internalPageSize"
            :total="total"
            layout="total, prev, pager, next"
            small
            @current-change="fetchData"
          />
        </div>
      </div>
    </el-popover>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted } from 'vue'
import { Close, ArrowDown, Search, Check } from '@element-plus/icons-vue'
import { entityLookup } from '@/api/entity-lookup'

export interface EntityPickerProps {
  modelValue?: string | string[]
  multiple?: boolean
  fetchFn?: (params: { keyword: string; page: number; pageSize: number }) => Promise<{ items: any[]; total: number }>
  apiPath?: string
  valueField?: string
  labelField?: string
  placeholder?: string
  searchPlaceholder?: string
  disabled?: boolean
  readonly?: boolean
  clearable?: boolean
  pageSize?: number
}

const props = withDefaults(defineProps<EntityPickerProps>(), {
  modelValue: '',
  multiple: false,
  valueField: 'id',
  labelField: 'name',
  clearable: true,
  pageSize: 10,
  disabled: false,
  readonly: false,
})

const emit = defineEmits<{
  'update:modelValue': [value: string | string[]]
  'change': [value: string | string[], items: any[]]
}>()

// 状态
const popoverVisible = ref(false)
const popoverRef = ref()
const triggerRef = ref<HTMLElement>()
const searchInputRef = ref()
const keyword = ref('')
const loading = ref(false)
const listData = ref<any[]>([])
const total = ref(0)
const currentPage = ref(1)
const internalPageSize = computed(() => props.pageSize || 10)
const popoverWidth = ref(360)

// 已选中项缓存（用于显示 label）
const selectedItemsCache = ref<Map<string, any>>(new Map())

// 计算当前值数组
const currentValues = computed<string[]>(() => {
  if (!props.modelValue) return []
  if (Array.isArray(props.modelValue)) return props.modelValue.filter(Boolean)
  return String(props.modelValue).split(',').filter(Boolean)
})

const hasValue = computed(() => currentValues.value.length > 0)

// 已选中的项列表
const selectedItems = computed(() => {
  return currentValues.value
    .map(v => selectedItemsCache.value.get(v))
    .filter(Boolean)
})

// 单选时的显示文本
const displayLabel = computed(() => {
  if (props.multiple || !currentValues.value.length) return ''
  const item = selectedItemsCache.value.get(currentValues.value[0])
  return item ? getItemLabel(item) : currentValues.value[0]
})

function getItemValue(item: any): string {
  return String(item?.[props.valueField] ?? '')
}

function getItemLabel(item: any): string {
  return String(item?.[props.labelField] ?? item?.[props.valueField] ?? '')
}

function isSelected(item: any): boolean {
  return currentValues.value.includes(getItemValue(item))
}

// 数据获取
async function fetchData() {
  loading.value = true
  try {
    let result: { items: any[]; total: number }
    if (props.fetchFn) {
      result = await props.fetchFn({
        keyword: keyword.value,
        page: currentPage.value,
        pageSize: internalPageSize.value,
      })
    } else if (props.apiPath) {
      const res: any = await entityLookup({
        table: props.apiPath,
        valueField: props.valueField,
        labelField: props.labelField,
        keyword: keyword.value,
        page: currentPage.value,
        pageSize: internalPageSize.value,
      })
      result = { items: res?.items || res?.data?.items || [], total: Number(res?.total || res?.data?.total || 0) }
    } else {
      result = { items: [], total: 0 }
    }
    listData.value = result.items || []
    total.value = Number(result.total || 0)
    // 缓存已加载项
    listData.value.forEach(item => {
      const v = getItemValue(item)
      if (v) selectedItemsCache.value.set(v, item)
    })
  } catch (e) {
    console.error('[EntityPicker] 加载数据失败', e)
    listData.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

// 根据已选 ID 加载对应的 label（初始化时使用）
async function loadSelectedLabels() {
  if (!currentValues.value.length) return
  const missingIds = currentValues.value.filter(v => !selectedItemsCache.value.has(v))
  if (!missingIds.length) return
  // 逐个 ID 查找或通过一次查询获取
  try {
    if (props.fetchFn) {
      // 用空关键词查第一页，希望能命中；如果命中不了也没关系，显示 ID
      const result = await props.fetchFn({ keyword: '', page: 1, pageSize: 100 })
      ;(result.items || []).forEach((item: any) => {
        const v = getItemValue(item)
        if (v) selectedItemsCache.value.set(v, item)
      })
    } else if (props.apiPath) {
      const res: any = await entityLookup({
        table: props.apiPath,
        valueField: props.valueField,
        labelField: props.labelField,
        keyword: '',
        page: 1,
        pageSize: 100,
      })
      const items = res?.items || res?.data?.items || []
      items.forEach((item: any) => {
        const v = getItemValue(item)
        if (v) selectedItemsCache.value.set(v, item)
      })
    }
  } catch {
    // 加载失败时显示 ID
  }
}

// 搜索防抖
let searchTimer: ReturnType<typeof setTimeout> | null = null
function onSearchInput() {
  if (searchTimer) clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    currentPage.value = 1
    fetchData()
  }, 300)
}

function handleTriggerClick() {
  if (props.disabled || props.readonly) return
  popoverVisible.value = !popoverVisible.value
}

function onPopoverShow() {
  // 计算宽度
  if (triggerRef.value) {
    popoverWidth.value = Math.max(triggerRef.value.offsetWidth, 320)
  }
  keyword.value = ''
  currentPage.value = 1
  fetchData()
  nextTick(() => {
    searchInputRef.value?.focus()
  })
}

function handleSelect(item: any) {
  const val = getItemValue(item)
  if (!val) return
  // 缓存该项
  selectedItemsCache.value.set(val, item)

  if (props.multiple) {
    const newValues = [...currentValues.value]
    const idx = newValues.indexOf(val)
    if (idx >= 0) {
      newValues.splice(idx, 1)
    } else {
      newValues.push(val)
    }
    const output = newValues.join(',')
    emit('update:modelValue', output)
    emit('change', output, newValues.map(v => selectedItemsCache.value.get(v)).filter(Boolean))
  } else {
    emit('update:modelValue', val)
    emit('change', val, [item])
    popoverVisible.value = false
  }
}

function removeItem(item: any) {
  const val = getItemValue(item)
  const newValues = currentValues.value.filter(v => v !== val)
  const output = newValues.join(',')
  emit('update:modelValue', output)
  emit('change', output, newValues.map(v => selectedItemsCache.value.get(v)).filter(Boolean))
}

function handleClear() {
  emit('update:modelValue', props.multiple ? '' : '')
  emit('change', '', [])
  popoverVisible.value = false
}

// 点击外部关闭
function onClickOutside(e: MouseEvent) {
  if (!popoverVisible.value) return
  const target = e.target as HTMLElement
  const pickerEl = triggerRef.value?.closest('.entity-picker')
  const popoverEl = document.querySelector('.el-popover.el-popper')
  if (pickerEl?.contains(target) || popoverEl?.contains(target)) return
  popoverVisible.value = false
}

// 初始化加载已选项的 label
watch(() => props.modelValue, () => {
  loadSelectedLabels()
}, { immediate: true })

onMounted(() => {
  document.addEventListener('click', onClickOutside, true)
})
</script>

<style scoped>
.entity-picker {
  width: 100%;
}

.picker-trigger {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 4px;
  min-height: 32px;
  padding: 2px 30px 2px 8px;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  background: var(--el-fill-color-blank);
  cursor: pointer;
  position: relative;
  transition: border-color 0.2s;
}

.picker-trigger:hover {
  border-color: var(--el-border-color-hover);
}

.picker-trigger.is-focus {
  border-color: var(--el-color-primary);
}

.picker-trigger.is-disabled {
  background: var(--el-fill-color-light);
  cursor: not-allowed;
  color: var(--el-text-color-placeholder);
}

.picker-trigger.is-readonly {
  cursor: default;
}

.picker-placeholder {
  color: var(--el-text-color-placeholder);
  font-size: 14px;
  line-height: 28px;
}

.picker-label {
  font-size: 14px;
  line-height: 28px;
  color: var(--el-text-color-regular);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.picker-tag {
  max-width: 160px;
}

.picker-clear,
.picker-arrow {
  position: absolute;
  right: 8px;
  top: 50%;
  transform: translateY(-50%);
  color: var(--el-text-color-placeholder);
  font-size: 14px;
  cursor: pointer;
  transition: transform 0.3s, color 0.2s;
}

.picker-clear:hover {
  color: var(--el-text-color-regular);
}

.picker-arrow.is-reverse {
  transform: translateY(-50%) rotate(180deg);
}

.picker-panel {
  display: flex;
  flex-direction: column;
  max-height: 380px;
}

.picker-search {
  padding: 0 0 8px 0;
}

.picker-list {
  flex: 1;
  overflow-y: auto;
  max-height: 240px;
  min-height: 60px;
}

.picker-item {
  display: flex;
  align-items: center;
  padding: 6px 12px;
  cursor: pointer;
  border-radius: 4px;
  transition: background 0.15s;
  gap: 8px;
}

.picker-item:hover {
  background: var(--el-fill-color-light);
}

.picker-item.is-selected {
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.item-label {
  flex: 1;
  font-size: 14px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.item-value {
  font-size: 12px;
  color: var(--el-text-color-placeholder);
  flex-shrink: 0;
  max-width: 100px;
  overflow: hidden;
  text-overflow: ellipsis;
}

.item-check {
  flex-shrink: 0;
  font-size: 14px;
}

.picker-footer {
  padding: 8px 0 0 0;
  border-top: 1px solid var(--el-border-color-lighter);
  display: flex;
  justify-content: center;
}
</style>
