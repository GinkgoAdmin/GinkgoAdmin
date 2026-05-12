<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="verification-template-mgr">
    <!-- 工具栏 -->
    <div class="tmpl-toolbar">
      <el-button type="primary" @click="handleAdd">
        <el-icon><Plus /></el-icon>
        新增模板
      </el-button>
      <el-button @click="loadTemplates" :loading="loading">
        <el-icon><Refresh /></el-icon>
        刷新
      </el-button>
    </div>

    <!-- 模板列表 -->
    <el-table :data="templates" v-loading="loading" border stripe class="tmpl-table">
      <el-table-column label="模板名称" prop="name" min-width="180" />
      <el-table-column label="用途" width="130" align="center">
        <template #default="{ row }">
          <el-tag :type="purposeTagType(row.purpose)" size="small">{{ purposeLabel(row.purpose) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="渠道" width="100" align="center">
        <template #default="{ row }">
          <el-tag :type="row.channel === 0 ? undefined : 'success'" size="small" effect="plain">
            <i :class="row.channel === 0 ? 'ri-mail-line' : 'ri-smartphone-line'" style="margin-right:4px;" />
            {{ channelLabel(row.channel) }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="邮件主题" prop="subject" min-width="200" show-overflow-tooltip />
      <el-table-column label="默认" width="70" align="center">
        <template #default="{ row }">
          <el-icon v-if="row.isDefault" style="color:#10b981;font-size:18px;"><CircleCheckFilled /></el-icon>
        </template>
      </el-table-column>
      <el-table-column label="启用" width="70" align="center">
        <template #default="{ row }">
          <el-switch v-model="row.enabled" size="small" @change="handleToggleEnabled(row)" />
        </template>
      </el-table-column>
      <el-table-column label="操作" width="220" align="center" fixed="right">
        <template #default="{ row }">
          <el-button size="small" text type="primary" @click="handleEdit(row)">编辑</el-button>
          <el-button size="small" text type="info" @click="handlePreview(row)">预览</el-button>
          <el-button size="small" text type="danger" @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 编辑对话框 -->
    <el-dialog
      v-model="dialogVisible"
      :title="editingTemplate.id ? '编辑模板' : '新增模板'"
      width="85vw"
      top="5vh"
      :close-on-click-modal="false"
      class="tmpl-edit-dialog"
    >
      <div style="max-height:72vh;overflow-y:auto;padding-right:8px;">
      <el-form :model="editingTemplate" label-width="100px" label-position="left">
        <el-form-item label="模板名称" required>
          <el-input v-model="editingTemplate.name" placeholder="如：找回密码邮件模板" />
        </el-form-item>
        <div style="display:flex;gap:16px;">
          <el-form-item label="验证用途" required style="flex:1;">
            <el-select v-model="editingTemplate.purpose" placeholder="请选择" style="width:100%;">
              <el-option v-for="p in purposeOptions" :key="p.value" :label="p.label" :value="p.value" />
            </el-select>
          </el-form-item>
          <el-form-item label="渠道" required style="flex:1;">
            <el-select v-model="editingTemplate.channel" placeholder="请选择" style="width:100%;">
              <el-option label="邮件" :value="0" />
              <el-option label="短信" :value="1" />
            </el-select>
          </el-form-item>
        </div>
        <el-form-item label="邮件主题" v-if="editingTemplate.channel === 0">
          <el-input v-model="editingTemplate.subject" placeholder="支持占位符：{appName} {purpose}" />
        </el-form-item>
        <el-form-item label="模板正文" required>
          <div style="width:100%;">
            <div class="edit-mode-bar">
              <el-radio-group v-model="editMode" size="small">
                <el-radio-button value="visual">可视化编辑</el-radio-button>
                <el-radio-button value="source">源代码</el-radio-button>
              </el-radio-group>
              <div class="placeholder-tips" style="margin:0;">
                <span class="tip-item"><code>{code}</code> 验证码</span>
                <span class="tip-item"><code>{minutes}</code> 有效分钟数</span>
                <span class="tip-item"><code>{purpose}</code> 用途描述</span>
                <span class="tip-item"><code>{appName}</code> 应用名称</span>
              </div>
            </div>
            <!-- 可视化模式：WangEditor 富文本 -->
            <div v-show="editMode === 'visual'" class="visual-editor-wrap">
              <DynamicEditor
                v-if="editorReady"
                :model-value="editingTemplate.bodyTemplate ?? ''"
                @update:model-value="(v: string) => editingTemplate.bodyTemplate = v"
                editor-type="rich"
                :height="380"
                placeholder="在此编辑邮件模板内容..."
              />
            </div>
            <!-- 源代码模式：原始 HTML -->
            <div v-show="editMode === 'source'" class="source-editor-wrap">
              <el-input
                v-model="editingTemplate.bodyTemplate"
                type="textarea"
                :autosize="{ minRows: 18, maxRows: 36 }"
                placeholder="在此编辑 HTML 源代码"
                style="font-family:ui-monospace,Menlo,Consolas,monospace;font-size:12px;"
              />
            </div>
          </div>
        </el-form-item>
        <div style="display:flex;gap:16px;">
          <el-form-item label="HTML格式" style="flex:1;">
            <el-switch v-model="editingTemplate.isHtml" />
          </el-form-item>
          <el-form-item label="默认模板" style="flex:1;">
            <el-switch v-model="editingTemplate.isDefault" />
          </el-form-item>
          <el-form-item label="启用" style="flex:1;">
            <el-switch v-model="editingTemplate.enabled" />
          </el-form-item>
        </div>
        <el-form-item label="排序">
          <el-input-number v-model="editingTemplate.sortOrder" :min="0" :max="999" />
        </el-form-item>
      </el-form>
      </div>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave" :loading="saving">保存</el-button>
      </template>
    </el-dialog>

    <!-- 预览对话框 -->
    <el-dialog v-model="previewVisible" title="模板预览" width="640px">
      <div class="preview-info">
        <el-descriptions :column="2" size="small" border>
          <el-descriptions-item label="模板名称">{{ previewData.name }}</el-descriptions-item>
          <el-descriptions-item label="用途">{{ purposeLabel(previewData.purpose) }}</el-descriptions-item>
          <el-descriptions-item label="渠道">{{ channelLabel(previewData.channel) }}</el-descriptions-item>
          <el-descriptions-item label="邮件主题">{{ previewData.subject || '-' }}</el-descriptions-item>
        </el-descriptions>
      </div>
      <div class="preview-frame-wrap">
        <iframe
          v-if="previewData.isHtml"
          class="preview-frame"
          :srcdoc="renderPreview(previewData.bodyTemplate)"
          sandbox="allow-same-origin"
        />
        <pre v-else class="preview-text">{{ renderPreview(previewData.bodyTemplate) }}</pre>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, nextTick } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Refresh, CircleCheckFilled } from '@element-plus/icons-vue'
import DynamicEditor from './DynamicEditor.vue'
import {
  getVerificationTemplates,
  saveVerificationTemplate,
  deleteVerificationTemplate,
  type VerificationTemplate
} from '../api/auth'

// 状态
const loading = ref(false)
const saving = ref(false)
const templates = ref<VerificationTemplate[]>([])
const dialogVisible = ref(false)
const previewVisible = ref(false)
const editMode = ref<'visual' | 'source'>('visual')
const editorReady = ref(false)

// 编辑中的模板
const editingTemplate = reactive<Partial<VerificationTemplate>>({
  purpose: 0,
  channel: 0,
  name: '',
  subject: '',
  bodyTemplate: '',
  isHtml: true,
  isDefault: false,
  enabled: true,
  sortOrder: 0,
})

// 预览数据
const previewData = reactive<any>({
  name: '', purpose: 0, channel: 0, subject: '', bodyTemplate: '', isHtml: true,
})

// 用途配置
const purposeOptions = [
  { value: 0, label: '找回密码' },
  { value: 1, label: '登录验证' },
  { value: 2, label: '注册验证' },
  { value: 3, label: '绑定邮箱' },
  { value: 4, label: '绑定手机' },
  { value: 10, label: '危险操作确认' },
  { value: 99, label: '自定义' },
]

function purposeLabel(p: number | string): string {
  const n = Number(p)
  return purposeOptions.find(o => o.value === n)?.label || `未知(${p})`
}

function purposeTagType(p: number | string) {
  const n = Number(p)
  const map: Record<number, string> = { 0: 'warning', 1: 'primary', 2: 'success', 3: 'info', 4: 'info', 10: 'danger' }
  return map[n]
}

function channelLabel(c: number): string {
  return c === 0 ? '邮件' : c === 1 ? '短信' : '站内通知'
}

// 加载模板列表
async function loadTemplates() {
  loading.value = true
  try {
    templates.value = await getVerificationTemplates()
  } catch (e: any) {
    ElMessage.error(e?.message || '加载模板失败')
  } finally {
    loading.value = false
  }
}

// 新增
function openEditorDialog() {
  editorReady.value = false
  editMode.value = 'visual'
  dialogVisible.value = true
  nextTick(() => {
    editorReady.value = true
  })
}

function handleAdd() {
  Object.assign(editingTemplate, {
    id: undefined,
    purpose: 0,
    channel: 0,
    name: '',
    subject: '',
    bodyTemplate: '',
    isHtml: true,
    isDefault: false,
    enabled: true,
    sortOrder: 0,
  })
  openEditorDialog()
}

// 编辑
function handleEdit(row: VerificationTemplate) {
  Object.assign(editingTemplate, {
    ...row,
    purpose: Number(row.purpose),
    channel: Number(row.channel),
  })
  openEditorDialog()
}

// 保存
async function handleSave() {
  if (!editingTemplate.name?.trim()) {
    ElMessage.warning('请输入模板名称')
    return
  }
  if (!editingTemplate.bodyTemplate?.trim()) {
    ElMessage.warning('请输入模板正文')
    return
  }
  saving.value = true
  try {
    await saveVerificationTemplate(editingTemplate)
    ElMessage.success('保存成功')
    dialogVisible.value = false
    await loadTemplates()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

// 切换启用状态
async function handleToggleEnabled(row: VerificationTemplate) {
  try {
    await saveVerificationTemplate({ ...row })
  } catch (e: any) {
    ElMessage.error(e?.message || '操作失败')
    row.enabled = !row.enabled
  }
}

// 删除
async function handleDelete(row: VerificationTemplate) {
  try {
    await ElMessageBox.confirm(`确定删除模板「${row.name}」？`, '确认删除', {
      confirmButtonText: '删除',
      cancelButtonText: '取消',
      type: 'warning',
    })
    await deleteVerificationTemplate(row.id)
    ElMessage.success('删除成功')
    await loadTemplates()
  } catch (e: any) {
    if (e !== 'cancel') ElMessage.error(e?.message || '删除失败')
  }
}

// 预览
function handlePreview(row: VerificationTemplate) {
  Object.assign(previewData, { ...row })
  previewVisible.value = true
}

// 渲染预览：替换占位符为示例值
function renderPreview(body: string): string {
  return (body || '')
    .replace(/\{code\}/g, '586429')
    .replace(/\{minutes\}/g, '15')
    .replace(/\{purpose\}/g, '找回密码')
    .replace(/\{appName\}/g, 'GinkgoAdmin')
}

onMounted(() => {
  loadTemplates()
})
</script>

<style scoped>
.verification-template-mgr {
  padding: 0;
}
.tmpl-toolbar {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
}
.tmpl-table {
  border-radius: 8px;
  overflow: hidden;
}
.placeholder-tips {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-top: 8px;
}
.tip-item {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.tip-item code {
  background: var(--el-fill-color-light);
  padding: 1px 6px;
  border-radius: 4px;
  font-family: 'Courier New', monospace;
  color: var(--el-color-primary);
  margin-right: 4px;
}
.preview-info {
  margin-bottom: 16px;
}
.preview-frame-wrap {
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  overflow: hidden;
  background: #f9fafb;
}
.preview-frame {
  width: 100%;
  height: 420px;
  border: none;
  display: block;
}
.preview-text {
  padding: 16px;
  font-size: 13px;
  white-space: pre-wrap;
  word-break: break-all;
  margin: 0;
  max-height: 420px;
  overflow: auto;
}
/* 编辑模式切换栏 */
.edit-mode-bar {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 10px;
}
.visual-editor-wrap {
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  overflow: hidden;
}
.source-editor-wrap {
  margin-top: 4px;
}
</style>
