<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="dict-page">
    <div class="page-layout">
      <!-- 左侧：分类列表 -->
      <div class="cat-panel">
        <el-card shadow="never" class="cat-card">
          <template #header>
            <div class="card-header">
              <span class="card-title"><i class="bi bi-tags"></i> 字典分类</span>
              <el-button v-permission="'/system/dictionaries:cat:add'" type="primary" size="small" @click="onAddCategory">
                <i class="bi bi-plus-lg" style="margin-right: 4px;"></i>新增
              </el-button>
            </div>
          </template>

          <div class="cat-toolbar">
            <el-input v-model="catKeyword" placeholder="搜索编码/名称" clearable size="default">
              <template #prefix><i class="bi bi-search"></i></template>
            </el-input>
            <el-button circle size="default" @click="loadCategories" title="刷新">
              <i class="bi bi-arrow-clockwise"></i>
            </el-button>
          </div>

          <el-scrollbar height="calc(100vh - 320px)">
            <div class="cat-list">
              <div
                v-for="cat in filteredCategories"
                :key="cat.id"
                class="cat-item"
                :class="{ active: currentCategory?.id === cat.id }"
                @click="onOpenItems(cat)"
              >
                <div class="cat-info">
                  <span class="cat-name">{{ cat.name }}</span>
                  <span class="cat-code">{{ cat.code }}</span>
                </div>
                <el-tag size="small" type="info">{{ categoryText(cat.category) }}</el-tag>
              </div>
              <el-empty v-if="!filteredCategories.length" description="暂无分类" :image-size="60" />
            </div>
          </el-scrollbar>
        </el-card>
      </div>

      <!-- 右侧：条目列表 -->
      <div class="item-panel">
        <el-card shadow="never" class="item-card">
          <template #header>
            <div class="card-header">
              <div class="card-title-section">
                <span class="card-title"><i class="bi bi-list-ul"></i> 字典条目</span>
                <el-tag v-if="currentCategory?.name" type="primary" size="default">{{ currentCategory?.name }}</el-tag>
              </div>
              <div v-if="currentCategory" class="card-actions">
                <el-button v-permission="'/system/dictionaries:item:add'" size="small" @click="onAddItem"><i class="bi bi-plus-lg" style="margin-right: 4px;"></i>新增条目</el-button>
                <el-tooltip content="复制 C# 后端调用代码" placement="top">
                  <el-button size="small" @click="copyCSharpUsage"><i class="bi bi-filetype-cs" style="margin-right: 4px;"></i>C#</el-button>
                </el-tooltip>
                <el-tooltip content="复制 Vue 前端调用代码" placement="top">
                  <el-button size="small" @click="copyVueUsage"><i class="bi bi-filetype-vue" style="margin-right: 4px;"></i>Vue</el-button>
                </el-tooltip>
                <el-button v-permission="'/system/dictionaries:cat:edit'" size="small" @click="onEditCategory(currentCategory)">编辑分类</el-button>
                <el-button v-permission="'/system/dictionaries:cat:delete'" size="small" type="danger" @click="onDeleteCategory(currentCategory)">删除分类</el-button>
              </div>
            </div>
          </template>

          <div v-if="!currentCategory" class="empty-state">
            <i class="bi bi-cursor"></i>
            <p>请点击左侧分类查看条目</p>
          </div>

          <!-- 层级型：树表 -->
          <DataTable
            v-else-if="isHierarchy"
            :data="itemTreeData"
            :loading="loadingItems"
            :columns="itemColumns"
            :tree-config="{ children: 'children', expandAll: true }"
            :action-column-width="180"
            :compact-mode="true"
            cache-key="dict-items-tree"
          >
            <template #actions="{ row }">
              <el-button v-permission="'/system/dictionaries:item:edit'" size="small" link @click="onEditItem(row)">编辑</el-button>
              <el-button v-permission="'/system/dictionaries:item:delete'" size="small" type="danger" link @click="onDeleteItem(row)">删除</el-button>
            </template>
          </DataTable>

          <!-- 其他类型：平铺表 -->
          <DataTable
            v-else
            :data="itemPageData"
            :loading="loadingItems"
            :columns="itemColumns"
            :pagination="itemPagination"
            :action-column-width="180"
            :compact-mode="true"
            cache-key="dict-items"
            @page-change="paginateItems"
            @size-change="paginateItems"
          >
            <template #actions="{ row }">
              <el-button v-permission="'/system/dictionaries:item:edit'" size="small" link @click="onEditItem(row)">编辑</el-button>
              <el-button v-permission="'/system/dictionaries:item:delete'" size="small" type="danger" link @click="onDeleteItem(row)">删除</el-button>
            </template>
          </DataTable>
        </el-card>
      </div>
    </div>

    <!-- 分类对话框 -->
    <el-dialog v-model="catDialogVisible" width="560px" :close-on-click-modal="false">
      <template #header><div class="dlg-header"><span>{{ catDialogTitle }}</span><AdminLangSwitcher v-if="multiLangEnabled" /></div></template>
      <el-form :model="catForm" label-width="90px">
        <el-form-item label="编码" required><el-input v-model="catForm.code" maxlength="64" show-word-limit /></el-form-item>
        <el-form-item label="名称" required>
          <LangInput v-if="multiLangEnabled" v-model="catForm.nameI18n" placeholder="字典名称" />
          <el-input v-else v-model="catForm.name" maxlength="64" show-word-limit />
        </el-form-item>
        <el-form-item label="类型" required>
          <el-select v-model="catForm.category" style="width: 100%">
            <el-option label="STATIC（静态字典）" value="STATIC" />
            <el-option label="DYNAMIC（动态字典）" value="DYNAMIC" />
            <el-option label="MAPPING（映射字典）" value="MAPPING" />
            <el-option label="HIERARCHY（层级字典）" value="HIERARCHY" />
            <el-option label="CONFIG（配置字典）" value="CONFIG" />
            <el-option label="MULTI_LANG（多语言字典）" value="MULTI_LANG" />
            <el-option label="REFERENCE（引用字典）" value="REFERENCE" />
          </el-select>
        </el-form-item>
        <el-form-item label="来源"><el-input v-model="catForm.sourceType" /></el-form-item>
        <el-form-item label="启用"><el-switch v-model="catForm.enabled" /></el-form-item>
        <el-form-item label="描述">
          <LangInput v-if="multiLangEnabled" v-model="catForm.descriptionI18n" :is-textarea="true" :rows="2" placeholder="字典描述" />
          <el-input v-else v-model="catForm.description" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="catDialogVisible=false">取消</el-button>
        <el-button type="primary" @click="saveCategory">保存</el-button>
      </template>
    </el-dialog>

    <!-- 条目对话框 -->
    <el-dialog v-model="itemDialogVisible" width="480px" :close-on-click-modal="false">
      <template #header><div class="dlg-header"><span>{{ itemDialogTitle }}</span><AdminLangSwitcher v-if="multiLangEnabled" /></div></template>
      <el-form :model="itemForm" label-width="90px">
        <el-form-item label="键" required><el-input v-model="itemForm.itemKey" maxlength="128" /></el-form-item>
        <el-form-item label="值" required>
          <LangInput v-if="multiLangEnabled" v-model="itemForm.valueI18n" placeholder="条目值" />
          <el-input v-else v-model="itemForm.itemValue" maxlength="256" />
        </el-form-item>
        <el-form-item label="排序"><el-input-number v-model="itemForm.order" :min="0" :max="999999" style="width: 100%" /></el-form-item>
        <el-form-item label="启用"><el-switch v-model="itemForm.enabled" /></el-form-item>
        <el-form-item v-if="currentCategory?.category==='HIERARCHY'" label="上级">
          <el-tree-select v-model="itemForm.parentId" :data="parentTree" node-key="id" :props="{ label: 'text', value: 'id', children: 'children' }" check-strictly clearable style="width: 100%" placeholder="留空为顶级" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="itemDialogVisible=false">取消</el-button>
        <el-button type="primary" @click="saveItem">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import DataTable from '../../../components/DataTable/index.vue'
