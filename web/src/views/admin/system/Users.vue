<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="users-page">
    <DataTable
      class="table-card"
      :data="tableData"
      :loading="loading"
      :columns="columns"
      :pagination="pagination"
      :search-config="searchConfig"
      :actions="rowActions"
      :import-config="{ enabled: true, templateUrl: '', showPreview: true }"
      :print-config="{ enabled: true, title: '用户列表', showPreview: true }"
      :show-selection="true"
      :show-column-settings="true"
      :show-export="true"
      :compact-mode="true"
      :default-expand-search="false"
      cache-key="users"
      @search="onDtSearch"
      @page-change="handlePageChange"
      @size-change="handleSizeChange"
      @sort-change="onDtSortChange"
      @selection-change="handleSelectionChange"
      @action-click="onRowAction"
    >
      <!-- 页面标题 -->
      <template #header>
        <h2>用户管理</h2>
        <p>管理系统用户账户和权限</p>
      </template>

      <!-- 页面操作按钮 -->
      <template #header-actions>
        <el-button
          v-if="selectedIds.length > 0"
          v-permission="'/system/users:delete'"
          type="danger"
          :icon="Delete"
          @click="handleBatchDelete"
        >
          批量删除 ({{ selectedIds.length }})
        </el-button>
        <el-button v-permission="'/system/users:add'" type="primary" :icon="Plus" @click="handleAdd">
          新增用户
        </el-button>
      </template>

      <!-- 列自定义 -->
      <template #column-enabled="{ row }">
        <el-switch
          :model-value="row.enabled"
          :loading="switchLoadingSet.has(row.id)"
          @change="(val:boolean)=>onToggleStatus(row, val)"
        />
      </template>
      <template #column-departmentNames="{ row }">
        <template v-if="row.departmentNames && row.departmentNames.length">
          <el-tag
            v-for="(name, idx) in row.departmentNames"
            :key="idx"
            size="small"
            type="info"
            effect="plain"
            style="margin-right: 6px; margin-bottom: 4px;"
          >{{ name }}</el-tag>
        </template>
        <span v-else>-</span>
      </template>
      <template #column-roleNames="{ row }">
        <template v-if="row.roleNames && row.roleNames.length">
          <el-tag
            v-for="(name, idx) in row.roleNames"
            :key="idx"
            size="small"
            type="success"
            effect="plain"
            style="margin-right: 6px; margin-bottom: 4px;"
          >{{ name }}</el-tag>
        </template>
        <span v-else>-</span>
      </template>
      <template #column-createdAt="{ row }">
        {{ formatDateTime(row.createdAt) }}
      </template>

      <!-- 行操作：来自 DataTable 默认插槽，用于触发编辑/重置密码/删除 -->
      <template #actions="{ row }">
        <el-button v-permission="'/system/users:edit'" class="row-action-btn" type="primary" link size="small" :icon="Edit" @click="handleEdit(row)">编辑</el-button>
        <el-button v-permission="'/system/users:reset-password'" class="row-action-btn" type="warning" link size="small" :icon="Key" @click="handleResetPassword(row)">重置密码</el-button>
        <el-button v-permission="'/system/users:delete'" class="row-action-btn" type="danger" link size="small" :icon="Delete" @click="handleDelete(row)">删除</el-button>
      </template>
    </DataTable>

    <!-- 新增/编辑用户对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogTitle"
      width="600px"
      :close-on-click-modal="false"
      @closed="handleDialogClosed"
    >
      <el-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        label-width="100px"
      >
        <el-form-item label="用户名" prop="userName">
          <el-input
            v-model="formData.userName"
            placeholder="请输入用户名"
            :disabled="isEdit"
          />
        </el-form-item>
        <el-form-item label="姓名" prop="displayName">
          <el-input v-model="formData.displayName" placeholder="请输入姓名" />
        </el-form-item>
        <el-form-item v-if="!isEdit" label="密码" prop="password">
          <el-input
            v-model="formData.password"
            type="password"
            placeholder="请输入密码"
            show-password
          />
        </el-form-item>
        <el-form-item v-if="!isEdit" label="确认密码" prop="confirmPassword">
          <el-input
            v-model="formData.confirmPassword"
            type="password"
            placeholder="请再次输入密码"
            show-password
          />
        </el-form-item>
        <el-form-item label="邮箱" prop="email">
          <el-input v-model="formData.email" placeholder="请输入邮箱" />
        </el-form-item>
        <el-form-item label="手机号" prop="phone">
          <el-input v-model="formData.phone" placeholder="请输入手机号" />
        </el-form-item>
        <el-form-item label="部门" prop="departmentIds">
          <el-tree-select
            v-model="formData.departmentIds"
            :data="departmentTree"
            :props="{ label: 'name', value: 'id', children: 'children' }"
            node-key="id"
            placeholder="请选择部门"
            multiple
            filterable
            check-strictly
            style="width: 100%;"
          />
        </el-form-item>
        <el-form-item label="角色" prop="roleIds">
          <el-tree-select
            v-model="formData.roleIds"
            :data="roleTree"
            :props="{ label: 'name', value: 'id', children: 'children' }"
            node-key="id"
            placeholder="请选择角色"
            multiple
            filterable
            check-strictly
            show-checkbox
            style="width: 100%;"
          />
        </el-form-item>
        <el-form-item label="状态" prop="enabled">
          <el-switch v-model="formData.enabled" />
          <span class="form-tip">{{ formData.enabled ? '启用' : '禁用' }}</span>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="handleSubmit">
          确定
        </el-button>
      </template>
    </el-dialog>

    <!-- 重置密码对话框 -->
    <el-dialog
      v-model="resetPasswordDialogVisible"
      title="重置密码"
      width="400px"
      :close-on-click-modal="false"
    >
      <el-form
        ref="resetPasswordFormRef"
        :model="resetPasswordForm"
        :rules="resetPasswordRules"
        label-width="100px"
      >
        <el-form-item label="新密码" prop="newPassword">
          <el-input
            v-model="resetPasswordForm.newPassword"
            type="password"
            placeholder="请输入新密码"
            show-password
          />
        </el-form-item>
        <el-form-item label="确认密码" prop="confirmPassword">
          <el-input
            v-model="resetPasswordForm.confirmPassword"
            type="password"
            placeholder="请再次输入新密码"
            show-password
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="resetPasswordDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="resettingPassword" @click="handleResetPasswordSubmit">
          确定
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>




