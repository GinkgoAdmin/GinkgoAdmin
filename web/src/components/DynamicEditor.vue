<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="dynamic-editor">
    <!-- 如果有编辑器插件或内置编辑器，渲染对应编辑器 -->
    <component 
      v-if="editorComponent"
      :is="editorComponent"
      ref="editorRef"
      :model-value="modelValue"
      v-bind="editorProps"
      @update:model-value="handleUpdate"
      @editor-ready="handleEditorReady"
      @editor-change="handleEditorChange"
    />
    
    <!-- 默认的多行文本框 -->
    <el-input
      v-else
      :model-value="modelValue"
      type="textarea"
      :rows="rows"
      :placeholder="placeholder"
      :disabled="disabled"
      :readonly="readonly"
      :maxlength="maxlength"
      :show-word-limit="showWordLimit"
      @update:model-value="handleUpdate"
      @blur="handleBlur"
      @focus="handleFocus"
      class="default-editor"
    />
    
    <!-- 编辑器工具栏插槽 -->
    <PluginSlot 
      name="editor-toolbar" 
      :props="{ editorType: editorType, value: modelValue }"
      :context="{ editorId: editorId }"
      @plugin-event="handleToolbarEvent"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, watch } from 'vue'
import { ElInput } from 'element-plus'
import { PluginManager } from '../plugins/core/PluginManager'
import PluginSlot from './PluginSlot.vue'
import WangEditor from './WangEditor.vue'
import type { EditorAdapterExposed } from './editor-adapter'

interface Props {
  modelValue: string
  editorType?: string // 编辑器类型：'rich', 'markdown', 'code' 等
  placeholder?: string
  disabled?: boolean
  readonly?: boolean
  rows?: number
  maxlength?: number
  showWordLimit?: boolean
  config?: Record<string, any> // 编辑器配置
}

const props = withDefaults(defineProps<Props>(), {
  editorType: 'rich',
  placeholder: '请输入内容...',
  disabled: false,
  readonly: false,
  rows: 4,
  showWordLimit: false,
  config: () => ({})
})

const emit = defineEmits<{
  'update:modelValue': [value: string]
  'editor-ready': [editor: any]
  'editor-change': [value: string, editor: any]
  'focus': [event: FocusEvent]
  'blur': [event: FocusEvent]
}>()

const pluginManager = PluginManager.getInstance()
const editorId = ref(`editor-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`)
const editorRef = ref<InstanceType<typeof WangEditor> | null>(null)

// 查找可用的编辑器组件
const editorComponent = computed(() => {
  // 1. 通过钩子系统查询编辑器插件（最高优先级）
  const result = pluginManager.executeHook('editor:get-component', {
    editorType: props.editorType,
    config: props.config
  })
  
  if (result && result.component) {
    return result.component
  }
  
  // 2. 对于 rich 类型，使用内置 wangEditor
  if (props.editorType === 'rich') {
    return WangEditor
  }
  
  // 3. 其他类型从组件注册表查找
  const componentName = `${props.editorType}-editor`
  return pluginManager.getComponent(componentName)
})

// 编辑器属性
const editorProps = computed(() => {
  const baseProps = {
    placeholder: props.placeholder,
    disabled: props.disabled,
    readonly: props.readonly,
    config: props.config,
    editorId: editorId.value
  }
  
  // 让插件有机会修改属性
  return pluginManager.executeHook('editor:props', baseProps, {
    editorType: props.editorType,
    originalProps: props
  })
})

// 处理值更新
const handleUpdate = (value: string) => {
  emit('update:modelValue', value)
}

// 处理编辑器就绪
const handleEditorReady = (editor: any) => {
  emit('editor-ready', editor)
  
  // 通知插件系统编辑器已就绪
  pluginManager.executeHook('editor:ready', {
    editor,
    editorType: props.editorType,
    editorId: editorId.value
  })
}

// 处理编辑器内容变化
const handleEditorChange = (value: string, editor: any) => {
  emit('editor-change', value, editor)
  
  // 通知插件系统编辑器内容已变化
  pluginManager.executeHook('editor:change', {
    value,
    editor,
    editorType: props.editorType,
    editorId: editorId.value
  })
}

// 处理焦点事件
const handleFocus = (event: FocusEvent) => {
  emit('focus', event)
}

const handleBlur = (event: FocusEvent) => {
  emit('blur', event)
}

// 处理工具栏事件
const handleToolbarEvent = (event: string, data: any) => {
  pluginManager.executeHook(`editor:toolbar:${event}`, {
    data,
    editorType: props.editorType,
    editorId: editorId.value,
    value: props.modelValue
  })
}

// 监听编辑器类型变化
watch(() => props.editorType, (newType, oldType) => {
  if (newType !== oldType) {
    pluginManager.executeHook('editor:type-changed', {
      newType,
      oldType,
      editorId: editorId.value
    })
  }
})

onMounted(() => {
  // 通知插件系统编辑器组件已挂载
  pluginManager.executeHook('editor:mounted', {
    editorType: props.editorType,
    editorId: editorId.value,
    config: props.config
  })
})

// 代理编辑器方法到当前活跃编辑器实例
const getHTML = (): string => {
  const editor = editorRef.value as unknown as EditorAdapterExposed | null
  return editor?.getHTML?.() ?? ''
}
const getText = (): string => {
  const editor = editorRef.value as unknown as EditorAdapterExposed | null
  return editor?.getText?.() ?? ''
}
const isEmpty = (): boolean => {
  const editor = editorRef.value as unknown as EditorAdapterExposed | null
  return editor?.isEmpty?.() ?? true
}
const reset = (): void => {
  const editor = editorRef.value as unknown as EditorAdapterExposed | null
  editor?.reset?.()
}
const focus = (): void => {
  const editor = editorRef.value as unknown as EditorAdapterExposed | null
  editor?.focus?.()
}
const blur = (): void => {
  const editor = editorRef.value as unknown as EditorAdapterExposed | null
  editor?.blur?.()
}

defineExpose<EditorAdapterExposed>({
  getHTML,
  getText,
  isEmpty,
  reset,
  focus,
  blur
})
</script>

<style scoped>
.dynamic-editor {
  position: relative;
  width: 100%;
}

.default-editor {
  width: 100%;
}

.default-editor :deep(.el-textarea__inner) {
  font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', monospace;
  line-height: 1.5;
}

/* 编辑器容器样式 */
.dynamic-editor :deep(.editor-container) {
  border: 1px solid #dcdfe6;
  border-radius: 4px;
  overflow: hidden;
}

.dynamic-editor :deep(.editor-container:focus-within) {
  border-color: #409eff;
  box-shadow: 0 0 0 2px rgba(64, 158, 255, 0.2);
}

.dynamic-editor :deep(.editor-toolbar) {
  background: #f5f7fa;
  border-bottom: 1px solid #e4e7ed;
  padding: 8px 12px;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.dynamic-editor :deep(.editor-content) {
  min-height: 120px;
  padding: 12px;
}
</style>