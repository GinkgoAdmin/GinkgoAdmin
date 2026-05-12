<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="editor-test-page">
    <div class="container">
      <h1>编辑器测试页面</h1>
      <p>测试Summernote编辑器插件的功能</p>

      <!-- Summernote编辑器测试 -->
      <div class="editor-section">
        <h2>Summernote富文本编辑器</h2>
        <DynamicEditor 
          v-model="richContent"
          editor-type="rich"
          :config="{
            height: 300,
            toolbar: 'full',
            lang: 'zh-CN'
          }"
          @editor-ready="handleEditorReady"
          @editor-change="handleEditorChange"
        />
        
        <div class="content-preview">
          <h3>内容预览：</h3>
          <div class="preview-content" v-html="richContent"></div>
        </div>
      </div>

      <!-- Markdown编辑器测试 -->
      <div class="editor-section">
        <h2>Markdown编辑器</h2>
        <DynamicEditor 
          v-model="markdownContent"
          editor-type="markdown"
          :config="{
            preview: true,
            theme: 'default'
          }"
        />
        
        <div class="content-preview">
          <h3>Markdown内容：</h3>
          <pre class="markdown-content">{{ markdownContent }}</pre>
        </div>
      </div>

      <!-- 代码编辑器测试 -->
      <div class="editor-section">
        <h2>代码编辑器</h2>
        <DynamicEditor 
          v-model="codeContent"
          editor-type="code"
          :config="{
            language: 'javascript',
            theme: 'vs-dark'
          }"
        />
        
        <div class="content-preview">
          <h3>代码内容：</h3>
          <pre class="code-content">{{ codeContent }}</pre>
        </div>
      </div>

      <!-- 插件状态信息 -->
      <div class="plugin-status">
        <div class="status-header">
          <h2>插件状态</h2>
          <div class="status-actions">
            <el-button 
              @click="runDependencyTests" 
              :loading="testLoading"
              size="small"
              type="primary"
            >
              重新测试
            </el-button>
            <el-button 
              @click="waitForDeps" 
              :loading="testLoading"
              size="small"
            >
              等待加载
            </el-button>
          </div>
        </div>
        
        <div class="status-grid">
          <div class="status-item">
            <strong>jQuery:</strong> 
            <span :class="jqueryStatus.class">{{ jqueryStatus.text }}</span>
            <div v-if="jqueryStatus.details" class="status-details">
              版本: {{ jqueryStatus.details.version }}
            </div>
          </div>
          <div class="status-item">
            <strong>Summernote:</strong> 
            <span :class="summernoteStatus.class">{{ summernoteStatus.text }}</span>
            <div v-if="summernoteStatus.details" class="status-details">
              版本: {{ summernoteStatus.details.version }}
            </div>
          </div>
          <div class="status-item">
            <strong>Bootstrap:</strong> 
            <span :class="bootstrapStatus.class">{{ bootstrapStatus.text }}</span>
            <div v-if="bootstrapStatus.details" class="status-details">
              CSS: {{ bootstrapStatus.details.css ? '✓' : '✗' }}
              JS: {{ bootstrapStatus.details.js ? '✓' : '✗' }}
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, reactive } from 'vue'
import { ElMessage, ElButton } from 'element-plus'
import DynamicEditor from '../../../components/DynamicEditor.vue'
import { 
  runAllEditorTests, 
  waitForDependencies, 
  createTestSummernoteInstance,
  destroyTestSummernoteInstance,
  type EditorTestResult 
} from '../../../utils/editorTest'

const richContent = ref('<p>这是一个<strong>Summernote</strong>富文本编辑器的测试内容。</p>')
const markdownContent = ref('# Markdown标题\n\n这是一个**Markdown**编辑器的测试内容。\n\n- 列表项1\n- 列表项2')
const codeContent = ref(`function hello() {
  console.log('Hello, Summernote Editor!');
  return 'success';
}`)

// 测试结果
const testResults = reactive<Record<string, EditorTestResult>>({})
const testLoading = ref(false)

// 检查依赖状态
const jqueryStatus = computed(() => {
  const result = testResults.jquery
  return {
    class: result?.success ? 'status-success' : 'status-error',
    text: result?.success ? '已加载' : '未加载',
    details: result?.details
  }
})

