<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <!-- 主框架级多语言富文本编辑器 -->
  <div class="lang-rich-editor">
    <div class="editor-bar">
      <span class="lang-badge">{{ activeLangLabel }}</span>
      <el-tooltip content="自动翻译到其他语言" placement="top">
        <el-icon class="translate-btn" :class="{ spinning: translating }" @click="autoTranslate">
          <svg viewBox="0 0 24 24" fill="currentColor"><path d="M12.87 15.07l-2.54-2.51.03-.03A17.52 17.52 0 0014.07 6H17V4h-7V2H8v2H1v2h11.17C11.5 7.92 10.44 9.75 9 11.35 8.07 10.32 7.3 9.19 6.69 8h-2c.73 1.63 1.73 3.17 2.98 4.56l-5.09 5.02L4 19l5-5 3.11 3.11.76-2.04zM18.5 10h-2L12 22h2l1.12-3h4.75L21 22h2l-4.5-12zm-2.62 7l1.62-4.33L19.12 17h-3.24z"/></svg>
        </el-icon>
      </el-tooltip>
    </div>
    <DynamicEditor
      :key="activeLang"
      :model-value="values[activeLang] || ''"
      :height="height"
      :toolbar="toolbar"
      :placeholder="placeholder"
      @update:model-value="onEditorChange"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, watch, onMounted, computed } from 'vue'
import { ElMessage } from 'element-plus'
import DynamicEditor from '@/components/DynamicEditor.vue'
import {
  useLangRef,
  useAvailableLangs,
  useTranslateAllTrigger,
  translateText,
  getDefaultLang,
} from '@/utils/lang'

const props = withDefaults(defineProps<{
  modelValue: string
  placeholder?: string
  height?: number | string
  toolbar?: 'minimal' | 'basic' | 'full'
}>(), {
  placeholder: '请输入正文内容...',
  height: 400,
  toolbar: 'full'
})

const emit = defineEmits(['update:modelValue'])

// 全局语言状态
const lang = useLangRef()
const langs = useAvailableLangs()
const activeLang = computed(() => lang.value)
const activeLangLabel = computed(() => langs.value.find(l => l.code === activeLang.value)?.label || activeLang.value)
const translating = ref(false)

// 全局翻译触发器
const translateTrigger = useTranslateAllTrigger()

// 各语言的富文本内容
const values = reactive<Record<string, string>>({})

// 初始化
function initValues() {
  langs.value.forEach(l => {
    if (!(l.code in values)) values[l.code] = ''
  })
}

// 解析 JSON
function parseInput(v: string) {
  initValues()
  if (!v) {
    langs.value.forEach(l => { values[l.code] = '' })
    return
  }
  try {
    const obj = JSON.parse(v)
    if (typeof obj === 'string') {
      values[getDefaultLang()] = obj
      return
    }
    langs.value.forEach(l => { values[l.code] = obj[l.code] || '' })
  } catch {
    // 解析失败，可能是纯 HTML 字符串
    values[getDefaultLang()] = v
    langs.value.filter(l => l.code !== getDefaultLang()).forEach(l => { values[l.code] = '' })
  }
}

function emitValue() {
  const obj: Record<string, string> = {}
  langs.value.forEach(l => { if (values[l.code]) obj[l.code] = values[l.code] })
  emit('update:modelValue', JSON.stringify(obj))
}

function onEditorChange(html: string) {
  values[activeLang.value] = html
  emitValue()
}

// ====== HTML 保留翻译核心工具 ======

/**
 * 从 HTML 字符串中提取所有文本节点，返回占位符替换后的 HTML 和文本列表。
 * 策略：用 @@TX_n@@ 占位符替换每个非空文本节点，之后可按占位符复原。
 */
function extractTextNodes(html: string): { template: string; texts: string[] } {
  const texts: string[] = []
  const template = html.replace(/(>)([^<]+?)(<)/g, (m, open, text, close) => {
    const trimmed = text.trim()
    if (!trimmed) return m
    const idx = texts.length
    texts.push(trimmed)
    return `${open}@@TX_${idx}@@${close}`
  })
  return { template, texts }
}

/**
 * 把翻译后的文本列表还原到占位符 HTML 模板中
 */
function restoreTextNodes(template: string, translated: string[]): string {
  return template.replace(/@@TX_(\d+)@@/g, (_, idx) => translated[Number(idx)] || '')
}

