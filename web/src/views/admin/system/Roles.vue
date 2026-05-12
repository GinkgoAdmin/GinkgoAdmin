<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="roles-page">
    <div class="page-layout">
      <!-- 左侧：角色树 -->
      <div class="tree-panel">
        <el-card shadow="never" class="tree-card">
          <template #header>
            <div class="card-header">
              <span class="card-title">
                <i class="bi bi-diagram-3"></i>
                角色列表
              </span>
              <span class="card-stat">共 {{ totalRoles }} 个</span>
            </div>
          </template>

          <div class="tree-toolbar">
            <el-input v-model="keyword" placeholder="搜索角色..." clearable size="default" class="search-input">
              <template #prefix><i class="bi bi-search"></i></template>
            </el-input>
            <div class="toolbar-actions">
              <el-button circle size="default" @click="loadTree" title="刷新">
                <i class="bi bi-arrow-clockwise"></i>
              </el-button>
              <el-button v-permission="'/system/roles:add'" circle type="primary" size="default" @click="onAdd" title="新增">
                <i class="bi bi-plus-lg"></i>
              </el-button>
            </div>
          </div>

          <el-scrollbar height="calc(100vh - 280px)">
            <el-tree
              :data="roleTree"
              node-key="id"
              :props="{ label: 'name', children: 'children' }"
              highlight-current
              default-expand-all
              :expand-on-click-node="false"
              class="role-tree"
              @node-click="onRoleClick"
            >
              <template #default="{ node, data }">
                <div class="tree-node-content">
                  <i class="bi bi-person-badge node-icon"></i>
                  <span class="node-label">{{ data.name }}</span>
                  <span v-if="data.code" class="node-code">{{ data.code }}</span>
                  <span v-if="data.id === currentRoleId" class="node-badge">当前</span>
                  <div class="node-actions" @click.stop>
                    <el-button v-permission="'/system/roles:edit'" link size="small" @click="onEdit(data)"><i class="bi bi-pencil"></i></el-button>
                    <el-button v-permission="'/system/roles:delete'" link type="danger" size="small" @click="onDelete(data)"><i class="bi bi-trash3"></i></el-button>
                  </div>
                </div>
              </template>
            </el-tree>
            <el-empty v-if="!roleTree.length" description="暂无角色数据" :image-size="80" />
          </el-scrollbar>
        </el-card>
      </div>

      <!-- 右侧：权限分配 -->
      <div class="perm-panel">
        <el-card shadow="never" class="perm-card">
          <template #header>
            <div class="card-header">
              <div class="card-title-section">
                <span class="card-title">
                  <i class="bi bi-key-fill"></i>
                  权限分配
                </span>
                <el-tag v-if="currentRoleName" type="primary" size="default">{{ currentRoleName }}</el-tag>
              </div>
              <div v-if="currentRoleId" class="card-actions">
                <el-button size="small" @click="selectAllPerms">全选</el-button>
                <el-button size="small" @click="selectNonePerms">全不选</el-button>
                <el-button v-permission="'/system/roles:save'" size="small" type="primary" @click="savePerms">
                  <i class="bi bi-check-lg" style="margin-right: 4px;"></i>保存权限
                </el-button>
              </div>
            </div>
          </template>

          <div v-if="!currentRoleId" class="empty-state">
            <i class="bi bi-cursor"></i>
            <p>请从左侧选择一个角色进行权限分配</p>
          </div>

          <div v-else class="perm-content">
            <div class="scope-row">
              <span class="scope-label"><i class="bi bi-shield-lock"></i> 数据范围：</span>
              <el-select v-model="dataScope.strategy" size="default" style="width: 220px" @change="onPanelScopeChange">
                <el-option label="全部数据" value="All" />
                <el-option label="仅本人" value="OwnOnly" />
                <el-option label="仅本部门" value="DepartmentOnly" />
                <el-option label="本部门及子部门" value="DepartmentAndChildren" />
                <el-option label="指定部门" value="SpecifiedDepartments" />
              </el-select>
              <el-tree-select
                v-if="dataScope.strategy === 'SpecifiedDepartments'"
                v-model="dataScope.departmentIds"
                :data="deptTree"
                :props="{ label: 'name', value: 'id', children: 'children' }"
                node-key="id"
                multiple
                show-checkbox
                check-strictly
                collapse-tags
                collapse-tags-tooltip
                clearable
                placeholder="请选择部门（可多选）"
                size="default"
                style="min-width: 280px; margin-left: 12px; flex: 1;"
              />
            </div>
            <div v-if="dataScope.strategy === 'SpecifiedDepartments'" class="scope-tip">
              <i class="bi bi-info-circle"></i>
              选择该角色可访问的部门（多选）。注意：必须点击右上角“保存权限”按钮才会持久化。
            </div>

            <el-divider style="margin: 12px 0;" />

            <!-- 权限类型统计 -->
            <div class="perm-stats">
              <span class="perm-stat-item perm-stat-directory">
                <i class="bi bi-folder"></i> 目录 <b>{{ permTypeCounts.Directory }}</b>
              </span>
              <span class="perm-stat-item perm-stat-menu">
                <i class="bi bi-window"></i> 菜单 <b>{{ permTypeCounts.Menu }}</b>
              </span>
              <span class="perm-stat-item perm-stat-item-type">
                <i class="bi bi-file-earmark"></i> 菜单项 <b>{{ permTypeCounts.Item }}</b>
              </span>
              <span class="perm-stat-item perm-stat-button">
                <i class="bi bi-hand-index"></i> 按钮 <b>{{ permTypeCounts.Button }}</b>
              </span>
              <span class="perm-stat-item perm-stat-api">
                <i class="bi bi-hdd-network"></i> 接口 <b>{{ permTypeCounts.Api }}</b>
              </span>
              <span class="perm-stat-item perm-stat-total">
                合计 <b>{{ permTypeCounts.total }}</b>
              </span>
            </div>

            <el-divider style="margin: 12px 0;" />

            <div class="perm-search-row">
              <el-input
                v-model="permissionKeyword"
                clearable
                placeholder="搜索权限名称、路由或Code"
                class="perm-search-input"
              >
                <template #prefix><i class="bi bi-search"></i></template>
              </el-input>
            </div>

            <el-scrollbar height="calc(100vh - 440px)">
              <el-tree
                ref="permTreeRef"
                :data="permissionTree"
                node-key="id"
                show-checkbox
                check-strictly
                default-expand-all
                :default-checked-keys="checkedPermIds"
                :filter-node-method="filterPermissionNode"
                :props="{ label: 'name', children: 'children' }"
                class="perm-tree"
                @check="onPermCheck"
              >
                <template #default="{ data }">
                  <div class="perm-node-content">
                    <i :class="permNodeIcon(data.type)" class="perm-node-icon" :style="{ color: permNodeColor(data.type) }"></i>
                    <span class="perm-node-label">{{ data.name }}</span>
                    <span class="perm-type-tag" :class="'perm-type-' + (data.type || 'unknown').toLowerCase()">
                      {{ permTypeLabel(data.type) }}
                    </span>
                    <span v-if="data.code" class="perm-node-code">{{ data.code }}</span>
                  </div>
                </template>
              </el-tree>
            </el-scrollbar>
          </div>
        </el-card>
      </div>
    </div>

    <!-- 新增/编辑对话框 -->
    <el-dialog v-model="dialogVisible" :title="dialogTitle" width="520px" :close-on-click-modal="false">
      <el-form :model="form" label-width="90px">
        <el-form-item label="角色名称" required>
          <el-input v-model="form.name" placeholder="请输入角色名称" maxlength="50" show-word-limit />
        </el-form-item>
        <el-form-item label="角色编码">
          <el-input v-model="form.code" placeholder="用于权限标识（可选）" maxlength="50" />
        </el-form-item>
        <el-form-item label="状态">
          <el-switch v-model="form.enabled" active-text="启用" inactive-text="禁用" />
          <div class="enabled-tip">禁用后该角色下的用户将无法登录</div>
        </el-form-item>
        <el-form-item label="数据范围">
          <el-select v-model="form.dataScope" style="width: 100%" @change="onFormScopeChange">
            <el-option label="全部数据" value="All" />
            <el-option label="仅本人" value="OwnOnly" />
            <el-option label="仅本部门" value="DepartmentOnly" />
            <el-option label="本部门及子部门" value="DepartmentAndChildren" />
            <el-option label="指定部门" value="SpecifiedDepartments" />
          </el-select>
        </el-form-item>
        <el-form-item v-if="form.dataScope === 'SpecifiedDepartments'" label="指定部门">
          <el-tree-select
            v-model="form.departmentIds"
            :data="deptTree"
            :props="{ label: 'name', value: 'id', children: 'children' }"
            node-key="id"
            multiple
            show-checkbox
            check-strictly
            collapse-tags
            collapse-tags-tooltip
            clearable
            placeholder="请选择部门（可多选）"
            style="width: 100%"
          />
          <div class="enabled-tip">该角色用户将只能看到所选部门下的数据</div>
        </el-form-item>
        <el-form-item label="登录权限">
          <el-checkbox-group v-model="form.allowedClients">
            <el-checkbox value="WEB_ADMIN">WEB后台</el-checkbox>
            <el-checkbox value="WEB_PORTAL">WEB前台</el-checkbox>
            <el-checkbox value="WPF">WPF后台</el-checkbox>
            <el-checkbox value="UNIAPP">UniApp移动端</el-checkbox>
          </el-checkbox-group>
          <div class="allowed-clients-tip">不勾选表示禁止登录所有客户端</div>
        </el-form-item>
        <el-form-item label="上级角色">
          <el-tree-select
            v-model="form.parentId"
            :data="parentTree"
            :props="{ label: 'name', value: 'id', children: 'children' }"
            node-key="id"
            check-strictly
            clearable
            placeholder="留空则为顶级角色"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item v-if="isTopLevel" label="超级管理员">
          <el-switch v-model="form.isSuperAdmin" active-text="开启" inactive-text="关闭" />
          <div class="super-admin-tip">开启后该角色拥有所有权限，不受登录权限和数据范围限制</div>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible=false">取消</el-button>
        <el-button type="primary" @click="submitForm">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, reactive, nextTick, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { ElTree } from 'element-plus'
