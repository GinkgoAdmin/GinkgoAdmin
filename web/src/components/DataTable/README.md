# DataTable 组件优化说明

## 🎨 设计升级

### 1. 整体风格
- ✅ 现代化卡片式布局，带圆角和阴影效果
- ✅ 渐变背景和精美的视觉层次
- ✅ 流畅的动画过渡效果
- ✅ 完整的暗黑模式支持

### 2. 搜索区域 (SearchBar)
**视觉优化：**
- 可折叠的搜索面板，带展开/收起动画
- 渐变色头部背景
- Bootstrap Icons 图标装饰
- 响应式栅格布局（el-row/el-col）
- 输入框 hover 和 focus 态的微动效果

**交互优化：**
- 点击头部可展开/收起搜索区域
- 搜索和重置按钮带悬停动画
- 表单项支持 span 属性自定义宽度
- 支持回车键快速搜索

### 3. 工具栏 (Toolbar)
**视觉优化：**
- 渐变色背景分隔区域
- 批量操作区域带蓝色渐变背景和徽章显示
- 圆形工具按钮（刷新、导出、列设置）
- 按钮 hover 时上浮效果和阴影

**交互优化：**
- 批量操作按钮带滑入动画
- 工具按钮带 tooltip 提示
- 激活状态的视觉反馈

### 4. 表格区域 (Table)
**视觉优化：**
- 斑马纹表格提升可读性
- 表头固定，加粗字体，底部双线分隔
- 行 hover 时上浮效果和浅色背景
- 精美的空状态 SVG 图标
- 排序图标高亮显示

**交互优化：**
- 行点击反馈
- 平滑的排序动画
- 固定选择列和序号列

### 5. 操作列 (TableActions)
**视觉优化：**
- 彩色文字按钮（主色、成功、警告、危险）
- 按钮带 tooltip 提示
- hover 时背景色高亮和上浮动画

**交互优化：**
- 支持动态显示/隐藏（visible）
- 支持动态禁用（disabled）
- 支持动态 label 和 icon（函数返回）
- 阻止行点击事件冒泡

### 6. 列设置 (ColumnSettings)
**视觉优化：**
- 圆形设置按钮，激活时高亮
- 弹出面板带标题和分隔线
- 滚动列表，最大高度 400px
- 列表项 hover 时背景高亮
- 全选、重置、应用按钮

**交互优化：**
- 实时保存列配置
- 一键全选/重置
- 支持滚动浏览大量列

### 7. 分页 (Pagination)
**视觉优化：**
- 圆角按钮和页码
- 激活页码带渐变背景和阴影
- 按钮 hover 时上浮效果
- 分隔线分隔表格和分页

**交互优化：**
- 背景色分页
- 支持页码跳转
- 每页条数选择

## 🎯 设计特色

