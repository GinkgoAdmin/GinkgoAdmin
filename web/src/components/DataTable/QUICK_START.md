# DataTable 快速开始指南

## 📦 安装

DataTable 组件已内置在项目中，无需额外安装。

## 🚀 快速开始

### 1. 基础示例

```vue
<template>
  <div class="page">
    <DataTable
      :data="users"
      :loading="loading"
      :columns="columns"
      :pagination="pagination"
      @page-change="handlePageChange"
      @size-change="handleSizeChange"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import DataTable from '@/components/DataTable/index.vue'
import type { ColumnConfig } from '@/components/DataTable/types'

// 表格数据
const users = ref([
  { id: '1', name: '张三', email: 'zhangsan@example.com', age: 28 },
  { id: '2', name: '李四', email: 'lisi@example.com', age: 32 }
])

const loading = ref(false)

// 列配置
const columns: ColumnConfig[] = [
  { prop: 'name', label: '姓名', width: 120 },
  { prop: 'email', label: '邮箱', minWidth: 200 },
  { prop: 'age', label: '年龄', width: 100, align: 'center', sortable: true }
]

// 分页配置
const pagination = reactive({
  total: 100,
  page: 1,
  pageSize: 20,
  pageSizes: [10, 20, 50, 100]
})

function handlePageChange(page: number) {
  pagination.page = page
  // 加载数据...
}

function handleSizeChange(size: number) {
  pagination.pageSize = size
  pagination.page = 1
  // 加载数据...
}
</script>
```

---

### 2. 带搜索的完整示例

```vue
<template>
  <div class="page">
    <DataTable
      :data="users"
      :loading="loading"
      :columns="columns"
      :pagination="pagination"
      :search-config="searchConfig"
      cache-key="users"
      @search="handleSearch"
      @page-change="handlePageChange"
      @size-change="handleSizeChange"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive } from 'vue'
import DataTable from '@/components/DataTable/index.vue'
import type { ColumnConfig, SearchFieldConfig } from '@/components/DataTable/types'

// ... 表格数据和列配置 ...

// 搜索配置
const searchConfig: SearchFieldConfig[] = [
  {
    key: 'keyword',
    label: '关键字',
    type: 'input',
    placeholder: '姓名/邮箱',
    clearable: true,
    span: 6
  },
  {
    key: 'status',
    label: '状态',
    type: 'select',
    options: [
      { label: '启用', value: true },
      { label: '禁用', value: false }
    ],
    span: 4
  },
  {
    key: 'dateRange',
    label: '创建时间',
    type: 'daterange',
    span: 8
  }
]

function handleSearch(params: Record<string, any>) {
  console.log('搜索参数:', params)
  pagination.page = 1
  // 重新加载数据...
}
</script>
```

---

### 3. 带操作列的示例

```vue
<template>
  <div class="page">
    <DataTable
      :data="users"
      :loading="loading"
      :columns="columns"
      :pagination="pagination"
      :actions="rowActions"
      @action-click="handleAction"
    />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { Edit, Delete, Key } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import DataTable from '@/components/DataTable/index.vue'
import type { ActionConfig } from '@/components/DataTable/types'

// ... 表格数据和列配置 ...

// 操作列配置
const rowActions: ActionConfig[] = [
  {
    label: '编辑',
    type: 'primary',
    icon: Edit,
    handler: (row) => {
      console.log('编辑', row)
      // 打开编辑对话框...
    }
  },
  {
    label: '重置密码',
    type: 'warning',
    icon: Key,
    handler: async (row) => {
      try {
        await ElMessageBox.confirm('确定要重置密码吗？')
        // 调用重置密码 API...
        ElMessage.success('重置成功')
      } catch {}
    },
    visible: (row) => row.userName !== 'admin'
  },
  {
    label: '删除',
    type: 'danger',
    icon: Delete,
    handler: async (row) => {
      try {
        await ElMessageBox.confirm('确定要删除吗？', '警告', { type: 'warning' })
        // 调用删除 API...
        ElMessage.success('删除成功')
      } catch {}
    },
    disabled: (row) => row.userName === 'admin'
  }
]

function handleAction(actionLabel: string, row: any) {
  console.log('操作:', actionLabel, row)
}
</script>
```

---

### 4. 带批量操作的示例

