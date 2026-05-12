import http from './http'

export interface DepartmentTreeNode {
  id: string
  name: string
  children?: DepartmentTreeNode[]
}

// 来自 departments.ts 的类型 (合并)
export interface DepartmentListItemDto {
  id: string
  name: string
  code?: string
  parentId?: string
  enabled: boolean
  order: number
}

export interface DepartmentTreeNodeDto {
  id: string
  name: string
  children: DepartmentTreeNodeDto[]
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

export interface DepartmentDetail {
  id: string
  name: string
  code?: string
  parentId?: string | null
  enabled?: boolean
  sort?: number
}

export interface CreateDepartmentInput {
  name: string
  code?: string
  parentId?: string | null
  enabled?: boolean
  sort?: number
}

export type UpdateDepartmentInput = Partial<CreateDepartmentInput>

export interface DepartmentUserItem {
  id: string
  displayName: string
  email?: string
  phone?: string
  isManager?: boolean
}

// 获取完整部门树
export async function getDepartmentsTree(): Promise<DepartmentTreeNode[]> {
  const res = await http.get<any, DepartmentTreeNode[] | { data?: DepartmentTreeNode[] }>(
    '/v1/departments/tree/all'
  )
  return (res as any)?.data ?? (res as DepartmentTreeNode[]) ?? []
}

export async function getDepartmentDetail(id: string): Promise<DepartmentDetail> {
  const res = await http.get<any, DepartmentDetail | { data?: DepartmentDetail }>(`/v1/departments/${id}`)
  return (res as any)?.data ?? (res as DepartmentDetail)
}

export async function createDepartment(input: CreateDepartmentInput): Promise<string> {
  const res = await http.post<any, { data?: string } | string>('/v1/departments', input)
  if (typeof res === 'string') return res
  return (res as any)?.data as string
}

export async function updateDepartment(id: string, input: UpdateDepartmentInput): Promise<void> {
  await http.put(`/v1/departments/${id}`, input)
}

export async function deleteDepartment(id: string): Promise<void> {
  await http.delete(`/v1/departments/${id}`)
}

export async function getDepartmentUsers(id: string): Promise<DepartmentUserItem[]> {
  const res = await http.get<any, DepartmentUserItem[] | { data?: DepartmentUserItem[] }>(
    `/v1/departments/${id}/users`
  )
  return (res as any)?.data ?? (res as DepartmentUserItem[]) ?? []
}

export async function removeDepartmentUser(id: string, userId: string): Promise<void> {
  await http.delete(`/v1/departments/${id}/users/${userId}`)
}

export async function setDepartmentManager(id: string, userId: string, isManager: boolean): Promise<void> {
  await http.post(`/v1/departments/${id}/users/${userId}/manager`, { isManager })
}

// 来自 departments.ts 的函数 (合并)

/**
 * 获取部门列表（分页）
 */
export async function getDepartments(params: PageRequest & { keyword?: string }): Promise<PagedResult<DepartmentListItemDto>> {
  return await http.get<any, PagedResult<DepartmentListItemDto>>('/v1/departments', { params })
}

/**
 * 获取部门树（全部）— 返回 DepartmentTreeNodeDto[]
 */
export async function getDepartmentTree(): Promise<DepartmentTreeNodeDto[]> {
  return await http.get<any, DepartmentTreeNodeDto[]>('/v1/departments/tree/all')
}