<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import DataTable from '@/components/DataTable/index.vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { Plus, Search, Refresh, Edit, Delete, Key } from '@element-plus/icons-vue'
import {
  getUsers,
  getUserDetail,
  createUser,
  updateUser,
  deleteUser,
  batchDeleteUsers,
  resetUserPassword,
  toggleUserStatus,
  getUserRoleIds,
  saveUserRoles,
  getUserDepartmentIds,
  saveUserDepartments,
  type UserListItemDto,
  type CreateUserInput,
  type UpdateUserInput
} from '@/api/users'
import { getDepartmentTree, type DepartmentTreeNodeDto } from '@/api/department'
import { getRoleTree, type RoleTreeNode } from '@/api/role'

// ==================== 数据定义 ====================

// 搜索表单
const searchForm = reactive({
  keyword: '',
  departmentId: '',
  roleId: '',
  enabled: undefined as boolean | undefined
})

// 分页信息
const pagination = reactive({
  page: 1,
  pageSize: 20,
  total: 0
})

// 表格数据
const tableData = ref<UserListItemDto[]>([])
const loading = ref(false)
const selectedIds = ref<string[]>([])
const tableRef = ref()

// DataTable columns
const columns = [
  { prop: 'userName', label: '用户名', minWidth: 130, sortable: true },
  { prop: 'displayName', label: '姓名', minWidth: 100 },
  { prop: 'email', label: '邮箱', minWidth: 160 },
  { prop: 'phone', label: '手机号', minWidth: 120 },
  { prop: 'departmentNames', label: '部门', minWidth: 140, slot: 'column-departmentNames' },
  { prop: 'roleNames', label: '角色', minWidth: 160, slot: 'column-roleNames' },
  { prop: 'enabled', label: '状态', width: 80, slot: 'column-enabled' },
  { prop: 'createdAt', label: '创建时间', minWidth: 160, slot: 'column-createdAt' },
]

