<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="departments-page">
    <div class="page-layout">
      <!-- 左侧：部门树 -->
      <div class="tree-panel">
        <el-card shadow="never" class="tree-card">
          <template #header>
            <div class="card-header">
              <span class="card-title">
                <i class="bi bi-building"></i>
                组织架构
              </span>
              <span class="card-stat">共 {{ totalDepts }} 个</span>
            </div>
          </template>

          <div class="tree-toolbar">
            <el-input v-model="treeKeyword" placeholder="搜索部门..." clearable size="default" class="search-input">
              <template #prefix><i class="bi bi-search"></i></template>
            </el-input>
            <div class="toolbar-actions">
              <el-button circle size="default" @click="loadTree" title="刷新">
                <i class="bi bi-arrow-clockwise"></i>
              </el-button>
              <el-button circle size="default" @click="expandAll" title="展开">
                <i class="bi bi-chevron-double-down"></i>
              </el-button>
              <el-button circle size="default" @click="collapseAll" title="折叠">
                <i class="bi bi-chevron-double-up"></i>
              </el-button>
            </div>
          </div>

          <el-scrollbar height="calc(100vh - 320px)">
            <el-tree
              ref="treeRef"
              :data="filteredTree"
              node-key="id"
              :props="{ label: 'name', children: 'children' }"
              highlight-current
              :default-expand-all="isExpandAll"
              :expand-on-click-node="false"
              class="dept-tree"
              @node-click="onDeptSelected"
            >
              <template #default="{ node, data }">
                <div class="tree-node-content">
                  <i :class="node.expanded ? 'bi bi-folder2-open' : 'bi bi-folder2'" class="node-icon"></i>
                  <span class="node-label">{{ data.name }}</span>
                  <span v-if="data.id === currentDeptId" class="node-badge">当前</span>
                </div>
              </template>
            </el-tree>
            <el-empty v-if="!filteredTree.length" description="暂无部门数据" :image-size="80" />
          </el-scrollbar>

          <div class="tree-footer">
            <el-button v-permission="'/system/departments:add'" type="primary" size="default" style="width: 100%;" @click="openAddDialog()">
              <i class="bi bi-plus-lg" style="margin-right: 6px;"></i>新增部门
            </el-button>
          </div>
        </el-card>
      </div>

      <!-- 右侧：部门成员 -->
      <div class="users-panel">
        <el-card shadow="never" class="users-card">
          <template #header>
            <div class="card-header">
              <div class="card-title-section">
                <span class="card-title">
                  <i class="bi bi-people-fill"></i>
                  部门成员
                </span>
                <el-tag v-if="currentDeptName" type="primary" size="default">{{ currentDeptName }}</el-tag>
              </div>
              <div v-if="currentDeptId" class="card-actions">
                <el-button v-permission="'/system/departments:edit'" size="small" @click="openEditDialog">
                  <i class="bi bi-pencil-square" style="margin-right: 4px;"></i>编辑
                </el-button>
                <el-button v-permission="'/system/departments:delete'" size="small" type="danger" @click="deleteDept">
                  <i class="bi bi-trash3" style="margin-right: 4px;"></i>删除
                </el-button>
              </div>
            </div>
          </template>

          <div v-if="!currentDeptId" class="empty-state">
            <i class="bi bi-cursor"></i>
            <p>请从左侧选择一个部门查看成员</p>
          </div>

          <DataTable
            v-else
            :data="tableData"
            :loading="loadingUsers"
            :columns="columns"
            :pagination="dtPagination"
            :compact-mode="true"
            :show-index="true"
            cache-key="dept-users"
            @page-change="loadUsers"
            @size-change="loadUsers"
          >
            <template #column-displayName="{ row }">
              <div class="user-cell">
                <el-avatar :size="32" :src="row.avatar || undefined">{{ row.displayName?.charAt(0) }}</el-avatar>
                <span class="user-name">{{ row.displayName }}</span>
                <el-tag v-if="row.isManager" type="warning" size="small" effect="plain">
                  <i class="bi bi-star-fill" style="margin-right: 2px;"></i>负责人
                </el-tag>
              </div>
            </template>

            <template #actions="{ row }">
              <el-button v-if="!row.isManager" v-permission="'/system/departments:set-manager'" size="small" type="warning" link @click="setManager(row, true)">设为负责人</el-button>
              <el-button v-else v-permission="'/system/departments:set-manager'" size="small" type="primary" link @click="setManager(row, false)">撤销负责人</el-button>
              <el-button v-permission="'/system/departments:remove-user'" size="small" type="danger" link @click="removeUser(row)">移除</el-button>
            </template>
          </DataTable>
        </el-card>
      </div>
    </div>

    <!-- 新增/编辑部门对话框 -->
    <el-dialog v-model="deptDialogVisible" :title="deptDialogTitle" width="520px" :close-on-click-modal="false">
      <el-form :model="deptForm" label-width="90px">
        <el-form-item label="部门名称" required>
          <el-input v-model="deptForm.name" placeholder="请输入部门名称" maxlength="50" show-word-limit />
        </el-form-item>
        <el-form-item label="部门编码">
          <el-input v-model="deptForm.code" placeholder="选填，用于系统标识" maxlength="50" />
        </el-form-item>
        <el-form-item label="上级部门">
          <el-tree-select
            v-model="deptForm.parentId"
            :data="parentTree"
            :props="{ label: 'name', value: 'id', children: 'children' }"
            node-key="id"
            check-strictly
            clearable
            placeholder="留空则为顶级部门"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="deptForm.sort" :min="0" :max="9999" controls-position="right" style="width: 100%" />
        </el-form-item>
        <el-form-item label="状态">
          <el-switch v-model="deptForm.enabled" active-text="启用" inactive-text="禁用" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="deptDialogVisible=false">取消</el-button>
        <el-button type="primary" @click="saveDepartment">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue'
