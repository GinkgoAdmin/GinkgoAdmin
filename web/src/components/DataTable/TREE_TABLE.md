# DataTable 树形表格使用指南

## 📖 功能介绍

DataTable 组件现在支持树形表格功能，可以展示具有层级关系的数据结构，支持展开/折叠子节点、懒加载、自定义缩进等特性。

---

## 🚀 快速开始

### 1. 基础树形表格

```vue
<template>
  <DataTable
    :data="departmentTree"
    :columns="columns"
    :tree-config="true"
    row-key="id"
  />
</template>

<script setup lang="ts">
import { ref } from 'vue'
import DataTable from '@/components/DataTable/index.vue'
import type { ColumnConfig } from '@/components/DataTable/types'

// 树形数据
const departmentTree = ref([
  {
    id: '1',
    name: '总公司',
    manager: '张三',
    employeeCount: 100,
    children: [
      {
        id: '1-1',
        name: '技术部',
        manager: '李四',
        employeeCount: 30,
        children: [
          {
            id: '1-1-1',
            name: '前端组',
            manager: '王五',
            employeeCount: 10
          },
          {
            id: '1-1-2',
            name: '后端组',
            manager: '赵六',
            employeeCount: 20
          }
        ]
      },
      {
        id: '1-2',
        name: '市场部',
        manager: '孙七',
        employeeCount: 25
      }
    ]
  },
  {
    id: '2',
    name: '分公司',
    manager: '周八',
    employeeCount: 50,
    children: [
      {
        id: '2-1',
        name: '销售部',
        manager: '吴九',
        employeeCount: 30
      }
    ]
  }
])

// 列配置
const columns: ColumnConfig[] = [
  { prop: 'name', label: '部门名称', minWidth: 200 },
  { prop: 'manager', label: '负责人', width: 120 },
  { prop: 'employeeCount', label: '员工数', width: 100, align: 'center' }
]
</script>
```

**效果：**
```
▼ 总公司        张三    100
  ▼ 技术部      李四     30
    ▶ 前端组    王五     10
    ▶ 后端组    赵六     20
  ▶ 市场部      孙七     25
▼ 分公司        周八     50
  ▶ 销售部      吴九     30
```

---

### 2. 自定义树形配置

```vue
<template>
  <DataTable
    :data="menuTree"
    :columns="columns"
    :tree-config="treeConfig"
    row-key="id"
  />
</template>

<script setup lang="ts">
import { ref } from 'vue'
import DataTable from '@/components/DataTable/index.vue'
import type { ColumnConfig, TreeConfig } from '@/components/DataTable/types'

// 树形数据（使用自定义字段名）
const menuTree = ref([
  {
    id: '1',
    title: '系统管理',
    icon: 'setting',
    sort: 1,
    subMenus: [ // 自定义子节点字段名
      {
        id: '1-1',
        title: '用户管理',
        icon: 'user',
        sort: 1
      },
      {
        id: '1-2',
        title: '角色管理',
        icon: 'role',
        sort: 2
      }
    ]
  }
])

// 树形配置
const treeConfig: TreeConfig = {
  children: 'subMenus', // 自定义子节点字段名
  hasChildren: 'hasSubMenus', // 自定义是否有子节点的字段名
  indent: 24, // 自定义缩进像素（默认 16）
  expandAll: true // 默认展开所有节点
}

// 列配置
const columns: ColumnConfig[] = [
  { prop: 'title', label: '菜单名称', minWidth: 200 },
  { prop: 'icon', label: '图标', width: 100 },
  { prop: 'sort', label: '排序', width: 80, align: 'center' }
]
</script>
```

---

### 3. 懒加载树形表格

