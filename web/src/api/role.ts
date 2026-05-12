import http from './http'

export interface RoleTreeNode {
  id: string
  name: string
  code?: string
  allowedClients?: string
  children?: RoleTreeNode[]
}

export interface RoleDetail {
  id: string
  name: string
  code?: string
  enabled?: boolean
  parentId?: string | null
  allowedClients?: string
  isSuperAdmin?: boolean
}

export interface CreateRoleInput {
  name: string
  code?: string
  enabled?: boolean
  parentId?: string | null
  dataScope?: string
  allowedClients?: string
  isSuperAdmin?: boolean
}

export type UpdateRoleInput = Partial<CreateRoleInput>

export interface PermissionItem {
  id: string
  name: string
  code?: string
}

export interface PermissionTreeNode {
  id: string
  name: string
  route?: string
  code?: string
  resource?: string
  method?: string
  type?: string
  children?: PermissionTreeNode[]
}

export interface RoleDataScopeDto {
  dataScope?: string
  strategy?: string
  departmentIds?: string[]
}

export interface SetRoleDataScopeInput {
  dataScope: string
  departmentIds?: string[]
}

export interface RoleListItemDto {
  id: string
  name: string
  code: string
  enabled: boolean
  dataScope: string
}

export interface RoleTreeNodeDto {
  id: string
  name: string
  code: string
  enabled: boolean
  dataScope: string
  children: RoleTreeNodeDto[]
}

export interface PermissionTreeNodeDto {
  id: string
  name: string
  type: string
  route?: string
  code?: string
  resource?: string
  method?: string
  permissionId?: string
  children: PermissionTreeNodeDto[]
}

export interface PageRequest {
  page: number
  pageSize: number
}

export interface PagedResult<T> {
  total: number
  page: number
  pageSize: number
  items: T[]
}

export async function getRoleTree(): Promise<RoleTreeNode[]> {
  const res = await http.get<any, RoleTreeNode[] | { data?: RoleTreeNode[] }>(`/v1/roles/tree`)
  return (res as any)?.data ?? (res as RoleTreeNode[]) ?? []
}

export async function createRole(input: CreateRoleInput): Promise<string> {
  const res = await http.post<any, { data?: string } | string>(`/v1/roles`, input)
  if (typeof res === 'string') return res
  return (res as any)?.data as string
}

export async function updateRole(id: string, input: UpdateRoleInput): Promise<void> {
  await http.put(`/v1/roles/${id}`, input)
}

export async function deleteRole(id: string): Promise<void> {
  await http.delete(`/v1/roles/${id}`)
}

export async function getRoleDetail(id: string): Promise<RoleDetail> {
  const res = await http.get<any, RoleDetail | { data?: RoleDetail }>(`/v1/roles/${id}`)
  return (res as any)?.data ?? (res as RoleDetail)
}

export async function getPermissionTree(): Promise<PermissionTreeNode[]> {
  const res = await http.get<any, PermissionTreeNode[] | { data?: PermissionTreeNode[] }>(`/v1/roles/permissions/tree`)
  return (res as any)?.data ?? (res as PermissionTreeNode[]) ?? []
}

export async function getAllPermissions(): Promise<PermissionItem[]> {
  const res = await http.get<any, PermissionItem[] | { data?: PermissionItem[] }>(`/v1/roles/permissions/all`)
  return (res as any)?.data ?? (res as PermissionItem[]) ?? []
}

export async function getRolePermissionIds(id: string): Promise<string[]> {
  const res = await http.get<any, string[] | { data?: string[] }>(`/v1/roles/${id}/permissions`)
  return (res as any)?.data ?? (res as string[]) ?? []
}

export async function saveRolePermissions(id: string, permissionIds: string[]): Promise<void> {
  await http.post(`/v1/roles/${id}/permissions`, permissionIds)
}

export async function getRoleDataScope(id: string): Promise<RoleDataScopeDto> {
  const res = await http.get<any, RoleDataScopeDto | { data?: RoleDataScopeDto }>(`/v1/roles/${id}/data-scope`)
  return (res as any)?.data ?? (res as RoleDataScopeDto)
}

export async function setRoleDataScope(id: string, input: SetRoleDataScopeInput): Promise<void> {
  await http.post(`/v1/roles/${id}/data-scope`, input)
}

export async function getRoles(params: PageRequest & { keyword?: string }): Promise<PagedResult<RoleListItemDto>> {
  return await http.get<any, PagedResult<RoleListItemDto>>('/v1/roles', { params })
}

export async function getAllRoles(): Promise<RoleListItemDto[]> {
  const result = await http.get<any, PagedResult<RoleListItemDto>>('/v1/roles', {
    params: { page: 1, pageSize: 1000 }
  })
  return result.items
}