import DataTable from '../../../components/DataTable/index.vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { ElTree } from 'element-plus'
import {
  getDepartmentsTree,
  getDepartmentUsers,
  getDepartmentDetail,
  removeDepartmentUser,
  setDepartmentManager,
  createDepartment,
  updateDepartment,
  deleteDepartment,
  type DepartmentTreeNode,
  type DepartmentUserItem
} from '../../../api/department'

const treeRef = ref<InstanceType<typeof ElTree>>()
const isExpandAll = ref(true)
const treeData = ref<DepartmentTreeNode[]>([])
const treeKeyword = ref('')
const currentDeptId = ref<string>('')
const currentDeptName = ref<string>('')

const totalDepts = computed(() => {
  const count = (nodes: DepartmentTreeNode[]): number => nodes.reduce((sum, node) => sum + 1 + (node.children ? count(node.children) : 0), 0)
  return count(treeData.value)
})

const filteredTree = computed(() => {
  const kw = treeKeyword.value.trim().toLowerCase()
  if (!kw) return treeData.value
  const filterNode = (node: DepartmentTreeNode): DepartmentTreeNode | null => {
    if (node.name.toLowerCase().includes(kw)) return { ...node }
    const children = (node.children || []).map(filterNode).filter((n): n is DepartmentTreeNode => n !== null)
    if (children.length) return { ...node, children }
    return null
  }
  return treeData.value.map(filterNode).filter((n): n is DepartmentTreeNode => n !== null)
})

const parentTree = computed(() => {
  if (!deptForm.value.id) return treeData.value
  const excludeIds = new Set<string>([deptForm.value.id])
  const collectChildIds = (node: DepartmentTreeNode) => { node.children?.forEach(child => { excludeIds.add(child.id); collectChildIds(child) }) }
  const findAndCollect = (nodes: DepartmentTreeNode[]): boolean => {
    for (const node of nodes) {
      if (node.id === deptForm.value.id) { collectChildIds(node); return true }
      if (node.children && findAndCollect(node.children)) return true
    }
    return false
  }
  findAndCollect(treeData.value)
  const filterTree = (nodes: DepartmentTreeNode[]): DepartmentTreeNode[] => nodes.filter(node => !excludeIds.has(node.id)).map(node => ({ ...node, children: node.children ? filterTree(node.children) : undefined }))
  return filterTree(treeData.value)
})

async function loadTree() {
  try { treeData.value = await getDepartmentsTree() } catch { treeData.value = []; ElMessage.error('加载部门树失败') }
}

function expandAll() {
  isExpandAll.value = true
  nextTick(() => { (treeRef.value as any)?.setExpandedKeys?.(getAllNodeKeys(treeData.value)) })
}

function collapseAll() {
  isExpandAll.value = false
  nextTick(() => { (treeRef.value as any)?.setExpandedKeys?.([]) })
}

function getAllNodeKeys(nodes: DepartmentTreeNode[]): string[] {
  let keys: string[] = []
  nodes.forEach(node => { keys.push(node.id); if (node.children) keys = keys.concat(getAllNodeKeys(node.children)) })
  return keys
}

