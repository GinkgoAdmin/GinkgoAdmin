<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="page-container">
    <div class="page-header">
      <div class="page-title">
        <h1>权限测试页面</h1>
        <p>测试权限指令和按钮权限控制</p>
      </div>
    </div>
    
    <el-card class="content-card">
      <div class="test-section">
        <h3>按钮权限测试</h3>
        <div class="button-group">
          <el-button type="primary" v-permission="'/admin/system/users:add'">
            用户新增按钮 (需要权限)
          </el-button>
          <el-button type="success" v-permission="'/admin/system/users:edit'">
            用户编辑按钮 (需要权限)
          </el-button>
          <el-button type="danger" v-permission="'/admin/system/users:delete'">
            用户删除按钮 (需要权限)
          </el-button>
          <el-button type="info">
            无权限要求按钮 (总是显示)
          </el-button>
        </div>
      </div>
      
      <div class="test-section">
        <h3>当前用户信息</h3>
        <div class="info-grid">
          <div class="info-item">
            <span class="label">用户名:</span>
            <span class="value">{{ auth.userName || '未登录' }}</span>
          </div>
          <div class="info-item">
            <span class="label">认证状态:</span>
            <span class="value">{{ auth.isAuthenticated ? '已登录' : '未登录' }}</span>
          </div>
          <div class="info-item">
            <span class="label">主题:</span>
            <span class="value">{{ auth.theme }}</span>
          </div>
        </div>
      </div>
      
      <div class="test-section">
        <h3>菜单信息</h3>
        <div class="info-grid">
          <div class="info-item">
            <span class="label">菜单加载状态:</span>
            <span class="value">{{ menuStore.loaded ? '已加载' : '未加载' }}</span>
          </div>
          <div class="info-item">
            <span class="label">菜单数量:</span>
            <span class="value">{{ menuStore.menus.length }}</span>
          </div>
          <div class="info-item">
            <span class="label">按钮权限数量:</span>
            <span class="value">{{ menuStore.buttonCodes.length }}</span>
          </div>
        </div>
      </div>
      
      <div class="test-section">
        <h3>按钮权限列表</h3>
        <div class="permission-list">
          <el-tag v-for="code in menuStore.buttonCodes" :key="code" class="permission-tag">
            {{ code }}
          </el-tag>
          <div v-if="menuStore.buttonCodes.length === 0" class="empty-state">
            暂无按钮权限
          </div>
        </div>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { useAuthStore } from '../../stores/auth'
import { useMenuStore } from '../../stores/menu'

const auth = useAuthStore()
const menuStore = useMenuStore()
</script>

<style scoped>
.page-container {
  padding: 0;
}

.page-header {
  margin-bottom: 24px;
}

.page-title h1 {
  font-size: 24px;
  font-weight: 600;
  color: #1f2937;
  margin: 0 0 4px 0;
}

.page-title p {
  font-size: 14px;
  color: #6b7280;
  margin: 0;
}

.content-card {
  border-radius: 12px;
  border: 1px solid #e5e7eb;
}

.test-section {
  margin-bottom: 32px;
}

.test-section:last-child {
  margin-bottom: 0;
}

.test-section h3 {
  font-size: 18px;
  font-weight: 600;
  color: #1f2937;
  margin: 0 0 16px 0;
}

.button-group {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
}

.info-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px;
  background: #f9fafb;
  border-radius: 8px;
}

.label {
  font-weight: 500;
  color: #6b7280;
}

.value {
  font-weight: 600;
  color: #1f2937;
}

.permission-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.permission-tag {
  margin: 0;
}

.empty-state {
  color: #9ca3af;
  font-style: italic;
}

/* 深色模式 */
.dark .page-title h1 {
  color: #f9fafb;
}

.dark .page-title p {
  color: #9ca3af;
}

.dark .content-card {
  background: #1f2937;
  border-color: #374151;
}

.dark .test-section h3 {
  color: #f9fafb;
}

.dark .info-item {
  background: #374151;
}

.dark .label {
  color: #9ca3af;
}

.dark .value {
  color: #f9fafb;
}

.dark .empty-state {
  color: #6b7280;
}
</style>