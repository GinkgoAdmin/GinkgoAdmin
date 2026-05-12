<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="portal-closed">
    <div class="portal-closed-content">
      <i class="ri-lock-line portal-closed-icon"></i>
      <h2>站点未开放</h2>
      <p>当前站点暂未开放访问，请联系管理员。</p>
    </div>
  </div>
</template>

<style scoped>
.portal-closed {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: #f5f7fa;
}
.portal-closed-content {
  text-align: center;
  color: #909399;
}
.portal-closed-icon {
  font-size: 64px;
  color: #c0c4cc;
  margin-bottom: 16px;
}
.portal-closed-content h2 {
  font-size: 20px;
  font-weight: 500;
  color: #606266;
  margin: 0 0 8px;
}
.portal-closed-content p {
  font-size: 14px;
  margin: 0;
}
</style>
