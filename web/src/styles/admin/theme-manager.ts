// 使用与 auth store 相同的 localStorage 键，确保主题状态统一
const THEME_KEY = 'auth-theme'

type ThemeName = 'light' | 'dark'

type ThemeChunk = 'variables' | 'layout' | 'components' | 'animations' | 'overrides' | 'pages'

type ThemeManifest = Record<ThemeName, Record<ThemeChunk, string>>

// 由构建生成的 CSS 资源 URL（通过 new URL 解析），Vite 在打包后会生成可用链接。
const manifest: ThemeManifest = {
  light: {
    variables: new URL('./themes/light/variables.css', import.meta.url).href,
    layout: new URL('./themes/light/layout.css', import.meta.url).href,
    components: new URL('./themes/light/components.css', import.meta.url).href,
    animations: new URL('./themes/light/animations.css', import.meta.url).href,
    overrides: new URL('./themes/light/overrides.css', import.meta.url).href,
    pages: new URL('./themes/light/pages.css', import.meta.url).href,
  },
  dark: {
    variables: new URL('./themes/dark/variables.css', import.meta.url).href,
    layout: new URL('./themes/dark/layout.css', import.meta.url).href,
    components: new URL('./themes/dark/components.css', import.meta.url).href,
    animations: new URL('./themes/dark/animations.css', import.meta.url).href,
    overrides: new URL('./themes/dark/overrides.css', import.meta.url).href,
    pages: new URL('./themes/dark/pages.css', import.meta.url).href,
  },
}

const THEME_LINK_ATTR = 'data-admin-theme-link'

function applyThemeClass(target: ThemeName) {
  const html = document.documentElement
  const body = document.body
  // 设置 data-admin-theme 属性，用于后台主题标识
  html.setAttribute('data-admin-theme', target)
  body.setAttribute('data-admin-theme', target)
  // 不再在全局添加 dark 类，避免影响前台页面
  // 暗黑主题通过 MainLayout.vue 的 admin-dark 类控制
}

function removeCurrentThemeLinks() {
  document
    .querySelectorAll(`link[${THEME_LINK_ATTR}]`)
    .forEach((el) => el.parentElement?.removeChild(el))
}

function injectCssLink(href: string, id: string) {
  const link = document.createElement('link')
  link.rel = 'stylesheet'
  link.href = href
  link.setAttribute(THEME_LINK_ATTR, id)
  document.head.appendChild(link)
  return link
}

async function loadThemeCss(theme: ThemeName) {
  // 先加载变量文件（variables）以避免颜色闪烁（FOUC）
  const order: ThemeChunk[] = ['variables', 'layout', 'components', 'animations', 'overrides', 'pages']
  const hrefs = order.map((chunk) => manifest[theme][chunk])

  removeCurrentThemeLinks()
  for (let i = 0; i < hrefs.length; i++) {
    const href = hrefs[i]
    // 略过空的或缺失的条目
    if (!href) continue
    const link = injectCssLink(href, `${theme}:${order[i]}`)
    // 等待关键的 variables.css 加载完成后再继续，确保首屏稳定
    if (order[i] === 'variables') {
      await new Promise<void>((resolve, reject) => {
        link.onload = () => resolve()
        link.onerror = () => reject(new Error(`Failed to load theme css: ${href}`))
      })
    }
  }
}

export async function switchTheme(theme: ThemeName) {
  const target: ThemeName = theme === 'dark' ? 'dark' : 'light'
  await loadThemeCss(target)
  applyThemeClass(target)
  localStorage.setItem(THEME_KEY, target)
}

export async function initTheme() {
  const saved = (localStorage.getItem(THEME_KEY) as ThemeName) || (
    window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
  )
  await loadThemeCss(saved)
  applyThemeClass(saved)
  // 返回实际加载的主题，供 auth store 同步状态
  return saved
}

export function currentTheme(): ThemeName {
  const t = document.documentElement.getAttribute('data-theme') as ThemeName | null
  return t || 'light'
}

