import { ref, onMounted, onUnmounted, type Ref } from 'vue'

export interface ScrollAnimationOptions {
  /** 触发阈值，0-1 之间，默认 0.15 */
  threshold?: number
  /** 延迟（毫秒），用于交错动画 */
  delay?: number
  /** 只触发一次，默认 true */
  once?: boolean
  /** 根边距，如 '-50px' */
  rootMargin?: string
}

/**
 * 基于 IntersectionObserver 的滚动入场动画 composable
 * 返回 ref 和 isVisible 状态，绑定到元素上即可实现滚动触发动画
 */
export function useScrollAnimation(options: ScrollAnimationOptions = {}) {
  const { threshold = 0.15, once = true, rootMargin = '0px' } = options
  const elementRef = ref<HTMLElement | null>(null) as Ref<HTMLElement | null>
  const isVisible = ref(false)
  let observer: IntersectionObserver | null = null

  onMounted(() => {
    if (!elementRef.value) return

    observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            isVisible.value = true
            if (once && observer && elementRef.value) {
              observer.unobserve(elementRef.value)
            }
          } else if (!once) {
            isVisible.value = false
          }
        })
      },
      { threshold, rootMargin }
    )

    observer.observe(elementRef.value)
  })

  onUnmounted(() => {
    if (observer) {
      observer.disconnect()
      observer = null
    }
  })

  return { elementRef, isVisible }
}

/**
 * 批量创建多个滚动动画观察器，适用于列表项交错动画
 */
export function useScrollAnimations(count: number, options: ScrollAnimationOptions = {}) {
  const items: Array<{ elementRef: Ref<HTMLElement | null>; isVisible: Ref<boolean> }> = []
  for (let i = 0; i < count; i++) {
    items.push(useScrollAnimation({ ...options, delay: (options.delay || 100) * i }))
  }
  return items
}
