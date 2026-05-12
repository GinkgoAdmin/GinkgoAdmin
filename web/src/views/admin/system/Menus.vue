<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="page-container">
    <DataTable
      :key="tableKey"
      :data="filteredTableData"
      :loading="loading"
      :columns="columns"
      :tree-config="{ children: 'children', expandAll: treeExpandAll }"
      :show-selection="true"
      :show-column-settings="true"
      :show-export="true"
      :search-config="searchConfig"
      cache-key="menus-admin"
      :compact-mode="true"
      :action-column-width="200"
      @search="handleSearch"
      @selection-change="onSelectionChange"
    >
      <template #header>
        <div class="menu-header-row">
          <div>
            <h2>菜单管理</h2>
            <p>管理系统菜单和导航结构</p>
          </div>
          <el-button type="primary" plain size="small" @click="goMenuGroups">
            <el-icon style="margin-right:4px"><Menu /></el-icon>导航菜单管理
          </el-button>
        </div>
      </template>

      <template #header-actions>
        <el-button v-permission="'/system/menus:add'" type="primary" @click="handleAdd">新增</el-button>
        <el-button v-permission="'/system/menus:batchdelete'" type="danger" :disabled="selectedIds.length===0" @click="handleBatchDelete">批量删除</el-button>
        <el-button @click="expandAll">全部展开</el-button>
        <el-button @click="collapseAll">全部折叠</el-button>
      </template>

      <template #column-name="{ row }">
        <div class="menu-name-cell">
          <i :class="typeIcon(row.type)" class="menu-type-icon" :style="{ color: typeColor(row.type) }"></i>
          <el-link type="primary" underline="never" class="name-text" @click="handleEdit(row)">{{ row.name }}</el-link>
        </div>
      </template>

      <template #column-type="{ row }">
        <span class="type-badge" :class="'type-'+(row.type||'Menu').toLowerCase()">{{ typeText(row.type) }}</span>
      </template>

      <template #column-enabled="{ row }">
        <el-switch :model-value="row.enabled" @change="(v:boolean)=>toggleEnabled(row,v)" />
      </template>

      <template #actions="{ row }">
        <el-button v-permission="'/system/menus:info'" type="primary" link size="small" @click="handleView(row)">查看</el-button>
        <el-button v-permission="'/system/menus:edit'" type="warning" link size="small" @click="handleEdit(row)">编辑</el-button>
        <el-button v-permission="'/system/menus:copy'" type="success" link size="small" @click="handleCopy(row)">复制</el-button>
        <el-button v-permission="'/system/menus:delete'" type="danger" link size="small" @click="handleDelete(row)">删除</el-button>
      </template>
    </DataTable>

    <!-- 菜单编辑对话框 -->
    <el-dialog 
      v-model="dialogVisible" 
      :title="dialogTitle" 
      width="900px" 
      top="5vh"
      :close-on-click-modal="false"
      class="menu-edit-dialog"
    >
      <!-- Header（含语言切换器） -->
      <div class="dialog-header">
        <div>
          <div class="header-text">{{ dialogTitle }}</div>
          <div class="sub-header-text">{{ dialogSubTitle }}</div>
        </div>
        <AdminLangSwitcher v-if="!isViewMode && multiLangEnabled" />
      </div>

      <el-scrollbar max-height="70vh">
        <el-form :model="form" :rules="formRules" ref="formRef" label-width="120px" class="menu-form">
          <!-- 基本信息 -->
          <div class="form-section">
            <div class="section-title">基本信息</div>
            <el-form-item label="名称" prop="name">
              <LangInput v-if="multiLangEnabled" v-model="form.nameI18n" :placeholder="'请输入菜单名称'" />
              <el-input v-else v-model="form.name" :readonly="isViewMode" placeholder="请输入菜单名称" />
            </el-form-item>

            <el-form-item label="路由" prop="route">
              <el-input v-model="form.route" :readonly="isViewMode" placeholder="请输入路由标识" />
            </el-form-item>

            <el-form-item label="类型" prop="type">
              <el-select v-model="form.type" :disabled="isViewMode" @change="onTypeChange" style="width: 100%">
                <el-option label="目录 (Directory)" value="Directory" />
                <el-option label="菜单 (Menu)" value="Menu" />
                <el-option label="项 (Item)" value="Item" />
                <el-option label="按钮 (Button)" value="Button" />
                <el-option label="接口 (Api)" value="Api" />
              </el-select>
            </el-form-item>

            <el-form-item label="父级菜单" prop="parentId">
              <el-tree-select
                v-model="parentId"
                :data="parentTree"
                node-key="id"
                :props="{ label: 'name', value: 'id', children: 'children' }"
                :render-after-expand="false"
                check-strictly
                :disabled="isViewMode"
                placeholder="（顶级菜单）"
                style="width: 100%"
              />
            </el-form-item>

            <el-form-item label="图标" prop="icon">
              <BootstrapIconPicker v-model="form.icon" :disabled="isViewMode" />
            </el-form-item>

            <!-- Item模式相关 -->
            <el-form-item v-if="form.type === 'Item'" label="显示模式" prop="itemMode">
              <el-select v-model="form.itemMode" :disabled="isViewMode" @change="onItemModeChange" style="width: 100%">
                <el-option label="Tab (标签页)" value="Tab" />
                <el-option label="Link (链接)" value="Link" />
              </el-select>
            </el-form-item>

            <el-form-item v-if="form.type === 'Item' && form.itemMode === 'Link'" label="链接地址" prop="url">
              <el-input v-model="form.url" :readonly="isViewMode" placeholder="请输入链接地址" />
            </el-form-item>

            <el-form-item label="顺序" prop="order">
              <el-input-number v-model="form.order" :readonly="isViewMode" :min="0" style="width: 100%" />
            </el-form-item>

            <el-form-item label="启用" prop="enabled">
              <el-switch v-model="form.enabled" :disabled="isViewMode" />
            </el-form-item>
          </div>

          <!-- 多客户端配置 -->
          <div class="form-section">
            <div class="section-title">多客户端配置</div>
            <el-form-item label="支持的客户端">
              <el-checkbox-group v-model="supportedClientsArr" :disabled="isViewMode" @change="onClientSupportChange">
                <el-checkbox value="WPF">WPF</el-checkbox>
                <el-checkbox value="WEB">Web</el-checkbox>
                <el-checkbox value="MOBILE">Mobile</el-checkbox>
              </el-checkbox-group>
            </el-form-item>

            <!-- WPF配置 -->
            <div v-if="supportedClientsArr.includes('WPF')" class="client-config-panel wpf-panel">
              <div class="panel-title">WPF 客户端配置</div>
              <el-form-item label="显示模式">
                <el-select v-model="form.wpfDisplayMode" :disabled="isViewMode" style="width: 100%">
                  <el-option label="Route (路由)" value="Route" />
                  <el-option label="URL (链接)" value="URL" />
                  <el-option label="External (外部)" value="External" />
                </el-select>
              </el-form-item>
              <el-form-item label="地址">
                <el-input v-model="form.wpfRouteUrl" :readonly="isViewMode" placeholder="请输入地址" />
              </el-form-item>
            </div>

            <!-- Web配置 -->
            <div v-if="supportedClientsArr.includes('WEB')" class="client-config-panel web-panel">
              <div class="panel-title">Web 客户端配置</div>
              <el-form-item label="显示模式">
                <el-select v-model="form.webDisplayMode" :disabled="isViewMode" style="width: 100%">
                  <el-option label="Route (路由)" value="Route" />
                  <el-option label="URL (链接)" value="URL" />
                  <el-option label="External (外部)" value="External" />
                </el-select>
              </el-form-item>
              <el-form-item label="地址">
                <el-input v-model="form.webRouteUrl" :readonly="isViewMode" placeholder="请输入地址" />
              </el-form-item>
            </div>

            <!-- Mobile配置 -->
            <div v-if="supportedClientsArr.includes('MOBILE')" class="client-config-panel mobile-panel">
              <div class="panel-title">Mobile 客户端配置</div>
              <el-form-item label="显示模式">
                <el-select v-model="form.mobileDisplayMode" :disabled="isViewMode" style="width: 100%">
                  <el-option label="Route (路由)" value="Route" />
                  <el-option label="URL (链接)" value="URL" />
                  <el-option label="External (外部)" value="External" />
                </el-select>
              </el-form-item>
              <el-form-item label="地址">
                <el-input v-model="form.mobileRouteUrl" :readonly="isViewMode" placeholder="请输入地址" />
              </el-form-item>
      </div>
    </div>
    
          <!-- 权限配置 -->
          <div v-if="form.type === 'Button' || form.type === 'Api'" class="form-section">
            <div class="section-title">权限配置</div>
            <el-form-item label="权限代码" prop="code">
              <el-input v-model="form.code" :readonly="isViewMode" placeholder="请输入权限代码" />
            </el-form-item>
            <el-form-item label="资源" prop="resource">
              <el-input v-model="form.resource" :readonly="isViewMode" placeholder="请输入资源标识" />
            </el-form-item>
            <el-form-item label="方法" prop="method">
              <el-input v-model="form.method" :readonly="isViewMode" placeholder="请输入方法名" />
            </el-form-item>
      </div>
        </el-form>
      </el-scrollbar>

      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button v-if="!isViewMode" type="primary" @click="submitForm">{{ dialogMode === 'edit' ? '保存' : '创建' }}</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { Menu } from '@element-plus/icons-vue'
