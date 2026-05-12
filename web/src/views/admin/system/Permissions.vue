<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="page-container">
    <div class="page-header">
      <div class="page-title">
        <h1>权限管理</h1>
        <p>管理系统权限和访问控制</p>
      </div>
    </div>
    
    <el-card class="content-card">
      <div class="coming-soon">
        <el-icon size="64" color="#3b82f6">
          <Key />
        </el-icon>
        <h2>权限管理</h2>
        <p>功能开发中，敬请期待...</p>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { Key } from '@element-plus/icons-vue'
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

.coming-soon {
  text-align: center;
  padding: 60px 20px;
}

.coming-soon h2 {
  font-size: 20px;
  font-weight: 600;
  color: #1f2937;
  margin: 16px 0 8px 0;
}

.coming-soon p {
  font-size: 14px;
  color: #6b7280;
  margin: 0;
}

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

.dark .coming-soon h2 {
  color: #f9fafb;
}

.dark .coming-soon p {
  color: #9ca3af;
}
</style>