const summernoteStatus = computed(() => {
  const result = testResults.summernote
  return {
    class: result?.success ? 'status-success' : 'status-error',
    text: result?.success ? '已加载' : '未加载',
    details: result?.details
  }
})

const bootstrapStatus = computed(() => {
  const result = testResults.bootstrap
  return {
    class: result?.success ? 'status-success' : 'status-error',
    text: result?.success ? '已加载' : '未加载',
    details: result?.details
  }
})

// 运行依赖测试
const runDependencyTests = async () => {
  testLoading.value = true
  try {
    const results = runAllEditorTests()
    Object.assign(testResults, results)
    
    const allSuccess = Object.values(results).every(r => r.success)
    if (allSuccess) {
      ElMessage.success('所有依赖测试通过')
    } else {
      ElMessage.warning('部分依赖测试失败')
    }
  } catch (error) {
    ElMessage.error('依赖测试失败')
  } finally {
    testLoading.value = false
  }
}

// 等待依赖加载
const waitForDeps = async () => {
  testLoading.value = true
  try {
    const success = await waitForDependencies(['jquery', 'bootstrap', 'summernote'], 15000)
    if (success) {
      ElMessage.success('所有依赖已加载完成')
      await runDependencyTests()
    } else {
      ElMessage.error('等待依赖加载超时')
    }
  } catch (error) {
    ElMessage.error('等待依赖失败')
  } finally {
    testLoading.value = false
  }
}

const handleEditorReady = (editor: any) => {
  ElMessage.success('编辑器初始化成功')
}

const handleEditorChange = (value: string, editor: any) => {
  // silently ignored
}

onMounted(async () => {
  // 自动运行依赖测试
  await runDependencyTests()
})
</script>

<style scoped>
.editor-test-page {
  padding: 20px;
  max-width: 1200px;
  margin: 0 auto;
}

.container {
  background: white;
  border-radius: 8px;
  padding: 24px;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.editor-section {
  margin-bottom: 40px;
  padding: 20px;
  border: 1px solid #e4e7ed;
  border-radius: 8px;
  background: #fafafa;
}

.editor-section h2 {
  margin: 0 0 16px 0;
  color: #303133;
  font-size: 18px;
}

.content-preview {
  margin-top: 20px;
  padding: 16px;
  background: white;
  border: 1px solid #dcdfe6;
  border-radius: 4px;
}

.content-preview h3 {
  margin: 0 0 12px 0;
  font-size: 14px;
  color: #606266;
}

.preview-content {
  min-height: 60px;
  padding: 8px;
  border: 1px solid #e4e7ed;
  border-radius: 4px;
  background: #f9f9f9;
}

.markdown-content,
.code-content {
  margin: 0;
  padding: 12px;
  background: #f5f5f5;
  border: 1px solid #e4e7ed;
  border-radius: 4px;
  font-family: 'Monaco', 'Menlo', 'Ubuntu Mono', monospace;
  font-size: 13px;
  line-height: 1.5;
  white-space: pre-wrap;
  word-wrap: break-word;
}

.plugin-status {
  margin-top: 40px;
  padding: 20px;
  background: #f0f9ff;
  border: 1px solid #b3d8ff;
  border-radius: 8px;
}

.status-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}

.status-header h2 {
  margin: 0;
  color: #303133;
  font-size: 18px;
}

.status-actions {
  display: flex;
  gap: 8px;
}

.status-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
}

.status-item {
  padding: 12px;
  background: white;
  border-radius: 4px;
  border: 1px solid #e4e7ed;
}

.status-item strong {
  color: #606266;
}

.status-success {
  color: #67c23a;
  font-weight: bold;
}

.status-error {
  color: #f56c6c;
  font-weight: bold;
}

.status-details {
  margin-top: 4px;
  font-size: 12px;
  color: #909399;
  font-weight: normal;
}

/* 响应式设计 */
@media (max-width: 768px) {
  .editor-test-page {
    padding: 10px;
  }
  
  .container {
    padding: 16px;
  }
  
  .editor-section {
    padding: 16px;
  }
  
  .status-grid {
    grid-template-columns: 1fr;
  }
}
</style>