import {
  getRoleTree,
  createRole,
  updateRole,
  deleteRole,
  getRoleDetail,
  getPermissionTree,
  getRolePermissionIds,
  saveRolePermissions,
  getRoleDataScope,
  setRoleDataScope,
  type RoleTreeNode,
  type PermissionTreeNode
} from '../../../api/role'
import { matchesSearchableTreeNodeDeep } from './treeSearch.utils'
import { getDepartmentsTree, type DepartmentTreeNode } from '../../../api/department'

const keyword = ref('')
const loadingTree = ref(false)
const fullTree = ref<RoleTreeNode[]>([])
const currentRoleName = ref<string>('')

const roleTree = computed(() => {
  const kw = keyword.value.trim().toLowerCase()
  if (!kw) return fullTree.value
  const filterNode = (n: RoleTreeNode): RoleTreeNode | null => {
    if (n.name.toLowerCase().includes(kw) || (n.code || '').toLowerCase().includes(kw)) return { ...n }
    const children = (n.children || []).map(filterNode).filter(Boolean) as RoleTreeNode[]
    if (children.length) return { ...n, children }
    return null
  }
  return fullTree.value.map(filterNode).filter(Boolean) as RoleTreeNode[]
})

const totalRoles = computed(() => {
  const count = (nodes: RoleTreeNode[]): number => nodes.reduce((sum, node) => sum + 1 + (node.children ? count(node.children) : 0), 0)
  return count(fullTree.value)
})

