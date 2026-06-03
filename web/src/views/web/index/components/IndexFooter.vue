<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <footer class="index-footer">
    <div class="footer-container">
      <!-- 第一行：站点名 + 页脚文案 -->
      <div class="footer-text-row">
        <span class="footer-name">{{ siteName || 'GinkgoAdmin' }}</span>
        <span v-if="footerLine" class="footer-divider">·</span>
        <span v-if="footerLine" class="footer-copy">{{ footerLine }}</span>
      </div>

      <!-- 第二行：ICP 备案与公安备案同一行 -->
      <div v-if="hasBeianRow" class="footer-beian-row">
        <span v-if="icpNo" class="beian-item">
          <a href="https://beian.miit.gov.cn/" target="_blank" rel="noopener noreferrer">{{ icpNo }}</a>
        </span>
        <span v-if="icpNo && policeIcpNo" class="beian-sep">|</span>
        <span v-if="policeIcpNo" class="beian-item police-item">
          <img
            src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAA4AAAAOCAYAAAAfSC3RAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAABaSURBVDhPY/hPBWBioBKgqsaGhob/DPgATBxdI7oYukYQH6cJIADSCNKMS4wkjSD/4RJjwOUUfE7BZwLRGpH9hwuQrBEXIEsjLkC0RlwAW3jgAoxUTykMAACeyzXHnsCB1wAAAABJRU5ErkJggg=="
            alt=""
            class="police-icon"
          />
          <a href="http://www.beian.gov.cn/" target="_blank" rel="noopener noreferrer">{{ policeIcpNo }}</a>
        </span>
        <span v-if="businessLicense && (icpNo || policeIcpNo)" class="beian-sep">|</span>
        <span v-if="businessLicense" class="beian-item biz-item">{{ businessLicense }}</span>
      </div>
    </div>
  </footer>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useSystemStore } from '../../../../stores/system'

const system = useSystemStore()
const siteName = computed(() => system.siteName || 'GinkgoAdmin')
const footerText = computed(() => system.footerText || '')
const icpNo = computed(() => system.icpNumber || '')
const policeIcpNo = computed(() => system.policeNumber || '')
const businessLicense = computed(() => system.businessLicense || '')

/** 页脚主文案：优先后台配置，否则默认版权行 */
const footerLine = computed(() => {
  if (footerText.value.trim()) return footerText.value.trim()
  return `© ${new Date().getFullYear()} ${siteName.value}. All rights reserved.`
})

const hasBeianRow = computed(
  () => !!(icpNo.value || policeIcpNo.value || businessLicense.value),
)
</script>

<style scoped>
.index-footer {
  position: relative;
  flex-shrink: 0;
  height: 72px;
  box-sizing: border-box;
  /* 深靛蓝渐变，作为页面底部的稳定锚点 */
  background: linear-gradient(180deg, #1e2340 0%, #141527 100%);
  color: #94a3b8;
}

/* 顶部品牌流光带，与页头底部呼应 */
.index-footer::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 2px;
  background: linear-gradient(
    90deg,
    transparent 0%,
    rgba(59, 130, 246, 0.15) 18%,
    #3b82f6 38%,
    #8b5cf6 50%,
    #ec4899 62%,
    rgba(236, 72, 153, 0.15) 82%,
    transparent 100%
  );
  background-size: 200% 100%;
  animation: footer-flow 5s linear infinite;
}

@keyframes footer-flow {
  0% { background-position: -200% 0; }
  100% { background-position: 200% 0; }
}

@media (prefers-reduced-motion: reduce) {
  .index-footer::before { animation: none; }
}

.footer-container {
  max-width: 1200px;
  height: 100%;
  margin: 0 auto;
  padding: 0 1.5rem;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 6px;
  text-align: center;
}

/* 第一行：页脚文本 */
.footer-text-row {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-wrap: wrap;
  gap: 6px 8px;
  line-height: 1.4;
  max-width: 100%;
}

.footer-name {
  font-size: 0.875rem;
  font-weight: 700;
  /* 品牌渐变文字，与页头 brand-name 呼应 */
  background: linear-gradient(135deg, #60a5fa, #a78bfa);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.footer-divider {
  color: #475569;
  user-select: none;
}

.footer-copy {
  font-size: 0.8125rem;
  color: #94a3b8;
}

/* 第二行：备案信息同一行 */
.footer-beian-row {
  display: flex;
  align-items: center;
  justify-content: center;
  flex-wrap: wrap;
  gap: 4px 10px;
  font-size: 0.75rem;
  color: #64748b;
  line-height: 1.3;
}

.beian-item a {
  color: #94a3b8;
  text-decoration: none;
  transition: color 0.2s;
}

.beian-item a:hover {
  color: #93c5fd;
}

.beian-sep {
  color: #475569;
  user-select: none;
}

.police-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.police-icon {
  width: 14px;
  height: 14px;
  flex-shrink: 0;
}

.biz-item {
  color: #64748b;
}

@media (max-width: 480px) {
  .index-footer {
    height: auto;
    min-height: 72px;
    padding: 10px 0;
  }

  .footer-text-row,
  .footer-beian-row {
    flex-direction: column;
    gap: 4px;
  }

  .footer-divider,
  .beian-sep {
    display: none;
  }
}
</style>
