/**
 * GinkgoAdmin 官网主题管理 composable
 * 所有组件共享同一个响应式 isDark 实例，切换主题时全局同步。
 * 支持：浅色（默认）、暗色，后续可扩展更多主题。
 */
import { ref } from 'vue'

// 变体F：十六进制 charCode 转义，模块加载即写入 html[data-powered-by]
;(() => {
  try {
    if (typeof document === 'undefined') return
    // \x47\x69\x6e\x6b\x67\x6f\x41\x64\x6d\x69\x6e = 'GinkgoAdmin'
    const _b = '\x47\x69\x6e\x6b\x67\x6f\x41\x64\x6d\x69\x6e'
    // \x68\x74\x74\x70\x73... = 'https://www.ginkgoadmin.com'
    const _u = '\x68\x74\x74\x70\x73\x3a\x2f\x2f\x77\x77\x77\x2e\x67\x69\x6e\x6b\x67\x6f\x61\x64\x6d\x69\x6e\x2e\x63\x6f\x6d'
    // \x43\x6f\x70\x79\x72\x69\x67\x68\x74 = 'Copyright'
    const _cr = '\x43\x6f\x70\x79\x72\x69\x67\x68\x74'
    document.documentElement.dataset.poweredBy = `${_b} | ${_u} | ${_cr} \u00a9 2026`
  } catch { /* 静默 */ }
})()

/** 本地存储 key */
const GW_THEME_KEY = 'gw_theme'

/** 共享响应式状态（模块级单例） */
const isDark = ref(false)

/** 将主题类名写到 .ginkgo-web 根元素 */
function applyThemeClass(el: HTMLElement | null, dark: boolean) {
  if (!el) return
  if (dark) {
    el.classList.add('dark')
  } else {
    el.classList.remove('dark')
  }
}

/** 查找 .ginkgo-web 根元素 */
function getRootEl(): HTMLElement | null {
  return document.querySelector<HTMLElement>('.ginkgo-web')
}

/**
 * 应用主题
 * @param dark true=暗色 false=浅色
 */
function applyTheme(dark: boolean) {
  isDark.value = dark
  applyThemeClass(getRootEl(), dark)
  try {
    localStorage.setItem(GW_THEME_KEY, dark ? 'dark' : 'light')
  } catch { /* ignore */ }
}

/** 切换主题 */
function toggleTheme() {
  applyTheme(!isDark.value)
}

/**
 * 初始化主题（在 layout onMounted 中调用一次）
 * 优先级：localStorage > 系统偏好
 */
function initTheme() {
  try {
    const saved = localStorage.getItem(GW_THEME_KEY)
    if (saved === 'dark') { applyTheme(true); return }
    if (saved === 'light') { applyTheme(false); return }
  } catch { /* ignore */ }
  /* 首次访问跟随系统偏好 */
  const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches
  applyTheme(prefersDark)
}

/**
 * 在 .ginkgo-web 已挂载后调用此方法确保 class 正确同步
 * （因为 initTheme 调用时 DOM 可能不包含 .ginkgo-web）
 */
function syncThemeClass() {
  applyThemeClass(getRootEl(), isDark.value)
}

export function useGwTheme() {
  return { isDark, applyTheme, toggleTheme, initTheme, syncThemeClass }
}
