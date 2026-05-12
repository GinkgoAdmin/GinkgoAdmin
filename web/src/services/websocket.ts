import * as signalR from '@microsoft/signalr'
import { API_BASE_URL } from '../config/env'
import { useWebAuthStore } from '../stores/webAuth'
import { useAuthStore } from '../stores/auth'

export interface NotificationMessage {
  id?: string
  title: string
  content: string
  type: 'info' | 'success' | 'warning' | 'error'
  timestamp: string
  isImportant?: boolean
}

export class WebSocketNotificationService {
  private connection: signalR.HubConnection | null = null
  private listeners: Array<(message: NotificationMessage) => void> = []
  private reconnectAttempts = 0
  private maxReconnectAttempts = 5
  private reconnectDelay = 3000
  private startPromise: Promise<void> | null = null

  constructor() {
    this.initConnection()
  }

  private initConnection() {
    // 构建 SignalR Hub URL
    // 优先使用当前页面的 origin（适用于前端被 API 托管的场景）
    let hubUrl: string
    const baseUrl = API_BASE_URL.replace(/\/$/, '')
    
    if (baseUrl.startsWith('/') || baseUrl === '') {
      // 相对路径或空字符串，使用当前页面 origin
      hubUrl = window.location.origin + '/hubs/notify'
    } else {
      // 完整 URL
      try {
        const apiUrl = new URL(baseUrl)
        const currentOrigin = window.location.origin
        
        // 如果 API URL 的主机与当前页面不同，可能是配置错误
        // 使用当前页面的 origin 作为 SignalR 连接地址
        if (apiUrl.origin !== currentOrigin && currentOrigin.includes('localhost')) {
          hubUrl = currentOrigin + '/hubs/notify'
        } else {
          hubUrl = apiUrl.origin + '/hubs/notify'
        }
      } catch {
        // URL 解析失败，使用当前页面 origin
        hubUrl = window.location.origin + '/hubs/notify'
      }
    }
    
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => {
          // 获取当前用户 token
          const webAuth = useWebAuthStore()
          const auth = useAuthStore()
          const path = window.location.pathname || ''
          
          // Web 端优先使用 web token
          if (path.startsWith('/web')) {
            return webAuth.token || auth.token || ''
          }
          return auth.token || webAuth.token || ''
        }
      })
      .withAutomaticReconnect([1000, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Information)
      .build()

    // 监听通知消息
    this.connection.on('Notify.Message', (message: string | NotificationMessage) => {
      let notification: NotificationMessage
      
      if (typeof message === 'string') {
        notification = {
          title: '系统通知',
          content: message,
          type: 'info',
          timestamp: new Date().toISOString()
        }
      } else {
        notification = {
          ...message,
          timestamp: message.timestamp || new Date().toISOString()
        }
      }

      this.notifyListeners(notification)
    })

    // 连接状态处理
    this.connection.onreconnecting(() => {
      // silently ignored
    })

    this.connection.onreconnected(() => {
      this.reconnectAttempts = 0
    })

    this.connection.onclose(async () => {
      if (this.reconnectAttempts < this.maxReconnectAttempts) {
        this.reconnectAttempts++
        setTimeout(() => this.start(), this.reconnectDelay)
      }
    })
  }

  async start() {
    // 确保存在连接实例
    if (!this.connection) {
      this.initConnection()
    }
    if (!this.connection) return

    // 避免在非 Disconnected 状态重复 start
    const state = this.connection.state
    if (
      state === signalR.HubConnectionState.Connected ||
      state === signalR.HubConnectionState.Connecting ||
      state === signalR.HubConnectionState.Reconnecting
    ) {
      return
    }

    // 去重并串行化 start 调用
    if (this.startPromise) return this.startPromise

    this.startPromise = (async () => {
      try {
        await this.connection!.start()
        this.reconnectAttempts = 0
      } catch (error) {
        if (this.reconnectAttempts < this.maxReconnectAttempts) {
          this.reconnectAttempts++
          setTimeout(() => this.start(), this.reconnectDelay)
        }
      } finally {
        this.startPromise = null
      }
    })()

    return this.startPromise
  }

  async stop() {
    if (!this.connection) return
    // 若在连接过程中，等待当前 start 完成后再停止
    try {
      if (
        this.connection.state !== signalR.HubConnectionState.Disconnected
      ) {
        await this.connection.stop()
      }
    } finally {
      this.connection = null
      this.startPromise = null
    }
  }

  addListener(callback: (message: NotificationMessage) => void) {
    this.listeners.push(callback)
  }

  removeListener(callback: (message: NotificationMessage) => void) {
    const index = this.listeners.indexOf(callback)
    if (index > -1) {
      this.listeners.splice(index, 1)
    }
  }

  private notifyListeners(message: NotificationMessage) {
    this.listeners.forEach(listener => {
      try {
        listener(message)
      } catch (error) {
        // silently ignored
      }
    })
  }

  isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected
  }

  getConnectionState(): string {
    return this.connection?.state || 'Disconnected'
  }
}

// 全局单例实例
let notificationService: WebSocketNotificationService | null = null

export function getNotificationService(): WebSocketNotificationService {
  if (!notificationService) {
    notificationService = new WebSocketNotificationService()
  }
  return notificationService
}

export function destroyNotificationService() {
  if (notificationService) {
    notificationService.stop()
    notificationService = null
  }
}

