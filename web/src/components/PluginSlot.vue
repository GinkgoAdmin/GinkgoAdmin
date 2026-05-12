<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="plugin-slot" :class="`plugin-slot-${name}`">
    <!-- 渲染插件注入的组件 -->
    <template v-for="(component, index) in slotComponents" :key="`${name}-${index}`">
      <component 
        :is="component.component" 
        v-bind="{ ...component.props, ...props }"
        :plugin-context="context"
        @plugin-event="handlePluginEvent"
      />
    </template>
    
    <!-- 默认插槽内容 -->
    <slot v-if="slotComponents.length === 0" />
  </div>
</template>

<script setup lang="ts">
import { ref, watch, inject, onMounted, onUnmounted } from 'vue'
import { PluginManager } from '../plugins/core/PluginManager'

interface Props {
  name: string // 插槽名称
  props?: Record<string, any> // 传递给插件组件的属性
  context?: Record<string, any> // 插槽上下文
  fallback?: boolean // 是否显示默认内容
}

const props = withDefaults(defineProps<Props>(), {
  props: () => ({}),
  context: () => ({}),
  fallback: true
})

const emit = defineEmits<{
  'plugin-event': [event: string, data: any]
}>()

const pluginManager = PluginManager.getInstance()

// 存储当前插槽组件
const slotComponents = ref<Array<{ component: any; props: Record<string, any> }>>([])

// 加载插件组件的方法
const loadSlotComponents = async () => {
  try {
    const components: Array<{ component: any; props: Record<string, any> }> = []
    
    // 执行异步插槽钩子，以支持需要 await 请求的第三方插件等
    const result = await pluginManager.executeHookAsync(`slot:${props.name}`, {
      slotName: props.name,
      context: props.context,
      props: props.props
    })
    
    if (Array.isArray(result)) {
      components.push(...result)
    } else if (result && typeof result === 'object' && ('component' in result)) {
      components.push(result)
    }
    
    slotComponents.value = components
  } catch (error) {
    console.error(`Error loading plugin slot [${props.name}]:`, error)
  }
}

// 监听参数变化动态重新加载
watch(
  () => [props.name, props.context, props.props],
  () => {
    loadSlotComponents()
  },
  { deep: true, immediate: true }
)

// 处理插件事件
const handlePluginEvent = (event: string, data: any) => {
  emit('plugin-event', event, data)
  
  // 也通过钩子系统广播事件
  pluginManager.executeHook(`slot:${props.name}:event`, {
    event,
    data,
    slotName: props.name,
    context: props.context
  })
}

onMounted(() => {
  // 通知插件系统插槽已挂载
  pluginManager.executeHook(`slot:${props.name}:mounted`, {
    slotName: props.name,
    context: props.context
  })
})

onUnmounted(() => {
  // 通知插件系统插槽即将卸载
  pluginManager.executeHook(`slot:${props.name}:unmounted`, {
    slotName: props.name,
    context: props.context
  })
})
</script>

<style scoped>
.plugin-slot {
  display: contents;
}

/* 为不同类型的插槽提供基础样式 */
.plugin-slot-login-actions {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  margin-top: 1rem;
}

.plugin-slot-editor {
  width: 100%;
  min-height: 120px;
}

.plugin-slot-toolbar {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}
</style>