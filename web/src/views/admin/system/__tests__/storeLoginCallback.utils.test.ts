import { describe, expect, it } from 'vitest'
import { normalizeDirectStoreLoginResult, normalizeStoreLoginMessage } from '../storeLoginCallback.utils'

describe('store login callback utils', () => {
  it('normalizes the current postMessage payload', () => {
    const result = normalizeStoreLoginMessage({
      type: 'ginkgo-store-login',
      token: 'token-a',
      state: 'state-a',
      user: { userName: 'admin', name: '管理员', balance: 12.5 }
    })

    expect(result).toEqual({
      token: 'token-a',
      state: 'state-a',
      user: { username: 'admin', nickname: '管理员', balance: 12.5 }
    })
  })

  it('normalizes relay or remote messages wrapped in payload', () => {
    const result = normalizeStoreLoginMessage({
      type: 'ginkgo-store-login-success',
      payload: {
        accessToken: 'token-b',
        state: 'state-b',
        userInfo: { username: 'store-user', nickname: '商城用户' }
      }
    })

    expect(result).toEqual({
      token: 'token-b',
      state: 'state-b',
      user: { username: 'store-user', nickname: '商城用户' }
    })
  })

  it('normalizes JSON string messages from cross-site login pages', () => {
    const result = normalizeStoreLoginMessage(JSON.stringify({
      event: 'store-login-success',
      authToken: 'token-c',
      state: 'state-c',
      account: { userName: 'json-user', name: 'JSON 用户' }
    }))

    expect(result).toEqual({
      token: 'token-c',
      state: 'state-c',
      user: { username: 'json-user', nickname: 'JSON 用户' }
    })
  })

  it('ignores unrelated messages', () => {
    expect(normalizeStoreLoginMessage({ type: 'resize' })).toBeNull()
    expect(normalizeStoreLoginMessage('not json')).toBeNull()
    expect(normalizeStoreLoginMessage({ type: 'ginkgo-store-login' })).toBeNull()
  })

  it('normalizes local proxy login results', () => {
    const result = normalizeDirectStoreLoginResult({
      token: 'proxy-token',
      userName: 'proxy-user',
      displayName: '代理用户',
      email: 'proxy@example.com'
    })

    expect(result).toEqual({
      token: 'proxy-token',
      user: {
        username: 'proxy-user',
        nickname: '代理用户',
        email: 'proxy@example.com'
      }
    })
  })

  it('normalizes local proxy login results with PascalCase fields', () => {
    const result = normalizeDirectStoreLoginResult({
      Token: 'proxy-token',
      UserName: 'proxy-user',
      DisplayName: '代理用户'
    })

    expect(result).toEqual({
      token: 'proxy-token',
      user: {
        username: 'proxy-user',
        nickname: '代理用户',
        email: undefined,
        balance: undefined
      }
    })
  })

  it('rejects local proxy login results without token', () => {
    expect(normalizeDirectStoreLoginResult({ userName: 'proxy-user' })).toBeNull()
  })
})