import LangInput from '@/components/framework/LangInput.vue'
import AdminLangSwitcher from '@/components/framework/AdminLangSwitcher.vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getDefaultLang, useMultiLangEnabled } from '@/utils/lang'

const multiLangEnabled = useMultiLangEnabled()
import type { DictionaryCategoryListItem, DictionaryCategoryDetail, CreateDictionaryCategoryInput, UpdateDictionaryCategoryInput, DictItemListItem, CreateDictItemInput } from '../../../api/dictionary'
import { getDictionaryCategories, getDictionaryCategoryDetail, createDictionaryCategory, updateDictionaryCategory, deleteDictionaryCategory, getDictionaryItems, getDictionaryItemDetail, createDictionaryItem, updateDictionaryItem, deleteDictionaryItem } from '../../../api/dictionary'

const loadingCats = ref(false)
const categories = ref<DictionaryCategoryListItem[]>([])
const catKeyword = ref('')

const filteredCategories = computed(() => {
  const kw = catKeyword.value.trim().toLowerCase()
  const src = Array.isArray(categories.value) ? categories.value : []
  return kw ? src.filter(c => ((c.code ?? '') + ' ' + (c.name ?? '')).toLowerCase().includes(kw)) : src
})

async function loadCategories() {
  loadingCats.value = true
  try { categories.value = await getDictionaryCategories() } catch { categories.value = []; ElMessage.error('加载分类失败') } finally { loadingCats.value = false }
}