// Search config
const searchConfig: any[] = [
  { key: 'keyword', label: '关键字', type: 'input' as const, placeholder: '用户名/姓名/邮箱/手机号', simple: true },
  { key: 'enabled', label: '状态', type: 'select' as const, options: [ { label: '启用', value: true }, { label: '禁用', value: false } ], simple: true },
  { key: 'departmentId', label: '部门', type: 'tree' as const, options: [], placeholder: '请选择部门', span: 8, multiple: true },
  { key: 'roleId', label: '角色', type: 'tree' as const, options: [], placeholder: '请选择角色', span: 8, multiple: true },
]

// Row actions
const rowActions: any[] = [
  { key: 'edit', label: '编辑', type: 'primary' as const, icon: Edit },
  { key: 'resetPwd', label: '重置密码', type: 'warning' as const, icon: Key },
  { key: 'delete', label: '删除', type: 'danger' as const, icon: Delete },
]

// 部门树和角色树
const departmentTree = ref<DepartmentTreeNodeDto[]>([])
const roleTree = ref<RoleTreeNode[]>([])

// 对话框
const dialogVisible = ref(false)
const dialogTitle = computed(() => isEdit.value ? '编辑用户' : '新增用户')
const isEdit = ref(false)
const submitting = ref(false)

// 表单
const formRef = ref<FormInstance>()
const formData = reactive({
  id: '',
  userName: '',
  displayName: '',
  password: '',
  confirmPassword: '',
  email: '',
  phone: '',
  departmentIds: [] as string[],
  roleIds: [] as string[],
  enabled: true
})

// 表单验证规则
const validateConfirmPassword = (rule: any, value: any, callback: any) => {
  if (value === '') {
    callback(new Error('请再次输入密码'))
  } else if (value !== formData.password) {
    callback(new Error('两次输入密码不一致'))
  } else {
    callback()
  }
}

const formRules: FormRules = {
  userName: [
    { required: true, message: '请输入用户名', trigger: 'blur' },
    { min: 2, max: 64, message: '用户名长度在 2 到 64 个字符', trigger: 'blur' }
  ],
  displayName: [
    { required: true, message: '请输入姓名', trigger: 'blur' },
    { min: 2, max: 128, message: '姓名长度在 2 到 128 个字符', trigger: 'blur' }
  ],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 6, max: 128, message: '密码长度在 6 到 128 个字符', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, validator: validateConfirmPassword, trigger: 'blur' }
  ],
  email: [
    { type: 'email', message: '请输入正确的邮箱地址', trigger: 'blur' }
  ],
  phone: [
    { pattern: /^1[3-9]\d{9}$/, message: '请输入正确的手机号', trigger: 'blur' }
  ],
  departmentIds: [
    { required: true, message: '请选择部门', trigger: 'change' }
  ],
  roleIds: [
    { required: true, message: '请选择角色', trigger: 'change' }
  ]
}

// 重置密码对话框
const resetPasswordDialogVisible = ref(false)
const resetPasswordFormRef = ref<FormInstance>()
const resetPasswordForm = reactive({
  userId: '',
  userName: '',
  newPassword: '',
  confirmPassword: ''
})
const resettingPassword = ref(false)

const validateResetConfirmPassword = (rule: any, value: any, callback: any) => {
  if (value === '') {
    callback(new Error('请再次输入密码'))
  } else if (value !== resetPasswordForm.newPassword) {
    callback(new Error('两次输入密码不一致'))
  } else {
    callback()
  }
}

const resetPasswordRules: FormRules = {
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, max: 128, message: '密码长度在 6 到 128 个字符', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, validator: validateResetConfirmPassword, trigger: 'blur' }
  ]
}

// ==================== 工具函数 ====================

// 格式化日期时间
function formatDateTime(dateStr?: string): string {
  if (!dateStr) return '-'
  const date = new Date(dateStr)
  return date.toLocaleString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  })
}

