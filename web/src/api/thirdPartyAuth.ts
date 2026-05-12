// 第三方登录配置API

export interface ThirdPartyProvider {
  enabled: boolean
  appId?: string
  clientId?: string
  appSecret?: string
  clientSecret?: string
  appKey?: string
  redirectUri: string
  scope: string
}

export interface ThirdPartyAuthConfig {
  style: 'buttons' | 'qrcode'
  providers: {
    wechat: ThirdPartyProvider
    qq: ThirdPartyProvider
    github: ThirdPartyProvider
    google: ThirdPartyProvider
  }
}

/**
 * 获取第三方登录配置
 */
export async function getThirdPartyAuthConfig(): Promise<ThirdPartyAuthConfig> {
  try {
    // 实际项目中应该从后端API获取
    const response = await fetch('/api/auth/third-party/config')
    
    if (response.ok) {
      return await response.json()
    } else {
      throw new Error('Failed to fetch config')
    }
  } catch (error) {
    // 降级到localStorage
    const savedConfig = localStorage.getItem('third_party_auth_config')
    if (savedConfig) {
      return JSON.parse(savedConfig)
    }
    
    // 返回默认配置
    return getDefaultConfig()
  }
}

/**
 * 保存第三方登录配置
 */
export async function saveThirdPartyAuthConfig(config: ThirdPartyAuthConfig): Promise<void> {
  try {
    // 实际项目中应该保存到后端API
    const response = await fetch('/api/auth/third-party/config', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(config)
    })
    
    if (!response.ok) {
      throw new Error('Failed to save config')
    }
  } catch (error) {
    // 降级到localStorage
    localStorage.setItem('third_party_auth_config', JSON.stringify(config))
  }
}

/**
 * 获取默认配置
 */
function getDefaultConfig(): ThirdPartyAuthConfig {
  return {
    style: 'buttons',
    providers: {
      wechat: {
        enabled: false,
        appId: '',
        appSecret: '',
        redirectUri: `${window.location.origin}/auth/callback/wechat`,
        scope: 'snsapi_userinfo'
      },
      qq: {
        enabled: false,
        appId: '',
        appKey: '',
        redirectUri: `${window.location.origin}/auth/callback/qq`,
        scope: 'get_user_info'
      },
      github: {
        enabled: false,
        clientId: '',
        clientSecret: '',
        redirectUri: `${window.location.origin}/auth/callback/github`,
        scope: 'user:email'
      },
      google: {
        enabled: false,
        clientId: '',
        clientSecret: '',
        redirectUri: `${window.location.origin}/auth/callback/google`,
        scope: 'openid email profile'
      }
    }
  }
}

/**
 * 测试第三方登录配置
 */
export async function testThirdPartyProvider(provider: string, config: ThirdPartyProvider): Promise<boolean> {
  try {
    const response = await fetch(`/api/auth/third-party/test/${provider}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(config)
    })
    
    return response.ok
  } catch (error) {
    return false
  }
}

/**
 * 获取第三方登录统计信息
 */
export async function getThirdPartyAuthStats(): Promise<{
  totalLogins: number
  providerStats: Record<string, number>
}> {
  try {
    const response = await fetch('/api/auth/third-party/stats')
    
    if (response.ok) {
      return await response.json()
    } else {
      throw new Error('Failed to fetch stats')
    }
  } catch (error) {
    return {
      totalLogins: 0,
      providerStats: {}
    }
  }
}