const parentTree = computed(() => {
  if (!form.value.id) return fullTree.value
  const excludeIds = new Set<string>([form.value.id])
  const collectChildIds = (node: RoleTreeNode) => { node.children?.forEach(child => { excludeIds.add(child.id); collectChildIds(child) }) }
  const findAndCollect = (nodes: RoleTreeNode[]): boolean => {
    for (const node of nodes) {
      if (node.id === form.value.id) { collectChildIds(node); return true }
      if (node.children && findAndCollect(node.children)) return true
    }
    return false
  }
  findAndCollect(fullTree.value)
  const filterTree = (nodes: RoleTreeNode[]): RoleTreeNode[] => nodes.filter(node => !excludeIds.has(node.id)).map(node => ({ ...node, children: node.children ? filterTree(node.children) : [] }))
  return filterTree(fullTree.value)
})

async function loadTree() {
  loadingTree.value = true
  try { fullTree.value = await getRoleTree() } finally { loadingTree.value = false }
}

// 权限树与数据范围
const permTreeRef = ref<InstanceType<typeof ElTree>>()
const permissionTree = ref<PermissionTreeNode[]>([])
const permissionKeyword = ref('')
const currentRoleId = ref<string>('')
const checkedPermIds = ref<string[]>([])
const dataScope = ref<{ strategy: string; departmentIds?: string[] }>({ strategy: 'All', departmentIds: [] })
// 部门树（供「指定部门」多选使用，进入页面时一次性拉取并缓存）
const deptTree = ref<DepartmentTreeNode[]>([])

