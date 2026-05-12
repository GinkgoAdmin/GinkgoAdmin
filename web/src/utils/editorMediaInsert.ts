export type MediaInsertType = 'image' | 'video' | 'file'

export interface MediaInsertItem {
  url: string
  name?: string
  alt?: string
  text?: string
}

/** 只允许外部 HTTP(S) 地址和系统资源绝对路径，避免脚本协议进入编辑器内容 */
export function isSafeMediaUrl(url: string): boolean {
  const value = (url || '').trim()
  return /^https?:\/\//i.test(value)
    || value.startsWith('/uploads/')
    || value.startsWith('/resource/')
    || value.startsWith('/api/')
}

/** 将多行地址输入解析为插入项 */
export function normalizeMediaItems(value: string): MediaInsertItem[] {
  return (value || '')
    .split(/\r?\n/)
    .map(url => url.trim())
    .filter(Boolean)
    .map(url => ({ url }))
}

/** 转义 HTML 属性和文本，避免特殊字符破坏插入片段 */
export function escapeHtml(value: string): string {
  return (value || '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

function getFileNameFromUrl(url: string): string {
  const cleanUrl = (url || '').split('?')[0]
  const parts = cleanUrl.split('/')
  return parts[parts.length - 1] || url
}

export function buildMediaInsertHtml(type: MediaInsertType, items: MediaInsertItem[]): string {
  return items
    .map(item => {
      const url = item.url.trim()
      if (!isSafeMediaUrl(url)) return ''

      const safeUrl = escapeHtml(url)
      if (type === 'image') {
        const safeAlt = escapeHtml((item.alt || item.name || '').trim())
        return `<img src="${safeUrl}" alt="${safeAlt}" style="max-width:100%;" />`
      }

      if (type === 'video') {
        return `<video src="${safeUrl}" controls style="max-width:100%;">您的浏览器不支持视频播放</video>`
      }

      const label = (item.text || item.name || getFileNameFromUrl(url)).trim()
      const safeText = escapeHtml(label || url)
      return `<a href="${safeUrl}" target="_blank" download="${safeText}">${safeText}</a> `
    })
    .filter(Boolean)
    .join('')
}