```vue
<template>
  <DataTable
    :data="lazyTree"
    :columns="columns"
    :tree-config="lazyTreeConfig"
    row-key="id"
  />
</template>

<script setup lang="ts">
import { ref } from 'vue'
import DataTable from '@/components/DataTable/index.vue'
import type { ColumnConfig, TreeConfig } from '@/components/DataTable/types'

// 懒加载数据（只有第一层）
const lazyTree = ref([
  {
    id: '1',
    name: '总公司',
    manager: '张三',
    hasChildren: true // 标记有子节点
  },
  {
    id: '2',
    name: '分公司',
    manager: '周八',
    hasChildren: true
  }
])

// 懒加载配置
const lazyTreeConfig: TreeConfig = {
  lazy: true, // 开启懒加载
  hasChildren: 'hasChildren', // 指定是否有子节点的字段
  load: async (row, treeNode, resolve) => {
    // 模拟异步加载子节点
    setTimeout(() => {
      if (row.id === '1') {
        resolve([
          {
            id: '1-1',
            name: '技术部',
            manager: '李四',
            hasChildren: true
          },
          {
            id: '1-2',
            name: '市场部',
            manager: '孙七',
            hasChildren: false
          }
        ])
      } else if (row.id === '1-1') {
        resolve([
          {
            id: '1-1-1',
            name: '前端组',
            manager: '王五',
            hasChildren: false
          },
          {
            id: '1-1-2',
            name: '后端组',
            manager: '赵六',
            hasChildren: false
          }
        ])
      } else {
        resolve([])
      }
    }, 1000) // 模拟网络延迟
  }
}

// 列配置
const columns: ColumnConfig[] = [
  { prop: 'name', label: '部门名称', minWidth: 200 },
  { prop: 'manager', label: '负责人', width: 120 }
]
</script>
```

**懒加载流程：**
1. 初始数据只包含第一层节点
2. 点击展开图标时触发 `load` 函数
3. `load` 函数异步加载子节点数据
4. 调用 `resolve` 返回子节点数据
5. 表格自动渲染子节点

---

### 4. 默认展开指定节点

```vue
<template>
  <DataTable
    :data="departmentTree"
    :columns="columns"
    :tree-config="treeConfig"
    row-key="id"
  />
</template>

<script setup lang="ts">
import { ref } from 'vue'
import DataTable from '@/components/DataTable/index.vue'
import type { ColumnConfig, TreeConfig } from '@/components/DataTable/types'

// 树形数据
const departmentTree = ref([
  {
    id: '1',
    name: '总公司',
    children: [
      { id: '1-1', name: '技术部' },
      { id: '1-2', name: '市场部' }
    ]
  },
  {
    id: '2',
    name: '分公司',
    children: [
      { id: '2-1', name: '销售部' }
    ]
  }
])

// 树形配置
const treeConfig: TreeConfig = {
  defaultExpandedKeys: ['1', '1-1'] // 默认展开的节点 key 数组
}

// 列配置
const columns: ColumnConfig[] = [
  { prop: 'name', label: '部门名称', minWidth: 200 }
]
</script>
```

**注意：** `defaultExpandedKeys` 需要配合 Element Plus 的 `default-expand-all` 属性使用，目前已在组件内部实现。

---

### 5. 带操作列的树形表格

```vue
<template>
  <DataTable
    :data="departmentTree"
    :columns="columns"
    :tree-config="true"
    :actions="rowActions"
    row-key="id"
    @action-click="handleAction"
  />
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Edit, Delete } from '@element-plus/icons-vue'
import DataTable from '@/components/DataTable/index.vue'
import type { ColumnConfig, ActionConfig } from '@/components/DataTable/types'

// 树形数据
const departmentTree = ref([
  {
    id: '1',
    name: '总公司',
    level: 0,
    children: [
      { id: '1-1', name: '技术部', level: 1 },
      { id: '1-2', name: '市场部', level: 1 }
    ]
  }
])

// 列配置
const columns: ColumnConfig[] = [
  { prop: 'name', label: '部门名称', minWidth: 200 },
  { prop: 'level', label: '层级', width: 80, align: 'center' }
]

// 操作列配置
const rowActions: ActionConfig[] = [
  {
    label: '新增子部门',
    type: 'primary',
    icon: Plus,
    handler: (row) => {
      console.log('新增子部门:', row)
      ElMessage.success('新增子部门功能')
    }
  },
  {
    label: '编辑',
    type: 'primary',
    icon: Edit,
    handler: (row) => {
      console.log('编辑部门:', row)
    }
  },
  {
    label: '删除',
    type: 'danger',
    icon: Delete,
    handler: async (row) => {
      try {
        await ElMessageBox.confirm(`确定要删除部门 "${row.name}" 吗？`, '确认删除', {
          type: 'warning'
        })
        console.log('删除部门:', row)
        ElMessage.success('删除成功')
      } catch {}
    },
    disabled: (row) => row.level === 0 // 禁止删除顶级节点
  }
]

function handleAction(actionLabel: string, row: any) {
  console.log('操作:', actionLabel, row)
}
</script>
```