// 翻译当前语言内容到其他语言，保留 HTML 结构
async function autoTranslate() {
  const sourceHtml = values[activeLang.value]
  if (!sourceHtml?.trim() || sourceHtml === '<p><br></p>') {
    ElMessage.warning('请先输入当前语言的内容')
    return
  }
  const targets = langs.value.filter(l => l.code !== activeLang.value)
  if (targets.length === 0) return

  translating.value = true
  let successCount = 0
  try {
    // 第一步：提取文本节点，获得带占位符的 HTML 模板
    const { template, texts } = extractTextNodes(sourceHtml)

    if (texts.length === 0) {
      ElMessage.warning('未检测到可翻译的文字内容')
      translating.value = false
      return
    }

    // 第二步：为每个目标语言，翻译所有文本节点，然后还原结构
    const promises = targets.map(async (target) => {
      try {
        // 批量翻译：将所有文本用换行符连接为一次请求，提升翻译效率
        const joined = texts.join('\n')
        const translated = await translateText(joined, activeLang.value, target.code)
        if (translated) {
          const parts = translated.split('\n')
          // 如果翻译结果数量匹配，直接用；否则逐段翻译
          if (parts.length === texts.length) {
            values[target.code] = restoreTextNodes(template, parts)
          } else {
            // 降级：逐个文本节点单独翻译
            const translatedTexts: string[] = []
            for (const text of texts) {
              const r = await translateText(text, activeLang.value, target.code)
              translatedTexts.push(r || text) // 翻译失败则保留原文
            }
            values[target.code] = restoreTextNodes(template, translatedTexts)
          }
          successCount++
        }
      } catch {}
    })

    await Promise.all(promises)
    emitValue()
    if (successCount > 0) ElMessage.success(`已翻译到 ${successCount} 种语言，HTML格式已保留`)
    else ElMessage.error('翻译失败，请检查网络')
  } catch { ElMessage.error('翻译服务不可用') }
  finally { translating.value = false }
}

// 监听全局一键翻译
watch(translateTrigger, () => {
  let sourceCode = activeLang.value
  if (!values[sourceCode]?.trim() || values[sourceCode] === '<p><br></p>') sourceCode = getDefaultLang()
  if (!values[sourceCode]?.trim()) {
    const found = langs.value.find(l => values[l.code]?.trim() && values[l.code] !== '<p><br></p>')
    if (found) sourceCode = found.code
  }
  if (!values[sourceCode]?.trim()) return
  doTranslateFrom(sourceCode)
})

async function doTranslateFrom(sourceCode: string) {
  const sourceHtml = values[sourceCode]
  if (!sourceHtml?.trim()) return
  const targets = langs.value.filter(l => l.code !== sourceCode)
  if (targets.length === 0) return
  translating.value = true
  try {
    const { template, texts } = extractTextNodes(sourceHtml)
    if (texts.length === 0) return

    const promises = targets.map(async (target) => {
      try {
        const joined = texts.join('\n')
        const translated = await translateText(joined, sourceCode, target.code)
        if (translated) {
          const parts = translated.split('\n')
          if (parts.length === texts.length) {
            values[target.code] = restoreTextNodes(template, parts)
          } else {
            const translatedTexts: string[] = []
            for (const text of texts) {
              const r = await translateText(text, sourceCode, target.code)
              translatedTexts.push(r || text)
            }
            values[target.code] = restoreTextNodes(template, translatedTexts)
          }
        }
      } catch {}
    })

    await Promise.all(promises)
    emitValue()
  } catch {} finally { translating.value = false }
}


watch(() => props.modelValue, v => parseInput(v), { immediate: true })
onMounted(() => { initValues(); parseInput(props.modelValue) })
</script>

<style scoped>
.lang-rich-editor { width: 100%; }
.editor-bar {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 6px;
}
.lang-badge {
  font-size: 11px;
  color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
  padding: 2px 8px;
  border-radius: 3px;
  font-weight: 500;
}
.translate-btn {
  cursor: pointer;
  color: var(--el-color-primary);
  font-size: 14px;
  transition: all 0.2s;
}
.translate-btn:hover { color: var(--el-color-primary-dark-2); transform: scale(1.2); }
.translate-btn.spinning { animation: spin 1s linear infinite; pointer-events: none; opacity: 0.6; }
@keyframes spin { from { transform: rotate(0deg); } to { transform: rotate(360deg); } }
</style>