// ==================== 数据加载（统一 filters 查询） ====================

// 最近一次查询参数（统一结构）：{ filters, page, pageSize, sortProp, sortOrder }
const lastQuery = ref<any>({ page: pagination.page, pageSize: pagination.pageSize })

// 加载用户列表
async function loadUsers(query?: any) {
  try {
    loading.value = true
    if (query) lastQuery.value = { ...lastQuery.value, ...query }
    // 同步分页到 UI
    if (lastQuery.value?.page) pagination.page = lastQuery.value.page
    if (lastQuery.value?.pageSize) pagination.pageSize = lastQuery.value.pageSize

    const result = await getUsers(lastQuery.value)
    tableData.value = result.items
    pagination.total = result.total
  } catch (error) {
    ElMessage.error('加载用户列表失败')
  } finally {
    loading.value = false
  }
}

// 加载部门树
async function loadDepartmentTree() {
  try {
    const data = await getDepartmentTree()
    departmentTree.value = Array.isArray(data) ? data : []
    // 同步到 DataTable 搜索配置
    const nodeMap: any = (arr:any[]): any[] => arr.map(n=> ({ label: n.name, value: n.id, children: Array.isArray(n.children)? nodeMap(n.children): [] }))
    const depOpt = nodeMap(departmentTree.value as any)
    const depField = searchConfig.find(s=> s.key==='departmentId') as any
    if (depField) depField.options = depOpt
  } catch (error) {
    ElMessage.error('加载部门树失败')
  }
}

// 加载角色树
async function loadRoleTree() {
  try {
    const data = await getRoleTree()
    roleTree.value = Array.isArray(data) ? data : []
    const nodeMap: any = (arr:any[]): any[] => arr.map(n=> ({ label: n.name, value: n.id, children: Array.isArray(n.children)? nodeMap(n.children): [] }))
    const roleOpt = nodeMap(roleTree.value as any)
    const roleField = searchConfig.find(s=> s.key==='roleId') as any
    if (roleField) roleField.options = roleOpt
  } catch (error) {
    ElMessage.error('加载角色树失败')
  }
}

// ==================== 搜索和筛选（统一 payload 直传） ====================
function onDtSearch(params: Record<string, any>) {
  // 统一 filters：将 departmentId/roleId 转换为关系查询描述
  const payload = { ...params }
  const filters = { ...(params.filters || {}) } as any

  const normalizeToArray = (v: any) => (Array.isArray(v) ? v : (v !== undefined && v !== null && v !== '' ? [v] : []))

  const dep = filters.departmentId
  if (dep !== undefined) {
    const ids = normalizeToArray(dep)
    delete filters.departmentId
    if (ids.length) {
      filters.relations = filters.relations || {}
      filters.relations.departments = { ids, mode: 'in', includeDescendants: true }
    }
  }

  const role = filters.roleId
  if (role !== undefined) {
    const ids = normalizeToArray(role)
    delete filters.roleId
    if (ids.length) {
      filters.relations = filters.relations || {}
      filters.relations.roles = { ids, mode: 'in' }
    }
  }

  payload.filters = filters
  loadUsers({ ...payload, page: payload.page || 1 })
}

function onDtSortChange(sort: { prop?: string; order?: string }) {
  loadUsers({ sortProp: sort.prop, sortOrder: sort.order, page: 1 })
}

// ==================== 表格操作 ====================

// 选择变化
function handleSelectionChange(selection: UserListItemDto[]) {
  selectedIds.value = selection.map(item => item.id)
}

function onRowAction(action: string, row: any) {
  switch (action) {
    case 'edit':
      handleEdit(row)
      break
    case 'resetPwd':
      handleResetPassword(row)
      break
    case 'delete':
      handleDelete(row)
      break
  }
}

// 分页大小改变
function handleSizeChange(size: number) {
  pagination.pageSize = size
  pagination.page = 1
  loadUsers()
}

// 当前页改变
function handlePageChange(page: number) {
  pagination.page = page
  loadUsers()
}

