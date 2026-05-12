<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="page-container">
    <div class="page-header">
      <div class="page-title">
        <h1>个人资料</h1>
        <p>管理您的个人信息和账户设置</p>
      </div>
    </div>

    <el-card class="profile-card">
      <template #header>
        <div class="card-header">
          <h3>基本信息</h3>
        </div>
      </template>
      
      <div class="profile-content">
        <p>用户名: {{ userInfo.userName }}</p>
        <p>显示名称: {{ userInfo.displayName }}</p>
        <p>邮箱: {{ userInfo.email }}</p>
        <p>状态: {{ userInfo.enabled ? '正常' : '禁用' }}</p>
        
        <el-button type="primary" @click="handleTest">测试按钮</el-button>
      </div>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { ElMessage } from 'element-plus'

const userInfo = ref({
  userName: 'demo',
  displayName: '演示用户',
  email: 'demo@example.com',
  enabled: true
})

function handleTest() {
  ElMessage.success('测试成功！')
}
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

.profile-card {
  border-radius: 12px;
  border: 1px solid #e5e7eb;
}

.card-header h3 {
  font-size: 18px;
  font-weight: 600;
  color: #1f2937;
  margin: 0;
}

.profile-content {
  padding: 20px 0;
}

.profile-content p {
  margin-bottom: 12px;
  color: #374151;
}
</style>