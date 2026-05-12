<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <footer class="index-footer">
    <div class="footer-container">
      <div class="footer-brand">
        <span class="footer-name">{{ siteName || 'GinkgoAdmin' }}</span>
        <span class="footer-desc">高效、可扩展的现代化应用开发框架</span>
      </div>
      <div class="footer-bottom">
        <span class="copyright">{{ footerText || `© ${new Date().getFullYear()} GinkgoAdmin. All rights reserved.` }}</span>
        <span v-if="icpNo" class="icp-info">
          <a href="https://beian.miit.gov.cn/" target="_blank" rel="noopener">{{ icpNo }}</a>
        </span>
        <span v-if="policeIcpNo" class="police-info">
          <img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAA4AAAAOCAYAAAAfSC3RAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAABaSURBVDhPY/hPBWBioBKgqsaGhob/DPgATBxdI7oYukYQH6cJIADSCNKMS4wkjSD/4RJjwOUUfE7BZwLRGpH9hwuQrBEXIEsjLkC0RlwAW3jgAoxUTykMAACeyzXHnsCB1wAAAABJRU5ErkJggg==" alt="" class="police-icon" />
          <a href="http://www.beian.gov.cn/" target="_blank" rel="noopener">{{ policeIcpNo }}</a>
        </span>
        <span v-if="businessLicense" class="biz-info">{{ businessLicense }}</span>
      </div>
    </div>
  </footer>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useSystemStore } from '../../../../stores/system'

// 站点信息统一来源 system store；main.ts 启动时已加载 loadPublicConfig
const system = useSystemStore()
const siteName = computed(() => system.siteName || 'GinkgoAdmin')
const footerText = computed(() => system.footerText || '')
const icpNo = computed(() => system.icpNumber || '')
const policeIcpNo = computed(() => system.policeNumber || '')
const businessLicense = computed(() => system.businessLicense || '')
</script>

<style scoped>
.index-footer {
  background: #1f2937;
  color: #9ca3af;
  padding: 2rem 0;
}

.footer-container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 0 1.5rem;
  text-align: center;
}

.footer-brand {
  margin-bottom: 1rem;
}

.footer-name {
  font-size: 1.125rem;
  font-weight: 700;
  color: #f9fafb;
  display: block;
  margin-bottom: 4px;
}

.footer-desc {
  font-size: 0.875rem;
  color: #9ca3af;
}

.footer-bottom {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 16px;
  flex-wrap: wrap;
  font-size: 0.8125rem;
  color: #6b7280;
  border-top: 1px solid #374151;
  padding-top: 1rem;
}

.footer-bottom a {
  color: #9ca3af;
  text-decoration: none;
  transition: color 0.2s;
}

.footer-bottom a:hover { color: #60a5fa; }

.police-icon {
  width: 14px;
  height: 14px;
  vertical-align: middle;
  margin-right: 4px;
}

.police-info, .icp-info {
  display: inline-flex;
  align-items: center;
}
</style>