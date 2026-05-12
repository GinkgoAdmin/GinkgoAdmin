<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="min-h-screen flex" :class="`anim-${animLevel}`">
    <!-- 左侧品牌展示区域 -->
    <div
      class="hidden lg:flex lg:w-1/2 relative overflow-hidden login-left-panel"
      :class="{ 'has-bg-image': !!loginBackground, 'bg-gradient-to-br from-blue-600 via-blue-700 to-blue-800': !loginBackground }"
      :style="leftPanelStyle"
    >
      <!-- 背景动画元素：basic = light/medium/strong 都显示；extra = medium/strong；rich = 仅 strong -->
      <div class="absolute inset-0">
        <!-- 浮动圆圈 -->
        <div v-if="showAnim('basic')" class="floating-circle absolute top-20 left-20 w-32 h-32 bg-white/10 rounded-full animate-float-slow"></div>
        <div v-if="showAnim('extra')" class="floating-circle absolute top-40 right-32 w-24 h-24 bg-white/5 rounded-full animate-float-medium"></div>
        <div v-if="showAnim('rich')" class="floating-circle absolute bottom-32 left-40 w-40 h-40 bg-white/8 rounded-full animate-float-fast"></div>

        <!-- 几何装饰 -->
        <div v-if="showAnim('extra')" class="absolute top-1/4 right-20 w-16 h-16 border-2 border-white/20 rotate-45 animate-spin-slow"></div>
        <div v-if="showAnim('rich')" class="absolute bottom-1/4 left-16 w-12 h-12 border-2 border-white/15 rotate-12 animate-pulse"></div>

        <!-- 波浪效果（始终显示，不属于动画） -->
        <div class="absolute bottom-0 left-0 w-full h-32 bg-gradient-to-t from-blue-900/30 to-transparent"></div>
      </div>
      
      <!-- 品牌内容 -->
      <div class="relative z-10 flex flex-col justify-center items-center text-white p-12 w-full">
        <div class="text-center space-y-8">
          <!-- LOGO -->
          <div class="flex justify-center">
            <div class="relative">
              <img v-if="logoUrl" :src="logoUrl" alt="logo"
                   :class="['h-20 w-20 rounded-full shadow-lg', { 'animate-logo-rotate': showAnim('basic') }]"/>
              <div v-else :class="['h-20 w-20 bg-white/20 rounded-full flex items-center justify-center', { 'animate-logo-rotate': showAnim('basic') }]">
                <svg class="w-10 h-10 text-white" fill="currentColor" viewBox="0 0 20 20">
                  <path d="M10 2L3 7v11h4v-6h6v6h4V7l-7-5z"/>
                </svg>
              </div>
              <!-- LOGO 光环效果（仅 strong 显示） -->
              <div v-if="showAnim('rich')" class="absolute inset-0 rounded-full border-2 border-white/30 animate-ping"></div>
            </div>
          </div>

          <!-- 标题和副标题（任意启用级别都做入场动画） -->
          <div :class="['space-y-4', { 'animate-fade-in-up': showAnim('basic') }]">
            <h1 class="text-4xl font-bold tracking-wide">{{ pageTitle }}</h1>
            <p v-if="loginSubtitle" class="text-xl text-blue-100 font-light">{{ loginSubtitle }}</p>
            <p v-else class="text-xl text-blue-100 font-light">{{ welcomeText }}</p>
          </div>

          <!-- 装饰性文本（medium/strong 才做延迟入场） -->
          <div :class="['space-y-2 text-blue-100', { 'animate-fade-in-up-delay': showAnim('extra') }]">
            <p class="text-lg">安全 · 高效 · 智能</p>
            <div class="w-24 h-1 bg-white/30 mx-auto rounded-full"></div>
          </div>
        </div>
      </div>
    </div>
    
    <!-- 右侧登录表单区域 -->
    <div class="w-full lg:w-1/2 flex items-center justify-center bg-gray-50 dark:bg-gray-50 p-8">
      <div class="w-full max-w-md">
        <!-- 移动端LOGO -->
        <div class="lg:hidden flex flex-col items-center mb-8">
          <img v-if="logoUrl" :src="logoUrl" alt="logo" class="h-16 w-16 rounded-full mb-4"/>
          <h1 class="text-2xl font-bold text-gray-800 dark:text-gray-800">{{ pageTitle }}</h1>
        </div>
        
        <!-- 登录表单 -->
        <div class="bg-white dark:bg-white rounded-2xl shadow-xl p-8 border border-gray-100 dark:border-gray-100">
          <div class="text-center mb-8">
            <h2 class="text-2xl font-bold text-gray-800 dark:text-gray-800 mb-2">登录账户</h2>
            <p class="text-gray-500 dark:text-gray-500">请输入您的凭据以访问系统</p>
          </div>
          
          <el-form :model="form" :rules="rules" ref="formRef" label-width="0" class="space-y-6">
            <el-form-item prop="userName">
              <el-input 
                v-model="form.userName" 
                placeholder="用户名 / 邮箱 / 手机号" 
                clearable 
                size="large"
                class="login-input"
              >
                <template #prefix>
                  <el-icon><User /></el-icon>
                </template>
              </el-input>
            </el-form-item>
            
            <el-form-item prop="password">
              <el-input 
                v-model="form.password" 
                type="password" 
                placeholder="密码" 
                show-password 
                size="large"
                class="login-input"
              >
                <template #prefix>
                  <el-icon><Lock /></el-icon>
                </template>
              </el-input>
            </el-form-item>
            
            <el-form-item>
              <el-button 
                type="primary" 
                class="w-full login-button" 
                size="large"
                :loading="loading" 
                @click="onSubmit"
              >
                <span v-if="!loading">登 录</span>
                <span v-else>登录中...</span>
              </el-button>
            </el-form-item>
          </el-form>
          
          <!-- 插件注入的登录操作（如第三方登录） -->
          <PluginSlot name="admin-login-actions" :context="{ page: 'admin-login' }" />
          
          <!-- 页脚信息 -->
          <div class="text-center mt-6 text-sm text-gray-400 dark:text-gray-400">
            <p>{{ footerText }}</p>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, computed } from 'vue'
