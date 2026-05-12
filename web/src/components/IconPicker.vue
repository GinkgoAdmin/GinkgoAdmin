<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <el-popover
    :visible="visible"
    placement="bottom-start"
    :width="400"
    trigger="click"
    @update:visible="visible = $event"
  >
    <template #reference>
      <div class="icon-picker-trigger" @click="visible = !visible">
        <i v-if="modelValue" :class="`ri-${modelValue}`" class="selected-icon"></i>
        <span v-else class="placeholder">选择图标</span>
        <i class="ri-arrow-down-s-line arrow-icon"></i>
      </div>
    </template>
    
    <div class="icon-picker-content">
      <!-- 搜索框 -->
      <el-input
        v-model="searchText"
        placeholder="搜索图标..."
        clearable
        size="small"
        class="search-input"
      >
        <template #prefix>
          <i class="ri-search-line"></i>
        </template>
      </el-input>
      
      <!-- 分类标签 -->
      <div class="category-tabs">
        <el-radio-group v-model="activeCategory" size="small">
          <el-radio-button value="all">全部</el-radio-button>
          <el-radio-button value="system">系统</el-radio-button>
          <el-radio-button value="user">用户</el-radio-button>
          <el-radio-button value="business">业务</el-radio-button>
          <el-radio-button value="media">媒体</el-radio-button>
          <el-radio-button value="editor">编辑</el-radio-button>
        </el-radio-group>
      </div>
      
      <!-- 图标列表 -->
      <div class="icon-grid">
        <div
          v-for="icon in filteredIcons"
          :key="icon"
          class="icon-item"
          :class="{ active: modelValue === icon }"
          @click="selectIcon(icon)"
          :title="icon"
        >
          <i :class="`ri-${icon}`"></i>
        </div>
      </div>
      
      <!-- 清除按钮 -->
      <div class="picker-footer">
        <el-button size="small" @click="clearIcon">清除</el-button>
        <el-button size="small" type="primary" @click="visible = false">确定</el-button>
      </div>
    </div>
  </el-popover>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

const props = defineProps<{
  modelValue?: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: string | undefined): void
}>()

const visible = ref(false)
const searchText = ref('')
const activeCategory = ref('all')

