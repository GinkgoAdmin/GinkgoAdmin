/**
 * 通知状态管理
 * 管理实时通知、未读数量、通知列表
 */
import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { signalRService, type NotificationPayload } from '../services/signalr'
import { getUnreadNotificationCount, getMyNotifications, markNotificationAsRead, getNotificationDetail, type NotificationItem } from '../api/user'
import { ElNotification } from 'element-plus'
import { h } from 'vue'

// 默认通知音频地址（可通过系统配置覆盖）
const DEFAULT_NOTIFICATION_AUDIO = 'https://assets.mixkit.co/active_storage/sfx/2869/2869-preview.mp3'

export const useNotificationStore = defineStore('notification', () => {
  // 状态
  const unreadCount = ref(0)
  const notifications = ref<NotificationItem[]>([])
  const isConnected = ref(false)
  const isLoading = ref(false)
  const lastLoadTime = ref<Date | null>(null)
  
  // 音频配置
  const notificationAudioUrl = ref(DEFAULT_NOTIFICATION_AUDIO)
  const audioEnabled = ref(true)
  let audioElement: HTMLAudioElement | null = null

  // 计算属性
  const hasUnread = computed(() => unreadCount.value > 0)
  const displayBadge = computed(() => {
    if (unreadCount.value <= 0) return ''
    if (unreadCount.value > 99) return '99+'
    return String(unreadCount.value)
  })

  /**
   * 初始化 SignalR 连接
   */
  async function initConnection(token: string): Promise<void> {
    if (!token) {
      return
    }

    try {
      // 注册事件处理器
      signalRService.on('notification:new', handleNewNotification)
      signalRService.on('notification:read', handleNotificationRead)
      signalRService.on('notification:message', handleMessage)
      signalRService.on('connection:connected', () => { isConnected.value = true })
      signalRService.on('connection:closed', () => { isConnected.value = false })
      signalRService.on('connection:reconnected', () => { 
        isConnected.value = true
        // 重连后刷新未读数
        loadUnreadCount()
      })

      await signalRService.connect(token)
      isConnected.value = signalRService.isConnected

      // 连接成功后加载初始数据
      await loadUnreadCount()
    } catch (error) {
      // silently ignored
      isConnected.value = false
    }
  }

  /**
   * 断开连接
   */
  async function disconnect(): Promise<void> {
    signalRService.off('notification:new', handleNewNotification)
    signalRService.off('notification:read', handleNotificationRead)
    signalRService.off('notification:message', handleMessage)
    await signalRService.disconnect()
    isConnected.value = false
  }

  /**
   * 处理新通知
   */
  async function handleNewNotification(payload: NotificationPayload | { id: string }): Promise<void> {
    // 增加未读数
    unreadCount.value++

    let newItem: NotificationItem

    // 如果只有 id，需要从服务器获取详情
    if (!('title' in payload) || !payload.title) {
      try {
        const detail = await getNotificationDetail(payload.id)
        newItem = {
          id: detail.id,
          title: detail.title || '新通知',
          content: detail.content || '',
          type: detail.type || 'info',
          isRead: false,
          createdAt: detail.createdAt || new Date().toISOString()
        }
      } catch (error) {
        // 使用默认值
        newItem = {
          id: payload.id,
          title: '新通知',
          content: '',
          type: 'info',
          isRead: false,
          createdAt: new Date().toISOString()
        }
      }
    } else {
      // 完整的通知数据
      newItem = {
        id: payload.id,
        title: payload.title || '新通知',
        content: payload.content || '',
        type: payload.type || 'info',
        isRead: false,
        createdAt: payload.createdAt || new Date().toISOString()
      }
    }

    // 添加到通知列表头部（避免重复）
    const existingIndex = notifications.value.findIndex(n => n.id === newItem.id)
    if (existingIndex >= 0) {
      notifications.value.splice(existingIndex, 1)
    }
    notifications.value.unshift(newItem)

    // 向全局抛出 0 耦合通知事件
    window.dispatchEvent(new CustomEvent('ginkgo:notification:new', { detail: newItem }))

    // 显示桌面通知
    showDesktopNotification(newItem)
  }

  /**
   * 处理通知已读事件
   */
  function handleNotificationRead(payload: { id: string }): void {
    const notification = notifications.value.find(n => n.id === payload.id)
    if (notification && !notification.isRead) {
      notification.isRead = true
      unreadCount.value = Math.max(0, unreadCount.value - 1)
      window.dispatchEvent(new CustomEvent('ginkgo:notification:read_sync', { detail: payload.id }))
    }
  }

  // 监听外部 0 耦合的已读触发
  window.addEventListener('ginkgo:notification:mark_read', async (e: Event) => {
    const detail = (e as CustomEvent).detail
    if (detail) {
      await markAsRead(detail)
    }
  })

  /**
   * 处理普通消息
   */
  function handleMessage(payload: { message: string }): void {
    // 可以在这里处理其他类型的消息
  }

  /**
   * 播放通知音频
   */
  function playNotificationSound(): void {
    if (!audioEnabled.value || !notificationAudioUrl.value) return
    
    try {
      // 复用或创建音频元素
      if (!audioElement) {
        audioElement = new Audio()
      }
      audioElement.src = notificationAudioUrl.value
      audioElement.volume = 0.7
      audioElement.play().catch(() => {
        // silently ignored
      })
    } catch {
      // silently ignored
    }
  }

  /**
   * 设置通知音频地址
   */
  function setAudioUrl(url: string): void {
    notificationAudioUrl.value = url || DEFAULT_NOTIFICATION_AUDIO
  }

  /**
   * 启用/禁用音频
   */
  function setAudioEnabled(enabled: boolean): void {
    audioEnabled.value = enabled
  }

  /**
   * 显示桌面通知
   */
  function showDesktopNotification(notification: NotificationItem): void {
    // 播放通知音频
    playNotificationSound()
    
    // 创建更醒目的通知弹框
    ElNotification({
      title: '🔔 新通知',
      message: h('div', { 
        style: { 
          padding: '4px 0',
          fontSize: '14px',
          lineHeight: '1.6'
        } 
      }, [
        h('div', { 
          style: { 
            fontWeight: '500', 
            marginBottom: '4px',
            color: 'var(--el-text-color-primary)'
          } 
        }, notification.title),
        h('div', { 
          style: { 
            color: 'var(--el-text-color-secondary)', 
            fontSize: '12px' 
          } 
        }, notification.content || '您有一条新通知，请注意查看')
      ]),
      type: 'info',
      duration: 8000,
      position: 'bottom-right',
      offset: 20,
      showClose: true,
      customClass: 'notification-popup-enhanced',
      onClick: () => {
        // 点击通知时可以跳转到通知详情
        window.dispatchEvent(new CustomEvent('notification:click', { detail: notification }))
      }
    })
  }

  /**
   * 加载未读数量
   */
  async function loadUnreadCount(): Promise<void> {
    try {
      unreadCount.value = await getUnreadNotificationCount()
    } catch {
      // silently ignored
    }
  }

  /**
   * 加载通知列表
   */
  async function loadNotifications(force = false): Promise<void> {
    // 防止重复加载
    if (isLoading.value) return
    
    // 5分钟内不重复加载（除非强制）
    if (!force && lastLoadTime.value) {
      const elapsed = Date.now() - lastLoadTime.value.getTime()
      if (elapsed < 5 * 60 * 1000) return
    }

    isLoading.value = true
    try {
      notifications.value = await getMyNotifications()
      lastLoadTime.value = new Date()
    } catch {
      // silently ignored
    } finally {
      isLoading.value = false
    }
  }

  /**
   * 标记通知为已读
   */
  async function markAsRead(notificationId: string): Promise<void> {
    try {
      await markNotificationAsRead(notificationId)
      
      // 更新本地状态
      const notification = notifications.value.find(n => n.id === notificationId)
      if (notification && !notification.isRead) {
        notification.isRead = true
        unreadCount.value = Math.max(0, unreadCount.value - 1)
      }
    } catch (error) {
      throw error
    }
  }

  /**
   * 标记所有通知为已读
   */
  async function markAllAsRead(): Promise<void> {
    try {
      // 批量标记
      const unreadIds = notifications.value
        .filter(n => !n.isRead)
        .map(n => n.id)
      
      await Promise.all(unreadIds.map(id => markNotificationAsRead(id)))
      
      // 更新本地状态
      notifications.value.forEach(n => { n.isRead = true })
      unreadCount.value = 0
    } catch (error) {
      throw error
    }
  }

  /**
   * 清空通知列表（仅本地）
   */
  function clearNotifications(): void {
    notifications.value = []
  }

  /**
   * 重置状态
   */
  function reset(): void {
    unreadCount.value = 0
    notifications.value = []
    isConnected.value = false
    isLoading.value = false
    lastLoadTime.value = null
  }

  return {
    // 状态
    unreadCount,
    notifications,
    isConnected,
    isLoading,
    audioEnabled,
    notificationAudioUrl,
    // 计算属性
    hasUnread,
    displayBadge,
    // 方法
    initConnection,
    disconnect,
    loadUnreadCount,
    loadNotifications,
    markAsRead,
    markAllAsRead,
    clearNotifications,
    reset,
    setAudioUrl,
    setAudioEnabled,
    playNotificationSound
  }
})