function onDeptSelected(node: any) {
  currentDeptId.value = node.id
  currentDeptName.value = node.name
  dtPagination.value.page = 1
  loadUsers()
}

// 成员表
const loadingUsers = ref(false)
const tableData = ref<DepartmentUserItem[]>([])
const dtPagination = ref({ page: 1, pageSize: 20, total: 0 })
const columns = [
  { prop: 'displayName', label: '姓名', minWidth: 180 },
  { prop: 'phone', label: '手机号', width: 140 },
  { prop: 'email', label: '邮箱', minWidth: 200 },
]

async function loadUsers() {
  if (!currentDeptId.value) { tableData.value = []; dtPagination.value.total = 0; return }
  loadingUsers.value = true
  try {
    const list = await getDepartmentUsers(currentDeptId.value)
    dtPagination.value.total = list.length
    const start = (dtPagination.value.page - 1) * dtPagination.value.pageSize
    tableData.value = list.slice(start, start + dtPagination.value.pageSize)
  } catch { ElMessage.error('加载成员列表失败') } finally { loadingUsers.value = false }
}

async function removeUser(row: DepartmentUserItem) {
  if (!currentDeptId.value) return
  try {
    await ElMessageBox.confirm(`确定从部门「${currentDeptName.value}」中移除「${row.displayName}」？`, '移除确认', { type: 'warning' })
    await removeDepartmentUser(currentDeptId.value, row.id)
    ElMessage.success('已移除')
    loadUsers()
  } catch (e: any) { if (e !== 'cancel') ElMessage.error(e?.message || '移除失败') }
}

async function setManager(row: DepartmentUserItem, val: boolean) {
  if (!currentDeptId.value) return
  try {
    await setDepartmentManager(currentDeptId.value, row.id, val)
    ElMessage.success(val ? '已设为负责人' : '已撤销负责人')
    loadUsers()
  } catch (e: any) { ElMessage.error(e?.message || '操作失败') }
}

// 部门CRUD
const deptDialogVisible = ref(false)
const deptDialogTitle = ref('新增部门')
const deptForm = ref<{ id?: string; name: string; code?: string; parentId?: string | null; enabled: boolean; sort?: number }>({ name: '', code: '', parentId: undefined, enabled: true, sort: 0 })

function openAddDialog() {
  deptDialogTitle.value = '新增部门'
  deptForm.value = { name: '', code: '', parentId: currentDeptId.value || undefined, enabled: true, sort: 0 }
  deptDialogVisible.value = true
}

async function openEditDialog() {
  if (!currentDeptId.value) return
  deptDialogTitle.value = '编辑部门'
  try {
    const detail = await getDepartmentDetail(currentDeptId.value)
    deptForm.value = { id: detail.id, name: detail.name, code: detail.code, parentId: detail.parentId, enabled: detail.enabled ?? true, sort: detail.sort || 0 }
    deptDialogVisible.value = true
  } catch (e: any) { ElMessage.error(e?.message || '加载部门信息失败') }
}

async function deleteDept() {
  if (!currentDeptId.value) return
  try {
    await ElMessageBox.confirm(`确定删除部门「${currentDeptName.value}」？`, '删除确认', { type: 'warning' })
    await deleteDepartment(currentDeptId.value)
    ElMessage.success('已删除')
    currentDeptId.value = ''; currentDeptName.value = ''; tableData.value = []
    await loadTree()
  } catch (e: any) { if (e !== 'cancel') ElMessage.error(e?.message || '删除失败') }
}

async function saveDepartment() {
  try {
    if (!deptForm.value.name?.trim()) { ElMessage.warning('请输入部门名称'); return }
    if (deptForm.value.id) {
      await updateDepartment(deptForm.value.id, { name: deptForm.value.name, code: deptForm.value.code, parentId: deptForm.value.parentId, enabled: deptForm.value.enabled, sort: deptForm.value.sort })
      ElMessage.success('保存成功')
    } else {
      await createDepartment({ name: deptForm.value.name, code: deptForm.value.code, parentId: deptForm.value.parentId, enabled: deptForm.value.enabled, sort: deptForm.value.sort })
      ElMessage.success('创建成功')
    }
    deptDialogVisible.value = false
    await loadTree()
    if (deptForm.value.id === currentDeptId.value) currentDeptName.value = deptForm.value.name
  } catch (e: any) { ElMessage.error(e?.message || '操作失败') }
}

onMounted(loadTree)
</script>

