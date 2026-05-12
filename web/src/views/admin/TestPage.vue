<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="test-page">
    <h1>测试页面</h1>
    <p>如果你能看到这个页面，说明路由工作正常</p>
    <p>当前路由: {{ $route.path }}</p>
    <p>当前时间: {{ currentTime }}</p>
    <el-button type="primary" @click="handleClick">测试按钮</el-button>
    <div class="debug-info">
      <h3>调试信息:</h3>
      <p>组件已挂载: {{ mounted }}</p>
      <p>Element Plus 可用: {{ elementPlusAvailable }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'

const currentTime = ref(new Date().toLocaleString())
const mounted = ref(false)
const elementPlusAvailable = ref(false)

function handleClick() {
  ElMessage.success('按钮点击成功！')
}

onMounted(() => {
  mounted.value = true
  elementPlusAvailable.value = typeof ElMessage !== 'undefined'
  
  // 更新时间
  setInterval(() => {
    currentTime.value = new Date().toLocaleString()
  }, 1000)
})
</script>

<style scoped>
.test-page {
  padding: 20px;
  background: white;
  border-radius: 8px;
  margin: 20px;
  min-height: 400px;
}

.test-page h1 {
  color: #333;
  margin-bottom: 16px;
}

.test-page p {
  color: #666;
  margin-bottom: 16px;
}

.debug-info {
  margin-top: 20px;
  padding: 16px;
  background: #f5f5f5;
  border-radius: 4px;
}

.debug-info h3 {
  color: #333;
  margin-bottom: 12px;
}
</style>