import type { FormInstance, FormRules } from 'element-plus'
import type { SearchFieldConfig } from '../../../components/DataTable/types'
import DataTable from '../../../components/DataTable/index.vue'
import { getAdminMenusTree, type AdminMenuNode, createMenu, updateMenu, deleteMenu, batchDeleteMenus, getMenuDetail, type MenuDetail } from '../../../api/menu'
import { ElMessage, ElMessageBox } from 'element-plus'
// @ts-ignore
import BootstrapIconPicker from '../../../components/BootstrapIconPicker.vue'
import LangInput from '@/components/framework/LangInput.vue'
import AdminLangSwitcher from '@/components/framework/AdminLangSwitcher.vue'
import { getDefaultLang, parseLang, useMultiLangEnabled } from '@/utils/lang'
import { filterSearchableTree } from './treeSearch.utils'

const router = useRouter()
const multiLangEnabled = useMultiLangEnabled()

function goMenuGroups() {
  router.push({ name: 'menu-groups' })
}

type DialogMode = 'add' | 'edit' | 'view' | 'copy'

const loading = ref(false)
const tableData = ref<AdminMenuNode[]>([])
const menuKeyword = ref('')
const selectedIds = ref<string[]>([])
const treeExpandAll = ref(false)
const tableKey = ref(0)

