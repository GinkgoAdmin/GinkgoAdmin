import { ref, onMounted, onUnmounted, type Ref } from 'vue'

export interface CountUpOptions {
  /** 起始值，默认 0 */
  from?: number
  /** 目标值 */
  to: number
  /** 动画持续时间（毫秒），默认 2000 */
  duration?: number
  /** 缓动函数，默认 easeOutExpo */
  easing?: (t: number) => number
  /** 数字后缀，如 '+', 'K+' */
  suffix?: string
  /** 与 IntersectionObserver 结合，滚动可见时触发 */
  triggerOnVisible?: boolean
  /** IntersectionObserver 阈值 */
  threshold?: number
}

/** easeOutExpo 缓动 */
function easeOutExpo(t: number): number {
  return t === 1 ? 1 : 1 - Math.pow(2, -10 * t)
}

/**
 * 数字递增动画 composable
 * @returns elementRef（用于可见性检测）、displayValue（当前显示值）、formattedValue（格式化后的字符串）
 */
export function useCountUp(options: CountUpOptions) {
  const {
    from = 0,
    to,
    duration = 2000,
    easing = easeOutExpo,
    suffix = '',
    triggerOnVisible = true,
    threshold = 0.3
  } = options

  const elementRef = ref<HTMLElement | null>(null) as Ref<HTMLElement | null>
  const displayValue = ref(from)
  const formattedValue = ref(`${from}${suffix}`)
  let animationFrame: number | null = null
  let observer: IntersectionObserver | null = null
  let started = false

  function animate() {
    if (started) return
    started = true
    const startTime = performance.now()

    function tick(now: number) {
      const elapsed = now - startTime
      const progress = Math.min(elapsed / duration, 1)
      const easedProgress = easing(progress)
      const current = Math.round(from + (to - from) * easedProgress)

      displayValue.value = current
      formattedValue.value = `${current.toLocaleString()}${suffix}`

      if (progress < 1) {
        animationFrame = requestAnimationFrame(tick)
      }
    }

    animationFrame = requestAnimationFrame(tick)
  }

  onMounted(() => {
    if (!triggerOnVisible) {
      animate()
      return
    }

    if (!elementRef.value) return

    observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting && !started) {
            animate()
            if (observer && elementRef.value) {
              observer.unobserve(elementRef.value)
            }
          }
        })
      },
      { threshold }
    )

    observer.observe(elementRef.value)
  })

  onUnmounted(() => {
    if (animationFrame) {
      cancelAnimationFrame(animationFrame)
    }
    if (observer) {
      observer.disconnect()
    }
  })

  return { elementRef, displayValue, formattedValue }
}
