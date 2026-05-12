<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="nf-page">
    <div class="nf-card">
      <!-- 大号 404 数字 -->
      <div class="nf-code" aria-hidden="true">
        <span class="nf-four">4</span>
        <span class="nf-zero">0</span>
        <span class="nf-four">4</span>
      </div>

      <!-- 图标 -->
      <div class="nf-icon" aria-hidden="true">🔍</div>

      <!-- 标题 / 说明 -->
      <h1 class="nf-title">页面不存在</h1>
      <p class="nf-desc">您访问的地址不存在或已被移除，请检查链接是否正确。</p>

      <!-- 操作按钮 -->
      <div class="nf-actions">
        <button class="nf-btn nf-btn-primary" @click="goBack">返回上一页</button>
        <router-link class="nf-btn nf-btn-ghost" to="/">回到首页</router-link>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()

function goBack() {
  if (window.history.length > 1) {
    router.back()
  } else {
    router.push('/')
  }
}
</script>

<style scoped>
/* 全屏居中容器 */
.nf-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-body, #f3f4f6);
  padding: 24px;
  box-sizing: border-box;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

/* 卡片 */
.nf-card {
  background: var(--bg-surface, #fff);
  border: 1px solid var(--border-color, #e5e7eb);
  border-radius: 20px;
  padding: 64px 48px;
  text-align: center;
  max-width: 480px;
  width: 100%;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.08);
  animation: nf-fade-in 0.4s ease;
}

/* 404 大字 */
.nf-code {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;
  line-height: 1;
  margin-bottom: 16px;
}
.nf-four,
.nf-zero {
  font-size: 96px;
  font-weight: 900;
  letter-spacing: -4px;
}
.nf-four {
  color: var(--brand-500, #3b82f6);
}
.nf-zero {
  color: var(--text-muted, #9ca3af);
}

/* 表情图标 */
.nf-icon {
  font-size: 48px;
  margin-bottom: 12px;
  animation: nf-bounce 1.8s ease-in-out infinite;
}

/* 标题 */
.nf-title {
  font-size: 22px;
  font-weight: 700;
  color: var(--text-primary, #1f2937);
  margin: 0 0 10px;
}

/* 说明文字 */
.nf-desc {
  font-size: 14px;
  color: var(--text-secondary, #6b7280);
  margin: 0 0 36px;
  line-height: 1.7;
}

/* 按钮区 */
.nf-actions {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  flex-wrap: wrap;
}

/* 按钮基础 */
.nf-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 10px 28px;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  text-decoration: none;
  border: 1.5px solid transparent;
  transition: background 0.15s ease, border-color 0.15s ease, color 0.15s ease, transform 0.1s ease;
}
.nf-btn:active {
  transform: scale(0.97);
}

/* 主色按钮 */
.nf-btn-primary {
  background: var(--brand-600, #2563eb);
  border-color: var(--brand-600, #2563eb);
  color: #fff;
}
.nf-btn-primary:hover {
  background: var(--brand-700, #1d4ed8);
  border-color: var(--brand-700, #1d4ed8);
}

/* 幽灵按钮 */
.nf-btn-ghost {
  background: transparent;
  border-color: var(--border-color, #e5e7eb);
  color: var(--text-primary, #1f2937);
}
.nf-btn-ghost:hover {
  border-color: var(--brand-500, #3b82f6);
  color: var(--brand-600, #2563eb);
}

/* 淡入动画 */
@keyframes nf-fade-in {
  from { opacity: 0; transform: translateY(20px); }
  to   { opacity: 1; transform: translateY(0); }
}

/* 图标弹跳动画 */
@keyframes nf-bounce {
  0%, 100% { transform: translateY(0); }
  50%       { transform: translateY(-8px); }
}
</style>