---

## 📊 API 文档

### TreeConfig 类型定义

```typescript
export interface TreeConfig {
  // 子节点字段名，默认 'children'
  children?: string
  
  // 是否有子节点的字段名，默认 'hasChildren'
  hasChildren?: string
  
  // 是否懒加载，默认 false
  lazy?: boolean
  
  // 懒加载函数
  load?: (row: any, treeNode: any, resolve: (data: any[]) => void) => void
  
  // 缩进像素，默认 16
  indent?: number
  
  // 是否默认展开所有节点，默认 false
  expandAll?: boolean
  
  // 默认展开的节点 key 数组
  defaultExpandedKeys?: string[]
}
```

### DataTableProps 新增属性

```typescript
export interface DataTableProps {
  // ...其他属性
  
  // 树形表格配置
  // true 表示使用默认配置（children 和 hasChildren）
  // TreeConfig 对象表示自定义配置
  treeConfig?: TreeConfig | boolean
  
  // 行数据的 Key，树形表格必须指定
  rowKey?: string
}
```

---

## 🎨 样式特性

### 1. 展开图标样式

- ✅ 圆角背景（4px）
- ✅ Hover 时背景高亮
- ✅ 展开时旋转 90°
- ✅ 平滑过渡动画（0.2s）

### 2. 层级背景色

- ✅ 第 1 层：浅蓝色（rgba(59,130,246,0.02)）
- ✅ 第 2 层：中蓝色（rgba(59,130,246,0.04)）
- ✅ 第 3 层：深蓝色（rgba(59,130,246,0.06)）
- ✅ 暗黑模式自动适配

### 3. 加载图标

- ✅ 旋转动画（2s 循环）
- ✅ 蓝色主题色
- ✅ 暗黑模式更亮

---

## 💡 使用技巧

### 1. 数据结构要求

树形数据必须包含以下结构：

```typescript
interface TreeNode {
  id: string | number  // 唯一标识
  [key: string]: any   // 其他数据字段
  children?: TreeNode[] // 子节点数组
  hasChildren?: boolean // 是否有子节点（懒加载时使用）
}
```

### 2. rowKey 必须指定

树形表格必须指定 `row-key` 属性，用于唯一标识每一行数据：

```vue
<DataTable
  :data="treeData"
  row-key="id"  <!-- 必须指定 -->
  :tree-config="true"
/>
```

### 3. 懒加载最佳实践

```typescript
const lazyTreeConfig: TreeConfig = {
  lazy: true,
  hasChildren: 'hasChildren',
  load: async (row, treeNode, resolve) => {
    try {
      // 调用后端 API 加载子节点
      const children = await loadChildrenApi(row.id)
      resolve(children)
    } catch (error) {
      console.error('加载子节点失败:', error)
      resolve([]) // 失败时返回空数组
    }
  }
}
```

### 4. 自定义缩进

不同层级可以使用不同的缩进：

```typescript
const treeConfig: TreeConfig = {
  indent: 24 // 每层缩进 24px（默认 16px）
}
```

### 5. 默认展开所有节点

```typescript
const treeConfig: TreeConfig = {
  expandAll: true // 初始化时展开所有节点
}
```

---

## 🚨 注意事项

### 1. 分页与树形表格

⚠️ **树形表格不建议使用分页**，因为：
- 父节点和子节点可能分散在不同页
- 分页会破坏树形结构的完整性

