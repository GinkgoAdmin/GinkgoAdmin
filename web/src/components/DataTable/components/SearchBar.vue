<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="dt-search-wrapper" :class="{ 'compact-mode': compact }">
    <!-- 简单搜索区 -->
    <div class="simple-search">
      <el-form :model="model" inline size="default">
        <template v-for="field in simpleFields" :key="field.key">
          <el-form-item :label="field.label" class="simple-search-item">
            <component :is="resolveField(field)" :compact="true" />
          </el-form-item>
        </template>
        
        <!-- 搜索按钮组 -->
        <el-form-item class="simple-search-actions">
          <el-button type="primary" :icon="Search" :loading="loading" @click="emitSearch">
            搜索
          </el-button>
          <el-button :icon="RefreshLeft" @click="emitReset">
            重置
          </el-button>
          <el-button 
            v-if="hasAdvancedFields"
            :icon="isExpanded ? ArrowUp : ArrowDown" 
            @click="toggleExpand">
            {{ isExpanded ? '收起' : '高级' }}
          </el-button>
        </el-form-item>
      </el-form>
    </div>

    <!-- 高级搜索区 -->
    <transition name="expand">
      <div v-if="isExpanded && hasAdvancedFields" class="advanced-search">
        <el-divider content-position="left">
          <span class="advanced-title">
            <i class="bi bi-funnel"></i>
            高级筛选
          </span>
        </el-divider>
        <el-form :model="model" label-width="auto" size="default">
          <el-row :gutter="16">
            <template v-for="field in advancedFields" :key="field.key">
              <el-col :span="field.span || 8" :xs="24" :sm="12" :md="8" :lg="field.span || 8">
                <el-form-item :label="field.label" class="advanced-search-item">
                  <component :is="resolveField(field)" />
                </el-form-item>
              </el-col>
            </template>
            <slot />
          </el-row>
        </el-form>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { h, ref, computed, watch, onMounted } from 'vue'
import { Search, RefreshLeft, ArrowUp, ArrowDown } from '@element-plus/icons-vue'
import type { SearchFieldConfig } from '../types'

const props = defineProps<{ 
  config: SearchFieldConfig[]
  model: Record<string, any>
  loading?: boolean
  defaultExpanded?: boolean
  compact?: boolean
}>()
const emit = defineEmits<{ (e:'search'):void; (e:'reset'):void }>()

// 区分简单搜索和高级搜索字段
const simpleFields = computed(() => props.config.filter(f => f.simple === true))
const advancedFields = computed(() => props.config.filter(f => f.simple !== true))
const hasAdvancedFields = computed(() => advancedFields.value.length > 0)

// 展开状态（支持 localStorage 持久化）
const storageKey = 'dt-search-expanded'
const isExpanded = ref(props.defaultExpanded || false)

onMounted(() => {
  const stored = localStorage.getItem(storageKey)
  if (stored !== null) {
    isExpanded.value = stored === 'true'
  }
})

watch(isExpanded, (val) => {
  localStorage.setItem(storageKey, String(val))
})

function toggleExpand() {
  isExpanded.value = !isExpanded.value
}