const searchConfig: SearchFieldConfig[] = [
  {
    key: 'keyword',
    label: '\u5173\u952e\u5b57',
    type: 'input',
    placeholder: '\u641c\u7d22\u540d\u79f0\u3001\u8def\u7531\u6216Code',
    clearable: true,
    simple: true,
    width: 280
  }
]

const columns = [
  { prop: 'name', label: '名称', minWidth: 220, slot: 'column-name' },
  { prop: 'type', label: '类型', width: 100, slot: 'column-type' },
  { prop: 'route', label: '路由/标识', minWidth: 140 },
  { prop: 'code', label: 'Code', minWidth: 180 },
  { prop: 'webRouteUrl', label: 'WEB路由', minWidth: 160 },
  { prop: 'supportedClients', label: '支持客户端', width: 140 },
  { prop: 'enabled', label: '启用', width: 80, slot: 'column-enabled' }
]

const filteredTableData = computed(() => filterSearchableTree(tableData.value, menuKeyword.value))

function typeText(t?: string) {
  const map: Record<string, string> = { directory: '目录', menu: '菜单', item: '菜单项', button: '按钮', api: '接口' }
  return map[(t || '').toLowerCase()] || '菜单'
}

// 类型图标
function typeIcon(t?: string): string {
  const map: Record<string, string> = {
    directory: 'bi bi-folder-fill',
    menu: 'bi bi-window',
    item: 'bi bi-file-earmark-text',
    button: 'bi bi-hand-index-fill',
    api: 'bi bi-hdd-network-fill'
  }
  return map[(t || '').toLowerCase()] || 'bi bi-circle'
}

// 类型颜色
function typeColor(t?: string): string {
  const map: Record<string, string> = {
    directory: '#f59e0b',
    menu: '#3b82f6',
    item: '#6366f1',
    button: '#10b981',
    api: '#ef4444'
  }
  return map[(t || '').toLowerCase()] || '#9ca3af'
}

