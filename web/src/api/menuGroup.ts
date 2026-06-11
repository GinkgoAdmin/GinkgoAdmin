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
  isDefault: boolean
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
  isDefault: boolean
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
  isUniappHome?: boolean
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

// ===== 可授权入口（item 级授权）类型 =====

/**
 * 可授权入口树节点（角色编辑器树节点）。
 * 注意：雪花 Id（id / parentId）统一为字符串，禁止 Number() 转换。
 */
export interface GrantableItemNode {
  /** 菜单组项 Id（雪花 Id，字符串） */
  id: string
  /** 父节点 Id（雪花 Id，字符串）；根节点为 null */
  parentId: string | null
  /** 显示标题 */
  title: string
  /** 图标 */
  icon?: string | null
  /** 是否需要授权：false 表示公共可见、无需勾选（前端禁用勾选） */
  requireGrant: boolean
  /** 模块归属：主框架为 'sys'，插件为其 module.json 的 Id（区分大小写） */
  module: string
  /** 排序值 */
  order: number
  /** 子节点列表（树形） */
  children?: GrantableItemNode[]
}

/**
 * 可授权入口（按各端默认菜单组分组）。
 */
export interface GrantableMenuItem {
  /** 终端类型（UNIAPP / WPF / WEB_PORTAL） */
  clientType: string
  /** 默认菜单组 Id（雪花 Id，字符串） */
  groupId: string
  /** 默认菜单组名称 */
  groupName: string
  /** 该默认组下的可授权入口项（树形） */
  items: GrantableItemNode[]
}

/**
 * 角色菜单组项（item 级）授权设置输入（全量覆盖）。
 */
export interface SetRoleItemPermissionsInput {
  /** 角色 Id（雪花 Id，字符串） */
  roleId: string
  /** 授权的菜单组项 Id 集合（雪花 Id，字符串） */
  menuGroupItemIds: string[]
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

export async function setMenuGroupItemUniappHome(groupId: string, id: string, enabled: boolean): Promise<void> {
  await http.put(`/v1/menu-groups/${groupId}/items/${id}/set-uniapp-home`, { enabled })
}

// ===== 角色菜单组权限 API =====

export async function getRoleMenuGroupIds(roleId: string): Promise<string[]> {
  const resp = await http.get<any, string[]>(`/v1/menu-groups/role-permissions/${roleId}`)
  return (resp as any)?.data ?? resp ?? []
}

export async function setRoleMenuGroups(roleId: string, menuGroupIds: string[]): Promise<void> {
  await http.put('/v1/menu-groups/role-permissions', { roleId, menuGroupIds })
}

// ===== 角色菜单组项（item 级）授权 API =====

/**
 * 获取各端默认菜单组下的可授权入口项（供角色编辑器按端分组勾选）。
 */
export async function getGrantableItems(): Promise<GrantableMenuItem[]> {
  const resp = await http.get<any, GrantableMenuItem[]>('/v1/menu-groups/grantable-items')
  return (resp as any)?.data ?? resp ?? []
}

/**
 * 获取角色已授权的菜单组项 Id 列表（雪花 Id 序列化为字符串）。
 */
export async function getRoleItemPermissions(roleId: string): Promise<string[]> {
  const resp = await http.get<any, string[]>(`/v1/menu-groups/role-item-permissions/${roleId}`)
  return (resp as any)?.data ?? resp ?? []
}

/**
 * 设置角色的菜单组项（item 级）授权（以提交集合全量覆盖）。
 */
export async function setRoleItemPermissions(input: SetRoleItemPermissionsInput): Promise<void> {
  await http.put('/v1/menu-groups/role-item-permissions', input)
}

/**
 * 将菜单组设为默认（每端唯一）。
 */
export async function setGroupDefault(id: string): Promise<void> {
  await http.put(`/v1/menu-groups/${id}/set-default`)
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
