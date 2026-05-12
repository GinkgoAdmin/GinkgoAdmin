<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <header class="index-header">
    <div class="header-container">
      <!-- 品牌 LOGO -->
      <router-link :to="`/${currentLang}/web`" class="brand">
        <img v-if="logoUrl" :src="logoUrl" alt="Logo" class="brand-logo" />
        <span class="brand-name">{{ siteName || 'GinkgoAdmin' }}</span>
      </router-link>

      <!-- 右侧操作区 -->
      <div class="header-actions">
        <!-- 多语言切换 -->
        <el-dropdown v-if="languages.length > 1" trigger="click" @command="switchLang">
          <span class="lang-trigger">
            <i class="bi bi-globe2"></i>
            <span class="lang-label">{{ currentLangLabel }}</span>
          </span>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item
                v-for="lang in languages"
                :key="lang.code"
                :command="lang.urlCode"
                :class="{ 'is-active': lang.urlCode === currentLang }"
              >
                {{ lang.flag }} {{ lang.label }}
              </el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>

        <!-- 登录 / 注册 -->
        <template v-if="!isLoggedIn">
          <router-link :to="`/${currentLang}/web/login`" class="btn-login">登录</router-link>
          <router-link :to="`/${currentLang}/web/register`" class="btn-register">注册</router-link>
        </template>
        <template v-else>
          <router-link :to="`/${currentLang}/web/user`" class="btn-login">
            <i class="bi bi-person-circle"></i> 我的
          </router-link>
        </template>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../../../../stores/auth'
import { useWebAuthStore } from '../../../../stores/webAuth'
import { useSystemStore } from '../../../../stores/system'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const webAuth = useWebAuthStore()
const system = useSystemStore()
if (!webAuth.token) webAuth.initFromStorage()

// 同时检查后台 admin token 和前台 web_user_token
const isLoggedIn = computed(() => !!auth.token || webAuth.isAuthenticated)
// 站点名 / Logo 来自全局 system store（main.ts 启动时已通过 loadPublicConfig 加载）
const siteName = computed(() => system.siteName || 'GinkgoAdmin')
const logoUrl = computed(() => system.logoUrl || '')

// 多语言
const currentLang = computed(() => (route.params.lang as string) || 'zh')
const languages = computed(() => {
  try {
    const stored = localStorage.getItem('ginkgo_lang_config')
    if (stored) {
      const cfg = JSON.parse(stored)
      return cfg.langs || [{ code: 'zh-CN', urlCode: 'zh', label: '简体中文', flag: '🇨🇳' }]
    }
  } catch {}
  return [{ code: 'zh-CN', urlCode: 'zh', label: '简体中文', flag: '🇨🇳' }]
})
const currentLangLabel = computed(() => {
  const lang = languages.value.find((l: any) => l.urlCode === currentLang.value)
  return lang ? lang.label : '简体中文'
})

function switchLang(urlCode: string) {
  const currentPath = route.path
  const newPath = currentPath.replace(/^\/[^/]+/, `/${urlCode}`)
  router.push(newPath)
}
</script>

<style scoped>
.index-header {
  position: sticky;
  top: 0;
  z-index: 100;
  background: rgba(255, 255, 255, 0.95);
  backdrop-filter: blur(12px);
  border-bottom: 1px solid #e5e7eb;
  height: 64px;
}

.header-container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 0 1.5rem;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.brand {
  display: flex;
  align-items: center;
  gap: 10px;
  text-decoration: none;
}

.brand-logo {
  height: 32px;
  width: auto;
}

.brand-name {
  font-size: 1.25rem;
  font-weight: 700;
  color: #1f2937;
  background: linear-gradient(135deg, #3b82f6, #8b5cf6);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 16px;
}

.lang-trigger {
  display: flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  color: #6b7280;
  font-size: 14px;
  transition: color 0.2s;
}

.lang-trigger:hover { color: #3b82f6; }

.lang-label { font-size: 13px; }

.btn-login {
  color: #374151;
  text-decoration: none;
  font-size: 14px;
  font-weight: 500;
  padding: 6px 16px;
  border-radius: 8px;
  transition: all 0.2s;
}

.btn-login:hover { background: #f3f4f6; color: #3b82f6; }

.btn-register {
  background: linear-gradient(135deg, #3b82f6, #8b5cf6);
  color: #fff;
  text-decoration: none;
  font-size: 14px;
  font-weight: 600;
  padding: 8px 20px;
  border-radius: 8px;
  transition: all 0.2s;
}

.btn-register:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(59, 130, 246, 0.4);
}
</style>