function categoryText(c?: string) {
  switch ((c || '').toUpperCase()) {
    case 'STATIC': return '静态'
    case 'DYNAMIC': return '动态'
    case 'MAPPING': return '映射'
    case 'HIERARCHY': return '层级'
    case 'CONFIG': return '配置'
    case 'MULTI_LANG': return '多语言'
    case 'REFERENCE': return '引用'
    default: return c || ''
  }
}

const currentCategory = ref<DictionaryCategoryDetail | null>(null)
const loadingItems = ref(false)
const items = ref<DictItemListItem[]>([])
const itemPagination = ref({ page: 1, pageSize: 20, total: 0 })
const itemColumns = [
  { prop: 'itemKey', label: '键', minWidth: 160 },
  { prop: 'itemValue', label: '值', minWidth: 200 },
  { prop: 'order', label: '排序', width: 80 },
  { prop: 'enabled', label: '启用', width: 70 }
]

const isHierarchy = computed(() => currentCategory.value?.category === 'HIERARCHY')
const itemPageData = computed(() => {
  itemPagination.value.total = items.value.length
  const start = (itemPagination.value.page - 1) * itemPagination.value.pageSize
  return items.value.slice(start, start + itemPagination.value.pageSize)
})

const itemTreeData = computed(() => {
  const map = new Map<string, any>()
  const roots: any[] = []
  items.value.forEach(i => map.set(i.id, { ...i, children: [] as any[] }))
  items.value.forEach(i => {
    const node = map.get(i.id)
    if (i.parentId && map.has(i.parentId)) map.get(i.parentId).children.push(node)
    else roots.push(node)
  })
  const sortNodes = (arr: any[]) => { arr.sort((a, b) => (a.order || 0) - (b.order || 0)); arr.forEach(n => n.children && sortNodes(n.children)) }
  sortNodes(roots)
  return roots
})

async function onOpenItems(row: DictionaryCategoryListItem) {
  try {
    currentCategory.value = await getDictionaryCategoryDetail(row.id)
    await loadItems(row.id)
  } catch { currentCategory.value = null; items.value = []; ElMessage.error('加载分类详情失败') }
}

async function loadItems(categoryId: string) {
  loadingItems.value = true
  try { items.value = await getDictionaryItems(categoryId); itemPagination.value.page = 1; itemPagination.value.total = items.value.length; buildParentTree() } catch { items.value = []; ElMessage.error('加载条目失败') } finally { loadingItems.value = false }
}

function paginateItems() {}

// 分类对话框
const catDialogVisible = ref(false)
const catDialogTitle = ref('新增分类')
const catForm = ref<any>({ code: '', name: '', nameI18n: '', category: 'STATIC', enabled: true, sourceType: '', description: '', descriptionI18n: '', extraJson: '' })

