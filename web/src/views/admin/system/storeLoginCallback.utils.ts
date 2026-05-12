import type { StoreUserInfo } from '../../../api/plugin-store'

export interface NormalizedStoreLoginMessage {
  token: string
  state?: string
  user?: StoreUserInfo
}

const LOGIN_MESSAGE_TYPES = new Set([
  'ginkgo-store-login',
  'ginkgo-store-login-success',
  'store-login-success',
  'plugin-store-login-success'
])

function parseMessageData(data: unknown): any | null {
  if (!data) return null
  if (typeof data === 'string') {
    try {
      return JSON.parse(data)
    } catch {
      return null
    }
  }
  return typeof data === 'object' ? data : null
}

function pickString(...values: unknown[]): string | undefined {
  for (const value of values) {
    if (typeof value === 'string' && value.trim()) return value
  }
  return undefined
}

function pickNumber(value: unknown): number | undefined {
  if (typeof value === 'number' && Number.isFinite(value)) return value
  if (typeof value === 'string' && value.trim()) {
    const parsed = Number(value)
    if (Number.isFinite(parsed)) return parsed
  }
  return undefined
}

export function normalizeStoreUserInfo(rawUser: unknown): StoreUserInfo | undefined {
  if (!rawUser || typeof rawUser !== 'object') return undefined
  const user = rawUser as Record<string, unknown>
  const username = pickString(user.userName, user.UserName, user.username, user.Username, user.account, user.Account, user.email, user.Email, user.mobile, user.Mobile, user.phone, user.Phone)
  const nickname = pickString(user.nickname, user.Nickname, user.name, user.Name, user.displayName, user.DisplayName, user.userName, user.UserName, user.username, user.Username)
  const email = pickString(user.email, user.Email)
  const balance = pickNumber(user.balance ?? user.Balance)

  if (!username && !nickname && email === undefined && balance === undefined) return undefined

  return {
    username: username || nickname || email || '',
    nickname,
    email,
    balance
  }
}

export function normalizeStoreLoginMessage(data: unknown): NormalizedStoreLoginMessage | null {
  const parsed = parseMessageData(data)
  if (!parsed) return null

  const body = parsed.payload || parsed.data || parsed.detail || parsed
  const type = pickString(parsed.type, parsed.event, parsed.action, body.type, body.event, body.action)
  if (type && !LOGIN_MESSAGE_TYPES.has(type)) return null

  const token = pickString(
    body.token,
    body.Token,
    body.accessToken,
    body.AccessToken,
    body.authToken,
    body.AuthToken,
    body.storeToken,
    body.StoreToken,
    body.jwt,
    body.Jwt,
    parsed.token,
    parsed.Token,
    parsed.accessToken,
    parsed.AccessToken,
    parsed.authToken,
    parsed.AuthToken,
    parsed.storeToken,
    parsed.StoreToken,
    parsed.jwt,
    parsed.Jwt
  )
  if (!token) return null

  const user = normalizeStoreUserInfo(body.user || body.User || body.userInfo || body.UserInfo || body.account || body.Account || parsed.user || parsed.User || parsed.userInfo || parsed.UserInfo || parsed.account || parsed.Account)

  return {
    token,
    state: pickString(body.state, body.State, parsed.state, parsed.State),
    user
  }
}

export function normalizeDirectStoreLoginResult(data: unknown): NormalizedStoreLoginMessage | null {
  const parsed = parseMessageData(data)
  if (!parsed) return null

  const body = parsed.data || parsed.result || parsed
  const token = pickString(
    body.token,
    body.Token,
    body.accessToken,
    body.AccessToken,
    body.authToken,
    body.AuthToken,
    body.storeToken,
    body.StoreToken,
    body.jwt,
    body.Jwt,
    parsed.token,
    parsed.Token,
    parsed.accessToken,
    parsed.AccessToken,
    parsed.authToken,
    parsed.AuthToken,
    parsed.storeToken,
    parsed.StoreToken,
    parsed.jwt,
    parsed.Jwt
  )
  if (!token) return null

  const user = normalizeStoreUserInfo(body.user || body.User || body.userInfo || body.UserInfo || body.account || body.Account || body)

  return { token, user }
}