import { User, Lock } from '@element-plus/icons-vue'
import PluginSlot from '../../components/PluginSlot.vue'
import { ElMessage } from 'element-plus'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { useMenuStore } from '../../stores/menu'
import { useLanguageStore } from '../../stores/language'
import { ADMIN_TITLE, adminBasePath, loginFullPath } from '../../config/admin'
import { useSystemStore } from '../../stores/system'
import { login, type LoginResponse } from '../../api/auth'

const router = useRouter(); const route = useRoute();
const auth = useAuthStore()
const menuStore = useMenuStore()
const languageStore = useLanguageStore()
const system = useSystemStore()
const pageTitle = computed(() => system.siteName || ADMIN_TITLE)
const loginSubtitle = computed(() => system.loginSubtitle)
const logoUrl = computed(() => system.logoUrl)
const welcomeText = computed(() => system.welcomeText || '欢迎使用现代化管理系统')
const footerText = computed(() => system.footerText || `© 2024 ${pageTitle.value}. 保留所有权利.`)
const animationEnabled = computed(() => system.animationEnabled)

/**
 * 后台登录页背景图（来自 system.loginBackground，对应配置 Site.Login.LeftPanelBackground）。
 * 未配置时返回空串，模板会回落到默认蓝色渐变。
 */
const loginBackground = computed(() => system.loginBackground || '')

/**
 * 左侧面板内联背景样式：
 *  - 配置了背景图：图片覆盖整面板，并叠加半透明深色渐变保证文字可读
 *  - 未配置：返回 undefined，由 Tailwind class 提供默认渐变
 */