```vue
<template>
  <div class="page">
    <DataTable
      :data="users"
      :loading="loading"
      :columns="columns"
      :pagination="pagination"
      :show-selection="true"
      :batch-actions="batchActions"
      @selection-change="handleSelectionChange"
      @batch-action="handleBatchAction"
    />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { Delete, Lock, Unlock } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import DataTable from '@/components/DataTable/index.vue'
import type { BatchActionConfig } from '@/components/DataTable/types'

// ... 表格数据和列配置 ...

const selectedRows = ref<any[]>([])

// 批量操作配置
const batchActions: BatchActionConfig[] = [
  {
    key: 'delete',
    label: '批量删除',
    type: 'danger',
    icon: Delete,
    handler: async (rows) => {
      try {
        await ElMessageBox.confirm(`确定要删除选中的 ${rows.length} 个用户吗？`)
        // 调用批量删除 API...
        ElMessage.success('删除成功')
      } catch {}
    }
  },
  {
    key: 'enable',
    label: '批量启用',
    type: 'success',
    icon: Unlock,
    handler: async (rows) => {
      // 调用批量启用 API...
      ElMessage.success('启用成功')
    }
  }
]

function handleSelectionChange(selection: any[]) {
  selectedRows.value = selection
}

function handleBatchAction(action: string, selection: any[]) {
  console.log('批量操作:', action, selection)
}
</script>
```

---

### 5. 自定义列渲染

```vue
<template>
  <div class="page">
    <DataTable
      :data="users"
      :loading="loading"
      :columns="columns"
      :pagination="pagination"
    >
      <!-- 自定义状态列 -->
      <template #column-enabled="{ row }">
        <el-tag :type="row.enabled ? 'success' : 'danger'" size="small">
          {{ row.enabled ? '启用' : '禁用' }}
        </el-tag>
      </template>

      <!-- 自定义头像列 -->
      <template #column-avatar="{ row }">
        <el-avatar :src="row.avatar" :size="32">
          {{ row.name.charAt(0) }}
        </el-avatar>
      </template>

      <!-- 自定义日期列 -->
      <template #column-createdAt="{ row }">
        {{ formatDate(row.createdAt) }}
      </template>
    </DataTable>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import DataTable from '@/components/DataTable/index.vue'
import type { ColumnConfig } from '@/components/DataTable/types'

// ... 表格数据 ...

const columns: ColumnConfig[] = [
  { prop: 'avatar', label: '头像', width: 80, slot: 'column-avatar' },
  { prop: 'name', label: '姓名', width: 120 },
  { prop: 'enabled', label: '状态', width: 100, slot: 'column-enabled' },
  { prop: 'createdAt', label: '创建时间', width: 180, slot: 'column-createdAt' }
]

function formatDate(date: string) {
  return new Date(date).toLocaleDateString('zh-CN')
}
</script>
```

---

### 6. 完整功能示例（推荐）

