<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div style="padding: 20px; background: white; min-height: 400px;">
    <h1 style="color: red; font-size: 24px;">简单测试页面</h1>
    <p style="color: blue; font-size: 16px;">这是一个最简单的测试页面</p>
    <div style="background: yellow; padding: 10px; margin: 10px 0;">
      黄色背景区域 - 如果你能看到这个，说明页面正常渲染
    </div>
    <button style="padding: 10px 20px; background: green; color: white; border: none; cursor: pointer;" @click="handleClick">
      点击测试
    </button>
    <div v-if="clicked" style="color: green; margin-top: 10px;">
      按钮已点击！
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const clicked = ref(false)

function handleClick() {
  clicked.value = true
  alert('按钮点击成功！')
}
</script>