function onAddCategory() {
  catDialogTitle.value = '新增分类'
  catForm.value = { code: '', name: '', nameI18n: '', category: 'STATIC', enabled: true, sourceType: '', description: '', descriptionI18n: '', extraJson: '' }
  catDialogVisible.value = true
}

async function onEditCategory(row: DictionaryCategoryListItem) {
  catDialogTitle.value = '编辑分类'
  try {
    const detail = await getDictionaryCategoryDetail(row.id)
    catForm.value = { code: detail.code, name: detail.name, nameI18n: detail.nameI18n || '', category: detail.category, enabled: detail.enabled, sourceType: detail.sourceType || '', description: detail.description || '', descriptionI18n: detail.descriptionI18n || '', extraJson: detail.extraJson || '' }
    ;(catForm.value as any)._id = detail.id
    catDialogVisible.value = true
  } catch { ElMessage.error('读取分类失败') }
}

async function onDeleteCategory(row: DictionaryCategoryListItem) {
  try {
    await ElMessageBox.confirm(`确定删除分类「${row.name}」?`, '提示', { type: 'warning' })
    await deleteDictionaryCategory(row.id)
    ElMessage.success('已删除')
    if (currentCategory?.value?.id === row.id) { currentCategory.value = null; items.value = [] }
    await loadCategories()
  } catch {}
}

async function saveCategory() {
  try {
    // 多语言开启时从 JSON 提取默认语言值填充 name/description
    const data = { ...catForm.value }
    if (multiLangEnabled.value && data.nameI18n) {
      try { const obj = JSON.parse(data.nameI18n); data.name = obj[getDefaultLang()] || obj['zh-CN'] || Object.values(obj).find((v: any) => v?.trim()) || data.name } catch {}
    }
    if (multiLangEnabled.value && data.descriptionI18n) {
      try { const obj = JSON.parse(data.descriptionI18n); data.description = obj[getDefaultLang()] || obj['zh-CN'] || Object.values(obj).find((v: any) => v?.trim()) || data.description } catch {}
    }
    if (!data.code?.trim() || !data.name?.trim()) { ElMessage.warning('请填写编码与名称'); return }
    if ((catForm.value as any)._id) {
      await updateDictionaryCategory((catForm.value as any)._id, catForm.value as UpdateDictionaryCategoryInput)
      ElMessage.success('保存成功')
    } else {
      await createDictionaryCategory(catForm.value)
      ElMessage.success('创建成功')
    }
    catDialogVisible.value = false
    await loadCategories()
  } catch (e: any) { ElMessage.error(e?.message || '操作失败') }
}

// 条目对话框
const itemDialogVisible = ref(false)
const itemDialogTitle = ref('新增条目')
const itemForm = ref<any>({ categoryId: '', itemKey: '', itemValue: '', valueI18n: '', order: 0, enabled: true, parentId: undefined })
const parentTree = ref<any[]>([])

function buildParentTree(excludeId?: string) {
  if (!currentCategory.value || currentCategory.value.category !== 'HIERARCHY') { parentTree.value = []; return }
  const map = new Map<string, any>()
  items.value.forEach(i => { if (i.id !== excludeId) map.set(i.id, { id: i.id, text: `${i.itemValue}(${i.itemKey})`, parentId: i.parentId, children: [] as any[] }) })
  const roots: any[] = []
  map.forEach(n => { if (n.parentId && map.has(n.parentId)) map.get(n.parentId).children.push(n); else roots.push(n) })
  parentTree.value = roots
}

function onAddItem() {
  if (!currentCategory.value) return
  itemDialogTitle.value = '新增条目'
  itemForm.value = { categoryId: currentCategory.value.id, itemKey: '', itemValue: '', valueI18n: '', order: 0, enabled: true, parentId: undefined }
  itemDialogVisible.value = true
  buildParentTree()
}