function resolveField(field: SearchFieldConfig, compact = false) {
  const width = compact ? '200px' : '100%'
  const common = { 
    style: `width: ${width}`, 
    placeholder: field.placeholder, 
    clearable: field.clearable !== false 
  }
  
  switch (field.type) {
    case 'input':
      return { 
        render: () => h('el-input', { 
          ...common, 
          modelValue: props.model[field.key], 
          'onUpdate:modelValue': (v:any)=> props.model[field.key]=v 
        }) 
      }
    case 'select':
      return {
        render: () => h('el-select', { 
          ...common, 
          multiple: !!field.multiple, 
          modelValue: props.model[field.key], 
          'onUpdate:modelValue': (v:any)=> props.model[field.key]=v 
        }, () => (field.options||[]).map(o=> h('el-option', { label: o.label, value: o.value })))
      }
    case 'number':
      return { 
        render: () => h('el-input-number', { 
          ...common, 
          modelValue: props.model[field.key], 
          'onUpdate:modelValue': (v:any)=> props.model[field.key]=v, 
          style: compact ? 'width: 160px' : 'width: 100%'
        }) 
      }
    case 'date':
      return { 
        render: () => h('el-date-picker', { 
          ...common, 
          type: 'date', 
          modelValue: props.model[field.key], 
          'onUpdate:modelValue': (v:any)=> props.model[field.key]=v 
        }) 
      }
    case 'daterange':
      return { 
        render: () => h('el-date-picker', { 
          ...common, 
          type: 'daterange', 
          startPlaceholder: '开始日期', 
          endPlaceholder: '结束日期', 
          modelValue: props.model[field.key], 
          'onUpdate:modelValue': (v:any)=> props.model[field.key]=v 
        }) 
      }
    case 'tree':
      return { 
        render: () => h('el-tree-select', { 
          ...common, 
          data: field.options || [], 
          nodeKey: 'value', 
          props: { label: 'label', value: 'value', children: 'children' }, 
          modelValue: props.model[field.key], 
          'onUpdate:modelValue': (v:any)=> props.model[field.key]=v, 
          filterable: true 
        }) 
      }
    default:
      return { render: () => h('span') }
  }
}

function emitSearch() { emit('search') }
function emitReset() { 
  Object.keys(props.model).forEach(k=> props.model[k]=undefined)
  emit('reset') 
}
</script>

<style scoped>
/* ==================== 整体容器 ==================== */
.dt-search-wrapper {
  background: #fff;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  padding: 12px 16px;
  margin-bottom: 12px;
  transition: all 0.3s ease;
}

.dt-search-wrapper.compact-mode {
  padding: 8px 12px;
  margin-bottom: 8px;
}

.admin-dark .dt-search-wrapper {
  background: #1f2937;
  border-color: #374151;
}

/* ==================== 简单搜索区 ==================== */
.simple-search {
  margin: 0;
}

.simple-search :deep(.el-form) {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 12px;
  margin: 0;
}

.simple-search-item {
  margin: 0 !important;
}

.simple-search-item :deep(.el-form-item__label) {
  font-size: 14px;
  font-weight: 500;
  color: #374151;
  padding-right: 8px;
}

.admin-dark .simple-search-item :deep(.el-form-item__label) {
  color: #e5e7eb;
}

.simple-search-item :deep(.el-form-item__content) {
  margin-left: 0 !important;
}

.simple-search-actions {
  margin: 0 !important;
  margin-left: auto !important;
}

.simple-search-actions :deep(.el-form-item__content) {
  display: flex;
  gap: 8px;
  margin-left: 0 !important;
}

.simple-search :deep(.el-button) {
  padding: 8px 16px;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  transition: all 0.2s ease;
}

.simple-search :deep(.el-button--primary) {
  background: #3b82f6;
  border-color: #3b82f6;
}

.simple-search :deep(.el-button--primary:hover) {
  background: #2563eb;
  border-color: #2563eb;
  transform: translateY(-1px);
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.3);
}

.simple-search :deep(.el-button:not(.el-button--primary)) {
  border-color: #d1d5db;
  color: #6b7280;
}

.admin-dark .simple-search :deep(.el-button:not(.el-button--primary)) {
  background: #374151;
  border-color: #4b5563;
  color: #9ca3af;
}

.simple-search :deep(.el-button:not(.el-button--primary):hover) {
  color: #3b82f6;
  border-color: #3b82f6;
}

.admin-dark .simple-search :deep(.el-button:not(.el-button--primary):hover) {
  background: #4b5563;
  border-color: #60a5fa;
  color: #60a5fa;
}

/* ==================== 高级搜索区 ==================== */
.advanced-search {
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid #f3f4f6;
}

.compact-mode .advanced-search {
  margin-top: 12px;
  padding-top: 12px;
}

.admin-dark .advanced-search {
  border-top-color: #374151;
}