const leftPanelStyle = computed(() => {
  if (!loginBackground.value) return undefined
  return {
    backgroundImage:
      `linear-gradient(135deg, rgba(30, 58, 138, 0.65) 0%, rgba(37, 99, 235, 0.45) 100%), url("${loginBackground.value}")`,
    backgroundSize: 'cover',
    backgroundPosition: 'center',
    backgroundRepeat: 'no-repeat',
  }
})

/**
 * 当前动画级别：
 *  - none：未启用动画（animationEnabled=false）
 *  - light/medium/strong：启用动画时按 system.animationIntensity 决定
 * 模板根容器以 anim-{level} 形式输出，CSS 可据此调整动画时长。
 */
const animLevel = computed<'none' | 'light' | 'medium' | 'strong'>(() => {
  if (!system.animationEnabled) return 'none'
  const lv = system.animationIntensity
  if (lv === 'light' || lv === 'strong') return lv
  return 'medium'
})

/**
 * 按动画级别决定动画装饰元素是否显示：
 *  - basic：核心动画（任意启用级别都显示）
 *  - extra：常规动画（medium/strong 显示）
 *  - rich：进阶动画（仅 strong 显示）
 *  - 关闭动画 (none) 时全不显示
 */
const showAnim = (tier: 'basic' | 'extra' | 'rich'): boolean => {
  const lv = animLevel.value
  if (lv === 'none') return false
  if (lv === 'light') return tier === 'basic'
  if (lv === 'medium') return tier === 'basic' || tier === 'extra'
  return true // strong
}

const form = reactive({ userName: '', password: '' })
const formRef = ref()
const loading = ref(false)

const rules = {
  userName: [{ required: true, message: '请输入用户名、邮箱或手机号', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }]
}

async function onSubmit() {
  try {
    // @ts-ignore
    await formRef.value?.validate()
    loading.value = true
    const res: LoginResponse = await login(form.userName, form.password, 'WEB_ADMIN')
    auth.setToken(res.token)
    auth.setProfile({ userName: res.userName || form.userName, displayName: res.displayName, avatar: res.avatar, roles: res.roles, isSuperAdmin: res.isSuperAdmin })
    
    // 登录成功后自动清空缓存，确保加载最新数据
    try {
      menuStore.clearCache()
      languageStore.clearCache()
    } catch (e) {
      // silently ignored
    }
    
    ElMessage.success('登录成功')
    let redirectUrl = (route.query.redirect as string) || adminBasePath
    
    // 提取可能的嵌套 redirect，避免死循环回到登录页
    try {
      while (redirectUrl.includes(loginFullPath)) {
        const urlObj = new URL(redirectUrl, window.location.origin)
        redirectUrl = urlObj.searchParams.get('redirect') || adminBasePath
      }
    } catch {
      // 忽略解析错误
    }
    
    if (redirectUrl.includes(loginFullPath)) {
      redirectUrl = adminBasePath
    }

    router.replace(redirectUrl)
  } catch (e:any) {
    if (e?.message) ElMessage.error(e.message)
  } finally { loading.value = false }
}
</script>

<style scoped>
/* 浮动动画 */
@keyframes float-slow {
  0%, 100% { transform: translateY(0px) rotate(0deg); }
  50% { transform: translateY(-20px) rotate(180deg); }
}

@keyframes float-medium {
  0%, 100% { transform: translateY(0px) rotate(0deg); }
  50% { transform: translateY(-15px) rotate(90deg); }
}

@keyframes float-fast {
  0%, 100% { transform: translateY(0px) rotate(0deg); }
  50% { transform: translateY(-25px) rotate(270deg); }
}

/* LOGO旋转动画 */
@keyframes logo-rotate {
  0% { transform: rotate(0deg) scale(1); }
  25% { transform: rotate(90deg) scale(1.05); }
  50% { transform: rotate(180deg) scale(1); }
  75% { transform: rotate(270deg) scale(1.05); }
  100% { transform: rotate(360deg) scale(1); }
}