async function onEditItem(row: DictItemListItem) {
  if (!currentCategory.value) return
  itemDialogTitle.value = '编辑条目'
  const detail = await getDictionaryItemDetail(row.id)
  itemForm.value = { categoryId: currentCategory.value.id, itemKey: detail.itemKey, itemValue: detail.itemValue, valueI18n: detail.valueI18n || '', order: detail.order, enabled: detail.enabled, parentId: detail.parentId }
  ;(itemForm.value as any)._id = detail.id
  itemDialogVisible.value = true
  buildParentTree(detail.id)
}

async function onDeleteItem(row: DictItemListItem) {
  try {
    await ElMessageBox.confirm(`确定删除条目「${row.itemKey}」？`, '提示', { type: 'warning' })
    await deleteDictionaryItem(row.id)
    ElMessage.success('删除成功')
    await loadItems(currentCategory!.value!.id)
  } catch {}
}

async function saveItem() {
  try {
    // 多语言开启时从 JSON 提取默认语言值填充 itemValue
    const data = { ...itemForm.value }
    if (multiLangEnabled.value && data.valueI18n) {
      try { const obj = JSON.parse(data.valueI18n); data.itemValue = obj[getDefaultLang()] || obj['zh-CN'] || Object.values(obj).find((v: any) => v?.trim()) || data.itemValue } catch {}
    }
    if (!data.itemKey?.trim() || !data.itemValue?.trim()) { ElMessage.warning('请填写键和值'); return }
    if ((data as any)._id) {
      await updateDictionaryItem((data as any)._id, { itemKey: data.itemKey, itemValue: data.itemValue, valueI18n: data.valueI18n || null, order: data.order, enabled: data.enabled, parentId: data.parentId })
      ElMessage.success('保存成功')
    } else {
      await createDictionaryItem(itemForm.value)
      ElMessage.success('创建成功')
    }
    itemDialogVisible.value = false
    await loadItems(currentCategory!.value!.id)
  } catch (e: any) { ElMessage.error(e?.message || '操作失败') }
}

// ---------- 复制调用代码 ----------
function copyCSharpUsage() {
  if (!currentCategory.value) return
  const code = currentCategory.value.code
  const name = currentCategory.value.name
  const snippet = `// ${name}（字典编码：${code}）
// 方式一：通过 IDictionaryAppService 注入获取
// 构造函数注入 IDictionaryAppService dictService
var items = await dictService.GetItemsByCodesAsync(new[] { "${code}" });
var ${camelCase(code)}List = items["${code}"]; // List<DictionaryItemListItemDto>
// 遍历使用
foreach (var item in ${camelCase(code)}List)
{
    // item.ItemKey   — 字典键
    // item.ItemValue — 字典值
}

// 方式二：通过 ISqlSugarClient 直接查询
var dictItems = await db.Queryable<DictionaryItem>()
    .InnerJoin<DictionaryCategory>((i, c) => i.CategoryId == c.Id)
    .Where((i, c) => c.Code == "${code}" && i.Enabled)
    .OrderBy(i => i.Order)
    .Select(i => new { i.ItemKey, i.ItemValue })
    .ToListAsync();`
  copyToClipboard(snippet, 'C# 调用代码已复制到剪贴板')
}

function copyVueUsage() {
  if (!currentCategory.value) return
  const code = currentCategory.value.code
  const name = currentCategory.value.name
  const snippet = `// ${name}（字典编码：${code}）
import { useDictionary } from '@/composables/useDictionary'

// 在 setup 中调用
const { ${camelCase(code)} } = useDictionary('${code}')

// 模板中使用（${camelCase(code)}.value 是数组）
// <el-select v-model="form.xxx">
//   <el-option
//     v-for="item in ${camelCase(code)}"
//     :key="item.itemKey"
//     :label="item.itemValue"
//     :value="item.itemKey"
//   />
// </el-select>

// 也可通过 dicts 统一访问
// const { dicts } = useDictionary('${code}')
// dicts.value.${code} // => DictItemListItem[]`
  copyToClipboard(snippet, 'Vue 调用代码已复制到剪贴板')
}

function camelCase(str: string) {
  return str.replace(/_([a-z])/g, (_, c) => c.toUpperCase())
}

