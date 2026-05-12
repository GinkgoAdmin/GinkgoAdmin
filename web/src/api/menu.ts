import http from './http'

export interface MenuItem {
  id: string
  name: string
  // 数据库返回的前端路由路径（相对路径，如 'system/users'）。无则回退到 route
  webRouteUrl?: string
  // 兼容历史字段：可能为 '/system/users' 这样的路径
  route?: string
  icon?: string
  enabled: boolean
  order: number
  // 可选：菜单类型（Directory/Menu/Item/Button/Api），后端若返回可利用
  type?: string
  // 菜单业务编码（如 'aicore:sessions'），前端按前缀定位插件目录
  code?: string
  children?: MenuItem[]
}

// 管理端菜单树节点（与后端 MenuNodeDto 对齐）
export interface AdminMenuNode {
  id: string
  name: string
  route: string
  code?: string
  resource?: string
  method?: string
  type: string
  icon?: string
  enabled: boolean
  order: number
  supportedClients?: string
  wpfDisplayMode?: string
  webDisplayMode?: string
  mobileDisplayMode?: string
  wpfRouteUrl?: string
  webRouteUrl?: string
  mobileRouteUrl?: string
  children?: AdminMenuNode[]
}

export interface CreateMenuInput {
  name: string
  nameI18n?: string | null
  parentId?: string | null
  type?: string
  route?: string
  icon?: string
  enabled?: boolean
  order?: number
  supportedClients?: string
  wpfDisplayMode?: string
  webDisplayMode?: string
  mobileDisplayMode?: string
  wpfRouteUrl?: string
  webRouteUrl?: string
  mobileRouteUrl?: string
  itemMode?: string
  url?: string
  code?: string
  resource?: string
  method?: string
}

export type UpdateMenuInput = Partial<CreateMenuInput>

export interface MenuDetail extends CreateMenuInput {
  id: string
}

// 管理端：获取完整菜单树
export async function getAdminMenusTree(): Promise<AdminMenuNode[]> {
  const resp = await http.get<any, { code?: number; data?: AdminMenuNode[] } | AdminMenuNode[]>('/v1/menus/tree/all')
  const data = Array.isArray((resp as any)?.data) ? (resp as any).data : resp
  return (data as AdminMenuNode[]) || []
}

export async function createMenu(input: CreateMenuInput): Promise<string> {
  const id = await http.post<any, string>('/v1/menus', input)
  return id
}

export async function updateMenu(id: string, input: UpdateMenuInput): Promise<void> {
  await http.put(`/v1/menus/${id}`, input)
}

export async function getMenuDetail(id: string): Promise<MenuDetail> {
  const res = await http.get<any, MenuDetail | { data: MenuDetail }>(`/v1/menus/${id}`)
  return (res as any)?.data ?? (res as MenuDetail)
}

export async function deleteMenu(id: string): Promise<void> {
  await http.delete(`/v1/menus/${id}`)
}

export async function batchDeleteMenus(ids: string[]): Promise<void> {
  await http.post('/v1/menus/batch-delete', ids)
}

// 获取当前用户的菜单树
export async function getUserMenus(): Promise<MenuItem[]> {
  try {
    const response = await http.get<any, MenuItem[]>('/v1/menus/tree/my', { params: { clientType: 'WEB' } })
    return response || []
  } catch (error) {
    // 返回默认菜单
    return getDefaultMenus()
  }
}

// 获取当前用户的按钮权限代码
export async function getUserButtonCodes(): Promise<string[]> {
  try {
    const response = await http.get<any, string[]>('/v1/menus/my/buttons')
    return response || []
  } catch (error) {
    return []
  }
}

// 默认菜单（当API不可用时使用）
function getDefaultMenus(): MenuItem[] {
  return [
    {
      id: '1',
      name: '数据统计',
      route: '/admin/dashboard',
      icon: 'DataAnalysis',
      enabled: true,
      order: 10
    },
    {
      id: '2',
      name: '首页',
      icon: 'Setting',
      enabled: true,
      order: 100,
      children: [
        {
          id: '2-1',
          name: '用户管理',
          route: '/admin/system/users',
          icon: 'User',
          enabled: true,
          order: 110
        },
        {
          id: '2-2',
          name: '角色管理',
          route: '/admin/system/roles',
          icon: 'UserFilled',
          enabled: true,
          order: 120
        },
        {
          id: '2-3',
          name: '权限管理',
          route: '/admin/system/permissions',
          icon: 'Key',
          enabled: true,
          order: 130
        },
        {
          id: '2-4',
          name: '部门管理',
          route: '/admin/system/departments',
          icon: 'OfficeBuilding',
          enabled: true,
          order: 140
        },
        {
          id: '2-5',
          name: '菜单管理',
          route: '/admin/system/menus',
          icon: 'Menu',
          enabled: true,
          order: 150
        }
      ]
    },
    {
      id: '3',
      name: '系统工具',
      icon: 'Tools',
      enabled: true,
      order: 200,
      children: [
        {
          id: '3-1',
          name: '数据字典',
          route: '/admin/system/dictionaries',
          icon: 'Collection',
          enabled: true,
          order: 210
        },
        {
          id: '3-2',
          name: '日志管理',
          route: '/admin/system/logs',
          icon: 'Document',
          enabled: true,
          order: 220
        },
        {
          id: '3-3',
          name: '文件管理',
          route: '/admin/system/files',
          icon: 'Folder',
          enabled: true,
          order: 230
        }
      ]
    }
  ]
}
