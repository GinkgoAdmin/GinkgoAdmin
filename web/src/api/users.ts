import http from './http'

// ==================== 类型定义 ====================

// 分页请求参数
export interface PageRequest {
  page: number
  pageSize: number
}

// 分页结果
export interface PagedResult<T> {
  total: number
  page: number
  pageSize: number
  items: T[]
}

// 用户列表项 DTO
export interface UserListItemDto {
  id: string
  userName: string
  displayName: string
  email?: string
  phone?: string
  enabled: boolean
  createdAt?: string
  updatedAt?: string
  departmentNames?: string[]
  roleNames?: string[]
}

// 用户详情 DTO
export interface UserDetailDto {
  id: string
  userName: string
  displayName: string
  avatar?: string
  introduction?: string
  email?: string
  phone?: string
  enabled: boolean
  createdAt?: string
  updatedAt?: string
}

// 创建用户输入
export interface CreateUserInput {
  userName: string
  displayName: string
  password: string
  email?: string
  phone?: string
  enabled?: boolean
}

// 更新用户输入
export interface UpdateUserInput {
  displayName: string
  avatar?: string
  introduction?: string
  email?: string
  phone?: string
  enabled: boolean
}

// 修改密码输入
export interface ChangePasswordInput {
  oldPassword: string
  newPassword: string
}

// 重置密码输入
export interface ResetPasswordInput {
  newPassword: string
}

// 用户查询参数
export interface UserListQuery extends PageRequest {
  // 统一筛选条件对象，会被序列化为 filter=JSON 字符串传给服务端
  filters?: Record<string, any>
  // 排序（由前端 DataTable 提供）
  sortProp?: string
  sortOrder?: 'ascending' | 'descending' | '' | undefined
  // 兼容旧参数（将被收敛到 filters 内）
  keyword?: string
  departmentId?: string
  roleId?: string
  enabled?: boolean
}

// ==================== API 函数 ====================

/**
 * 获取用户列表（分页）
 * @param params 查询参数
 */
export async function getUsers(params: UserListQuery): Promise<PagedResult<UserListItemDto>> {
  const { filters, sortProp, sortOrder, keyword, departmentId, roleId, enabled, ...rest } = params || ({} as any)
  const mergedFilters: Record<string, any> = {
    ...(filters || {}),
    ...(keyword !== undefined ? { keyword } : {}),
    ...(departmentId ? { departmentId } : {}),
    ...(roleId ? { roleId } : {}),
    ...(enabled !== undefined ? { enabled } : {}),
  }

  const query: any = { ...rest }
  if (Object.keys(mergedFilters).length > 0) {
    try {
      query.filter = JSON.stringify(mergedFilters)
    } catch { /* ignore */ }
  }
  if (sortProp && sortOrder) {
    query.sort = `${sortProp}:${sortOrder}`
  }
  return await http.get<any, PagedResult<UserListItemDto>>('/v1/users', { params: query })
}

/**
 * 获取用户详情
 * @param id 用户 ID
 */
export async function getUserDetail(id: string): Promise<UserDetailDto> {
  return await http.get<any, UserDetailDto>(`/v1/users/${id}`)
}

/**
 * 创建用户
 * @param data 创建用户输入
 */
export async function createUser(data: CreateUserInput): Promise<string> {
  return await http.post<any, string>('/v1/users', data)
}

/**
 * 更新用户
 * @param id 用户 ID
 * @param data 更新用户输入
 */
export async function updateUser(id: string, data: UpdateUserInput): Promise<void> {
  await http.put(`/v1/users/${id}`, data)
}

/**
 * 删除用户
 * @param id 用户 ID
 */
export async function deleteUser(id: string): Promise<void> {
  await http.delete(`/v1/users/${id}`)
}

/**
 * 批量删除用户
 * @param ids 用户 ID 列表
 */
export async function batchDeleteUsers(ids: string[]): Promise<void> {
  await http.post('/v1/users/batch-delete', { ids })
}

/**
 * 重置用户密码
 * @param id 用户 ID
 * @param data 重置密码输入
 */
export async function resetUserPassword(id: string, data: ResetPasswordInput): Promise<void> {
  await http.post(`/v1/users/${id}/reset-password`, data)
}

/**
 * 切换用户状态（启用/禁用）
 * @param id 用户 ID
 * @param enabled 是否启用
 */
export async function toggleUserStatus(id: string, enabled: boolean): Promise<void> {
  await http.post(`/v1/users/${id}/toggle-status`, { enabled })
}

/**
 * 获取用户的角色 ID 列表
 * @param userId 用户 ID
 */
export async function getUserRoleIds(userId: string): Promise<string[]> {
  return await http.get<any, string[]>(`/v1/users/${userId}/roles`)
}

/**
 * 保存用户的角色 ID 列表
 * @param userId 用户 ID
 * @param roleIds 角色 ID 列表
 */
export async function saveUserRoles(userId: string, roleIds: string[]): Promise<void> {
  await http.post(`/v1/users/${userId}/roles`, roleIds)
}

/**
 * 获取用户的部门 ID 列表
 * @param userId 用户 ID
 */
export async function getUserDepartmentIds(userId: string): Promise<string[]> {
  return await http.get<any, string[]>(`/v1/users/${userId}/departments`)
}

/**
 * 保存用户的部门 ID 列表
 * @param userId 用户 ID
 * @param departmentIds 部门 ID 列表
 */
export async function saveUserDepartments(userId: string, departmentIds: string[]): Promise<void> {
  await http.post(`/v1/users/${userId}/departments`, departmentIds)
}

