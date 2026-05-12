<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <el-tooltip content="列设置" placement="top">
    <el-dropdown trigger="click" @visible-change="onVisibleChange">
      <el-button :icon="Setting" circle size="small" :class="{'is-active': dropdownVisible}" />
      <template #dropdown>
        <el-dropdown-menu class="dt-columns">
          <div class="columns-header">
            <span class="columns-title">
              <i class="bi bi-layout-three-columns"></i>
              显示列配置
            </span>
            <el-button 
              link 
              size="small" 
              type="primary"
              @click="selectAll">
              全选
            </el-button>
          </div>
          <el-divider style="margin: 8px 0;" />
          <el-scrollbar max-height="400px">
            <el-checkbox-group v-model="inner" class="columns-list">
              <div v-for="c in columns" :key="c.prop" class="dt-col-item">
                <el-checkbox :value="c.prop">
                  <span class="col-label">{{ c.label }}</span>
                </el-checkbox>
              </div>
            </el-checkbox-group>
          </el-scrollbar>
          <el-divider style="margin: 8px 0;" />
          <div class="columns-footer">
            <el-button size="small" @click="reset">重置</el-button>
            <el-button size="small" type="primary" @click="apply">应用</el-button>
          </div>
        </el-dropdown-menu>
      </template>
    </el-dropdown>
  </el-tooltip>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { Setting } from '@element-plus/icons-vue'
import type { ColumnConfig } from '../types'

const props = defineProps<{ columns: ColumnConfig[]; modelValue?: string[]; visibleKeys?: string[] }>()
const emit = defineEmits<{ (e:'update:visibleKeys', v:string[]):void }>()

const inner = ref<string[]>([])
const dropdownVisible = ref(false)
const allColumnKeys = props.columns.map(c => c.prop)

onMounted(() => {
  inner.value = (props.visibleKeys && props.visibleKeys.length) ? [...props.visibleKeys] : [...allColumnKeys]
})

watch(inner, (v) => {
  if (v.length > 0) {
    emit('update:visibleKeys', v)
  }
}, { deep: true })

function onVisibleChange(visible: boolean) {
  dropdownVisible.value = visible
}

function selectAll() {
  inner.value = [...allColumnKeys]
}

function reset() {
  inner.value = [...allColumnKeys]
}

function apply() {
  // 只是关闭下拉框，数据已经通过 watch 同步了
  dropdownVisible.value = false
}
</script>

<style scoped>
.dt-columns {
  padding: 12px;
  min-width: 240px;
  border-radius: 10px;
}

.columns-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 4px 8px;
}

.columns-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  font-weight: 600;
  color: #374151;
}

.admin-dark .columns-title {
  color: #e5e7eb;
}

.columns-title i {
  font-size: 16px;
  color: #3b82f6;
}

.admin-dark .columns-title i {
  color: #60a5fa;
}

.columns-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.dt-col-item {
  padding: 8px 12px;
  border-radius: 6px;
  transition: all 0.2s ease;
  cursor: pointer;
}

.dt-col-item:hover {
  background: #f9fafb;
}

.admin-dark .dt-col-item:hover {
  background: #374151;
}

.dt-col-item :deep(.el-checkbox) {
  width: 100%;
}

.dt-col-item :deep(.el-checkbox__label) {
  width: 100%;
  font-size: 14px;
  color: #1f2937;
}

.admin-dark .dt-col-item :deep(.el-checkbox__label) {
  color: #e5e7eb;
}

.col-label {
  font-weight: 500;
}

.columns-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding-top: 8px;
}

.columns-footer :deep(.el-button) {
  min-width: 70px;
  border-radius: 6px;
}

/* 激活状态的按钮 */
.is-active {
  background: #eff6ff !important;
  color: #3b82f6 !important;
  border-color: #3b82f6 !important;
}

.admin-dark .is-active {
  background: #1e3a8a !important;
  color: #60a5fa !important;
  border-color: #60a5fa !important;
}
</style>