async function copyToClipboard(text: string, successMsg: string) {
  try {
    await navigator.clipboard.writeText(text)
    ElMessage.success(successMsg)
  } catch {
    // 回退方案
    const ta = document.createElement('textarea')
    ta.value = text
    ta.style.position = 'fixed'
    ta.style.opacity = '0'
    document.body.appendChild(ta)
    ta.select()
    document.execCommand('copy')
    document.body.removeChild(ta)
    ElMessage.success(successMsg)
  }
}

onMounted(loadCategories)
</script>

<style scoped>
.dict-page {
  padding: 24px;
  background: var(--el-bg-color-page);
  min-height: 100vh;
}

.page-layout {
  display: grid;
  grid-template-columns: 320px 1fr;
  gap: 24px;
  align-items: start;
}

/* 左侧分类面板 */
.cat-panel { position: sticky; top: 24px; }

.cat-card, .item-card {
  border-radius: 12px;
  border: 1px solid #e5e7eb;
}

.admin-dark .cat-card, .admin-dark .item-card {
  background: #1f2937;
  border-color: #374151;
}

.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.card-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  font-size: 15px;
  color: #1f2937;
}

.card-title i { color: #8b5cf6; font-size: 18px; }

.admin-dark .card-title { color: #f9fafb; }

.card-title-section {
  display: flex;
  align-items: center;
  gap: 12px;
}

.card-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  justify-content: flex-end;
}

/* 分类工具栏 */
.cat-toolbar {
  display: flex;
  gap: 8px;
  margin-bottom: 12px;
}

.cat-toolbar .el-input { flex: 1; }

/* 分类列表 */
.cat-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.cat-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
}

.cat-item:hover { background: #f1f5f9; }
.admin-dark .cat-item:hover { background: #334155; }

.cat-item.active {
  background: rgba(139, 92, 246, 0.1);
  border-left: 3px solid #8b5cf6;
}

.admin-dark .cat-item.active { background: rgba(139, 92, 246, 0.2); }

.cat-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.cat-name {
  font-size: 14px;
  font-weight: 500;
  color: #1f2937;
}

.admin-dark .cat-name { color: #f1f5f9; }

.cat-code {
  font-size: 12px;
  color: #6b7280;
}

.admin-dark .cat-code { color: #9ca3af; }

/* 空状态 */
.empty-state {
  text-align: center;
  padding: 60px 20px;
  color: #9ca3af;
}

.empty-state i {
  font-size: 48px;
  margin-bottom: 16px;
  display: block;
  opacity: 0.5;
}

.empty-state p { margin: 0; font-size: 14px; }

/* 对话框 */
:deep(.el-dialog) { border-radius: 12px; }

:deep(.el-dialog__header) {
  background: linear-gradient(to right, #f9fafb 0%, #ffffff 100%);
  border-bottom: 1px solid #e5e7eb;
  padding: 20px 24px;
  margin: 0;
}

.admin-dark :deep(.el-dialog__header) {
  background: linear-gradient(to right, #1f2937 0%, #1a2332 100%);
  border-bottom-color: #374151;
}

:deep(.el-dialog__title) { font-size: 18px; font-weight: 600; color: #1f2937; }
.admin-dark :deep(.el-dialog__title) { color: #f9fafb; }

:deep(.el-dialog__body) { padding: 24px; }

:deep(.el-dialog__footer) {
  padding: 16px 24px;
  border-top: 1px solid #f3f4f6;
}

.admin-dark :deep(.el-dialog__footer) { border-top-color: #374151; }

:deep(.el-form-item__label) { font-weight: 500; color: #374151; }
.admin-dark :deep(.el-form-item__label) { color: #e5e7eb; }

/* 响应式 */
@media (max-width: 900px) {
  .page-layout { grid-template-columns: 1fr; }
  .cat-panel { position: static; }
}

@media (max-width: 768px) {
  .dict-page { padding: 16px; }
}

/* 对话框标题+语言切换器 */
.dlg-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}
.dlg-header span {
  font-size: 18px;
  font-weight: 600;
}
</style>
