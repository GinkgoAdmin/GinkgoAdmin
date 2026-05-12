<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <!--
    商城登录专用滑块验证码面板。
    - 完全自包含，不依赖任何插件。
    - 调用主框架后端已代理的 /system/plugin-store/remote-captcha/* 接口。
    - 通过 emits('verified', token) 把验证 token 回传给父组件。
  -->
  <div class="store-captcha-panel" :style="{ width: bgWidth + 'px' }">
    <div class="captcha-image-wrapper" :style="{ height: bgHeight + 'px' }">
      <img v-if="challenge?.backgroundImage" :src="challenge.backgroundImage" class="captcha-bg" draggable="false" />
      <img
        v-if="challenge?.sliderImage"
        :src="challenge.sliderImage"
        class="captcha-slider-piece"
        :style="{ left: (sliderX - 3) + 'px', top: ((challenge.sliderY || 0) - 3) + 'px' }"
        draggable="false"
      />
      <div v-if="status !== 'idle'" class="captcha-overlay" :class="status">
        <span v-if="status === 'success'">✓ 验证通过</span>
        <span v-if="status === 'fail'">✗ 验证失败，请重试</span>
        <span v-if="status === 'loading'">加载中...</span>
      </div>
      <div v-if="!challenge && status !== 'loading'" class="captcha-empty">
        <span>验证码加载失败，点击下方按钮重试</span>
      </div>
    </div>

    <div class="slider-track" ref="trackRef">
      <div class="slider-track-fill" :style="{ width: sliderX + 'px' }"></div>
      <div
        class="slider-thumb"
        :style="{ left: sliderX + 'px' }"
        :class="{ active: isDragging }"
        @mousedown="onDragStart"
        @touchstart.prevent="onDragStart"
      >
        <span class="slider-thumb-icon">→</span>
      </div>
      <span v-if="sliderX < 5" class="slider-hint">拖动滑块完成验证</span>
    </div>

    <button type="button" class="captcha-refresh" @click="refresh" title="刷新验证码">↻</button>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import http from '../../../api/http'

const emit = defineEmits<{
  verified: [token: string]
  fail: [message: string]
}>()

interface SliderChallenge {
  challengeId: string
  backgroundImage?: string
  sliderImage?: string
  sliderY?: number
}

const challenge = ref<SliderChallenge | null>(null)
const sliderX = ref(0)
const isDragging = ref(false)
const status = ref<'idle' | 'loading' | 'success' | 'fail'>('idle')
const trackRef = ref<HTMLElement>()
const bgWidth = ref(320)
const bgHeight = ref(160)
let startX = 0

/** 解包后端 Result<T>：兼容 { code, data } / { data } / 裸对象 */
function unwrap(res: any): any {
  if (!res) return null
  if (typeof res === 'object' && 'data' in res && (res as any).data && typeof (res as any).data === 'object')
    return (res as any).data
  return res
}

const loadCaptcha = async () => {
  status.value = 'loading'
  sliderX.value = 0
  challenge.value = null
  try {
    const raw = await http.post('/system/plugin-store/remote-captcha/generate', { type: 'Slider' })
    const data = unwrap(raw)
    if (data && data.challengeId) {
      challenge.value = data as SliderChallenge
      status.value = 'idle'
    } else {
      status.value = 'fail'
      emit('fail', '验证码加载失败')
    }
  } catch (e: any) {
    status.value = 'fail'
    emit('fail', e?.message || '验证码加载失败')
  }
}

const refresh = () => {
  status.value = 'idle'
  sliderX.value = 0
  loadCaptcha()
}

const onDragStart = (e: MouseEvent | TouchEvent) => {
  if (status.value !== 'idle' || !challenge.value) return
  isDragging.value = true
  startX = 'touches' in e ? e.touches[0].clientX : e.clientX
  startX -= sliderX.value

  document.addEventListener('mousemove', onDragMove)
  document.addEventListener('mouseup', onDragEnd)
  document.addEventListener('touchmove', onDragMove)
  document.addEventListener('touchend', onDragEnd)
}