// ==================== 新增/编辑用户 ====================

// 新增用户
function handleAdd() {
  isEdit.value = false
  resetFormData()
  dialogVisible.value = true
}

// 编辑用户
async function handleEdit(row: UserListItemDto) {
  try {
    isEdit.value = true
    const detail = await getUserDetail(row.id)
    const [departmentIds, roleIds] = await Promise.all([
      getUserDepartmentIds(row.id),
      getUserRoleIds(row.id)
    ])

    formData.id = detail.id
    formData.userName = detail.userName
    formData.displayName = detail.displayName
    formData.email = detail.email || ''
    formData.phone = detail.phone || ''
    formData.departmentIds = departmentIds
    formData.roleIds = roleIds
    formData.enabled = detail.enabled

    dialogVisible.value = true
  } catch (error) {
    ElMessage.error('加载用户详情失败')
  }
}

// 提交表单
async function handleSubmit() {
  if (!formRef.value) return

  try {
    await formRef.value.validate()
    submitting.value = true

    if (isEdit.value) {
      // 更新用户
      const updateData: UpdateUserInput = {
        displayName: formData.displayName,
        email: formData.email || undefined,
        phone: formData.phone || undefined,
        enabled: formData.enabled
      }
      await updateUser(formData.id, updateData)

      // 保存部门和角色
      await Promise.all([
        saveUserDepartments(formData.id, formData.departmentIds),
        saveUserRoles(formData.id, formData.roleIds)
      ])

      ElMessage.success('更新用户成功')
    } else {
      // 创建用户
      const createData: CreateUserInput = {
        userName: formData.userName,
        displayName: formData.displayName,
        password: formData.password,
        email: formData.email || undefined,
        phone: formData.phone || undefined,
        enabled: formData.enabled
      }
      const userId = await createUser(createData)

      // 保存部门和角色
      await Promise.all([
        saveUserDepartments(userId, formData.departmentIds),
        saveUserRoles(userId, formData.roleIds)
      ])

      ElMessage.success('创建用户成功')
    }

    dialogVisible.value = false
    loadUsers()
  } catch (error) {
    ElMessage.error('保存用户失败')
  } finally {
    submitting.value = false
  }
}

// 对话框关闭
function handleDialogClosed() {
  formRef.value?.resetFields()
  resetFormData()
}

// 重置表单数据
function resetFormData() {
  formData.id = ''
  formData.userName = ''
  formData.displayName = ''
  formData.password = ''
  formData.confirmPassword = ''
  formData.email = ''
  formData.phone = ''
  formData.departmentIds = []
  formData.roleIds = []
  formData.enabled = true
}

// ==================== 删除用户 ====================

// 删除用户
async function handleDelete(row: UserListItemDto) {
  try {
    await ElMessageBox.confirm(
      `确定要删除用户 "${row.displayName}" 吗？`,
      '确认删除',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )

    await deleteUser(row.id)
    ElMessage.success('删除用户成功')
    loadUsers()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error('删除用户失败')
    }
  }
}

// 批量删除用户
async function handleBatchDelete() {
  if (selectedIds.value.length === 0) {
    ElMessage.warning('请选择要删除的用户')
    return
  }

  try {
    await ElMessageBox.confirm(
      `确定要删除选中的 ${selectedIds.value.length} 个用户吗？`,
      '确认批量删除',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      }
    )

    await batchDeleteUsers(selectedIds.value)
    ElMessage.success('批量删除用户成功')
    selectedIds.value = []
    loadUsers()
  } catch (error: any) {
    if (error !== 'cancel') {
      ElMessage.error('批量删除用户失败')
    }
  }
}

// ==================== 重置密码 ====================

// 重置密码
function handleResetPassword(row: UserListItemDto) {
  resetPasswordForm.userId = row.id
  resetPasswordForm.userName = row.userName
  resetPasswordForm.newPassword = ''
  resetPasswordForm.confirmPassword = ''
  resetPasswordDialogVisible.value = true
}