async function loadTree() {
  loading.value = true
  try {
    tableData.value = await getAdminMenusTree()
    buildParentTree()
    treeExpandAll.value = false
    tableKey.value++
  } catch {
    tableData.value = []
    treeExpandAll.value = false
  } finally { loading.value = false }
}

function expandAll() { treeExpandAll.value = true; tableKey.value++ }
function collapseAll() { treeExpandAll.value = false; tableKey.value++ }

async function toggleEnabled(row: AdminMenuNode, val: boolean) {
  try { 
    await updateMenu(row.id, { enabled: val })
    row.enabled = val
    ElMessage.success('已更新') 
  } catch { 
    ElMessage.error('更新失败') 
  }
}

function onSelectionChange(selection: any[]) {
  selectedIds.value = (selection || []).map((x: any) => x.id)
}

function handleSearch(params: Record<string, any>) {
  menuKeyword.value = String(params.keyword || '')
  treeExpandAll.value = !!menuKeyword.value.trim()
  tableKey.value++
}

// 对话框状态
const dialogVisible = ref(false)
const dialogMode = ref<DialogMode>('add')
const dialogTitle = ref('编辑菜单')
const dialogSubTitle = ref('配置菜单的基本信息和多客户端支持设置')
const parentId = ref<string>('')
const formRef = ref<FormInstance>()

const isViewMode = computed(() => dialogMode.value === 'view')

// 表单数据
const form = ref<any>({
  name: '',
  nameI18n: '',
  route: '',
  type: 'Menu',
  itemMode: 'Tab',
  icon: '',
  url: '',
  order: 0,
  enabled: true,
  wpfDisplayMode: 'Route',
  webDisplayMode: 'Route',
  mobileDisplayMode: 'Route',
  wpfRouteUrl: '',
  webRouteUrl: '',
  mobileRouteUrl: '',
  code: '',
  resource: '',
  method: ''
})

const supportedClientsArr = ref<string[]>(['WEB'])

// 表单验证规则
const formRules: FormRules = {
  name: [{ required: true, message: '请输入菜单名称', trigger: 'blur' }],
  type: [{ required: true, message: '请选择类型', trigger: 'change' }]
}

// 父级菜单树
const parentTree = ref<AdminMenuNode[]>([])

function buildParentTree() {
  // 过滤掉当前编辑的菜单（防止循环）
  const currentId = (form.value as any).id
  const filterNode = (node: AdminMenuNode): AdminMenuNode | null => {
    if (node.id === currentId) return null
    return {
      ...node,
      children: node.children?.map(filterNode).filter(n => n !== null) as AdminMenuNode[]
    }
  }
  parentTree.value = tableData.value.map(filterNode).filter(n => n !== null) as AdminMenuNode[]
}

// 类型改变处理
function onTypeChange() {
  // Item类型自动显示itemMode
  if (form.value.type === 'Item' && !form.value.itemMode) {
    form.value.itemMode = 'Tab'
  }
}

// Item模式改变处理
function onItemModeChange() {
  // Link模式需要URL
  if (form.value.itemMode !== 'Link') {
    form.value.url = ''
  }
}

// 客户端支持改变处理
function onClientSupportChange() {
  // 初始化对应客户端的配置
  if (supportedClientsArr.value.includes('WPF') && !form.value.wpfDisplayMode) {
    form.value.wpfDisplayMode = 'Route'
  }
  if (supportedClientsArr.value.includes('WEB') && !form.value.webDisplayMode) {
    form.value.webDisplayMode = 'Route'
  }
  if (supportedClientsArr.value.includes('MOBILE') && !form.value.mobileDisplayMode) {
    form.value.mobileDisplayMode = 'Route'
  }
}

