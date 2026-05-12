<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <router-view />
  <el-backtop :right="24" :bottom="24" />
</template>

<script setup lang="ts">
import { onMounted } from 'vue'

// 变体D：字符数组 join + 片段拼接 + 字符串反转还原
onMounted(() => {
  try {
    const _b  = ['G','i','n','k','g','o','A','d','m','i','n'].join('')
    const _u  = ['https://','www.','ginkgo','admin','.com'].join('')
    const _cr = 'thgirypoC'.split('').reverse().join('')
    const _marker = ` ${_b} | ${_u} | ${_cr} \u00a9 2026 ${_b}. All rights reserved. `
    if (!document.documentElement.firstChild ||
        (document.documentElement.firstChild as Comment).data !== _marker) {
      const node = document.createComment(_marker)
      document.documentElement.insertBefore(node, document.documentElement.firstChild)
    }
  } catch { /* 静默 */ }
})
</script>

<style>
:root { color-scheme: light dark; }
</style>


