/**
 * 前台网站 Service Worker — 缓存优先策略
 * 
 * 功能：
 * 1. 首次加载后将 JS/CSS/HTML 缓存到浏览器
 * 2. 后续访问直接从 SW 缓存读取，实现瞬间加载
 * 3. 在后台静默更新缓存，保持内容最新
 * 4. API 数据使用 stale-while-revalidate 策略
 */

const CACHE_VERSION = 'ginkgo-web-v1'
const API_CACHE = 'ginkgo-api-v1'

// 需要缓存的静态资源模式
const STATIC_PATTERNS = [
  /\.(js|css|woff2?|ttf|eot|svg|png|jpg|jpeg|gif|webp|ico)(\?|$)/,
]

// 需要缓存的 API 路径（前台公开接口）
const API_PATTERNS = [
  /\/api\/website\/portal\//,
  /\/api\/language\/settings/,
]

// 不缓存的路径
const SKIP_PATTERNS = [
  /\/api\/auth\//,       // 认证接口
  /\/api\/admin\//,      // 后台管理接口
  /\/hubs\//,            // SignalR
  /hot-update/,          // HMR 热更新
  /__vite_ping/,         // Vite ping
  /\/@vite/,             // Vite 内部
  /\/@id/,               // Vite 模块 ID
]

// 安装：预缓存关键资源
self.addEventListener('install', (event) => {
  // 立即激活，不等待旧 SW 终结
  self.skipWaiting()
})

// 激活：清理旧版本缓存
self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(
        keys.filter(k => k !== CACHE_VERSION && k !== API_CACHE)
          .map(k => caches.delete(k))
      )
    ).then(() => self.clients.claim())
  )
})

// 请求拦截
self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url)

  // 跳过不应缓存的请求
  if (SKIP_PATTERNS.some(p => p.test(url.pathname + url.search))) return

  // 只处理 GET 请求
  if (event.request.method !== 'GET') return

  // 只处理同源请求
  if (url.origin !== self.location.origin) return

  // API 请求：stale-while-revalidate（先返回缓存，后台更新）
  if (API_PATTERNS.some(p => p.test(url.pathname))) {
    event.respondWith(staleWhileRevalidate(event.request, API_CACHE))
    return
  }

  // 静态资源和页面：cache-first（缓存优先，无缓存时走网络）
  if (STATIC_PATTERNS.some(p => p.test(url.pathname))) {
    event.respondWith(cacheFirst(event.request, CACHE_VERSION))
    return
  }

  // HTML 导航请求（SPA 页面）：network-first（网络优先，失败时用缓存）
  if (event.request.mode === 'navigate' || event.request.headers.get('accept')?.includes('text/html')) {
    event.respondWith(networkFirst(event.request, CACHE_VERSION))
    return
  }
})

// ===== 缓存策略 =====

// Cache First：优先使用缓存（适用于 JS/CSS/字体/图片等不常变的资源）
async function cacheFirst(request, cacheName) {
  const cached = await caches.match(request)
  if (cached) return cached

  try {
    const response = await fetch(request)
    if (response.ok) {
      const cache = await caches.open(cacheName)
      cache.put(request, response.clone())
    }
    return response
  } catch {
    return new Response('Offline', { status: 503 })
  }
}

// Network First：优先使用网络（适用于 HTML 页面）
async function networkFirst(request, cacheName) {
  try {
    const response = await fetch(request)
    if (response.ok) {
      const cache = await caches.open(cacheName)
      cache.put(request, response.clone())
    }
    return response
  } catch {
    const cached = await caches.match(request)
    return cached || new Response('Offline', { status: 503 })
  }
}

// Stale While Revalidate：先返回缓存（瞬间），后台更新
async function staleWhileRevalidate(request, cacheName) {
  const cache = await caches.open(cacheName)
  const cached = await cache.match(request)

  // 后台静默更新
  const fetchPromise = fetch(request).then(response => {
    if (response.ok) {
      cache.put(request, response.clone())
    }
    return response
  }).catch(() => null)

  // 有缓存则立即返回（0ms），无缓存则等待网络
  return cached || await fetchPromise || new Response('{}', { status: 503 })
}