// 新增菜单
function handleAdd() {
  dialogMode.value = 'add'
  dialogTitle.value = '新增菜单'
  dialogSubTitle.value = '创建新的菜单项，配置基本信息和多客户端支持'
  parentId.value = ''
  form.value = {
    name: '',
    nameI18n: '',
    route: '',
    type: 'Menu',
    itemMode: 'Tab',
    icon: '',
    url: '',
    order: 0,
    enabled: true,
    wpfDisplayMode: 'Route',
    webDisplayMode: 'Route',
    mobileDisplayMode: 'Route',
    wpfRouteUrl: '',
    webRouteUrl: '',
    mobileRouteUrl: '',
    code: '',
    resource: '',
    method: ''
  }
  supportedClientsArr.value = ['WEB']
  dialogVisible.value = true
}

// 编辑菜单
async function handleEdit(row: AdminMenuNode) {
  try {
    dialogMode.value = 'edit'
    dialogTitle.value = '编辑菜单'
    dialogSubTitle.value = '修改菜单项的配置信息'
    
    const d = await getMenuDetail(row.id)
    populateForm(d)
    
    dialogVisible.value = true
  } catch (error) {
    ElMessage.error('加载菜单详情失败')
  }
}

// 查看菜单
async function handleView(row: AdminMenuNode) {
  try {
    dialogMode.value = 'view'
    dialogTitle.value = '查看菜单'
    dialogSubTitle.value = '查看菜单项的详细信息（只读模式）'
    
    const d = await getMenuDetail(row.id)
    populateForm(d)
    
    dialogVisible.value = true
  } catch (error) {
    ElMessage.error('加载菜单详情失败')
  }
}

// 复制菜单
async function handleCopy(row: AdminMenuNode) {
  try {
    dialogMode.value = 'copy'
    dialogTitle.value = '复制菜单'
    dialogSubTitle.value = '基于现有菜单创建新的菜单项'
    
    const d = await getMenuDetail(row.id)
    populateForm(d)
    
    // 复制模式下修改名称和清空ID
    form.value.name = `${d.name} - 副本`
    form.value.route = ''
    delete (form.value as any).id
    parentId.value = ''
    
    dialogVisible.value = true
  } catch (error) {
    ElMessage.error('加载菜单详情失败')
  }
}

// 填充表单
function populateForm(data: MenuDetail) {
  form.value = {
    ...data,
    nameI18n: data.nameI18n || '',
    itemMode: data.itemMode || 'Tab',
    wpfDisplayMode: data.wpfDisplayMode || 'Route',
    webDisplayMode: data.webDisplayMode || 'Route',
    mobileDisplayMode: data.mobileDisplayMode || 'Route',
    wpfRouteUrl: data.wpfRouteUrl || '',
    webRouteUrl: data.webRouteUrl || '',
    mobileRouteUrl: data.mobileRouteUrl || '',
    code: data.code || '',
    resource: data.resource || '',
    method: data.method || '',
    url: data.url || ''
  }
  
  parentId.value = data.parentId || ''
  
  // 解析支持的客户端
  const clients = (data.supportedClients || '').split(',').map((c: string) => c.trim()).filter((c: string) => c)
  supportedClientsArr.value = clients.length > 0 ? clients : ['WEB']
}

// 删除菜单
async function handleDelete(row: AdminMenuNode) {
  await ElMessageBox.confirm(`确定删除「${row.name}」?`, '提示', { type: 'warning' })
  await deleteMenu(row.id)
  ElMessage.success('已删除')
  loadTree()
}

// 批量删除
async function handleBatchDelete() {
  await ElMessageBox.confirm(`确定删除选中的 ${selectedIds.value.length} 项?`, '提示', { type: 'warning' })
  await batchDeleteMenus(selectedIds.value)
  ElMessage.success('已删除')
  selectedIds.value = []
  loadTree()
}

// 提交表单
async function submitForm() {
  if (!formRef.value) return
  
  await formRef.value.validate()
  
  try {
    const payload = buildPayload()
    
    if (dialogMode.value === 'edit') {
      const id = (form.value as any).id as string
      await updateMenu(id, payload)
      ElMessage.success('保存成功')
    } else {
      await createMenu(payload)
      ElMessage.success('创建成功')
    }
    
    dialogVisible.value = false
    loadTree()
  } catch (error) {
    ElMessage.error('保存失败')
  }
}