/** 权限面板：数据范围切换时，非「指定部门」清空已选部门，避免脏数据 */
function onPanelScopeChange(val: string) {
  if (val !== 'SpecifiedDepartments') dataScope.value.departmentIds = []
}

/** 编辑对话框：数据范围切换时，非「指定部门」清空 form.departmentIds */
function onFormScopeChange(val: string) {
  if (val !== 'SpecifiedDepartments') form.value.departmentIds = []
}

/** 懒加载部门树（首次需要时调用，多次调用直接命中缓存） */
async function ensureDeptTreeLoaded() {
  if (deptTree.value.length > 0) return
  try { deptTree.value = await getDepartmentsTree() } catch { deptTree.value = [] }
}

async function loadPermissions(roleId: string) {
  permissionTree.value = await getPermissionTree()
  checkedPermIds.value = await getRolePermissionIds(roleId)
  await nextTick()
  permTreeRef.value?.filter(permissionKeyword.value)
  const scope = await getRoleDataScope(roleId)
  dataScope.value = { strategy: (scope.strategy || scope.dataScope || 'All') as string, departmentIds: scope.departmentIds || [] }
  // 进入权限分配面板时，懒加载部门树供「指定部门」多选使用
  await ensureDeptTreeLoaded()
}

function filterPermissionNode(keyword: string, data: PermissionTreeNode) {
  return matchesSearchableTreeNodeDeep(data, keyword)
}

/**
 * 权限树勾选联动：
 * - 勾选节点时，自动向上勾选所有祖先（保证一级菜单/目录在有子权限时必选）
 * - 取消节点时，自动向下取消所有后代（避免子节点已选但父节点未选的矛盾状态）
 * - 父节点可以独立勾选（不依赖子节点）
 */
function onPermCheck(
  data: PermissionTreeNode,
  { checkedKeys }: { checkedKeys: string[]; checkedNodes: PermissionTreeNode[]; halfCheckedKeys: string[]; halfCheckedNodes: PermissionTreeNode[] }
) {
  const checkedSet = new Set<string>(checkedKeys)
  if (checkedSet.has(data.id)) {
    // 勾选：向上自动勾选所有祖先节点
    permCheckAncestors(permissionTree.value, data.id, checkedSet)
  } else {
    // 取消：向下自动取消所有后代节点
    permUncheckDescendants(data, checkedSet)
  }
  permTreeRef.value?.setCheckedKeys([...checkedSet])
}