```vue
<template>
  <div class="page">
    <!-- 页面头部 -->
    <div class="page-header">
      <div class="page-title">
        <h2>用户管理</h2>
        <p>管理系统用户账户和权限</p>
      </div>
      <div class="page-actions">
        <el-button type="primary" :icon="Plus" @click="handleAdd">
          新增用户
        </el-button>
      </div>
    </div>

    <!-- 数据表格 -->
    <DataTable
      :data="users"
      :loading="loading"
      :columns="columns"
      :pagination="pagination"
      :search-config="searchConfig"
      :actions="rowActions"
      :batch-actions="batchActions"
      :show-selection="true"
      :show-index="true"
      :show-column-settings="true"
      :show-export="true"
      cache-key="users"
      @search="handleSearch"
      @page-change="handlePageChange"
      @size-change="handleSizeChange"
      @sort-change="handleSort"
      @selection-change="handleSelectionChange"
      @action-click="handleAction"
      @batch-action="handleBatchAction"
    >
      <!-- 自定义列 -->
      <template #column-enabled="{ row }">
        <el-switch
          v-model="row.enabled"
          @change="handleToggleStatus(row)"
        />
      </template>
    </DataTable>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { Plus, Edit, Delete, Key, Lock, Unlock } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import DataTable from '@/components/DataTable/index.vue'
import type { ColumnConfig, SearchFieldConfig, ActionConfig, BatchActionConfig } from '@/components/DataTable/types'
import { getUsers, deleteUser, batchDeleteUsers } from '@/api/users'

// 表格数据
const users = ref<any[]>([])
const loading = ref(false)

// 列配置
const columns: ColumnConfig[] = [
  { prop: 'userName', label: '用户名', width: 140, sortable: true },
  { prop: 'displayName', label: '姓名', width: 120, sortable: true },
  { prop: 'email', label: '邮箱', minWidth: 180, sortable: true },
  { prop: 'phone', label: '手机号', width: 130 },
  { prop: 'enabled', label: '状态', width: 100, align: 'center', slot: 'column-enabled' },
  { prop: 'createdAt', label: '创建时间', width: 160, sortable: true }
]

// 分页配置
const pagination = reactive({
  total: 0,
  page: 1,
  pageSize: 20,
  pageSizes: [10, 20, 50, 100]
})

// 搜索配置
const searchConfig: SearchFieldConfig[] = [
  {
    key: 'keyword',
    label: '关键字',
    type: 'input',
    placeholder: '用户名/姓名/邮箱',
    clearable: true,
    span: 6
  },
  {
    key: 'enabled',
    label: '状态',
    type: 'select',
    placeholder: '请选择状态',
    options: [
      { label: '启用', value: true },
      { label: '禁用', value: false }
    ],
    clearable: true,
    span: 4
  }
]

// 操作列配置
const rowActions: ActionConfig[] = [
  {
    label: '编辑',
    type: 'primary',
    icon: Edit,
    handler: (row) => handleEdit(row)
  },
  {
    label: '重置密码',
    type: 'warning',
    icon: Key,
    handler: (row) => handleResetPassword(row)
  },
  {
    label: (row) => row.enabled ? '禁用' : '启用',
    type: (row) => row.enabled ? 'warning' : 'success',
    icon: (row) => row.enabled ? Lock : Unlock,
    handler: (row) => handleToggleStatus(row)
  },
  {
    label: '删除',
    type: 'danger',
    icon: Delete,
    handler: (row) => handleDelete(row),
    visible: (row) => row.userName !== 'admin'
  }
]

// 批量操作配置
const batchActions: BatchActionConfig[] = [
  {
    key: 'delete',
    label: '批量删除',
    type: 'danger',
    icon: Delete,
    handler: (rows) => handleBatchDelete(rows)
  }
]

// 加载数据
async function loadUsers(params = {}) {
  try {
    loading.value = true
    const result = await getUsers({
      page: pagination.page,
      pageSize: pagination.pageSize,
      ...params
    })
    users.value = result.items
    pagination.total = result.total
  } catch (error) {
    ElMessage.error('加载数据失败')
  } finally {
    loading.value = false
  }
}

// 事件处理
function handleSearch(params: Record<string, any>) {
  pagination.page = 1
  loadUsers(params)
}

function handlePageChange(page: number) {
  pagination.page = page
  loadUsers()
}

function handleSizeChange(size: number) {
  pagination.pageSize = size
  pagination.page = 1
  loadUsers()
}

function handleSort({ prop, order }: { prop?: string; order?: string }) {
  console.log('排序:', prop, order)
  loadUsers()
}

function handleSelectionChange(selection: any[]) {
  console.log('选中:', selection)
}

function handleAdd() {
  console.log('新增用户')
}

function handleEdit(row: any) {
  console.log('编辑用户:', row)
}

function handleResetPassword(row: any) {
  console.log('重置密码:', row)
}

async function handleToggleStatus(row: any) {
  try {
    // 调用 API...
    ElMessage.success('操作成功')
    loadUsers()
  } catch (error) {
    ElMessage.error('操作失败')
  }
}

async function handleDelete(row: any) {
  try {
    await ElMessageBox.confirm(`确定要删除用户 "${row.displayName}" 吗？`, '确认删除', {
      type: 'warning'
    })
    await deleteUser(row.id)
    ElMessage.success('删除成功')
    loadUsers()
  } catch {}
}

async function handleBatchDelete(rows: any[]) {
  try {
    await ElMessageBox.confirm(`确定要删除选中的 ${rows.length} 个用户吗？`, '确认批量删除', {
      type: 'warning'
    })
    await batchDeleteUsers(rows.map(r => r.id))
    ElMessage.success('删除成功')
    loadUsers()
  } catch {}
}

function handleAction(actionLabel: string, row: any) {
  console.log('操作:', actionLabel, row)
}

function handleBatchAction(action: string, selection: any[]) {
  console.log('批量操作:', action, selection)
}

// 初始化
onMounted(() => {
  loadUsers()
})
</script>

<style scoped>
.page {
  padding: 24px;
  background: var(--el-bg-color-page);
  min-height: 100vh;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 24px;
  padding: 20px 24px;
  background: linear-gradient(135deg, #ffffff 0%, #f9fafb 100%);
  border-radius: 12px;
  border: 1px solid #e5e7eb;
}

.page-title h2 {
  font-size: 26px;
  font-weight: 700;
  margin: 0 0 8px 0;
}

.page-title p {
  font-size: 14px;
  color: #6b7280;
  margin: 0;
}

.page-actions {
  display: flex;
  gap: 12px;
}
</style>
```

