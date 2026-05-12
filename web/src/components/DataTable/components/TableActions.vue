<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="dt-actions">
    <template v-for="(action, index) in safeActions" :key="index">
      <el-tooltip 
        v-if="action.label" 
        :content="typeof action.label === 'function' ? action.label(row) : action.label" 
        placement="top"
        :show-after="500">
        <el-button 
          :type="typeof action.type === 'function' ? action.type(row) : (action.type || 'primary')"
          :icon="typeof action.icon === 'function' ? action.icon(row) : action.icon"
          :disabled="action.disabled ? action.disabled(row) : false"
          link 
          size="small"
          class="action-btn"
          @click.stop="handleAction(action)">
          {{ typeof action.label === 'function' ? action.label(row) : action.label }}
        </el-button>
      </el-tooltip>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { ActionConfig } from '../types'

const props = defineProps<{ actions?: ActionConfig[]; row: any; permissionChecker?: (p: string)=>boolean }>()
const emit = defineEmits<{ (e: 'action', key: string): void }>()

const safeActions = computed(() => {
  return (props.actions || []).filter(a => {
    if (a.visible && !a.visible(props.row)) return false
    if (a.permission) {
      try {
        if (!props.permissionChecker) return false
        return props.permissionChecker(a.permission)
      } catch {
        return false
      }
    }
    return true
  })
})

function handleAction(action: ActionConfig) {
  if (action.handler) {
    action.handler(props.row)
  }
  // 优先以 action.key 作为事件标识（与 emit 类型声明 (e:'action', key:string) 自洽）；
  // 仅当未配置 key 时才回退到 label 文本，保持对旧版本的向后兼容。
  const fallbackLabel = typeof action.label === 'function' ? action.label(props.row) : action.label
  const key = (action as any).key as string | undefined
  emit('action', key || fallbackLabel)
}
</script>

<style scoped>
.dt-actions {
  display: inline-grid;
  grid-template-columns: repeat(5, max-content);
  column-gap: 8px;
  row-gap: 6px;
  align-items: center;
  justify-content: center;
  width: max-content;
  max-width: 100%;
  margin: 0 auto;
}

.action-btn {
  min-width: 52px;
  font-weight: 500;
  transition: all 0.2s ease;
  padding: 4px 8px;
  border-radius: 6px;
  justify-content: center;
}

.action-btn:hover {
  transform: translateY(-1px);
}

.action-btn.el-button--primary {
  color: #3b82f6;
}

.action-btn.el-button--primary:hover {
  background: rgba(59, 130, 246, 0.1);
}

.action-btn.el-button--success {
  color: #10b981;
}

.action-btn.el-button--success:hover {
  background: rgba(16, 185, 129, 0.1);
}

.action-btn.el-button--warning {
  color: #f59e0b;
}

.action-btn.el-button--warning:hover {
  background: rgba(245, 158, 11, 0.1);
}

.action-btn.el-button--danger {
  color: #ef4444;
}

.action-btn.el-button--danger:hover {
  background: rgba(239, 68, 68, 0.1);
}

.action-btn.el-button--info {
  color: #6b7280;
}

.action-btn.el-button--info:hover {
  background: rgba(107, 114, 128, 0.1);
}

/* 暗黑模式 */
.admin-dark .action-btn.el-button--primary {
  color: #60a5fa;
}

.admin-dark .action-btn.el-button--success {
  color: #34d399;
}

.admin-dark .action-btn.el-button--warning {
  color: #fbbf24;
}

.admin-dark .action-btn.el-button--danger {
  color: #f87171;
}

.admin-dark .action-btn.el-button--info {
  color: #9ca3af;
}

.admin-dark .action-btn:hover {
  background: rgba(255, 255, 255, 0.05);
}
</style>