const onDragMove = (e: MouseEvent | TouchEvent) => {
  if (!isDragging.value) return
  const clientX = 'touches' in e ? e.touches[0].clientX : e.clientX
  const trackWidth = trackRef.value?.clientWidth || 280
  const maxX = trackWidth - 40
  let x = clientX - startX
  x = Math.max(0, Math.min(x, maxX))
  sliderX.value = x
}

const onDragEnd = async () => {
  isDragging.value = false
  document.removeEventListener('mousemove', onDragMove)
  document.removeEventListener('mouseup', onDragEnd)
  document.removeEventListener('touchmove', onDragMove)
  document.removeEventListener('touchend', onDragEnd)

  if (!challenge.value?.challengeId || sliderX.value < 5) return

  try {
    const payload = JSON.stringify({ x: sliderX.value })
    const raw = await http.post('/system/plugin-store/remote-captcha/validate', {
      challengeId: challenge.value.challengeId,
      payload
    })
    const result = unwrap(raw)
    if (result?.success && result.token) {
      status.value = 'success'
      emit('verified', result.token)
    } else {
      status.value = 'fail'
      emit('fail', result?.message || '验证失败')
      setTimeout(refresh, 1500)
    }
  } catch (e: any) {
    status.value = 'fail'
    emit('fail', e?.message || '验证失败')
    setTimeout(refresh, 1500)
  }
}

onMounted(() => loadCaptcha())
onUnmounted(() => {
  document.removeEventListener('mousemove', onDragMove)
  document.removeEventListener('mouseup', onDragEnd)
})

defineExpose({ refresh })
</script>

<style scoped>
.store-captcha-panel { position: relative; user-select: none; border-radius: 8px; overflow: hidden; background: #1a1a2e; box-shadow: 0 4px 20px rgba(0,0,0,0.3); margin: 0 auto; }
.captcha-image-wrapper { position: relative; overflow: hidden; }
.captcha-bg { width: 100%; height: 100%; object-fit: cover; display: block; }
.captcha-slider-piece { position: absolute; pointer-events: none; filter: drop-shadow(2px 2px 4px rgba(0,0,0,0.5)); }
.captcha-overlay { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; font-size: 16px; font-weight: 600; backdrop-filter: blur(2px); }
.captcha-overlay.success { background: rgba(46, 204, 113, 0.7); color: #fff; }
.captcha-overlay.fail { background: rgba(231, 76, 60, 0.7); color: #fff; }
.captcha-overlay.loading { background: rgba(0,0,0,0.5); color: #fff; }
.captcha-empty { position: absolute; inset: 0; display: flex; align-items: center; justify-content: center; color: rgba(255,255,255,0.7); font-size: 13px; }
.slider-track { position: relative; height: 44px; background: rgba(255,255,255,0.08); border-top: 1px solid rgba(255,255,255,0.1); }
.slider-track-fill { height: 100%; background: linear-gradient(90deg, rgba(52, 152, 219, 0.3), rgba(46, 204, 113, 0.3)); }
.slider-thumb { position: absolute; top: 2px; width: 40px; height: 40px; background: linear-gradient(135deg, #3498db, #2ecc71); border-radius: 6px; cursor: grab; display: flex; align-items: center; justify-content: center; transition: box-shadow 0.2s; }
.slider-thumb:hover, .slider-thumb.active { box-shadow: 0 0 12px rgba(52, 152, 219, 0.6); cursor: grabbing; }
.slider-thumb-icon { color: #fff; font-size: 18px; font-weight: bold; }
.slider-hint { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); color: rgba(255,255,255,0.4); font-size: 14px; pointer-events: none; }
.captcha-refresh { position: absolute; top: 8px; right: 8px; width: 30px; height: 30px; border-radius: 50%; background: rgba(0,0,0,0.5); color: #fff; border: none; font-size: 16px; cursor: pointer; display: flex; align-items: center; justify-content: center; transition: all 0.2s; z-index: 10; }
.captcha-refresh:hover { background: rgba(52, 152, 219, 0.8); transform: rotate(180deg); }
</style>
