<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="index-home">
    <!-- 英雄区域 -->
    <section class="hero-section">
      <div class="hero-container">
        <div class="hero-badge">🚀 业务插件交付平台</div>
        <h1 class="hero-title">
          <span class="gradient-text">GinkgoAdmin</span>
          <br />现代化业务交付平台
        </h1>
        <p class="hero-subtitle">
          基于插件化架构，快速交付多租户 SaaS、企业管理系统与行业解决方案，
          开箱即用的权限、工作流、移动端，让您专注于核心业务落地。
        </p>
        <div class="hero-actions">
          <router-link :to="`/${lang}/admin`" class="btn btn-primary">
            <i class="bi bi-box-arrow-in-right"></i> 进入后台
          </router-link>
          <a href="https://github.com" target="_blank" class="btn btn-secondary">
            <i class="bi bi-github"></i> 查看源码
          </a>
        </div>
      </div>
    </section>

    <!-- 特色功能 -->
    <section class="features-section">
      <div class="container">
        <h2 class="section-title">核心交付能力</h2>
        <div class="features-grid">
          <div v-for="f in features" :key="f.title" class="feature-card">
            <div class="feature-icon">{{ f.icon }}</div>
            <h3 class="feature-title">{{ f.title }}</h3>
            <p class="feature-desc">{{ f.desc }}</p>
          </div>
        </div>
      </div>
    </section>

    <!-- 技术栈 -->
    <section class="tech-section">
      <div class="container">
        <h2 class="section-title">技术栈</h2>
        <div class="tech-grid">
          <div v-for="t in techStack" :key="t.name" class="tech-card">
            <span class="tech-icon">{{ t.icon }}</span>
            <span class="tech-name">{{ t.name }}</span>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const lang = computed(() => (route.params.lang as string) || 'zh')

const features = [
  { icon: '�', title: '快速交付', desc: '开箱即用的业务模块与插件市场，大幅缩短从需求到上线的交付周期。' },
  { icon: '🧩', title: '插件生态', desc: '灵活的插件架构，支持热插拔，业务能力随需组合，按需安装扩展。' },
  { icon: '🔐', title: '权限管控', desc: '完善的 RBAC 权限体系，支持数据权限、菜单权限和按钮级权限控制。' },
  { icon: '🌐', title: '多租户 SaaS', desc: '内置多租户隔离机制，一套部署支撑多个客户，轻松实现 SaaS 化运营。' },
  { icon: '📱', title: '多端协同', desc: '同步覆盖 Web 管理后台、移动端（UniApp）与 WPF 桌面客户端，业务无缝衔接。' },
  { icon: '⚡', title: '高性能底座', desc: '基于 ASP.NET Core + SqlSugar 构建，稳定支撑高并发业务场景。' },
]

const techStack = [
  { icon: '💎', name: 'ASP.NET Core' },
  { icon: '🟢', name: 'Vue 3' },
  { icon: '🎯', name: 'Element Plus' },
  { icon: '🗃️', name: 'SqlSugar' },
  { icon: '🐬', name: 'MySQL' },
  { icon: '📦', name: 'Vite' },
]
</script>

<style scoped>
/* 英雄区域 */
.hero-section {
  background: linear-gradient(135deg, #0f172a 0%, #1e293b 50%, #0f172a 100%);
  padding: 6rem 1.5rem;
  text-align: center;
  position: relative;
  overflow: hidden;
}

.hero-section::before {
  content: '';
  position: absolute;
  inset: 0;
  background: radial-gradient(circle at 50% 0%, rgba(59,130,246,0.15) 0%, transparent 60%);
}

.hero-container {
  max-width: 800px;
  margin: 0 auto;
  position: relative;
  z-index: 1;
}

.hero-badge {
  display: inline-block;
  padding: 6px 16px;
  background: rgba(59,130,246,0.15);
  border: 1px solid rgba(59,130,246,0.3);
  border-radius: 999px;
  color: #60a5fa;
  font-size: 0.875rem;
  font-weight: 600;
  margin-bottom: 1.5rem;
}

.hero-title {
  font-size: 3rem;
  font-weight: 800;
  color: #f1f5f9;
  line-height: 1.2;
  margin: 0 0 1.5rem 0;
}

.gradient-text {
  background: linear-gradient(135deg, #60a5fa, #a78bfa, #f472b6);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.hero-subtitle {
  font-size: 1.125rem;
  color: #94a3b8;
  line-height: 1.8;
  margin: 0 0 2.5rem 0;
  max-width: 600px;
  margin-left: auto;
  margin-right: auto;
}

.hero-actions {
  display: flex;
  gap: 1rem;
  justify-content: center;
  flex-wrap: wrap;
}

.btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 12px 28px;
  border-radius: 10px;
  text-decoration: none;
  font-weight: 600;
  font-size: 1rem;
  transition: all 0.2s;
}

.btn-primary {
  background: linear-gradient(135deg, #3b82f6, #8b5cf6);
  color: #fff;
}

.btn-primary:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 24px rgba(59,130,246,0.4);
}

.btn-secondary {
  background: rgba(255,255,255,0.1);
  color: #e2e8f0;
  border: 1px solid rgba(255,255,255,0.2);
}

.btn-secondary:hover {
  background: rgba(255,255,255,0.15);
  transform: translateY(-2px);
}

/* 特性区域 */
.features-section {
  padding: 5rem 0;
  background: #f9fafb;
}

.container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 0 1.5rem;
}

.section-title {
  text-align: center;
  font-size: 2rem;
  font-weight: 700;
  color: #1f2937;
  margin: 0 0 3rem 0;
}

.features-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 1.5rem;
}

.feature-card {
  background: #fff;
  padding: 2rem;
  border-radius: 16px;
  border: 1px solid #e5e7eb;
  transition: all 0.3s;
}

.feature-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 12px 32px rgba(0,0,0,0.08);
  border-color: #3b82f6;
}

.feature-icon {
  font-size: 2rem;
  margin-bottom: 1rem;
}

.feature-title {
  font-size: 1.125rem;
  font-weight: 700;
  color: #1f2937;
  margin: 0 0 0.75rem 0;
}

.feature-desc {
  color: #6b7280;
  line-height: 1.6;
  margin: 0;
  font-size: 0.9375rem;
}

/* 技术栈 */
.tech-section {
  padding: 4rem 0 5rem 0;
  background: #fff;
}

.tech-grid {
  display: flex;
  justify-content: center;
  flex-wrap: wrap;
  gap: 1.5rem;
}

.tech-card {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 24px;
  background: #f1f5f9;
  border-radius: 12px;
  font-weight: 600;
  color: #334155;
  transition: all 0.2s;
}

.tech-card:hover {
  background: #e0e7ff;
  color: #3b82f6;
}

.tech-icon { font-size: 1.25rem; }
.tech-name { font-size: 0.9375rem; }

/* 响应式 */
@media (max-width: 768px) {
  .hero-title { font-size: 2rem; }
  .hero-subtitle { font-size: 1rem; }
  .features-grid { grid-template-columns: 1fr; }
}
</style>