/* 缓慢旋转 */
@keyframes spin-slow {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

/* 淡入上升动画 */
@keyframes fade-in-up {
  from {
    opacity: 0;
    transform: translateY(30px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* 应用动画类 */
.animate-float-slow {
  animation: float-slow 6s ease-in-out infinite;
}

.animate-float-medium {
  animation: float-medium 4s ease-in-out infinite;
}

.animate-float-fast {
  animation: float-fast 3s ease-in-out infinite;
}

.animate-logo-rotate {
  animation: logo-rotate 8s linear infinite;
  transition: all 0.3s ease;
}

.animate-logo-rotate:hover {
  animation-duration: 2s;
  transform: scale(1.1);
}

.animate-spin-slow {
  animation: spin-slow 20s linear infinite;
}

.animate-fade-in-up {
  animation: fade-in-up 1s ease-out 0.5s both;
}

.animate-fade-in-up-delay {
  animation: fade-in-up 1s ease-out 1s both;
}

/* 登录表单样式优化 */
.login-input :deep(.el-input__wrapper) {
  border-radius: 12px;
  border: 2px solid #e5e7eb;
  transition: all 0.3s ease;
  padding: 12px 16px;
}

.login-input :deep(.el-input__wrapper:hover) {
  border-color: #3b82f6;
  box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
}

.login-input :deep(.el-input__wrapper.is-focus) {
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.2);
}

.login-button {
  border-radius: 12px;
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  border: none;
  font-weight: 600;
  letter-spacing: 0.5px;
  transition: all 0.3s ease;
  height: 48px;
}

.login-button:hover {
  background: linear-gradient(135deg, #2563eb 0%, #1d4ed8 100%);
  transform: translateY(-2px);
  box-shadow: 0 8px 25px rgba(37, 99, 235, 0.3);
}

.login-button:active {
  transform: translateY(0);
}

/* 响应式调整 */
@media (max-width: 1024px) {
  .floating-circle {
    display: none;
  }
}

/* 背景渐变增强（仅在未配置自定义背景图时生效） */
.login-left-panel:not(.has-bg-image).bg-gradient-to-br {
  background-image: linear-gradient(135deg, #2563eb 0%, #1d4ed8 25%, #1e40af 50%, #1e3a8a 75%, #1e3a8a 100%);
}

/* 自定义登录背景图：使用 inline style 注入 background-image，这里只补底色避免图片透明区域漏底 */
.login-left-panel.has-bg-image {
  background-color: #1e3a8a;
}

/* 动画强度联动：light 放慢节奏，strong 加速；medium 保持默认 */
.anim-light .animate-float-slow { animation-duration: 12s; }
.anim-light .animate-float-medium { animation-duration: 8s; }
.anim-light .animate-logo-rotate { animation-duration: 16s; }
.anim-light .animate-spin-slow { animation-duration: 30s; }

.anim-strong .animate-float-slow { animation-duration: 4s; }
.anim-strong .animate-float-medium { animation-duration: 2.5s; }
.anim-strong .animate-float-fast { animation-duration: 1.8s; }
.anim-strong .animate-logo-rotate { animation-duration: 5s; }
.anim-strong .animate-spin-slow { animation-duration: 12s; }

/* 关闭动画时强制移除残留动画（v-if 已处理装饰元素，这里兜底处理 logo 图标） */
.anim-none .animate-logo-rotate,
.anim-none .animate-fade-in-up,
.anim-none .animate-fade-in-up-delay {
  animation: none !important;
}

/* 卡片阴影效果 */
.shadow-xl {
  box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04);
}

/* 光环效果 */
@keyframes ping {
  75%, 100% {
    transform: scale(2);
    opacity: 0;
  }
}

.animate-ping {
  animation: ping 2s cubic-bezier(0, 0, 0.2, 1) infinite;
}
</style>