---

## 📖 API 文档

### Props

| 参数 | 说明 | 类型 | 默认值 |
|------|------|------|--------|
| data | 表格数据 | `any[]` | `[]` |
| loading | 加载状态 | `boolean` | `false` |
| columns | 列配置 | `ColumnConfig[]` | `[]` |
| pagination | 分页配置 | `PaginationConfig` | - |
| searchConfig | 搜索配置 | `SearchFieldConfig[]` | - |
| actions | 操作列配置 | `ActionConfig[]` | - |
| batchActions | 批量操作配置 | `BatchActionConfig[]` | - |
| showSelection | 显示多选列 | `boolean` | `true` |
| showIndex | 显示序号列 | `boolean` | `false` |
| showColumnSettings | 显示列设置 | `boolean` | `true` |
| showExport | 显示导出按钮 | `boolean` | `true` |
| cacheKey | 缓存键名 | `string` | - |
| rowKey | 行数据的 Key | `string` | `'id'` |

### Events

| 事件名 | 说明 | 回调参数 |
|--------|------|----------|
| search | 搜索 | `(params: Record<string, any>) => void` |
| page-change | 页码改变 | `(page: number) => void` |
| size-change | 每页条数改变 | `(size: number) => void` |
| sort-change | 排序改变 | `({ prop, order }) => void` |
| selection-change | 选择改变 | `(selection: any[]) => void` |
| row-click | 行点击 | `(row: any) => void` |
| action-click | 操作点击 | `(label: string, row: any) => void` |
| batch-action | 批量操作 | `(action: string, selection: any[]) => void` |

### Slots

| 插槽名 | 说明 | 参数 |
|--------|------|------|
| column-{prop} | 自定义列内容 | `{ row, column, $index }` |
| actions | 自定义操作列 | `{ row, $index }` |
| empty | 自定义空状态 | - |
| search-extra | 搜索栏额外内容 | - |
| toolbar-left | 工具栏左侧 | - |
| toolbar-right | 工具栏右侧 | - |

---

## 💡 常见问题

### 1. 如何隐藏某个功能？

```vue
<DataTable
  :show-selection="false"
  :show-index="false"
  :show-column-settings="false"
  :show-export="false"
/>
```

### 2. 如何自定义操作列宽度？

操作列宽度会根据按钮数量自动计算（每个按钮 80px + 80px 基础宽度）。如需自定义，可以使用插槽：

```vue
<DataTable>
  <template #actions="{ row }">
    <div style="width: 200px">
      <!-- 自定义操作按钮 -->
    </div>
  </template>
</DataTable>
```

### 3. 如何实现树形表格？

目前不支持树形表格，但可以使用 `el-table` 的 `row-key` 和 `tree-props` 属性（未来版本计划支持）。

### 4. 如何导出全部数据？

目前只支持导出当前页数据。如需导出全部数据，建议：
1. 调用后端接口导出
2. 或先加载全部数据再导出

### 5. 如何自定义样式？

使用 `:deep()` 选择器覆盖组件样式：

```vue
<style scoped>
:deep(.dt-table) {
  font-size: 16px;
}

:deep(.el-button) {
  border-radius: 4px;
}
</style>
```

---

## 🎨 样式定制

### 主题色定制

DataTable 使用 CSS 变量，可以全局定制：

```css
:root {
  --el-color-primary: #3b82f6;
  --el-color-success: #10b981;
  --el-color-warning: #f59e0b;
  --el-color-danger: #ef4444;
}
```

### 暗黑模式

在根元素添加 `admin-dark` 类即可自动切换暗黑模式：

```vue
<div class="admin-dark">
  <DataTable />
</div>
```

---

## 📚 更多资源

- [完整文档](./README.md)
- [更新日志](./CHANGELOG.md)
- [类型定义](./types.ts)
- [在线示例](https://example.com)

---

**祝您使用愉快！** 🎉