/** 找到 targetId 的所有祖先链并全部加入 checkedSet；返回是否在当前层找到 targetId */
function permCheckAncestors(nodes: PermissionTreeNode[], targetId: string, checkedSet: Set<string>): boolean {
  for (const node of nodes) {
    if (node.id === targetId) return true
    if (node.children?.length && permCheckAncestors(node.children, targetId, checkedSet)) {
      checkedSet.add(node.id)
      return true
    }
  }
  return false
}

/** 递归取消 node 下所有后代节点 */
function permUncheckDescendants(node: PermissionTreeNode, checkedSet: Set<string>) {
  node.children?.forEach(child => {
    checkedSet.delete(child.id)
    permUncheckDescendants(child, checkedSet)
  })
}

watch(permissionKeyword, value => {
  permTreeRef.value?.filter(value)
})

// 权限类型统计（计算属性）
const permTypeCounts = computed(() => {
  const counts = { Directory: 0, Menu: 0, Item: 0, Button: 0, Api: 0, total: 0 }
  const walk = (nodes: PermissionTreeNode[]) => {
    nodes.forEach(n => {
      const t = n.type || 'unknown'
      if (t in counts) (counts as any)[t]++
      counts.total++
      if (n.children) walk(n.children)
    })
  }
  walk(permissionTree.value)
  return counts
})

// 权限类型中文标签
function permTypeLabel(type?: string): string {
  const map: Record<string, string> = { Directory: '目录', Menu: '菜单', Item: '菜单项', Button: '按钮', Api: '接口' }
  return map[type || ''] || '未知'
}
// 权限类型图标
function permNodeIcon(type?: string): string {
  const map: Record<string, string> = {
    Directory: 'bi bi-folder-fill',
    Menu: 'bi bi-window',
    Item: 'bi bi-file-earmark-text',
    Button: 'bi bi-hand-index-fill',
    Api: 'bi bi-hdd-network-fill'
  }
  return map[type || ''] || 'bi bi-circle'
}
// 权限类型颜色
function permNodeColor(type?: string): string {
  const map: Record<string, string> = {
    Directory: '#f59e0b',
    Menu: '#3b82f6',
    Item: '#6366f1',
    Button: '#10b981',
    Api: '#ef4444'
  }
  return map[type || ''] || '#9ca3af'
}

function selectAllPerms() {
  const allIds: string[] = []
  const walk = (nodes: PermissionTreeNode[]) => { nodes.forEach(n => { allIds.push(n.id); if (n.children) walk(n.children) }) }
  walk(permissionTree.value)
  permTreeRef.value?.setCheckedKeys(allIds)
}

function selectNonePerms() { permTreeRef.value?.setCheckedKeys([]) }

async function savePerms() {
  if (!currentRoleId.value) return
  const ids = (permTreeRef.value?.getCheckedKeys(false) as string[]) || []
  await saveRolePermissions(currentRoleId.value, ids)
  await setRoleDataScope(currentRoleId.value, { dataScope: dataScope.value.strategy, departmentIds: dataScope.value.departmentIds })
  ElMessage.success('已保存')
}

// CRUD
const dialogVisible = ref(false)
const dialogTitle = ref('新增角色')
const form = ref<{ id?: string; name: string; code?: string; enabled: boolean; parentId?: string | null; dataScope?: string; departmentIds?: string[]; allowedClients: string[]; isSuperAdmin: boolean }>({ name: '', enabled: true, allowedClients: [], isSuperAdmin: false, departmentIds: [] })

// 是否为顶级角色（上级角色为空）
const isTopLevel = computed(() => !form.value.parentId)

