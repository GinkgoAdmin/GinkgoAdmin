import { defineStore } from 'pinia'

export interface WebUser {
  userName: string
  name: string
  email?: string
  phone?: string
  bio?: string
  avatar?: string
}

export const useWebAuthStore = defineStore('webAuth', {
  state: () => ({
    token: '' as string,
    userInfo: null as WebUser | null,
  }),
  
  getters: {
    isAuthenticated: (state) => !!state.token && !!state.userInfo,
    userName: (state) => state.userInfo?.userName || '',
    displayName: (state) => state.userInfo?.name || state.userInfo?.userName || '用户',
    avatar: (state) => state.userInfo?.avatar || '',
  },
  
  actions: {
    // 登录
    login(token: string, userInfo: WebUser) {
      this.token = token
      this.userInfo = userInfo
      this.saveToStorage()
    },
    
    // 更新用户信息
    updateUserInfo(userInfo: Partial<WebUser>) {
      if (this.userInfo) {
        this.userInfo = { ...this.userInfo, ...userInfo }
        this.saveToStorage()
      }
    },
    
    // 退出登录
    logout() {
      this.token = ''
      this.userInfo = null
      this.clearStorage()
    },
    
    // 从localStorage初始化
    initFromStorage() {
      const token = localStorage.getItem('web_user_token')
      const userInfoStr = localStorage.getItem('web_user_info')
      
      if (token && userInfoStr) {
        try {
          const userInfo = JSON.parse(userInfoStr)
          this.token = token
          this.userInfo = userInfo
        } catch (e) {
          this.clearStorage()
        }
      }
    },
    
    // 保存到localStorage
    saveToStorage() {
      if (this.token && this.userInfo) {
        localStorage.setItem('web_user_token', this.token)
        localStorage.setItem('web_user_info', JSON.stringify(this.userInfo))
      }
    },
    
    // 清除localStorage
    clearStorage() {
      localStorage.removeItem('web_user_token')
      localStorage.removeItem('web_user_info')
    },
    
    // 检查登录状态
    checkAuthStatus() {
      const token = localStorage.getItem('web_user_token')
      const userInfoStr = localStorage.getItem('web_user_info')
      
      if (!token || !userInfoStr) {
        this.logout()
        return false
      }
      
      try {
        const userInfo = JSON.parse(userInfoStr)
        if (!this.token || !this.userInfo) {
          this.token = token
          this.userInfo = userInfo
        }
        return true
      } catch (e) {
        this.logout()
        return false
      }
    }
  }
})