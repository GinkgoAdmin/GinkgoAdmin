<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <aside class="sidebar">
    <div class="user-profile-card">
      <div class="user-avatar-large">
        <img v-if="(userInfo?.avatar || '').trim()" :src="(userInfo!.avatar as string)" :alt="userInfo!.name || t('role_default')" />
        <div v-else class="avatar-placeholder" style="font-weight:700; font-size:28px; color: rgba(255,255,255,.95);">
          {{ (userInfo?.name || userInfo?.userName || 'U').charAt(0) }}
        </div>
      </div>
      <div class="user-info">
        <h3 class="user-name">{{ userInfo?.name || t('role_default') }}</h3>
        <p class="user-role">{{ getUserRole() }}</p>
      </div>
    </div>

    <nav class="sidebar-nav">
      <router-link to="/web/user" class="nav-item" exact>
        <div class="nav-icon-wrapper nav-icon-home">
          <el-icon><House /></el-icon>
        </div>
        <span>{{ t('sidebar_center') }}</span>
      </router-link>
      <router-link to="/web/user/profile" class="nav-item">
        <div class="nav-icon-wrapper nav-icon-profile">
          <el-icon><User /></el-icon>
        </div>
        <span>{{ t('sidebar_profile') }}</span>
      </router-link>
      <router-link to="/web/user/notifications" class="nav-item">
        <div class="nav-icon-wrapper nav-icon-notification">
          <el-icon><Bell /></el-icon>
        </div>
        <span>{{ t('sidebar_notifications') }}</span>
      </router-link>
      <router-link to="/web/user/logs" class="nav-item">
        <div class="nav-icon-wrapper nav-icon-logs">
          <el-icon><Document /></el-icon>
        </div>
        <span>{{ t('sidebar_logs') }}</span>
      </router-link>
      <!-- 插件注入的菜单项（通用 hook） -->
      <router-link v-for="menu in pluginMenus" :key="menu.id" :to="menu.path" class="nav-item">
        <div class="nav-icon-wrapper nav-icon-plugin">
          <el-icon><component :is="resolveIcon(menu.icon)" /></el-icon>
        </div>
        <span>{{ menu.title }}</span>
      </router-link>
      <a href="#" class="nav-item logout-item" @click.prevent="emit('logout')">
        <div class="nav-icon-wrapper nav-icon-logout">
          <el-icon><SwitchButton /></el-icon>
        </div>
        <span>{{ t('sidebar_logout') }}</span>
      </a>
    </nav>
  </aside>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { Bell, Document, House, Menu, SwitchButton, User } from '@element-plus/icons-vue'
import * as ElIcons from '@element-plus/icons-vue'
import { getPluginManager } from '@/plugins'
import { t } from '@/utils/lang'

interface WebUserInfo { userName?: string; name?: string; avatar?: string }
const props = defineProps<{ userInfo?: WebUserInfo | null }>()
const emit = defineEmits<{ (e:'logout'): void }>()

/** 根据图标名称字符串解析为实际组件 */
function resolveIcon(name?: string) {
  if (!name) return Menu
  return (ElIcons as any)[name] || Menu
}

const pluginMenus = computed(() => {
  try {
    const pm = getPluginManager()
    // 引用 loading ref 作为响应式依赖，确保插件加载完成后重新计算
    const _loading = pm.getLoadingRef().value
    if (_loading) return []
    const menus = pm.executeHook('portal:user-menu', [])
    return Array.isArray(menus) ? menus : []
  } catch {
    return []
  }
})
// 根据用户名返回多语言角色名
const getUserRole = () => {
  const uname = props.userInfo?.userName
  if (!uname) return t('role_visitor')
  switch (uname) {
    case 'admin': return t('role_admin')
    case 'user': return t('role_user')
    case 'demo': return t('role_demo')
    default: return t('role_default')
  }
}
</script>

<style scoped>
.sidebar {
  background: #fff;
  border-radius: 16px;
  box-shadow: 0 4px 20px rgba(0,0,0,.06);
  overflow: hidden;
  position: sticky;
  top: 6rem;
}

.user-profile-card {
  padding: 1.75rem 1.5rem;
  text-align: center;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: #fff;
  position: relative;
  overflow: hidden;
}