// 常用图标分类
const iconCategories: Record<string, string[]> = {
  system: [
    'home-line', 'home-fill', 'dashboard-line', 'dashboard-fill',
    'settings-line', 'settings-fill', 'settings-2-line', 'settings-3-line',
    'menu-line', 'menu-2-line', 'menu-3-line', 'menu-4-line',
    'apps-line', 'apps-2-line', 'grid-line', 'layout-line',
    'search-line', 'search-2-line', 'filter-line', 'filter-2-line',
    'notification-line', 'notification-fill', 'bell-line', 'bell-fill',
    'lock-line', 'lock-fill', 'lock-unlock-line', 'key-line', 'key-2-line',
    'shield-line', 'shield-check-line', 'shield-user-line',
    'database-line', 'database-2-line', 'server-line', 'cloud-line',
    'terminal-line', 'code-line', 'code-s-line', 'bug-line',
  ],
  user: [
    'user-line', 'user-fill', 'user-2-line', 'user-3-line',
    'user-add-line', 'user-follow-line', 'user-unfollow-line',
    'user-settings-line', 'user-star-line', 'user-heart-line',
    'group-line', 'group-2-line', 'team-line', 'parent-line',
    'account-circle-line', 'account-box-line', 'contacts-line',
    'admin-line', 'spy-line', 'robot-line', 'aliens-line',
  ],
  business: [
    'file-line', 'file-2-line', 'file-3-line', 'file-list-line', 'file-list-2-line',
    'folder-line', 'folder-2-line', 'folder-open-line', 'folder-add-line',
    'file-copy-line', 'file-edit-line', 'file-download-line', 'file-upload-line',
    'clipboard-line', 'task-line', 'todo-line', 'checkbox-line',
    'calendar-line', 'calendar-2-line', 'calendar-check-line', 'calendar-todo-line',
    'time-line', 'timer-line', 'alarm-line', 'history-line',
    'bar-chart-line', 'bar-chart-2-line', 'pie-chart-line', 'line-chart-line',
    'funds-line', 'stock-line', 'exchange-line', 'money-dollar-circle-line',
    'building-line', 'building-2-line', 'store-line', 'store-2-line',
    'briefcase-line', 'suitcase-line', 'archive-line', 'inbox-line',
  ],
  media: [
    'image-line', 'image-2-line', 'gallery-line', 'camera-line',
    'video-line', 'movie-line', 'film-line', 'clapperboard-line',
    'music-line', 'music-2-line', 'headphone-line', 'speaker-line',
    'mic-line', 'mic-2-line', 'volume-up-line', 'volume-down-line',
    'play-line', 'pause-line', 'stop-line', 'skip-forward-line',
    'fullscreen-line', 'picture-in-picture-line', 'aspect-ratio-line',
  ],
  editor: [
    'edit-line', 'edit-2-line', 'pencil-line', 'pen-nib-line',
    'add-line', 'add-circle-line', 'add-box-line', 'subtract-line',
    'delete-bin-line', 'delete-bin-2-line', 'close-line', 'close-circle-line',
    'check-line', 'check-double-line', 'checkbox-circle-line',
    'save-line', 'save-2-line', 'save-3-line',
    'refresh-line', 'loop-left-line', 'restart-line',
    'download-line', 'download-2-line', 'upload-line', 'upload-2-line',
    'share-line', 'share-forward-line', 'external-link-line',
    'link-line', 'unlink-line', 'attachment-line',
    'eye-line', 'eye-off-line', 'eye-close-line',
    'zoom-in-line', 'zoom-out-line', 'focus-line',
    'more-line', 'more-2-line', 'more-fill',
    'arrow-left-line', 'arrow-right-line', 'arrow-up-line', 'arrow-down-line',
    'arrow-left-s-line', 'arrow-right-s-line', 'arrow-up-s-line', 'arrow-down-s-line',
  ],
}

// 所有图标
const allIcons = computed(() => {
  const icons = new Set<string>()
  Object.values(iconCategories).forEach(category => {
    category.forEach(icon => icons.add(icon))
  })
  return Array.from(icons).sort()
})

// 过滤后的图标
const filteredIcons = computed(() => {
  let icons: string[]
  
  if (activeCategory.value === 'all') {
    icons = allIcons.value
  } else {
    icons = iconCategories[activeCategory.value] || []
  }
  
  if (searchText.value) {
    const search = searchText.value.toLowerCase()
    icons = icons.filter(icon => icon.toLowerCase().includes(search))
  }
  
  return icons
})

function selectIcon(icon: string) {
  emit('update:modelValue', icon)
}

function clearIcon() {
  emit('update:modelValue', undefined)
  visible.value = false
}
</script>

<style scoped>
.icon-picker-trigger {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  border: 1px solid var(--el-border-color);
  border-radius: 4px;
  cursor: pointer;
  min-width: 120px;
  background: var(--el-bg-color);
  transition: all 0.2s;
}

.icon-picker-trigger:hover {
  border-color: var(--el-color-primary);
}

.selected-icon {
  font-size: 20px;
  color: var(--el-text-color-primary);
}

.placeholder {
  color: var(--el-text-color-placeholder);
  font-size: 14px;
}

.arrow-icon {
  margin-left: auto;
  color: var(--el-text-color-secondary);
  transition: transform 0.2s;
}

.icon-picker-content {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.search-input {
  width: 100%;
}

.category-tabs {
  overflow-x: auto;
}

.icon-grid {
  display: grid;
  grid-template-columns: repeat(8, 1fr);
  gap: 4px;
  max-height: 240px;
  overflow-y: auto;
  padding: 4px;
}

.icon-item {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border-radius: 4px;
  cursor: pointer;
  transition: all 0.2s;
  font-size: 20px;
  color: var(--el-text-color-regular);
}

.icon-item:hover {
  background: var(--el-fill-color-light);
  color: var(--el-color-primary);
}

.icon-item.active {
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
}

.picker-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding-top: 8px;
  border-top: 1px solid var(--el-border-color-lighter);
}
</style>
