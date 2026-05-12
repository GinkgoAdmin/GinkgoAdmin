<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div v-if="shouldBlock" class="mtn-gate">
    <div class="mtn-card">
      <div class="mtn-icon">
        <i class="bi bi-cone-striped"></i>
      </div>
      <h1 class="mtn-title">{{ siteName }} 正在维护中</h1>
      <p class="mtn-desc">
        系统已开启维护模式，普通用户暂时无法访问。<br />
        给您带来不便，敬请谅解，请稍后再来。
      </p>
      <div class="mtn-meta" v-if="footerText || icp || police">
        <span v-if="footerText">{{ footerText }}</span>
        <span v-if="icp">{{ icp }}</span>
        <span v-if="police">{{ police }}</span>
      </div>
      <div class="mtn-actions">
        <a href="/admin/login" class="mtn-admin-btn">
          <i class="bi bi-shield-lock"></i>
          管理员登录
        </a>
      </div>
    </div>
  </div>
  <slot v-else />
</template>

<script setup lang="ts">
/**
 * 维护模式门禁组件。
 *
 * 使用场景：包裹在 web 前端的 Layout 内（IndexLayout / GinkgoWebLayout），
 * 当后台开启"维护模式"时，普通访客与已登录的 web 用户都将看到维护页；
 * 仅本地有 admin token 的管理员可以继续浏览（便于排查 / 关闭维护）。
 *
 * 数据来源：
 *  - system.maintenanceMode：来自 /api/v1/settings 公开配置
 *  - auth.token：后台管理员 token（admin login 后写入）
 */
import { computed } from 'vue'
import { useSystemStore } from '../../../stores/system'
import { useAuthStore } from '../../../stores/auth'

const system = useSystemStore()
const auth = useAuthStore()

const siteName = computed(() => system.siteName || 'GinkgoAdmin')
const footerText = computed(() => system.footerText || '')
const icp = computed(() => system.icpNumber || '')
const police = computed(() => system.policeNumber || '')

// 仅"开启维护模式 & 当前不是 admin"时阻止
const shouldBlock = computed(() => system.maintenanceMode && !auth.token)
</script>

<style scoped>
.mtn-gate {
  position: fixed;
  inset: 0;
  z-index: 9999;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
  background: linear-gradient(135deg, #f8fafc 0%, #eef2ff 50%, #f1f5f9 100%);
  color: #1e293b;
  overflow-y: auto;
}

.mtn-card {
  width: 100%;
  max-width: 560px;
  background: #ffffff;
  border-radius: 18px;
  padding: 56px 48px 40px;
  box-shadow: 0 18px 48px rgba(15, 23, 42, 0.08), 0 2px 8px rgba(15, 23, 42, 0.04);
  text-align: center;
  border: 1px solid #e2e8f0;
}

.mtn-icon {
  width: 84px;
  height: 84px;
  margin: 0 auto 24px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #fbbf24, #f59e0b);
  color: #fff;
  font-size: 40px;
  box-shadow: 0 8px 24px rgba(245, 158, 11, 0.32);
}

.mtn-title {
  font-size: 28px;
  font-weight: 700;
  margin: 0 0 16px;
  color: #0f172a;
  letter-spacing: 0.5px;
}

.mtn-desc {
  font-size: 15px;
  line-height: 1.8;
  color: #475569;
  margin: 0 0 28px;
}

.mtn-meta {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 8px 16px;
  font-size: 12px;
  color: #94a3b8;
  margin: 0 0 28px;
  padding: 12px 0;
  border-top: 1px dashed #e2e8f0;
  border-bottom: 1px dashed #e2e8f0;
}

.mtn-actions {
  display: flex;
  justify-content: center;
}

.mtn-admin-btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 24px;
  border-radius: 10px;
  background: linear-gradient(135deg, #3b82f6, #6366f1);
  color: #fff;
  text-decoration: none;
  font-size: 14px;
  font-weight: 600;
  box-shadow: 0 4px 12px rgba(59, 130, 246, 0.25);
  transition: transform 0.2s, box-shadow 0.2s;
}

.mtn-admin-btn:hover {
  transform: translateY(-1px);
  box-shadow: 0 6px 18px rgba(59, 130, 246, 0.35);
}

@media (max-width: 480px) {
  .mtn-card {
    padding: 40px 28px 32px;
  }
  .mtn-title {
    font-size: 22px;
  }
}
</style>