// 提交重置密码
async function handleResetPasswordSubmit() {
  if (!resetPasswordFormRef.value) return

  try {
    await resetPasswordFormRef.value.validate()
    resettingPassword.value = true

    await resetUserPassword(resetPasswordForm.userId, {
      newPassword: resetPasswordForm.newPassword
    })

    ElMessage.success('重置密码成功')
    resetPasswordDialogVisible.value = false
  } catch (error) {
    ElMessage.error('重置密码失败')
  } finally {
    resettingPassword.value = false
  }
}

// ==================== 切换用户状态 ====================

// 状态开关点击
const switchLoadingSet = ref<Set<string>>(new Set())
async function onToggleStatus(row: UserListItemDto, val: boolean) {
  if (!row) return
  try {
    switchLoadingSet.value.add(row.id)
    await toggleUserStatus(row.id, val)
    row.enabled = val
    ElMessage.success(val ? '已启用' : '已禁用')
  } catch (error) {
    ElMessage.error('状态更新失败')
  } finally {
    switchLoadingSet.value.delete(row.id)
  }
}

// ==================== 生命周期 ====================

onMounted(() => {
  loadUsers()
  loadDepartmentTree()
  loadRoleTree()
})

</script>

<style scoped>
/* ==================== 页面容器 ==================== */
.users-page {
  padding: 24px;
  background: var(--el-bg-color-page);
  min-height: 100vh;
  animation: fadeIn 0.3s ease-out;
}

@keyframes fadeIn {
  from {
    opacity: 0;
    transform: translateY(10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* ==================== 对话框优化 ==================== */
:deep(.el-dialog) {
  border-radius: 12px;
  overflow: hidden;
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

/* ==================== 表单优化 ==================== */
:deep(.el-form-item__label) {
  font-weight: 500;
  color: #374151;
}

.admin-dark :deep(.el-form-item__label) {
  color: #e5e7eb;
}

:deep(.el-input__wrapper),
:deep(.el-select .el-input__wrapper),
:deep(.el-tree-select .el-input__wrapper) {
  border-radius: 8px;
  transition: all 0.2s ease;
}

:deep(.el-input__wrapper:hover),
:deep(.el-select .el-input__wrapper:hover),
:deep(.el-tree-select .el-input__wrapper:hover) {
  border-color: #3b82f6;
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.1);
}

:deep(.el-input__wrapper.is-focus),
:deep(.el-select .el-input__wrapper.is-focus),
:deep(.el-tree-select .el-input__wrapper.is-focus) {
  border-color: #3b82f6;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.15);
}

/* 表单提示 */
.form-tip {
  margin-left: 12px;
  font-size: 13px;
  color: #6b7280;
  font-weight: 500;
}

.admin-dark .form-tip {
  color: #9ca3af;
}

/* ==================== 响应式布局 ==================== */
@media (max-width: 768px) {
  .users-page {
    padding: 16px;
  }

  /* 移动端响应式样式由 DataTable 组件处理 */

  :deep(.el-dialog) {
    width: 90% !important;
    margin: 20px auto;
  }

  :deep(.el-dialog__body) {
    padding: 16px;
  }

  :deep(.el-form-item) {
    margin-bottom: 16px;
  }
}

/* ==================== 加载动画 ==================== */
:deep(.el-loading-spinner) {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
}

:deep(.el-loading-spinner .circular) {
  width: 48px;
  height: 48px;
}

:deep(.el-loading-text) {
  font-size: 14px;
  font-weight: 500;
  color: #3b82f6;
}

.admin-dark :deep(.el-loading-text) {
  color: #60a5fa;
}

/* 行操作按钮在暗黑模式下的可读性修复 */
.admin-dark .row-action-btn {
  color: #cbd5e1 !important; /* 更亮的文字颜色 */
}
.admin-dark .row-action-btn.el-button--primary {
  color: #90caf9 !important;
}
.admin-dark .row-action-btn.el-button--warning {
  color: #fbbf24 !important;
}
.admin-dark .row-action-btn.el-button--danger {
  color: #f87171 !important;
}
.admin-dark .row-action-btn:hover {
  background: rgba(255, 255, 255, 0.06) !important;
}
</style>