如果数据量大，建议：
- 使用懒加载（lazy load）
- 后端接口按需返回子节点
- 前端只展示部分节点

### 2. 排序与树形表格

⚠️ **树形表格的排序需要特殊处理**：
- 排序应该在同一层级内进行
- 不要打乱父子关系
- 建议后端排序好后返回

### 3. 搜索与树形表格

⚠️ **树形表格搜索需要特殊逻辑**：
- 搜索结果应该包含父节点路径
- 展开到匹配的节点
- 建议后端处理搜索逻辑

### 4. 性能优化

对于大量数据的树形表格：
- ✅ 使用懒加载
- ✅ 限制展开层级
- ✅ 虚拟滚动（未来支持）

---

## 📝 完整示例

### 部门管理（树形 + 操作）

```vue
<template>
  <div class="department-page">
    <div class="page-header">
      <div class="page-title">
        <h2>部门管理</h2>
        <p>管理公司部门组织架构</p>
      </div>
      <div class="page-actions">
        <el-button type="primary" :icon="Plus" @click="handleAdd">
          新增部门
        </el-button>
      </div>
    </div>

    <DataTable
      :data="departmentTree"
      :loading="loading"
      :columns="columns"
      :tree-config="treeConfig"
      :actions="rowActions"
      row-key="id"
      :show-index="true"
      :show-column-settings="true"
      @action-click="handleAction"
    >
      <!-- 自定义状态列 -->
      <template #column-enabled="{ row }">
        <el-switch
          v-model="row.enabled"
          @change="handleToggleStatus(row)"
        />
      </template>

      <!-- 自定义层级列 -->
      <template #column-level="{ row }">
        <el-tag :type="getLevelTagType(row.level)" size="small">
          {{ getLevelLabel(row.level) }}
        </el-tag>
      </template>
    </DataTable>

    <!-- 新增/编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="dialogTitle"
      width="600px"
    >
      <el-form
        ref="formRef"
        :model="formData"
        :rules="formRules"
        label-width="100px"
      >
        <el-form-item label="上级部门" prop="parentId">
          <el-tree-select
            v-model="formData.parentId"
            :data="departmentTree"
            node-key="id"
            :props="{ label: 'name', value: 'id' }"
            placeholder="选择上级部门（留空为顶级部门）"
            clearable
            check-strictly
          />
        </el-form-item>
        <el-form-item label="部门名称" prop="name">
          <el-input v-model="formData.name" placeholder="请输入部门名称" />
        </el-form-item>
        <el-form-item label="负责人" prop="manager">
          <el-input v-model="formData.manager" placeholder="请输入负责人" />
        </el-form-item>
        <el-form-item label="排序" prop="sort">
          <el-input-number v-model="formData.sort" :min="0" />
        </el-form-item>
        <el-form-item label="状态" prop="enabled">
          <el-switch v-model="formData.enabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="handleSubmit">
          确定
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Edit, Delete, User } from '@element-plus/icons-vue'
import DataTable from '@/components/DataTable/index.vue'
import type { ColumnConfig, TreeConfig, ActionConfig } from '@/components/DataTable/types'
import { getDepartmentTree, createDepartment, updateDepartment, deleteDepartment } from '@/api/departments'

// 部门树数据
const departmentTree = ref([])
const loading = ref(false)

// 树形配置
const treeConfig: TreeConfig = {
  children: 'children',
  hasChildren: 'hasChildren',
  indent: 20,
  expandAll: false
}

// 列配置
const columns: ColumnConfig[] = [
  { prop: 'name', label: '部门名称', minWidth: 200 },
  { prop: 'manager', label: '负责人', width: 120 },
  { prop: 'employeeCount', label: '员工数', width: 100, align: 'center' },
  { prop: 'level', label: '层级', width: 100, align: 'center', slot: 'column-level' },
  { prop: 'enabled', label: '状态', width: 100, align: 'center', slot: 'column-enabled' },
  { prop: 'sort', label: '排序', width: 80, align: 'center' }
]

// 操作列
const rowActions: ActionConfig[] = [
  {
    label: '新增子部门',
    type: 'primary',
    icon: Plus,
    handler: (row) => handleAddChild(row)
  },
  {
    label: '编辑',
    type: 'primary',
    icon: Edit,
    handler: (row) => handleEdit(row)
  },
  {
    label: '删除',
    type: 'danger',
    icon: Delete,
    handler: (row) => handleDelete(row),
    disabled: (row) => row.level === 0 || (row.children && row.children.length > 0)
  }
]

// 对话框
const dialogVisible = ref(false)
const dialogTitle = computed(() => formData.id ? '编辑部门' : '新增部门')
const submitting = ref(false)
const formRef = ref()
const formData = reactive({
  id: '',
  parentId: '',
  name: '',
  manager: '',
  sort: 0,
  enabled: true
})

const formRules = {
  name: [
    { required: true, message: '请输入部门名称', trigger: 'blur' }
  ]
}

// 加载数据
async function loadData() {
  try {
    loading.value = true
    departmentTree.value = await getDepartmentTree()
  } catch (error) {
    ElMessage.error('加载部门数据失败')
  } finally {
    loading.value = false
  }
}

// 新增部门
function handleAdd() {
  Object.assign(formData, {
    id: '',
    parentId: '',
    name: '',
    manager: '',
    sort: 0,
    enabled: true
  })
  dialogVisible.value = true
}

// 新增子部门
function handleAddChild(row: any) {
  Object.assign(formData, {
    id: '',
    parentId: row.id,
    name: '',
    manager: '',
    sort: 0,
    enabled: true
  })
  dialogVisible.value = true
}

// 编辑
function handleEdit(row: any) {
  Object.assign(formData, {
    id: row.id,
    parentId: row.parentId || '',
    name: row.name,
    manager: row.manager,
    sort: row.sort,
    enabled: row.enabled
  })
  dialogVisible.value = true
}

// 删除
async function handleDelete(row: any) {
  try {
    await ElMessageBox.confirm(`确定要删除部门 "${row.name}" 吗？`, '确认删除', {
      type: 'warning'
    })
    await deleteDepartment(row.id)
    ElMessage.success('删除成功')
    loadData()
  } catch {}
}

// 提交
async function handleSubmit() {
  try {
    await formRef.value.validate()
    submitting.value = true
    if (formData.id) {
      await updateDepartment(formData.id, formData)
    } else {
      await createDepartment(formData)
    }
    ElMessage.success('保存成功')
    dialogVisible.value = false
    loadData()
  } catch (error) {
    ElMessage.error('保存失败')
  } finally {
    submitting.value = false
  }
}

// 切换状态
async function handleToggleStatus(row: any) {
  try {
    await updateDepartment(row.id, { enabled: row.enabled })
    ElMessage.success('状态更新成功')
  } catch (error) {
    row.enabled = !row.enabled // 回滚
    ElMessage.error('状态更新失败')
  }
}

// 获取层级标签类型
function getLevelTagType(level: number) {
  const types = ['', 'success', 'warning', 'info', 'danger']
  return types[level] || 'info'
}

// 获取层级标签文本
function getLevelLabel(level: number) {
  const labels = ['顶级', '一级', '二级', '三级', '四级']
  return labels[level] || `${level}级`
}

function handleAction(actionLabel: string, row: any) {
  console.log('操作:', actionLabel, row)
}

onMounted(() => {
  loadData()
})
</script>

<style scoped>
.department-page {
  padding: 24px;
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
</style>
```

---

## 🎉 总结

DataTable 的树形表格功能特性：

✅ **基础功能**
- 展开/折叠子节点
- 多层级显示
- 自定义字段名
- 自定义缩进

✅ **高级功能**
- 懒加载
- 默认展开
- 层级背景色
- 精美展开图标

✅ **完美集成**
- 支持操作列
- 支持自定义列
- 支持搜索（需配合后端）
- 暗黑模式支持

**立即体验树形表格功能吧！** 🎊

---

**最后更新**: 2025-10-01  
**版本**: v2.1.0  
**作者**: AI Assistant

