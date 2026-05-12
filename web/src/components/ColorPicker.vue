<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="color-picker-wrapper">
    <el-color-picker
      v-model="colorValue"
      :show-alpha="showAlpha"
      :color-format="colorFormat"
      :predefine="predefine"
      :size="size"
      @change="handleChange"
    />
    <el-input
      v-if="showInput"
      v-model="colorValue"
      :size="size"
      :placeholder="placeholder"
      class="color-input"
      @input="handleInputChange"
    >
      <template #prepend>
        <span class="color-preview" :style="{ backgroundColor: colorValue }"></span>
      </template>
    </el-input>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'

interface Props {
  modelValue?: string
  showAlpha?: boolean
  colorFormat?: 'hex' | 'rgb' | 'hsl' | 'hsv'
  showInput?: boolean
  size?: 'large' | 'default' | 'small'
  placeholder?: string
  predefine?: string[]
}

const props = withDefaults(defineProps<Props>(), {
  modelValue: '#3b82f6',
  showAlpha: false,
  colorFormat: 'hex',
  showInput: true,
  size: 'default',
  placeholder: '请选择颜色',
  predefine: () => [
    '#3b82f6', // 蓝色
    '#2563eb', // 深蓝
    '#10b981', // 绿色
    '#f59e0b', // 橙色
    '#ef4444', // 红色
    '#8b5cf6', // 紫色
    '#ec4899', // 粉色
    '#06b6d4', // 青色
    '#84cc16', // 黄绿
    '#f97316', // 深橙
    '#6366f1', // 靛蓝
    '#14b8a6', // 青绿
  ]
})

const emit = defineEmits<{
  (e: 'update:modelValue', value: string): void
  (e: 'change', value: string): void
}>()

const colorValue = ref(props.modelValue || '#3b82f6')

// 监听外部值变化
watch(() => props.modelValue, (newValue) => {
  if (newValue && newValue !== colorValue.value) {
    colorValue.value = newValue
  }
})

// 颜色选择器变化
function handleChange(value: string | null) {
  const color = value || '#3b82f6'
  colorValue.value = color
  emit('update:modelValue', color)
  emit('change', color)
}

// 输入框变化
function handleInputChange(value: string) {
  // 验证十六进制颜色格式
  if (/^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$/.test(value)) {
    emit('update:modelValue', value)
    emit('change', value)
  }
}
</script>

<style scoped>
.color-picker-wrapper {
  display: flex;
  align-items: center;
  gap: 12px;
}

.color-input {
  width: 200px;
}

.color-preview {
  display: inline-block;
  width: 20px;
  height: 20px;
  border-radius: 4px;
  border: 1px solid var(--el-border-color);
}

/* 暗黑主题适配 - 已移至 web/src/styles/admin/themes/dark/pages.css */
/* 使用通用的暗黑主题样式类，无需在此重复定义 */
</style>