.advanced-search :deep(.el-divider) {
  margin: 0 0 16px 0;
}

.advanced-title {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  font-weight: 600;
  color: #374151;
}

.admin-dark .advanced-title {
  color: #e5e7eb;
}

.advanced-title i {
  font-size: 14px;
  color: #3b82f6;
}

.admin-dark .advanced-title i {
  color: #60a5fa;
}

.advanced-search-item {
  margin-bottom: 16px;
}

.compact-mode .advanced-search-item {
  margin-bottom: 12px;
}

.advanced-search-item :deep(.el-form-item__label) {
  font-size: 14px;
  font-weight: 500;
  color: #374151;
}

.admin-dark .advanced-search-item :deep(.el-form-item__label) {
  color: #e5e7eb;
}

/* ==================== 输入框样式 ==================== */
.dt-search-wrapper :deep(.el-input__wrapper),
.dt-search-wrapper :deep(.el-select .el-input__wrapper),
.dt-search-wrapper :deep(.el-date-editor .el-input__wrapper),
.dt-search-wrapper :deep(.el-input-number .el-input__wrapper),
.dt-search-wrapper :deep(.el-tree-select .el-input__wrapper) {
  border-radius: 6px;
  border-color: #d1d5db;
  transition: all 0.2s ease;
  box-shadow: none;
}

.admin-dark .dt-search-wrapper :deep(.el-input__wrapper),
.admin-dark .dt-search-wrapper :deep(.el-select .el-input__wrapper),
.admin-dark .dt-search-wrapper :deep(.el-date-editor .el-input__wrapper),
.admin-dark .dt-search-wrapper :deep(.el-input-number .el-input__wrapper),
.admin-dark .dt-search-wrapper :deep(.el-tree-select .el-input__wrapper) {
  background: #374151;
  border-color: #4b5563;
}

.dt-search-wrapper :deep(.el-input__wrapper:hover),
.dt-search-wrapper :deep(.el-select .el-input__wrapper:hover),
.dt-search-wrapper :deep(.el-date-editor .el-input__wrapper:hover),
.dt-search-wrapper :deep(.el-input-number .el-input__wrapper:hover),
.dt-search-wrapper :deep(.el-tree-select .el-input__wrapper:hover) {
  border-color: #3b82f6;
}

.dt-search-wrapper :deep(.el-input__wrapper.is-focus),
.dt-search-wrapper :deep(.el-select .el-input__wrapper.is-focus),
.dt-search-wrapper :deep(.el-date-editor .el-input__wrapper.is-focus),
.dt-search-wrapper :deep(.el-input-number .el-input__wrapper.is-focus),
.dt-search-wrapper :deep(.el-tree-select .el-input__wrapper.is-focus) {
  border-color: #3b82f6;
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.1);
}

/* ==================== 展开/收起动画 ==================== */
.expand-enter-active,
.expand-leave-active {
  transition: all 0.3s ease;
  overflow: hidden;
}

.expand-enter-from,
.expand-leave-to {
  max-height: 0;
  opacity: 0;
  margin-top: 0;
  padding-top: 0;
}

.expand-enter-to,
.expand-leave-from {
  max-height: 800px;
  opacity: 1;
}

/* ==================== 响应式 ==================== */
@media (max-width: 768px) {
  .simple-search :deep(.el-form) {
    flex-direction: column;
    align-items: stretch;
    gap: 8px;
  }

  .simple-search-item :deep(.el-form-item__content),
  .simple-search-actions :deep(.el-form-item__content) {
    width: 100%;
  }

  .simple-search-item :deep(.el-input),
  .simple-search-item :deep(.el-select),
  .simple-search-item :deep(.el-date-editor) {
    width: 100% !important;
  }

  .simple-search-actions {
    margin-left: 0 !important;
  }

  .simple-search-actions :deep(.el-form-item__content) {
    flex-direction: column;
  }

  .simple-search :deep(.el-button) {
    width: 100%;
  }
}
</style>