// 构建提交数据
function buildPayload() {
  // 从多语言 JSON 提取默认语言值作为 name
  let name = form.value.name?.trim() || ''
  const nameI18n = form.value.nameI18n || null
  if (nameI18n) {
    try {
      const obj = JSON.parse(nameI18n)
      // 按优先级取值：默认语言 → zh-CN → 第一个有值的
      name = obj[getDefaultLang()] || obj['zh-CN'] || Object.values(obj).find((v: any) => v?.trim()) as string || name
    } catch {}
  }
  const payload: any = {
    name,
    nameI18n,
    route: form.value.route?.trim(),
    type: form.value.type,
    icon: form.value.icon?.trim(),
    order: form.value.order || 0,
    enabled: form.value.enabled,
    parentId: parentId.value || undefined,
    supportedClients: supportedClientsArr.value.join(',')
  }
  
  // Item类型特定字段
  if (form.value.type === 'Item') {
    payload.itemMode = form.value.itemMode
    if (form.value.itemMode === 'Link') {
      payload.url = form.value.url?.trim()
    }
  }
  
  // 各客户端配置
  if (supportedClientsArr.value.includes('WPF')) {
    payload.wpfDisplayMode = form.value.wpfDisplayMode
    payload.wpfRouteUrl = form.value.wpfRouteUrl?.trim()
  }
  if (supportedClientsArr.value.includes('WEB')) {
    payload.webDisplayMode = form.value.webDisplayMode
    payload.webRouteUrl = form.value.webRouteUrl?.trim()
  }
  if (supportedClientsArr.value.includes('MOBILE')) {
    payload.mobileDisplayMode = form.value.mobileDisplayMode
    payload.mobileRouteUrl = form.value.mobileRouteUrl?.trim()
  }
  
  // 权限配置（Button/Api类型）
  if (form.value.type === 'Button' || form.value.type === 'Api') {
    payload.code = form.value.code?.trim()
    payload.resource = form.value.resource?.trim()
    payload.method = form.value.method?.trim()
  }
  
  return payload
}

onMounted(loadTree)
</script>

<style scoped>
/* ==================== 顶部区域 ==================== */
.menu-header-row {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  width: 100%;
}

/* ==================== 名称单元格 ==================== */
.menu-name-cell {
  display: inline-flex;
  vertical-align: middle;
  align-items: center;
  gap: 8px;
  min-width: 0;
}

.menu-type-icon {
  font-size: 16px;
  flex-shrink: 0;
}

.name-text {
  font-weight: 600;
  font-size: 13px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ==================== 类型标签 ==================== */
.type-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 48px;
  padding: 2px 10px;
  border-radius: 10px;
  font-size: 12px;
  font-weight: 500;
  letter-spacing: 0.5px;
  line-height: 20px;
}

.type-directory {
  background: rgba(245, 158, 11, 0.12);
  color: #d97706;
  border: 1px solid rgba(245, 158, 11, 0.25);
}

.type-menu {
  background: rgba(59, 130, 246, 0.12);
  color: #2563eb;
  border: 1px solid rgba(59, 130, 246, 0.25);
}

.type-item {
  background: rgba(99, 102, 241, 0.12);
  color: #4f46e5;
  border: 1px solid rgba(99, 102, 241, 0.25);
}

.type-button {
  background: rgba(16, 185, 129, 0.12);
  color: #059669;
  border: 1px solid rgba(16, 185, 129, 0.25);
}

.type-api {
  background: rgba(239, 68, 68, 0.12);
  color: #dc2626;
  border: 1px solid rgba(239, 68, 68, 0.25);
}

/* 深色模式类型标签 */
.admin-dark .type-directory {
  background: rgba(245, 158, 11, 0.2);
  color: #fbbf24;
  border-color: rgba(245, 158, 11, 0.35);
}

.admin-dark .type-menu {
  background: rgba(59, 130, 246, 0.2);
  color: #60a5fa;
  border-color: rgba(59, 130, 246, 0.35);
}

.admin-dark .type-item {
  background: rgba(99, 102, 241, 0.2);
  color: #818cf8;
  border-color: rgba(99, 102, 241, 0.35);
}

.admin-dark .type-button {
  background: rgba(16, 185, 129, 0.2);
  color: #34d399;
  border-color: rgba(16, 185, 129, 0.35);
}

.admin-dark .type-api {
  background: rgba(239, 68, 68, 0.2);
  color: #f87171;
  border-color: rgba(239, 68, 68, 0.35);
}

