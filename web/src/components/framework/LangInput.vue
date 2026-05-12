<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <!-- 主框架级多语言输入组件：跟随全局语言设置，编辑当前选中语言的内容 -->
  <div class="lang-input">
    <el-input
      v-if="!isTextarea"
      v-model="values[activeLang]"
      :placeholder="placeholder"
      @input="emitValue"
    >
      <template #suffix>
        <span class="lang-badge">{{ activeLangLabel }}</span>
        <el-tooltip content="自动翻译到其他语言" placement="top">
          <el-icon class="translate-btn" :class="{ spinning: translating }" @click.stop="autoTranslate">
            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12.87 15.07l-2.54-2.51.03-.03A17.52 17.52 0 0014.07 6H17V4h-7V2H8v2H1v2h11.17C11.5 7.92 10.44 9.75 9 11.35 8.07 10.32 7.3 9.19 6.69 8h-2c.73 1.63 1.73 3.17 2.98 4.56l-5.09 5.02L4 19l5-5 3.11 3.11.76-2.04zM18.5 10h-2L12 22h2l1.12-3h4.75L21 22h2l-4.5-12zm-2.62 7l1.62-4.33L19.12 17h-3.24z"/></svg>
          </el-icon>
        </el-tooltip>
      </template>
    </el-input>
    <div v-else class="textarea-wrap">
      <el-input
        v-model="values[activeLang]"
        type="textarea"
        :rows="rows"
        :placeholder="placeholder"
        @input="emitValue"
      />
      <div class="textarea-bar">
        <span class="lang-badge">{{ activeLangLabel }}</span>
        <el-tooltip content="自动翻译到其他语言" placement="top">
          <el-icon class="translate-btn" :class="{ spinning: translating }" @click="autoTranslate">
            <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12.87 15.07l-2.54-2.51.03-.03A17.52 17.52 0 0014.07 6H17V4h-7V2H8v2H1v2h11.17C11.5 7.92 10.44 9.75 9 11.35 8.07 10.32 7.3 9.19 6.69 8h-2c.73 1.63 1.73 3.17 2.98 4.56l-5.09 5.02L4 19l5-5 3.11 3.11.76-2.04zM18.5 10h-2L12 22h2l1.12-3h4.75L21 22h2l-4.5-12zm-2.62 7l1.62-4.33L19.12 17h-3.24z"/></svg>
          </el-icon>
        </el-tooltip>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, watch, onMounted, computed } from 'vue'
import { ElMessage } from 'element-plus'
import {
  useLangRef,
  getAvailableLangs,
  useTranslateAllTrigger,
  useAvailableLangs,
  translateText,
  getDefaultLang,
} from '@/utils/lang'

const props = defineProps<{
  modelValue: string
  placeholder?: string
  isTextarea?: boolean
  rows?: number
  type?: string
}>()

const emit = defineEmits(['update:modelValue', 'change'])

// 使用全局语言状态
const lang = useLangRef()
const langs = useAvailableLangs()
const activeLang = computed(() => lang.value)
const activeLangLabel = computed(() => langs.value.find(l => l.code === activeLang.value)?.label || activeLang.value)
const translating = ref(false)
const isTextarea = computed(() => props.isTextarea || props.type === 'textarea')

// 全局翻译触发器
const translateTrigger = useTranslateAllTrigger()

// 各语言的值
const values = reactive<Record<string, string>>({})

// 初始化各语言字段
function initValues() {
  langs.value.forEach(l => {
    if (!(l.code in values)) values[l.code] = ''
  })
}

// 解析输入的JSON字符串
function parseInput(v: string) {
  initValues()
  if (!v) {
    langs.value.forEach(l => { values[l.code] = '' })
    return
  }
  try {
    const obj = JSON.parse(v)
    if (typeof obj === 'string') {
      // 纯字符串当作默认语言
      values[getDefaultLang()] = obj
      langs.value.filter(l => l.code !== getDefaultLang()).forEach(l => { values[l.code] = '' })
      return
    }
    langs.value.forEach(l => { values[l.code] = obj[l.code] || '' })
  } catch {
    // 非JSON直接当默认语言
    values[getDefaultLang()] = v
    langs.value.filter(l => l.code !== getDefaultLang()).forEach(l => { values[l.code] = '' })
  }
}

function emitValue() {
  const obj: Record<string, string> = {}
  langs.value.forEach(l => { if (values[l.code]) obj[l.code] = values[l.code] })
  const json = JSON.stringify(obj)
  emit('update:modelValue', json)
  emit('change', json)
}

/**
 * 自动翻译：将当前语言的内容翻译到其他所有语言
 */
async function autoTranslate() {
  const sourceText = values[activeLang.value]
  if (!sourceText || !sourceText.trim()) {
    ElMessage.warning('请先输入当前语言的内容')
    return
  }

  const targets = langs.value.filter(l => l.code !== activeLang.value)
  if (targets.length === 0) return

  translating.value = true
  let successCount = 0

  try {
    const promises = targets.map(async (target) => {
      const result = await translateText(sourceText, activeLang.value, target.code)
      if (result) { values[target.code] = result; successCount++ }
    })
    await Promise.all(promises)
    emitValue()
    if (successCount > 0) ElMessage.success(`已翻译到 ${successCount} 种语言`)
    else ElMessage.error('翻译失败，请稍后重试')
  } catch { ElMessage.error('翻译服务暂时不可用') }
  finally { translating.value = false }
}

// 监听全局"一键翻译"触发器
watch(translateTrigger, () => {
  let sourceCode = activeLang.value
  if (!values[sourceCode]?.trim()) sourceCode = getDefaultLang()
  if (!values[sourceCode]?.trim()) {
    const found = langs.value.find(l => values[l.code]?.trim())
    if (found) sourceCode = found.code
  }
  if (!values[sourceCode]?.trim()) return
  doTranslateFrom(sourceCode)
})

async function doTranslateFrom(sourceCode: string) {
  const sourceText = values[sourceCode]
  if (!sourceText?.trim()) return
  const targets = langs.value.filter(l => l.code !== sourceCode)
  if (targets.length === 0) return

  translating.value = true
  try {
    const promises = targets.map(async (target) => {
      const result = await translateText(sourceText, sourceCode, target.code)
      if (result) values[target.code] = result
    })
    await Promise.all(promises)
    emitValue()
  } catch {} finally { translating.value = false }
}

watch(() => props.modelValue, v => parseInput(v), { immediate: true })
onMounted(() => { initValues(); parseInput(props.modelValue) })
</script>

<style scoped>
.lang-input { width: 100%; }
.lang-badge {
  font-size: 10px;
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  padding: 1px 6px;
  border-radius: 3px;
  line-height: 16px;
  white-space: nowrap;
}
.translate-btn {
  cursor: pointer;
  margin-left: 6px;
  color: var(--el-color-primary);
  font-size: 14px;
  transition: all 0.2s;
}
.translate-btn:hover { color: var(--el-color-primary-dark-2); transform: scale(1.2); }
.translate-btn.spinning { animation: spin 1s linear infinite; pointer-events: none; opacity: 0.6; }
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
.textarea-wrap { position: relative; }
.textarea-bar {
  position: absolute;
  bottom: 6px;
  right: 10px;
  display: flex;
  align-items: center;
  gap: 4px;
  z-index: 1;
}
</style>
