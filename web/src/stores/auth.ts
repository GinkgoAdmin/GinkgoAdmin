import { defineStore } from 'pinia'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: '' as string,
    userName: '' as string,
    displayName: '' as string,
    avatar: '' as string,
    roles: [] as string[],
    isSuperAdmin: false as boolean,
    theme: 'light' as 'light' | 'dark',
  }),
  getters: {
    isAuthenticated: (s) => !!s.token,
  },
  actions: {
    setToken(t: string) { 
      this.token = t
      this.saveToStorage()
    },
    setProfile(p: { userName?: string; displayName?: string; avatar?: string; roles?: string[]; isSuperAdmin?: boolean }) {
      if (p.userName !== undefined) this.userName = p.userName
      if (p.displayName !== undefined) this.displayName = p.displayName
      if (p.avatar !== undefined) this.avatar = p.avatar
      if (p.roles !== undefined) this.roles = Array.isArray(p.roles) ? p.roles : []
      if (p.isSuperAdmin !== undefined) this.isSuperAdmin = !!p.isSuperAdmin
      this.saveToStorage()
    },
    logout() { 
      this.token = ''
      this.userName = ''
      this.displayName = ''
      this.avatar = ''
      this.roles = []
      this.isSuperAdmin = false
      // 清除localStorage
      localStorage.removeItem('auth-token')
      localStorage.removeItem('auth-userName')
      localStorage.removeItem('auth-displayName')
      localStorage.removeItem('auth-avatar')
      localStorage.removeItem('auth-roles')
      localStorage.removeItem('auth-isSuperAdmin')
      localStorage.removeItem('auth-theme')
    },
    toggleTheme() { 
      this.theme = this.theme === 'light' ? 'dark' : 'light'
      this.saveToStorage()
    },
    // 初始化时从localStorage恢复数据
    initFromStorage() {
      const token = localStorage.getItem('auth-token')
      const userName = localStorage.getItem('auth-userName')
      const displayName = localStorage.getItem('auth-displayName')
      const avatar = localStorage.getItem('auth-avatar')
      const roles = localStorage.getItem('auth-roles')
      const isSuperAdmin = localStorage.getItem('auth-isSuperAdmin')
      const theme = localStorage.getItem('auth-theme') as 'light' | 'dark'
      
      if (token) this.token = token
      if (userName) this.userName = userName
      if (displayName) this.displayName = displayName
      if (avatar) this.avatar = avatar
      if (roles) {
        try { this.roles = JSON.parse(roles) } catch { this.roles = [] }
      }
      this.isSuperAdmin = isSuperAdmin === '1'
      if (theme) this.theme = theme
    },
    // 保存到localStorage
    saveToStorage() {
      if (this.token) {
        localStorage.setItem('auth-token', this.token)
        localStorage.setItem('auth-userName', this.userName)
        localStorage.setItem('auth-displayName', this.displayName)
        localStorage.setItem('auth-avatar', this.avatar)
        localStorage.setItem('auth-roles', JSON.stringify(this.roles || []))
        localStorage.setItem('auth-isSuperAdmin', this.isSuperAdmin ? '1' : '0')
      }
      localStorage.setItem('auth-theme', this.theme)
    }
  }
})