/* 对话框 Header */
.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 16px 20px;
  margin: -20px -20px 20px -20px;
  background: linear-gradient(135deg, #f8f9fa 0%, #e9ecef 100%);
  border-bottom: 1px solid #dee2e6;
  border-radius: 8px 8px 0 0;
}

.header-text {
  font-size: 18px;
  font-weight: 700;
  color: #0066cc;
  margin-bottom: 6px;
}

.sub-header-text {
  font-size: 13px;
  color: #6c757d;
  line-height: 1.5;
}

/* 表单区域 */
.menu-form {
  padding: 8px 16px;
}

.form-section {
  margin-bottom: 24px;
  padding: 16px;
  background: #ffffff;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
}

.section-title {
  font-size: 16px;
  font-weight: 600;
  color: #374151;
  margin-bottom: 16px;
  padding-bottom: 8px;
  border-bottom: 2px solid #e5e7eb;
}

/* 客户端配置面板 */
.client-config-panel {
  margin-top: 16px;
  padding: 16px;
  border-radius: 8px;
  border: 1px solid #dee2e6;
}

.wpf-panel {
  background: linear-gradient(135deg, #e6f3ff 0%, #f0f7ff 100%);
  border-color: #99ccff;
}

.web-panel {
  background: linear-gradient(135deg, #e6f9f0 0%, #f0fdf7 100%);
  border-color: #86efac;
}

.mobile-panel {
  background: linear-gradient(135deg, #ffe6e6 0%, #fff0f0 100%);
  border-color: #ff9999;
}

.panel-title {
  font-size: 15px;
  font-weight: 600;
  margin-bottom: 12px;
}

.wpf-panel .panel-title { color: #0066cc; }
.web-panel .panel-title { color: #16a34a; }
.mobile-panel .panel-title { color: #dc2626; }

/* 表单项样式 */
.menu-form :deep(.el-form-item__label) {
  font-weight: 500;
  color: #374151;
}

.menu-form :deep(.el-input__wrapper),
.menu-form :deep(.el-select__wrapper),
.menu-form :deep(.el-textarea__inner) {
  border-radius: 6px;
  transition: all 0.3s ease;
}

.menu-form :deep(.el-input__wrapper:hover),
.menu-form :deep(.el-select__wrapper:hover),
.menu-form :deep(.el-textarea__inner:hover) {
  box-shadow: 0 0 0 1px #3b82f6;
}

.menu-form :deep(.el-input__wrapper.is-focus),
.menu-form :deep(.el-select__wrapper.is-focused),
.menu-form :deep(.el-textarea__inner:focus) {
  box-shadow: 0 0 0 2px #3b82f6;
}

/* Checkbox组样式 */
.menu-form :deep(.el-checkbox-group) {
  display: flex;
  gap: 16px;
}

.menu-form :deep(.el-checkbox) {
  font-weight: 500;
}

/* 暗黑模式 */
.admin-dark .dialog-header {
  background: linear-gradient(135deg, var(--admin-fill-light) 0%, var(--admin-card-bg) 100%);
  border-color: var(--admin-border);
}

.admin-dark .header-text {
  color: var(--admin-text-light);
}

.admin-dark .sub-header-text {
  color: var(--admin-text-secondary);
}

.admin-dark .form-section {
  background: var(--admin-card-bg);
  border-color: var(--admin-border);
}

.admin-dark .section-title {
  color: var(--admin-text-light);
  border-color: var(--admin-border);
}

.admin-dark .client-config-panel {
  border-color: var(--admin-border);
}

.admin-dark .wpf-panel {
  background: linear-gradient(135deg, rgba(0, 102, 204, 0.1) 0%, rgba(0, 102, 204, 0.05) 100%);
}

.admin-dark .web-panel {
  background: linear-gradient(135deg, rgba(22, 163, 74, 0.1) 0%, rgba(22, 163, 74, 0.05) 100%);
}

.admin-dark .mobile-panel {
  background: linear-gradient(135deg, rgba(220, 38, 38, 0.1) 0%, rgba(220, 38, 38, 0.05) 100%);
}

.admin-dark .menu-form :deep(.el-form-item__label) {
  color: var(--admin-text-secondary);
}
</style>
