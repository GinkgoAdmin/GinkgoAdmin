import http from './http'

// ===== 菜单组类型 =====

export interface MenuGroupListItem {
  id: string
  name: string
  slug: string
  description?: string
  location?: string
  clientType?: string
  isSystem: boolean
  enabled: boolean
  maxDepth: number
  version?: string
  itemCount: number
}

export interface MenuGroupDetail {
  id: string
  name: string
  slug: string
  description?: string
  location?: string
  clientType?: string
  isSystem: boolean
  enabled: boolean
  maxDepth: number
  version?: string
}

export interface CreateMenuGroupInput {
  name: string
  slug: string
  description?: string
  location?: string
  clientType?: string
  maxDepth?: number
  version?: string
}

export interface UpdateMenuGroupInput extends CreateMenuGroupInput {
  enabled: boolean
}

// ===== 菜单组项类型 =====

export interface MenuGroupItemNode {
  id: string
  menuGroupId: string
  parentId?: string | null
  title: string
  titleI18n?: string
  subtitle?: string
  icon?: string
  image?: string
  linkType: string
  url?: string
  target: string
  refMenuId?: string | null
  refMenuName?: string
  permissionCode?: string
  cssClass?: string
  badge?: string
  badgeType?: string
  extraData?: string
  order: number
  enabled: boolean
  children?: MenuGroupItemNode[]
}

export interface CreateMenuGroupItemInput {
  title: string
  titleI18n?: string
  subtitle?: string
  icon?: string
  image?: string
  linkType: string
  url?: string
  target?: string
  parentId?: string | null
  refMenuId?: string | null
  permissionCode?: string
  cssClass?: string
  badge?: string
  badgeType?: string
  extraData?: string
  order?: number
}

export interface UpdateMenuGroupItemInput extends CreateMenuGroupItemInput {
  enabled: boolean
}

export interface MenuGroupItemSortInput {
  id: string
  parentId?: string | null
  order: number
}

// ===== 导航类型 =====

export interface NavigationMenu {
  slug: string
  name: string
  location?: string
  version?: string
  items: NavigationItem[]
}

export interface NavigationItem {
  id: string
  title: string
  titleI18n?: string
  subtitle?: string
  icon?: string
  image?: string
  url?: string
  target: string
  cssClass?: string
  badge?: string
  badgeType?: string
  extraData?: string
  children?: NavigationItem[]
}

// ===== 菜单组管理 API =====

export async function getMenuGroups(): Promise<MenuGroupListItem[]> {
  const resp = await http.get<any, MenuGroupListItem[]>('/v1/menu-groups')
  return (resp as any)?.data ?? resp ?? []
}

export async function getMenuGroupDetail(id: string): Promise<MenuGroupDetail> {
  const resp = await http.get<any, MenuGroupDetail>(`/v1/menu-groups/${id}`)
  return (resp as any)?.data ?? resp
}

export async function createMenuGroup(input: CreateMenuGroupInput): Promise<string> {
  const resp = await http.post<any, string>('/v1/menu-groups', input)
  return (resp as any)?.data ?? resp
}

export async function updateMenuGroup(id: string, input: UpdateMenuGroupInput): Promise<void> {
  await http.put(`/v1/menu-groups/${id}`, input)
}

export async function deleteMenuGroup(id: string): Promise<void> {
  await http.delete(`/v1/menu-groups/${id}`)
}

// ===== 菜单组项管理 API =====

export async function getMenuGroupItems(groupId: string): Promise<MenuGroupItemNode[]> {
  const resp = await http.get<any, MenuGroupItemNode[]>(`/v1/menu-groups/${groupId}/items`)
  return (resp as any)?.data ?? resp ?? []
}

export async function getMenuGroupItemDetail(groupId: string, id: string): Promise<MenuGroupItemNode> {
  const resp = await http.get<any, MenuGroupItemNode>(`/v1/menu-groups/${groupId}/items/${id}`)
  return (resp as any)?.data ?? resp
}

export async function createMenuGroupItem(groupId: string, input: CreateMenuGroupItemInput): Promise<string> {
  const resp = await http.post<any, string>(`/v1/menu-groups/${groupId}/items`, input)
  return (resp as any)?.data ?? resp
}

export async function updateMenuGroupItem(groupId: string, id: string, input: UpdateMenuGroupItemInput): Promise<void> {
  await http.put(`/v1/menu-groups/${groupId}/items/${id}`, input)
}

export async function deleteMenuGroupItem(groupId: string, id: string): Promise<void> {
  await http.delete(`/v1/menu-groups/${groupId}/items/${id}`)
}

export async function batchDeleteMenuGroupItems(groupId: string, ids: string[]): Promise<void> {
  await http.post(`/v1/menu-groups/${groupId}/items/batch-delete`, ids)
}

export async function sortMenuGroupItems(groupId: string, items: MenuGroupItemSortInput[]): Promise<void> {
  await http.put(`/v1/menu-groups/${groupId}/items/sort`, items)
}

export async function importFromSystemMenu(groupId: string, menuIds: string[], parentId?: string | null): Promise<string[]> {
  const resp = await http.post<any, string[]>(`/v1/menu-groups/${groupId}/items/import-from-system`, { menuIds, parentId })
  return (resp as any)?.data ?? resp ?? []
}

// ===== 角色菜单组权限 API =====

export async function getRoleMenuGroupIds(roleId: string): Promise<string[]> {
  const resp = await http.get<any, string[]>(`/v1/menu-groups/role-permissions/${roleId}`)
  return (resp as any)?.data ?? resp ?? []
}

export async function setRoleMenuGroups(roleId: string, menuGroupIds: string[]): Promise<void> {
  await http.put('/v1/menu-groups/role-permissions', { roleId, menuGroupIds })
}

// ===== 导航查询 API =====

export async function getNavigation(slug: string): Promise<NavigationMenu | null> {
  try {
    const resp = await http.get<any, NavigationMenu>(`/v1/navigation/${slug}`)
    return (resp as any)?.data ?? resp ?? null
  } catch {
    return null
  }
}
