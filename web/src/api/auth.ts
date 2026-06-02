import http from './http'

export interface LoginResponse {
  token: string
  /** 刷新令牌：用于在 access token 过期前静默换取新令牌（一次性轮换） */
  refreshToken?: string
  /** access token 过期时间（后端返回的服务器本地时间 ISO 字符串） */
  expiresAt?: string
  userName: string
  displayName: string
  avatar?: string
  roles: string[]
  /** 是否超级管理员（来源：ginkgo_Sys_Role.IsSuperAdmin=1） */
  isSuperAdmin?: boolean
}

export async function login(userName: string, password: string, clientType: string = 'WEB_ADMIN'): Promise<LoginResponse> {
  const form = new URLSearchParams()
  form.append('userName', userName)
  form.append('password', password)
  form.append('clientType', clientType)
  // 注意：AuthController 路由为 [Route("api/auth")]，不带版本前缀
  // baseURL 已含 /api → 这里用 "/auth/login"
  const data = await http.post<any, LoginResponse>('/auth/login', form, { headers: { 'Content-Type': 'application/x-www-form-urlencoded' } })
  return data
}

/** 刷新令牌返回结构（对应后端 AuthController.RefreshAsync） */
export interface RefreshTokenResponse {
  token: string
  refreshToken: string
  expiresAt: string
  isSuperAdmin?: boolean
}

/**
 * 使用 Refresh Token 静默换取新的 Access Token + Refresh Token（一次性轮换）。
 * 对应后端 [AllowAnonymous] 的 POST /api/auth/refresh，仅凭 refreshToken 即可换发，
 * 不依赖当前可能已过期的 access token，适用于大屏长时间挂屏的静默续期场景。
 */
export async function refreshAuthToken(refreshToken: string): Promise<RefreshTokenResponse> {
  return await http.post<any, RefreshTokenResponse>('/auth/refresh', { refreshToken })
}

export interface RegisterInput {
  userName: string
  displayName: string
  email?: string
  phone?: string
  password: string
  confirmPassword: string
  emailCode?: string
  phoneCode?: string
}

export async function register(input: RegisterInput): Promise<string> {
  // 后端返回 Guid（string），不返回 token；成功后前端通常跳转到登录页
  const payload: any = { ...input }
  if (typeof payload.email === 'string' && payload.email.trim() === '') {
    delete payload.email
  }
  return await http.post<any, string>('/auth/register', payload)
}

export interface CheckAccountContactOutput {
  found: boolean
  hasEmail: boolean
  hasPhone: boolean
  maskedEmail: string | null
  maskedPhone: string | null
}

export async function checkAccountContact(account: string): Promise<CheckAccountContactOutput> {
  return await http.post<any, CheckAccountContactOutput>('/auth/password/check-contact', { account })
}

export interface ForgotPasswordStartInput { account: string; channel?: string }
export async function forgotPasswordStart(input: ForgotPasswordStartInput): Promise<void> {
  await http.post('/auth/password/forgot', input)
}

export interface ForgotPasswordResetInput { account: string; token: string; newPassword: string }
export async function forgotPasswordReset(input: ForgotPasswordResetInput): Promise<void> {
  await http.post('/auth/password/reset', input)
}

// 退出登录：调用后端 /api/auth/logout（注意 http 已包含 /api 前缀）
export async function logout(): Promise<void> {
  await http.post('/auth/logout')
}

// ===== 统一验证码 API（主框架基础能力）=====

/** 验证码发送结果 */
export interface SendCodeResult {
  success: boolean
  message: string
  cooldownSeconds: number
}

/** 验证码校验结果 */
export interface ValidateCodeResult {
  success: boolean
  message: string
  verifiedToken?: string | null
}

/** 渠道信息 */
export interface ChannelInfo {
  value: number
  label: string
}

/** 发送验证码请求 */
export interface SendCodeInput {
  target: string
  purpose: number
  channel?: number
  ttlSeconds?: number
  codeLength?: number
  throttleSeconds?: number
}

/** 校验验证码请求 */
export interface ValidateCodeInput {
  target: string
  purpose: number
  code: string
  consumeOnSuccess?: boolean
}

/** 发送验证码 */
export async function sendVerificationCode(input: SendCodeInput): Promise<SendCodeResult> {
  return await http.post<any, SendCodeResult>('/auth/verification/send', input)
}

/** 校验验证码 */
export async function validateVerificationCode(input: ValidateCodeInput): Promise<ValidateCodeResult> {
  return await http.post<any, ValidateCodeResult>('/auth/verification/validate', input)
}

/** 获取可用渠道列表 */
export async function getVerificationChannels(): Promise<ChannelInfo[]> {
  return await http.get<any, ChannelInfo[]>('/auth/verification/channels')
}

// ===== 验证码模板管理 API（管理后台用）=====

/** 验证码模板 */
export interface VerificationTemplate {
  id: string
  purpose: number
  channel: number
  name: string
  subject?: string
  bodyTemplate: string
  isHtml: boolean
  isDefault: boolean
  enabled: boolean
  sortOrder: number
  createdAt: string
  updatedAt?: string
}

/** 获取模板列表 */
export async function getVerificationTemplates(): Promise<VerificationTemplate[]> {
  return await http.get<any, VerificationTemplate[]>('/auth/verification/templates')
}

/** 保存模板（新增/更新） */
export async function saveVerificationTemplate(input: Partial<VerificationTemplate>): Promise<void> {
  // id 需要转为数字，后端 SaveTemplateInput.Id 是 long? 类型
  const payload = { ...input, id: input.id ? Number(input.id) : undefined }
  await http.post('/auth/verification/templates', payload)
}

/** 删除模板 */
export async function deleteVerificationTemplate(id: string): Promise<void> {
  await http.delete(`/auth/verification/templates/${id}`)
}