/* 背景装饰图案 */
.user-profile-card::before {
  content: '';
  position: absolute;
  top: -30%;
  right: -20%;
  width: 160px;
  height: 160px;
  border-radius: 50%;
  background: rgba(255,255,255,.08);
}
.user-profile-card::after {
  content: '';
  position: absolute;
  bottom: -25%;
  left: -15%;
  width: 120px;
  height: 120px;
  border-radius: 50%;
  background: rgba(255,255,255,.06);
}

.user-avatar-large {
  width: 76px;
  height: 76px;
  border-radius: 50%;
  margin: 0 auto .75rem;
  overflow: hidden;
  background: rgba(255,255,255,.2);
  display: flex;
  align-items: center;
  justify-content: center;
  border: 3px solid rgba(255,255,255,.35);
  position: relative;
  z-index: 1;
  transition: transform .3s ease, box-shadow .3s ease;
}

.user-avatar-large:hover {
  transform: scale(1.05);
  box-shadow: 0 6px 20px rgba(0,0,0,.15);
}

.user-avatar-large img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.avatar-placeholder {
  color: rgba(255,255,255,.8);
}

.user-info {
  position: relative;
  z-index: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: .35rem;
  max-width: 100%;
  overflow: hidden;
}

.user-name {
  font-size: 1.1rem;
  font-weight: 600;
  margin: 0;
  letter-spacing: .02em;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  line-height: 1.4;
}

.user-role {
  opacity: .85;
  margin: 0;
  font-size: .75rem;
  background: rgba(255,255,255,.15);
  display: inline-flex;
  align-items: center;
  padding: .2rem .7rem;
  border-radius: 10px;
  backdrop-filter: blur(4px);
  white-space: nowrap;
  line-height: 1.2;
  flex-shrink: 0;
}

.sidebar-nav {
  padding: .75rem 0;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: .75rem;
  padding: .7rem 1.25rem;
  color: #64748b;
  text-decoration: none;
  transition: all .25s ease;
  border-left: 3px solid transparent;
  font-size: .925rem;
  font-weight: 500;
}

.nav-item:hover {
  background: #f1f5f9;
  color: #475569;
}

.nav-item.router-link-active {
  background: linear-gradient(135deg, #eff6ff 0%, #f0f9ff 100%);
  color: #3b82f6;
  border-left-color: #3b82f6;
  font-weight: 600;
}

/* 图标包裹 - 通用 */
.nav-icon-wrapper {
  width: 32px;
  height: 32px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1rem;
  transition: all .25s ease;
  flex-shrink: 0;
}

/* 各菜单项图标的差异化配色 */
.nav-icon-home {
  background: #eff6ff;
  color: #3b82f6;
}
.nav-item.router-link-active .nav-icon-home,
.nav-item:hover .nav-icon-home {
  background: #3b82f6;
  color: #fff;
}

.nav-icon-profile {
  background: #f0fdf4;
  color: #22c55e;
}
.nav-item.router-link-active .nav-icon-profile,
.nav-item:hover .nav-icon-profile {
  background: #22c55e;
  color: #fff;
}

.nav-icon-notification {
  background: #fef3c7;
  color: #f59e0b;
}
.nav-item.router-link-active .nav-icon-notification,
.nav-item:hover .nav-icon-notification {
  background: #f59e0b;
  color: #fff;
}

.nav-icon-logs {
  background: #f0f9ff;
  color: #06b6d4;
}
.nav-item.router-link-active .nav-icon-logs,
.nav-item:hover .nav-icon-logs {
  background: #06b6d4;
  color: #fff;
}

.nav-icon-plugin {
  background: #faf5ff;
  color: #a855f7;
}
.nav-item.router-link-active .nav-icon-plugin,
.nav-item:hover .nav-icon-plugin {
  background: #a855f7;
  color: #fff;
}

.nav-icon-logout {
  background: #fef2f2;
  color: #ef4444;
}

.logout-item {
  color: #ef4444;
  border-top: 1px solid #f1f5f9;
  margin-top: .25rem;
}

.logout-item:hover {
  background: #fef2f2;
  color: #dc2626;
}
.logout-item:hover .nav-icon-logout {
  background: #ef4444;
  color: #fff;
}
</style>
