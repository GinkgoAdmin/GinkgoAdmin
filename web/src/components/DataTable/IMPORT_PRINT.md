# DataTable Excel 导入和打印功能

## 📋 目录

- [功能概览](#功能概览)
- [Excel 导入](#excel-导入)
- [数据打印](#数据打印)
- [API 文档](#api-文档)
- [使用示例](#使用示例)

---

## 功能概览

DataTable 组件现已支持 **Excel 导入** 和 **数据打印** 功能，为数据管理提供完整的导入导出解决方案。

### 核心特性

**Excel 导入：**
- ✅ 支持多种格式（.xlsx、.xls、.csv）
- ✅ 文件大小限制（10MB）
- ✅ 行数限制（可配置）
- ✅ 导入预览（最多显示 100 条）
- ✅ 字段映射（Excel 列名 → 数据字段名）
- ✅ 模板下载功能
- ✅ 自定义导入处理函数

**数据打印：**
- ✅ 打印当前页或全部数据
- ✅ 打印预览对话框
- ✅ 自定义打印标题
- ✅ 自定义打印样式（CSS）
- ✅ 打印前数据处理
- ✅ 精美的打印布局

---

## Excel 导入

### 基础用法

```vue
<template>
  <DataTable
    :data="users"
    :columns="columns"
    :import-config="true"
  />
</template>
```

### 完整配置

```vue
<template>
  <DataTable
    :data="users"
    :columns="columns"
    :import-config="{
      enabled: true,
      templateUrl: '/templates/user-import.xlsx',
      handler: handleImport,
      fieldMapping: {
        '用户名': 'username',
        '邮箱': 'email',
        '手机号': 'mobile',
        '部门': 'departmentName'
      },
      maxRows: 5000,
      showPreview: true
    }"
  />
</template>

<script setup lang="ts">
import { ElMessage } from 'element-plus'

// 导入处理函数
async function handleImport(data: any[]) {
  try {
    // 调用后端 API 批量创建用户
    const res = await api.post('/api/v1/users/import', { users: data })
    
    if (res.data.code === 200) {
      ElMessage.success(`成功导入 ${data.length} 条数据`)
      // 刷新列表
      loadUsers()
    } else {
      ElMessage.error(res.data.message)
    }
  } catch (error: any) {
    ElMessage.error(`导入失败: ${error.message}`)
    throw error
  }
}
</script>
```

### 导入流程

1. **点击导入按钮** → 弹出文件选择对话框
2. **选择文件** → 自动解析 Excel 文件
3. **字段映射** → 根据 `fieldMapping` 转换字段名
4. **显示预览** → 在对话框中预览前 100 条数据
5. **确认导入** → 调用 `handler` 函数处理数据
6. **完成导入** → 显示成功消息

### 字段映射说明

字段映射用于将 Excel 列名转换为数据字段名：

```typescript
fieldMapping: {
  'Excel列名': '数据字段名'
}
```

**示例：**

| Excel 列名 | 数据字段名 | 说明 |
|-----------|----------|------|
| 用户名 | username | 用户登录名 |
| 邮箱 | email | 用户邮箱 |
| 手机号 | mobile | 用户手机号 |
| 部门 | departmentName | 部门名称 |

### 导入模板

如果提供了 `templateUrl`，用户可以在导入预览对话框中点击"下载模板"按钮获取标准模板。

**模板示例（user-import.xlsx）：**

| 用户名 | 邮箱 | 手机号 | 部门 |
|-------|------|-------|------|
| zhangsan | zhangsan@example.com | 13800138000 | 技术部 |
| lisi | lisi@example.com | 13900139000 | 市场部 |

---

## 数据打印

### 基础用法

```vue
<template>
  <DataTable
    :data="users"
    :columns="columns"
    :print-config="true"
  />
</template>
```

### 完整配置

```vue
<template>
  <DataTable
    :data="users"
    :columns="columns"
    :print-config="{
      enabled: true,
      title: '用户列表',
      showPreview: true,
      printAll: false,
      beforePrint: formatPrintData,
      customStyles: `
        .print-table th {
          background-color: #3b82f6 !important;
          color: white !important;
          font-weight: bold;
        }
        .print-table td {
          font-size: 12px;
        }
        .print-header {
          border-bottom: 3px solid #3b82f6;
        }
      `
    }"
  />
</template>

<script setup lang="ts">
// 打印前数据格式化
function formatPrintData(data: any[]) {
  return data.map(item => ({
    ...item,
    // 格式化布尔值
    enabled: item.enabled ? '启用' : '禁用',
    // 格式化日期
    createdAt: new Date(item.createdAt).toLocaleDateString('zh-CN'),
    // 数组转字符串
    roleNames: item.roleNames?.join(', ') || '-'
  }))
}
</script>
```

### 打印流程

1. **点击打印按钮** → 准备打印数据
2. **显示预览** → 在对话框中预览数据
3. **确认打印** → 打开浏览器打印窗口
4. **执行打印** → 用户选择打印机并打印

### 打印样式自定义

通过 `customStyles` 可以自定义打印输出的样式：

```css
/* 自定义表头 */
.print-table th {
  background-color: #3b82f6 !important;
  color: white !important;
}

/* 自定义表格行 */
.print-table tr:nth-child(even) {
  background-color: #f9fafb !important;
}

/* 自定义标题 */
.print-title {
  font-size: 28px;
  color: #1f2937;
}

/* 自定义页脚 */
.print-footer {
  border-top: 2px solid #3b82f6;
}
```

### 打印布局说明

默认打印布局包含以下部分：

1. **打印头部（.print-header）**
   - 标题（.print-title）
   - 元信息（.print-meta）：打印时间、数据条数

2. **打印表格（.print-table）**
   - 表头（thead）
   - 表体（tbody）

3. **打印页脚（.print-footer）**
   - 系统生成提示

---

## API 文档

### ImportConfig 接口

```typescript
interface ImportConfig {
  // 是否启用导入功能
  enabled?: boolean
  
  // 导入模板下载 URL 或数据
  templateUrl?: string
  
  // 导入处理函数（必须实现）
  handler?: (data: any[]) => Promise<void> | void
  
  // 字段映射配置（Excel 列名 -> 数据字段名）
  fieldMapping?: Record<string, string>
  
  // 最大导入行数（默认 10000）
  maxRows?: number
  
  // 是否显示导入预览（默认 true）
  showPreview?: boolean
}
```

### PrintConfig 接口

```typescript
interface PrintConfig {
  // 是否启用打印功能
  enabled?: boolean
  
  // 打印标题（默认"数据列表"）
  title?: string
  
  // 是否显示打印预览（默认 true）
  showPreview?: boolean
  
  // 自定义打印样式（CSS 字符串）
  customStyles?: string
  
  // 是否打印所有数据（包括分页，默认 false）
  printAll?: boolean
  
  // 打印前的数据处理函数
  beforePrint?: (data: any[]) => any[]
}
```

### DataTableProps 新增属性

```typescript
interface DataTableProps {
  // ... 其他属性
  
  // Excel 导入配置，true 表示使用默认配置
  importConfig?: ImportConfig | boolean
  
  // 打印配置，true 表示使用默认配置
  printConfig?: PrintConfig | boolean
}
```

---

## 使用示例

### 示例 1：用户批量导入

```vue
<template>
  <DataTable
    :data="users"
    :loading="loading"
    :columns="userColumns"
    :pagination="pagination"
    :import-config="{
      enabled: true,
      templateUrl: '/templates/user-import.xlsx',
      handler: importUsers,
      fieldMapping: {
        '用户名': 'username',
        '姓名': 'realName',
        '邮箱': 'email',
        '手机号': 'mobile',
        '部门': 'departmentName',
        '角色': 'roleName'
      },
      maxRows: 1000,
      showPreview: true
    }"
    @search="loadUsers"
  />
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { userApi } from '@/api/user'

const users = ref([])
const loading = ref(false)

async function importUsers(data: any[]) {
  loading.value = true
  try {
    const res = await userApi.batchCreate(data)
    ElMessage.success(`成功导入 ${res.data.successCount} 条，失败 ${res.data.failCount} 条`)
    await loadUsers()
  } catch (error: any) {
    ElMessage.error(`导入失败: ${error.message}`)
    throw error
  } finally {
    loading.value = false
  }
}
</script>
```

### 示例 2：带格式化的打印

```vue
<template>
  <DataTable
    :data="products"
    :columns="productColumns"
    :print-config="{
      enabled: true,
      title: '产品清单',
      showPreview: true,
      beforePrint: formatProducts,
      customStyles: `
        .print-table th {
          background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
          color: white;
        }
        .print-title {
          color: #667eea;
        }
      `
    }"
  />
</template>

<script setup lang="ts">
function formatProducts(data: any[]) {
  return data.map(item => ({
    ...item,
    price: `¥${item.price.toFixed(2)}`,
    stock: item.stock > 0 ? `${item.stock} 件` : '缺货',
    status: item.status === 1 ? '上架' : '下架'
  }))
}
</script>
```

### 示例 3：全部数据打印

```vue
<template>
  <DataTable
    :data="currentPageData"
    :columns="columns"
    :pagination="pagination"
    :print-config="{
      enabled: true,
      title: '完整数据列表',
      printAll: true,
      showPreview: true
    }"
  />
</template>

<script setup lang="ts">
// 注意：printAll: true 时，需要从后端获取全部数据
// DataTable 会自动处理打印全部数据的逻辑
</script>
```

---

## 依赖说明

DataTable 的 Excel 导入功能依赖 `xlsx` 库进行文件解析：

```bash
npm install xlsx --save
```

该库会在使用时动态导入，不会影响初始加载性能。

---

## 浏览器兼容性

### Excel 导入
- ✅ Chrome 90+
- ✅ Firefox 88+
- ✅ Safari 14+
- ✅ Edge 90+

### 数据打印
- ✅ 所有支持 `window.print()` 的现代浏览器

---

## 常见问题

### Q1: 导入的文件解析失败？

**A:** 请确保：
1. 文件格式正确（.xlsx、.xls、.csv）
2. 文件大小不超过 10MB
3. Excel 列名与 `fieldMapping` 匹配

### Q2: 字段映射不生效？

**A:** 检查 Excel 中的列名是否与 `fieldMapping` 中的 key 完全一致（包括空格、大小写）。

### Q3: 打印样式不生效？

**A:** 确保使用 `!important` 覆盖默认样式，并注意打印 CSS 与屏幕 CSS 的差异。

### Q4: 打印时如何分页？

**A:** 使用 CSS `page-break` 属性：

```css
customStyles: `
  .print-table tbody tr {
    page-break-inside: avoid;
  }
`
```

### Q5: 导入大文件性能问题？

**A:** 建议：
1. 设置合理的 `maxRows` 限制（如 5000）
2. 在 `handler` 中使用分批上传
3. 显示进度条反馈

---

## 更新日志

**v2.2.0 (2025-10-01)**
- ✨ 新增 Excel 导入功能
- ✨ 新增数据打印功能
- 🔧 新增 `useImport` Hook
- 🔧 新增 `usePrint` Hook
- 📝 完善文档和示例

---

**文档版本**: v2.2.0  
**最后更新**: 2025-10-01  
**维护者**: GinkgoAdmin Team

