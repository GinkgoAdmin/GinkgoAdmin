import { onMounted, onUnmounted, type Ref } from 'vue'

/**
 * 页面滚动入场动画 composable
 * 模板中使用 data-animate="up|left|right|scale|fade|blur"
 * 可选 data-animate-delay="1~8" 设置阶梯延迟
 *
 * 支持异步/动态渲染的元素（v-for 列表、懒加载内容等）：
 * 内部通过 MutationObserver 监听新增节点，自动将带有 data-animate 属性的
 * 后续元素纳入 IntersectionObserver 观察范围，保证入场动画正常触发。
 */
export function useScrollReveal(rootRef: Ref<HTMLElement | null | undefined>) {
  let observer: IntersectionObserver | null = null
  let mutationObserver: MutationObserver | null = null

  /** 将单个元素纳入 IntersectionObserver 观察（如果尚未可见） */
  function observeElement(el: Element) {
    if (el.hasAttribute('data-animate') && !el.classList.contains('is-visible')) {
      observer?.observe(el)
    }
  }

  /** 批量观察根节点下所有带 data-animate 的元素 */
  function observeAll(root: HTMLElement) {
    root.querySelectorAll('[data-animate]').forEach(observeElement)
  }

  onMounted(() => {
    const root = rootRef.value
    if (!root) return

    observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('is-visible')
            observer?.unobserve(entry.target)
          }
        })
      },
      { threshold: 0.08, rootMargin: '0px 0px -40px 0px' }
    )

    // 观察已存在的元素（页面静态内容）
    observeAll(root)

    // 监听后续动态插入的节点（v-for 渲染、异步数据加载后的卡片等）
    mutationObserver = new MutationObserver((mutations) => {
      for (const mutation of mutations) {
        mutation.addedNodes.forEach((node) => {
          if (node.nodeType !== Node.ELEMENT_NODE) return
          const el = node as Element
          // 节点本身可能就带 data-animate
          observeElement(el)
          // 节点的子孙元素也需要检查（如整个 grid 容器被插入时）
          el.querySelectorAll('[data-animate]').forEach(observeElement)
        })
      }
    })

    mutationObserver.observe(root, { childList: true, subtree: true })
  })

  onUnmounted(() => {
    observer?.disconnect()
    observer = null
    mutationObserver?.disconnect()
    mutationObserver = null
  })
}