async function onAdd() {
  dialogTitle.value = '新增角色'
  form.value = { name: '', code: '', enabled: true, parentId: currentRoleId.value || null, dataScope: 'All', departmentIds: [], allowedClients: [], isSuperAdmin: false }
  // 提前拉取部门树，避免「数据范围」切到「指定部门」时出现空下拉
  await ensureDeptTreeLoaded()
  dialogVisible.value = true
}

async function onEdit(row: any) {
  dialogTitle.value = '编辑角色'
  try {
    const detail = await getRoleDetail(row.id)
    const scope = await getRoleDataScope(row.id)
    const ac = detail.allowedClients ? detail.allowedClients.split(',').map((s: string) => s.trim()).filter(Boolean) : []
    form.value = {
      id: detail.id,
      name: detail.name,
      code: detail.code,
      enabled: detail.enabled !== false,
      parentId: detail.parentId,
      dataScope: (scope.strategy || scope.dataScope || 'All') as string,
      departmentIds: scope.departmentIds || [],
      allowedClients: ac,
      isSuperAdmin: detail.isSuperAdmin === true
    }
    // 编辑对话框打开前预拉部门树
    await ensureDeptTreeLoaded()
    dialogVisible.value = true
  } catch (e: any) { ElMessage.error(e?.message || '加载角色信息失败') }
}

async function onDelete(row: any) {
  try {
    await ElMessageBox.confirm(`确定删除角色「${row.name}」？`, '删除确认', { type: 'warning' })
    await deleteRole(row.id)
    ElMessage.success('已删除')
    if (currentRoleId.value === row.id) { currentRoleId.value = ''; currentRoleName.value = ''; checkedPermIds.value = []; permissionTree.value = [] }
    await loadTree()
  } catch (e: any) { if (e !== 'cancel') ElMessage.error(e?.message || '删除失败') }
}

async function submitForm() {
  try {
    if (!form.value.name?.trim()) { ElMessage.warning('请输入角色名称'); return }
    // 「指定部门」必须至少选一个部门，否则该角色用户什么数据都看不到
    if (form.value.dataScope === 'SpecifiedDepartments' && (!form.value.departmentIds || form.value.departmentIds.length === 0)) {
      ElMessage.warning('数据范围为「指定部门」时，请至少选择一个部门')
      return
    }
    let savedRoleId: string | undefined
    if (form.value.id) {
      await updateRole(form.value.id, { name: form.value.name, code: form.value.code, enabled: form.value.enabled, parentId: form.value.parentId, dataScope: form.value.dataScope, allowedClients: form.value.allowedClients.length ? form.value.allowedClients.join(',') : undefined, isSuperAdmin: isTopLevel.value ? form.value.isSuperAdmin : false })
      savedRoleId = form.value.id
      ElMessage.success('保存成功')
      if (currentRoleId.value === form.value.id) currentRoleName.value = form.value.name
    } else {
      const created: any = await createRole({ name: form.value.name, code: form.value.code, enabled: form.value.enabled, parentId: form.value.parentId, dataScope: form.value.dataScope, allowedClients: form.value.allowedClients.length ? form.value.allowedClients.join(',') : undefined, isSuperAdmin: isTopLevel.value ? form.value.isSuperAdmin : false })
      savedRoleId = (created?.id || created?.data?.id) as string | undefined
      ElMessage.success('创建成功')
    }
    // 数据范围 + 指定部门：通过专用接口持久化部门列表（角色主体已写入 dataScope 字段，此处仅刷新部门关联）
    if (savedRoleId) {
      try {
        await setRoleDataScope(savedRoleId, {
          dataScope: form.value.dataScope || 'All',
          departmentIds: form.value.dataScope === 'SpecifiedDepartments' ? (form.value.departmentIds || []) : []
        })
      } catch (e) {
        // 不阻断主流程：角色已保存，仅部门列表写入失败
        console.warn('保存角色部门列表失败', e)
      }
      // 当前选中的就是这个角色，刷新权限面板的数据范围状态
      if (currentRoleId.value === savedRoleId) {
        dataScope.value = {
          strategy: form.value.dataScope || 'All',
          departmentIds: form.value.dataScope === 'SpecifiedDepartments' ? (form.value.departmentIds || []) : []
        }
      }
    }
    dialogVisible.value = false
    await loadTree()
  } catch (e: any) { ElMessage.error(e?.message || '操作失败') }
}

