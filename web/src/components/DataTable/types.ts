export type SortOrder = 'ascending' | 'descending' | ''

export interface ColumnConfig {
  prop: string
  label: string
  width?: number | string
  minWidth?: number | string
  align?: 'left' | 'center' | 'right'
  type?: 'text' | 'image' | 'tag' | 'status' | 'custom'
  sortable?: boolean | 'custom'
  formatter?: (row: any, column: ColumnConfig, value: any, index: number) => any
  slot?: string
}

export interface SearchOption {
  label: string
  value: any
  children?: any[]
}

export interface SearchFieldConfig {
  key: string
  label: string
  type: 'input' | 'select' | 'remote-select' | 'date' | 'daterange' | 'tree' | 'number'
  options?: SearchOption[]
  placeholder?: string
  clearable?: boolean
  multiple?: boolean
  filterable?: boolean
  remoteMethod?: (keyword: string, currentValues: Record<string, any>) => Promise<SearchOption[]> | SearchOption[]
  span?: number
  simple?: boolean
  width?: number | string
}

export interface ActionConfig {
  label: string | ((row: any) => string)
  type?: 'primary' | 'success' | 'warning' | 'danger' | 'info' | 'text' | ((row: any) => string)
  icon?: any
  permission?: string
  handler?: (row: any) => void
  visible?: (row: any) => boolean
  disabled?: (row: any) => boolean
}

export interface BatchActionConfig {
  key: string
  label: string
  type?: 'primary' | 'success' | 'warning' | 'danger' | 'text'
  icon?: any
  permission?: string
  handler?: (rows: any[]) => void
}

export interface TreeConfig {
  children?: string
  hasChildren?: string
  lazy?: boolean
  load?: (row: any, treeNode: any, resolve: (data: any[]) => void) => void
  indent?: number
  expandAll?: boolean
  defaultExpandedKeys?: string[]
}

export interface ImportConfig {
  enabled?: boolean
  templateUrl?: string
  handler?: (data: any[]) => Promise<void> | void
  fieldMapping?: Record<string, string>
  maxRows?: number
  showPreview?: boolean
}

export interface PrintConfig {
  enabled?: boolean
  title?: string
  showPreview?: boolean
  customStyles?: string
  printAll?: boolean
  beforePrint?: (data: any[]) => any[]
}

export interface DataTableProps {
  data: any[]
  loading?: boolean
  columns: ColumnConfig[]
  pagination?: { total: number; page: number; pageSize: number; pageSizes?: number[] }
  searchConfig?: SearchFieldConfig[]
  actions?: ActionConfig[]
  batchActions?: BatchActionConfig[]
  defaultSort?: { prop: string; order: SortOrder }
  showSelection?: boolean
  showIndex?: boolean
  showColumnSettings?: boolean
  showExport?: boolean
  enableVirtualScroll?: boolean
  cacheKey?: string
  cacheStrategy?: 'query' | 'localStorage' | 'none'
  responsive?: boolean
  mobileBreakpoint?: number
  rowKey?: string
  actionColumnWidth?: number | string
  permissionChecker?: (permission: string) => boolean
  treeConfig?: TreeConfig | boolean
  importConfig?: ImportConfig | boolean
  printConfig?: PrintConfig | boolean
  defaultExpandSearch?: boolean
  /** 搜索区初始值（如路由参数带入的筛选条件） */
  defaultSearchValues?: Record<string, any>
  compactMode?: boolean
  paginationSize?: 'default' | 'small'
  rowClassName?: string | ((data: { row: any; rowIndex: number }) => string)
}
