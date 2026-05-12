/**
 * SignalR 实时通信服务
 * 用于连接后端 NotifyHub，接收实时通知
 */
import * as signalR from '@microsoft/signalr'
import { API_BASE_URL } from '../config/env'

export type NotificationPayload = {
  id: string
  title: string
  content?: string
  type?: string
  createdAt?: string
}

export type SignalREventHandler = (payload: any) => void

class SignalRService {
  private connection: signalR.HubConnection | null = null
  private eventHandlers: Map<string, Set<SignalREventHandler>> = new Map()
  private reconnectAttempts = 0
  private maxReconnectAttempts = 10
  private isConnecting = false

  /**
   * 初始化并连接到 SignalR Hub
   */
  async connect(token: string): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      return
    }

    if (this.isConnecting) {
      return
    }

    this.isConnecting = true

    try {
      // 构建 Hub URL
      // 优先使用当前页面的 origin（适用于前端被 API 托管的场景）
      // 开发环境使用相对路径走 Vite 代理
      let hubUrl: string
      const baseUrl = API_BASE_URL.replace(/\/$/, '')
      
      if (baseUrl.startsWith('/')) {
        // 相对路径（开发环境），使用 /hubs/notify
        hubUrl = '/hubs/notify'
      } else {
        // 完整 URL，检查是否与当前页面同源
        try {
          const apiUrl = new URL(baseUrl)
          const currentOrigin = window.location.origin
          
          // 如果 API URL 的主机与当前页面不同，可能是配置错误
          // 使用当前页面的 origin 作为 SignalR 连接地址
          if (apiUrl.origin !== currentOrigin && currentOrigin.includes('localhost')) {
            // 当前页面是 localhost，使用当前 origin
            hubUrl = currentOrigin + '/hubs/notify'
          } else {
            // 使用配置的 API URL
            hubUrl = apiUrl.origin + '/hubs/notify'
          }
        } catch {
          // URL 解析失败，使用相对路径
          hubUrl = '/hubs/notify'
        }
      }
      
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: () => token,
          // 不跳过协商，让 SignalR 自动选择最佳传输方式
          // skipNegotiation: true,
          // transport: signalR.HttpTransportType.WebSockets
        })
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // 指数退避重连策略
            if (retryContext.previousRetryCount >= this.maxReconnectAttempts) {
              return null // 停止重连
            }
            return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000)
          }
        })
        .configureLogging(signalR.LogLevel.Information)
        .build()

      // 注册连接事件
      this.connection.onclose((error) => {
        this.emit('connection:closed', { error })
      })

      this.connection.onreconnecting((error) => {
        this.emit('connection:reconnecting', { error })
      })

      this.connection.onreconnected((connectionId) => {
        this.reconnectAttempts = 0
        this.emit('connection:reconnected', { connectionId })
      })

      // 注册通知消息处理
      this.connection.on('Notify.Message', async (data: { notifyId: string | number } | string) => {
        // 后端发送的是 { notifyId } 对象，notifyId 现在是字符串
        if (typeof data === 'object' && data.notifyId) {
          this.emit('notification:new', { id: String(data.notifyId) })
        } else if (typeof data === 'string') {
          this.emit('notification:message', { message: data })
        }
      })

      // 注册新通知事件（后端推送新通知时触发）
      this.connection.on('ReceiveNotification', (notification: NotificationPayload) => {
        this.emit('notification:new', notification)
      })

      // 注册通知已读事件
      this.connection.on('NotificationRead', (notificationId: string) => {
        this.emit('notification:read', { id: notificationId })
      })

      await this.connection.start()
      this.reconnectAttempts = 0
      this.emit('connection:connected', {})
    } catch (error) {
      this.emit('connection:error', { error })
      throw error
    } finally {
      this.isConnecting = false
    }
  }

  /**
   * 断开连接
   */
  async disconnect(): Promise<void> {
    if (this.connection) {
      try {
        await this.connection.stop()
      } catch (error) {
        // silently ignored
      }
      this.connection = null
    }
  }

  /**
   * 获取连接状态
   */
  get state(): signalR.HubConnectionState {
    return this.connection?.state ?? signalR.HubConnectionState.Disconnected
  }

  /**
   * 是否已连接
   */
  get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected
  }

  /**
   * 订阅事件
   */
  on(event: string, handler: SignalREventHandler): void {
    if (!this.eventHandlers.has(event)) {
      this.eventHandlers.set(event, new Set())
    }
    this.eventHandlers.get(event)!.add(handler)
  }

  /**
   * 取消订阅事件
   */
  off(event: string, handler: SignalREventHandler): void {
    this.eventHandlers.get(event)?.delete(handler)
  }

  /**
   * 触发事件
   */
  private emit(event: string, payload: any): void {
    this.eventHandlers.get(event)?.forEach(handler => {
      try {
        handler(payload)
      } catch (error) {
        // silently ignored
      }
    })
  }

  /**
   * 调用 Hub 方法
   */
  async invoke<T = any>(method: string, ...args: any[]): Promise<T> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      throw new Error('SignalR not connected')
    }
    return await this.connection.invoke<T>(method, ...args)
  }
}

// 单例导出
export const signalRService = new SignalRService()
export default signalRService
