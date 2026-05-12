<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <!-- 主框架级后台管理语言切换器 + 一键翻译按钮 -->
  <div class="admin-lang-wrap">
    <el-radio-group v-model="lang" size="small" @change="onSwitch" class="admin-lang-switch">
      <el-radio-button v-for="l in langs" :key="l.code" :value="l.code">
        {{ l.label }}
      </el-radio-button>
    </el-radio-group>
    <el-tooltip content="一键翻译：将当前语言内容翻译到其他所有语言" placement="bottom">
      <el-button size="small" :icon="translateIcon" :loading="translating" @click="handleTranslateAll" class="translate-all-btn">
        翻译
      </el-button>
    </el-tooltip>
  </div>
</template>

<script setup lang="ts">
import { ref, h } from 'vue'
import { ElMessage } from 'element-plus'
import {
  useLangRef,
  switchLang,
  useAvailableLangs,
  triggerTranslateAll,
} from '@/utils/lang'

const lang = useLangRef()
const langs = useAvailableLangs()
const translating = ref(false)

// 翻译图标
const translateIcon = h('svg', {
  viewBox: '0 0 24 24',
  fill: 'currentColor',
  style: 'width:14px;height:14px'
}, [
  h('path', { d: 'M12.87 15.07l-2.54-2.51.03-.03A17.52 17.52 0 0014.07 6H17V4h-7V2H8v2H1v2h11.17C11.5 7.92 10.44 9.75 9 11.35 8.07 10.32 7.3 9.19 6.69 8h-2c.73 1.63 1.73 3.17 2.98 4.56l-5.09 5.02L4 19l5-5 3.11 3.11.76-2.04zM18.5 10h-2L12 22h2l1.12-3h4.75L21 22h2l-4.5-12zm-2.62 7l1.62-4.33L19.12 17h-3.24z' })
])

function onSwitch(code: string) {
  switchLang(code)
}

async function handleTranslateAll() {
  translating.value = true
  const currentLabel = langs.value.find(l => l.code === lang.value)?.label || lang.value
  ElMessage.info(`正在将 ${currentLabel} 内容翻译到其他语言...`)

  triggerTranslateAll()

  setTimeout(() => {
    translating.value = false
    ElMessage.success('整页翻译完成')
  }, 3000)
}
</script>

<style scoped>
.admin-lang-wrap {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-right: 12px;
}
.translate-all-btn {
  font-size: 12px;
}
</style>