function onRoleClick(node: any) {
  currentRoleId.value = node.id
  currentRoleName.value = node.name
  loadPermissions(node.id)
}

onMounted(loadTree)
</script>

<style scoped>
/* ==================== 页面容器 ==================== */
.roles-page {
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
  color: #8b5cf6;
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

/* ==================== 角色树 ==================== */
.role-tree :deep(.el-tree-node__content) {
  height: 42px;
  padding: 4px 0;
  border-radius: 8px;
  margin-bottom: 2px;
  transition: all 0.2s ease;
}

.role-tree :deep(.el-tree-node__content:hover) {
  background: #f1f5f9;
}

.admin-dark .role-tree :deep(.el-tree-node__content:hover) {
  background: #334155;
}

.role-tree :deep(.el-tree-node.is-current > .el-tree-node__content) {
  background: rgba(139, 92, 246, 0.1);
  border-left: 3px solid #8b5cf6;
}

.admin-dark .role-tree :deep(.el-tree-node.is-current > .el-tree-node__content) {
  background: rgba(139, 92, 246, 0.2);
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
  color: #8b5cf6;
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

.node-code {
  font-size: 12px;
  color: #94a3b8;
  background: #f1f5f9;
  padding: 2px 8px;
  border-radius: 4px;
}

.admin-dark .node-code {
  background: #334155;
}

.node-badge {
  padding: 2px 8px;
  background: #8b5cf6;
  color: #fff;
  font-size: 11px;
  border-radius: 10px;
}

.node-actions {
  display: none;
  gap: 4px;
}

.tree-node-content:hover .node-actions {
  display: flex;
}

/* ==================== 右侧权限面板 ==================== */
.perm-card {
  border-radius: 12px;
  border: 1px solid #e5e7eb;
}

.admin-dark .perm-card {
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

/* ==================== 权限内容 ==================== */
.perm-content {
  padding: 4px 0;
}

.scope-row {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.scope-tip {
  margin-top: 8px;
  font-size: 12px;
  color: #94a3b8;
  display: flex;
  align-items: center;
  gap: 6px;
  line-height: 1.6;
}

.scope-tip i {
  color: #f59e0b;
}

.scope-label {
  color: #475569;
  font-weight: 500;
  display: flex;
  align-items: center;
  gap: 6px;
}

.scope-label i {
  color: #10b981;
}

.admin-dark .scope-label {
  color: #cbd5e1;
}

.perm-search-row {
  margin: 0 0 12px;
}

.perm-search-input {
  max-width: 420px;
}

.perm-tree :deep(.el-tree-node__content) {
  padding: 4px 0;
  border-radius: 6px;
  height: 36px;
}

.perm-tree :deep(.el-tree-node__content:hover) {
  background: #f0fdf4;
}

.admin-dark .perm-tree :deep(.el-tree-node__content:hover) {
  background: #334155;
}

/* ==================== 权限类型统计 ==================== */
.perm-stats {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  padding: 0 4px;
}

.perm-stat-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 13px;
  color: #6b7280;
  padding: 4px 10px;
  border-radius: 8px;
  background: #f9fafb;
  border: 1px solid #e5e7eb;
  transition: all 0.2s ease;
}

.admin-dark .perm-stat-item {
  color: #9ca3af;
  background: #1e293b;
  border-color: #374151;
}

.perm-stat-item b {
  font-weight: 700;
  margin-left: 2px;
}

.perm-stat-directory i { color: #f59e0b; }
.perm-stat-directory b { color: #f59e0b; }

.perm-stat-menu i { color: #3b82f6; }
.perm-stat-menu b { color: #3b82f6; }

.perm-stat-item-type i { color: #6366f1; }
.perm-stat-item-type b { color: #6366f1; }

.perm-stat-button i { color: #10b981; }
.perm-stat-button b { color: #10b981; }

.perm-stat-api i { color: #ef4444; }
.perm-stat-api b { color: #ef4444; }

.perm-stat-total {
  font-weight: 500;
  color: #374151;
}

.perm-stat-total b {
  color: #8b5cf6;
}

.admin-dark .perm-stat-total {
  color: #e5e7eb;
}

/* ==================== 权限节点类型标签 ==================== */
.perm-node-content {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
  min-width: 0;
}

.perm-node-icon {
  font-size: 14px;
  flex-shrink: 0;
}

.perm-node-label {
  flex: 1;
  font-size: 13px;
  color: #334155;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.admin-dark .perm-node-label {
  color: #e2e8f0;
}

.perm-node-code {
  flex-shrink: 1;
  min-width: 0;
  max-width: 260px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: #64748b;
  font-size: 12px;
  font-family: Consolas, 'Courier New', monospace;
}

.admin-dark .perm-node-code {
  color: #94a3b8;
}

.perm-type-tag {
  flex-shrink: 0;
  font-size: 11px;
  padding: 1px 8px;
  border-radius: 10px;
  font-weight: 500;
  line-height: 18px;
  letter-spacing: 0.5px;
}

.perm-type-directory {
  background: rgba(245, 158, 11, 0.12);
  color: #d97706;
  border: 1px solid rgba(245, 158, 11, 0.25);
}

.perm-type-menu {
  background: rgba(59, 130, 246, 0.12);
  color: #2563eb;
  border: 1px solid rgba(59, 130, 246, 0.25);
}

.perm-type-item {
  background: rgba(99, 102, 241, 0.12);
  color: #4f46e5;
  border: 1px solid rgba(99, 102, 241, 0.25);
}

.perm-type-button {
  background: rgba(16, 185, 129, 0.12);
  color: #059669;
  border: 1px solid rgba(16, 185, 129, 0.25);
}

.perm-type-api {
  background: rgba(239, 68, 68, 0.12);
  color: #dc2626;
  border: 1px solid rgba(239, 68, 68, 0.25);
}

.perm-type-unknown {
  background: rgba(156, 163, 175, 0.12);
  color: #6b7280;
  border: 1px solid rgba(156, 163, 175, 0.25);
}

/* 深色模式下的类型标签 */
.admin-dark .perm-type-directory {
  background: rgba(245, 158, 11, 0.2);
  color: #fbbf24;
  border-color: rgba(245, 158, 11, 0.35);
}

.admin-dark .perm-type-menu {
  background: rgba(59, 130, 246, 0.2);
  color: #60a5fa;
  border-color: rgba(59, 130, 246, 0.35);
}

.admin-dark .perm-type-item {
  background: rgba(99, 102, 241, 0.2);
  color: #818cf8;
  border-color: rgba(99, 102, 241, 0.35);
}

.admin-dark .perm-type-button {
  background: rgba(16, 185, 129, 0.2);
  color: #34d399;
  border-color: rgba(16, 185, 129, 0.35);
}

.admin-dark .perm-type-api {
  background: rgba(239, 68, 68, 0.2);
  color: #f87171;
  border-color: rgba(239, 68, 68, 0.35);
}

.admin-dark .perm-type-unknown {
  background: rgba(156, 163, 175, 0.2);
  color: #9ca3af;
  border-color: rgba(156, 163, 175, 0.35);
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

/* ==================== 登录权限提示 ==================== */
.allowed-clients-tip,
.enabled-tip,
.super-admin-tip {
  font-size: 12px;
  color: #9ca3af;
  margin-top: 4px;
  line-height: 1.4;
}

.super-admin-tip {
  color: #e67e22;
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
  .roles-page {
    padding: 16px;
  }
}
</style>
