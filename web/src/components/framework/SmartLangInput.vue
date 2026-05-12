<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <!--
    智能多语言输入组件
    - 多语言开启时：显示 LangInput（带语言切换和翻译按钮）
    - 多语言关闭时：显示普通 el-input
    使用方式与 el-input 完全一致，v-model 绑定值自动处理格式转换
  -->
  <LangInput
    v-if="multiLangOn"
    :modelValue="modelValue"
    :placeholder="placeholder"
    :is-textarea="type === 'textarea'"
    :rows="rows"
    v-bind="$attrs"
    @update:modelValue="$emit('update:modelValue', $event)"
    @change="$emit('change', $event)"
  />
  <el-input
    v-else
    :model-value="plainValue"
    :type="type"
    :rows="rows"
    :placeholder="placeholder"
    v-bind="$attrs"
    @update:model-value="onPlainInput"
  />
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useMultiLangEnabled, parseLang, getDefaultLang } from '@/utils/lang'
import LangInput from './LangInput.vue'

const props = defineProps<{
  modelValue: string
  placeholder?: string
  type?: string
  rows?: number
}>()

const emit = defineEmits(['update:modelValue', 'change'])

const multiLangOn = useMultiLangEnabled()

// 单语言模式：从 JSON 中提取默认语言的值显示
const plainValue = computed(() => {
  if (!props.modelValue) return ''
  return parseLang(props.modelValue)
})

// 单语言模式：输入时直接存为纯字符串
function onPlainInput(val: string) {
  emit('update:modelValue', val)
  emit('change', val)
}
</script>
