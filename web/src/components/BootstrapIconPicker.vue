<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="bi-picker">
    <el-input v-model="inner" placeholder="输入图标名，如: house" readonly>
      <template #prefix>
        <i class="bi" :class="previewClass" v-if="inner"/>
      </template>
      <template #suffix>
        <el-button text type="primary" size="small" @click="dialogVisible = true" :disabled="disabled">选择图标</el-button>
      </template>
    </el-input>

    <el-dialog v-model="dialogVisible" title="选择 Bootstrap 图标" width="900px">
      <el-input v-model="keyword" placeholder="搜索图标..." clearable class="mb-3">
        <template #prefix><i class="bi bi-search"></i></template>
      </el-input>
      <div class="search-stats">共 {{ filtered.length }} 个图标</div>
      <el-scrollbar height="500px">
        <div class="icon-grid">
          <div v-for="ic in paginatedIcons" :key="ic" class="icon-item" :class="{ active: isSelected(ic) }" @click="pick(ic)">
            <i class="bi" :class="'bi-'+ic"></i>
            <span class="icon-name">{{ ic }}</span>
          </div>
        </div>
      </el-scrollbar>
      <el-pagination v-model:current-page="currentPage" v-model:page-size="pageSize" :page-sizes="[50,100,200]" :total="filtered.length" layout="total,sizes,prev,pager,next" background class="mt-3" />
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="confirmSelection">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
interface Props { modelValue?: string; disabled?: boolean }
const props = defineProps<Props>()
const emit = defineEmits<{ (e:'update:modelValue', v:string):void }>()
const inner = ref(props.modelValue || '')
const dialogVisible = ref(false)
const keyword = ref('')
const currentPage = ref(1)
const pageSize = ref(100)
watch(() => props.modelValue, v => { if (v !== inner.value) inner.value = v || '' })
const ICONS = ['house','gear','person','people','grid','list','bell','chat','envelope','calendar','bookmark','folder','file','image','camera','cloud','upload','download','search','filter','star','check','x','trash','pencil','plus','dash','arrow-up','arrow-down','arrow-left','arrow-right','link','lock','unlock','key','box','diagram-3','card-text','cart','columns','collection','cpu','database','display','file-earmark','folder2','globe','layers','layout-text-window','list-ul','map','menu-up','shield','sliders','speedometer','tags','tools','tornado']
const filtered = computed(() => { if (!keyword.value.trim()) return ICONS; const kw = keyword.value.trim().toLowerCase(); return ICONS.filter(i => i.includes(kw)) })
const paginatedIcons = computed(() => { const start = (currentPage.value - 1) * pageSize.value; return filtered.value.slice(start, start + pageSize.value) })
const previewClass = computed(() => { if (!inner.value) return ''; return inner.value.startsWith('bi-') ? inner.value : ('bi-' + inner.value) })
function pick(name: string) { inner.value = name }
function isSelected(name: string): boolean { return inner.value === name || inner.value === ('bi-' + name) }
function confirmSelection() { emit('update:modelValue', inner.value); dialogVisible.value = false }
watch(keyword, () => { currentPage.value = 1 })
</script>

<style scoped>
.bi-picker { width: 100%; }
.search-stats { font-size: 13px; color: #6c757d; margin-bottom: 12px; }
.icon-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(110px, 1fr)); gap: 12px; }
.icon-item { display: flex; flex-direction: column; align-items: center; gap: 8px; padding: 16px 8px; border: 2px solid #e5e7eb; border-radius: 12px; cursor: pointer; transition: all 0.3s ease; }
.icon-item:hover { background: #f3f4f6; border-color: #3b82f6; transform: translateY(-2px); }
.icon-item.active { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); border-color: #667eea; color: white; }
.icon-item i { font-size: 28px; transition: all 0.3s ease; }
.icon-item:hover i { color: #3b82f6; transform: scale(1.15); }
.icon-item.active i { color: white; transform: scale(1.2); }
.icon-name { font-size: 12px; color: #6c757d; text-align: center; }
.icon-item:hover .icon-name { color: #3b82f6; font-weight: 500; }
.icon-item.active .icon-name { color: white; font-weight: 600; }
</style>