### 色彩系统
- **主色调**: 蓝色系 (#3b82f6 ~ #2563eb)
- **成功色**: 绿色系 (#10b981)
- **警告色**: 橙色系 (#f59e0b)
- **危险色**: 红色系 (#ef4444)
- **中性色**: 灰色系 (#1f2937 ~ #f9fafb)

### 动画效果
- **淡入动画**: 页面加载时 (fadeIn)
- **滑入动画**: 批量操作区域 (slideIn)
- **展开动画**: 搜索面板 (expand)
- **上浮动画**: 按钮和表格行 hover (translateY)

### 圆角规范
- **卡片**: 12px
- **按钮**: 6-8px
- **输入框**: 8px

### 阴影规范
- **静态**: 0 4px 12px rgba(0, 0, 0, 0.08)
- **Hover**: 0 4px 12px rgba(0, 0, 0, 0.12)
- **激活**: 0 2px 8px rgba(59, 130, 246, 0.3)

## 📱 响应式设计

### 断点
- **移动端**: ≤768px
  - 搜索栏垂直布局
  - 工具栏垂直布局
  - 分页居中显示
  - 按钮全宽

### 自适应栅格
- SearchBar 使用 el-row/el-col
- 默认 span=6 (一行 4 列)
- xs=24 (手机单列)
- sm=12 (平板两列)
- md=8 (小屏三列)
- lg=自定义

## 🌙 暗黑模式

完整支持暗黑模式，所有组件都包含 `.admin-dark` 样式：
- 背景色调整为深灰色系
- 文字颜色调整为浅色
- 边框颜色调整为中灰色
- 主题色适配（蓝色更亮）
- 阴影加深

## 🎁 新增特性

1. **简单/高级搜索分离**: 常用搜索字段默认显示，高级搜索按需展开，节省空间
2. **紧凑布局模式**: `compactMode` 优化间距，提升数据列表显示区域
3. **刷新按钮**: 快速重新加载数据
4. **批量操作徽章**: 显示已选数量
5. **列设置全选**: 一键显示/隐藏所有列
6. **工具提示**: 所有图标按钮带 tooltip
7. **空状态图标**: 精美的 SVG 空数据图标
8. **动态操作列**: 支持动态 label/icon/type
9. **Excel 导入**: 支持 xlsx、xls、csv 文件导入，带预览和字段映射
10. **数据打印**: 支持打印当前页或全部数据，带自定义样式

## 🔧 使用示例

### 基础表格
```vue
<DataTable
  :data="tableData"
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
  @search="onSearch"
  @page-change="handlePageChange"
  @size-change="handleSizeChange"
  @sort-change="onSortChange"
  @selection-change="handleSelectionChange"
  @action-click="onRowAction"
  @batch-action="onBatchAction"
>
  <!-- 自定义列插槽 -->
  <template #column-status="{ row }">
    <el-tag :type="row.enabled ? 'success' : 'danger'">
      {{ row.enabled ? '启用' : '禁用' }}
    </el-tag>
  </template>
</DataTable>
```

### 树形表格
```vue
<DataTable
  :data="treeData"
  :columns="columns"
  :tree-config="true"
  row-key="id"
>
  <!-- 树形数据会自动显示展开/折叠图标 -->
</DataTable>
```

查看完整树形表格文档：[TREE_TABLE.md](./TREE_TABLE.md)

### Excel 导入
```vue
<DataTable
  :data="tableData"
  :columns="columns"
  :import-config="{
    enabled: true,
    templateUrl: '/templates/user-import.xlsx',
    handler: handleImport,
    fieldMapping: {
      '用户名': 'username',
      '邮箱': 'email',
      '部门': 'departmentName'
    },
    maxRows: 5000,
    showPreview: true
  }"
/>
```

### 数据打印
```vue
<DataTable
  :data="tableData"
  :columns="columns"
  :print-config="{
    enabled: true,
    title: '用户列表',
    showPreview: true,
    printAll: false,
    beforePrint: (data) => {
      // 可在打印前处理数据
      return data
    },
    customStyles: `
      .print-table th {
        background-color: #3b82f6 !important;
        color: white !important;
      }
    `
  }"
/>
```

## 📝 配置说明

### SearchFieldConfig 新增属性
```typescript
interface SearchFieldConfig {
  span?: number // 栅格宽度，默认 6（简单搜索）或 8（高级搜索）
  simple?: boolean // 是否显示在简单搜索区（默认 false）
}
```

### ActionConfig 增强
```typescript
interface ActionConfig {
  label: string | ((row: any) => string) // 支持动态标签
  type?: string | ((row: any) => string) // 支持动态类型
  icon?: any // 支持 Element Plus 图标或 Bootstrap Icons
  disabled?: (row: any) => boolean // 支持动态禁用
}
```

### ImportConfig 配置
```typescript
interface ImportConfig {
  enabled?: boolean // 是否启用导入功能
  templateUrl?: string // 导入模板下载URL
  handler?: (data: any[]) => Promise<void> | void // 导入处理函数
  fieldMapping?: Record<string, string> // 字段映射（Excel列名 -> 数据字段名）
  maxRows?: number // 最大导入行数
  showPreview?: boolean // 是否显示导入预览
}
```

### PrintConfig 配置
```typescript
interface PrintConfig {
  enabled?: boolean // 是否启用打印功能
  title?: string // 打印标题
  showPreview?: boolean // 是否显示打印预览
  customStyles?: string // 自定义打印样式
  printAll?: boolean // 是否打印所有数据（包括分页）
  beforePrint?: (data: any[]) => any[] // 打印前的数据处理函数
}
```

## 🎨 技术亮点

1. **CSS 变量**: 未来可扩展为主题系统
2. **深度选择器**: 精确控制 Element Plus 组件样式
3. **Flex 布局**: 灵活的响应式布局
4. **CSS 动画**: 流畅的过渡效果
5. **TypeScript**: 完整的类型支持
6. **插槽系统**: 高度可定制

---

**最后更新**: 2025-10-01  
**版本**: v2.3  
**作者**: AI Assistant

## ⚠️ 依赖说明

DataTable 组件的 Excel 导入功能依赖 `xlsx` 库，请确保已安装：

```bash
npm install xlsx --save
```