<style scoped>
/* ==================== 页面容器 ==================== */
.departments-page {
  padding: 24px;
  background: var(--el-bg-color-page);
  min-height: 100vh;
}

/* ==================== 页面布局 ==================== */
.page-layout {
  display: grid;
  grid-template-columns: 360px 1fr;
  gap: 24px;
  align-items: start;
}

/* ==================== 左侧树面板 ==================== */
.tree-panel {
  position: sticky;
  top: 24px;
}

.tree-card {
  border-radius: 12px;
  border: 1px solid #e5e7eb;
}

.admin-dark .tree-card {
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

.card-title i {
  color: #667eea;
  font-size: 18px;
}

.admin-dark .card-title {
  color: #f9fafb;
}

.card-stat {
  font-size: 13px;
  color: #6b7280;
}

.admin-dark .card-stat {
  color: #9ca3af;
}

/* ==================== 树工具栏 ==================== */
.tree-toolbar {
  display: flex;
  gap: 12px;
  padding: 0 4px;
  margin-bottom: 12px;
}

.search-input {
  flex: 1;
}

.toolbar-actions {
  display: flex;
  gap: 8px;
}

/* ==================== 部门树 ==================== */
.dept-tree :deep(.el-tree-node__content) {
  height: 42px;
  padding: 4px 0;
  border-radius: 8px;
  margin-bottom: 2px;
  transition: all 0.2s ease;
}

.dept-tree :deep(.el-tree-node__content:hover) {
  background: #f1f5f9;
}

.admin-dark .dept-tree :deep(.el-tree-node__content:hover) {
  background: #334155;
}

.dept-tree :deep(.el-tree-node.is-current > .el-tree-node__content) {
  background: rgba(102, 126, 234, 0.1);
  border-left: 3px solid #667eea;
}

.admin-dark .dept-tree :deep(.el-tree-node.is-current > .el-tree-node__content) {
  background: rgba(102, 126, 234, 0.2);
}

.tree-node-content {
  display: flex;
  align-items: center;
  gap: 10px;
  flex: 1;
  padding-right: 12px;
}

.node-icon {
  font-size: 16px;
  color: #667eea;
}

.node-label {
  flex: 1;
  font-size: 14px;
  color: #334155;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.admin-dark .node-label {
  color: #e2e8f0;
}

.node-badge {
  padding: 2px 8px;
  background: #667eea;
  color: #fff;
  font-size: 11px;
  border-radius: 10px;
}

.tree-footer {
  padding: 16px;
  border-top: 1px solid #e5e7eb;
}

.admin-dark .tree-footer {
  border-top-color: #374151;
}

/* ==================== 右侧成员面板 ==================== */
.users-card {
  border-radius: 12px;
  border: 1px solid #e5e7eb;
}

.admin-dark .users-card {
  background: #1f2937;
  border-color: #374151;
}

.card-title-section {
  display: flex;
  align-items: center;
  gap: 12px;
}

.card-actions {
  display: flex;
  gap: 8px;
}

/* ==================== 空状态 ==================== */
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

.empty-state p {
  margin: 0;
  font-size: 14px;
}

/* ==================== 用户单元格 ==================== */
.user-cell {
  display: flex;
  align-items: center;
  gap: 12px;
}

.user-name {
  font-weight: 500;
  color: #1e293b;
}

.admin-dark .user-name {
  color: #f1f5f9;
}

/* ==================== 对话框 ==================== */
:deep(.el-dialog) {
  border-radius: 12px;
}

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

:deep(.el-dialog__title) {
  font-size: 18px;
  font-weight: 600;
  color: #1f2937;
}

.admin-dark :deep(.el-dialog__title) {
  color: #f9fafb;
}

:deep(.el-dialog__body) {
  padding: 24px;
}

:deep(.el-dialog__footer) {
  padding: 16px 24px;
  border-top: 1px solid #f3f4f6;
}

.admin-dark :deep(.el-dialog__footer) {
  border-top-color: #374151;
}

/* ==================== 表单 ==================== */
:deep(.el-form-item__label) {
  font-weight: 500;
  color: #374151;
}

.admin-dark :deep(.el-form-item__label) {
  color: #e5e7eb;
}

/* ==================== 响应式 ==================== */
@media (max-width: 900px) {
  .page-layout {
    grid-template-columns: 1fr;
  }
  
  .tree-panel {
    position: static;
  }
}

@media (max-width: 768px) {
  .departments-page {
    padding: 16px;
  }
}
</style>
