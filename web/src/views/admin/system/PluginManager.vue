<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="plugin-manager">
    <div class="page-header">
      <h1>插件管理</h1>
      <p>管理系统插件，启用或禁用功能模块</p>
    </div>

    <!-- 插件统计 -->
    <div class="stats-cards">
      <div class="stat-card">
        <div class="stat-icon total">
          <el-icon><Grid /></el-icon>
        </div>
        <div class="stat-content">
          <h3>{{ pluginStats.total }}</h3>
          <p>总插件数</p>
        </div>
      </div>
      
      <div class="stat-card">
        <div class="stat-icon enabled">
          <el-icon><Check /></el-icon>
        </div>
        <div class="stat-content">
          <h3>{{ pluginStats.enabled }}</h3>
          <p>已启用</p>
        </div>
      </div>
      
      <div class="stat-card">
        <div class="stat-icon disabled">
          <el-icon><Close /></el-icon>
        </div>
        <div class="stat-content">
          <h3>{{ pluginStats.disabled }}</h3>
          <p>已禁用</p>
        </div>
      </div>
    </div>

    <!-- 工具栏 -->
    <div class="toolbar">
      <div class="toolbar-left">
        <el-input
          v-model="searchQuery"
          placeholder="搜索插件..."
          clearable
          style="width: 300px"
        >
          <template #prefix>
            <el-icon><Search /></el-icon>
          </template>
        </el-input>
        
        <el-select v-model="filterStatus" placeholder="状态筛选" style="width: 120px">
          <el-option label="全部" value="" />
          <el-option label="已启用" value="enabled" />
          <el-option label="已禁用" value="disabled" />
        </el-select>
      </div>
      
      <div class="toolbar-right">
        <el-button @click="refreshPlugins" :loading="loading">
          <el-icon><Refresh /></el-icon>
          刷新
        </el-button>
        
        <el-button v-permission="'/system/plugins:install'" type="primary" @click="showInstallDialog = true">
          <el-icon><Plus /></el-icon>
          安装插件
        </el-button>
      </div>
    </div>

    <!-- 插件列表 -->
    <div class="plugin-list">
      <div v-for="plugin in filteredPlugins" :key="plugin.name" class="plugin-card">
        <div class="plugin-info">
          <div class="plugin-header">
            <h3 class="plugin-name">{{ plugin.name }}</h3>
            <div class="plugin-version">v{{ plugin.version }}</div>
            <el-tag :type="plugin.enabled ? 'success' : 'info'" size="small">
              {{ plugin.enabled ? '已启用' : '已禁用' }}
            </el-tag>
          </div>
          
          <p class="plugin-description">{{ plugin.description }}</p>
          
          <div class="plugin-meta">
            <span class="plugin-author">作者: {{ plugin.author }}</span>
            <span v-if="plugin.installTime" class="plugin-install-time">
              安装时间: {{ formatDate(plugin.installTime) }}
            </span>
          </div>
          
          <div v-if="plugin.hooks && plugin.hooks.length > 0" class="plugin-hooks">
            <span class="hooks-label">钩子:</span>
            <el-tag v-for="hook in plugin.hooks" :key="hook" size="small" class="hook-tag">
              {{ hook }}
            </el-tag>
          </div>

          <!-- 依赖状态 -->
          <div v-if="plugin.dependencyStatus" class="plugin-dependencies">
            <span class="deps-label">依赖状态:</span>
            <div class="dependency-list">
              <el-tag 
                v-for="(status, depName) in plugin.dependencyStatus" 
                :key="depName" 
                :type="getDependencyTagType(status)"
                size="small" 
                class="dependency-tag"
              >
                {{ depName }}: {{ getDependencyStatusText(status) }}
              </el-tag>
            </div>
          </div>

          <!-- 安装状态 -->
          <div v-if="plugin.installStatus && plugin.installStatus !== 'installed'" class="plugin-install-status">
            <el-tag :type="getInstallStatusTagType(plugin.installStatus)" size="small">
              {{ getInstallStatusText(plugin.installStatus) }}
            </el-tag>
          </div>
        </div>
        
        <div class="plugin-actions">
          <el-button
            v-if="plugin.enabled"
            v-permission="'/system/plugins:disable'"
            @click="disablePlugin(plugin.name)"
            :loading="loading"
            size="small"
          >
            禁用
          </el-button>
          
          <el-button
            v-else
            v-permission="'/system/plugins:enable'"
            type="primary"
            @click="enablePlugin(plugin.name)"
            :loading="loading"
            size="small"
          >
            启用
          </el-button>
          
          <el-dropdown @command="handlePluginAction">
            <el-button size="small">
              更多
              <el-icon><ArrowDown /></el-icon>
            </el-button>
            <template #dropdown>
              <el-dropdown-menu>
                <el-dropdown-item v-permission="'/system/plugins:reload'" :command="`reload:${plugin.name}`">
                  <el-icon><Refresh /></el-icon>
                  重载
                </el-dropdown-item>
                <el-dropdown-item :command="`config:${plugin.name}`">
                  <el-icon><Setting /></el-icon>
                  配置
                </el-dropdown-item>
                <el-dropdown-item v-permission="'/system/plugins:uninstall'" :command="`uninstall:${plugin.name}`" divided>
                  <el-icon><Delete /></el-icon>
                  卸载
                </el-dropdown-item>
              </el-dropdown-menu>
            </template>
          </el-dropdown>
        </div>
      </div>
    </div>

    <!-- 安装插件对话框 -->
    <el-dialog v-model="showInstallDialog" title="安装插件" width="600px">
      <div class="install-plugin-form">
        <el-tabs v-model="installMethod">
          <el-tab-pane label="从文件安装" name="file">
            <el-upload
              drag
              accept=".zip,.tar.gz"
              :auto-upload="false"
              :on-change="handleFileSelect"
            >
              <el-icon class="el-icon--upload"><UploadFilled /></el-icon>
              <div class="el-upload__text">
                将插件文件拖到此处，或<em>点击上传</em>
              </div>
              <template #tip>
                <div class="el-upload__tip">
                  支持 .zip 和 .tar.gz 格式的插件包
                </div>
              </template>
            </el-upload>
          </el-tab-pane>
          
          <el-tab-pane label="从URL安装" name="url">
            <el-form :model="installForm" label-width="80px">
              <el-form-item label="插件URL">
                <el-input
                  v-model="installForm.url"
                  placeholder="https://example.com/plugin.zip"
                />
              </el-form-item>
            </el-form>
          </el-tab-pane>
          
          <el-tab-pane label="开发模式" name="dev">
            <el-form :model="installForm" label-width="100px">
              <el-form-item label="插件目录">
                <el-input
                  v-model="installForm.devPath"
                  placeholder="/path/to/plugin"
                />
              </el-form-item>
              <el-form-item>
                <el-checkbox v-model="installForm.hotReload">
                  启用热重载
                </el-checkbox>
              </el-form-item>
            </el-form>
          </el-tab-pane>
        </el-tabs>
      </div>
      
      <template #footer>
        <el-button @click="showInstallDialog = false">取消</el-button>
        <el-button type="primary" @click="installPlugin" :loading="installing">
          安装
        </el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  Grid, Check, Close, Search, Refresh, Plus, ArrowDown,
  Setting, Delete, UploadFilled
} from '@element-plus/icons-vue'
import { usePlugins, usePluginDevelopment } from '../../../composables/usePlugins'

const {
  loading,
  plugins,
  enablePlugin: enablePluginAction,
  disablePlugin: disablePluginAction,
  uninstallPlugin,
  reloadPlugin,
  getPluginStats,
  searchPlugins,
  initializePlugins
} = usePlugins()

const { hotRestart, installPluginDependencies } = usePluginDevelopment()

const searchQuery = ref('')
const filterStatus = ref('')
const showInstallDialog = ref(false)
const installMethod = ref('file')
const installing = ref(false)

const installForm = ref({
  url: '',
  devPath: '',
  hotReload: false
})

// 插件统计
const pluginStats = computed(() => getPluginStats())

// 过滤后的插件列表
const filteredPlugins = computed(() => {
  let result = Object.values(plugins.value)
  
  // 搜索过滤
  if (searchQuery.value) {
    result = searchPlugins(searchQuery.value)
  }
  
  // 状态过滤
  if (filterStatus.value === 'enabled') {
    result = result.filter(p => p.enabled)
  } else if (filterStatus.value === 'disabled') {
    result = result.filter(p => !p.enabled)
  }
  
  return result
})

// 启用插件
const enablePlugin = async (name: string) => {
  try {
    await enablePluginAction(name)
    ElMessage.success(`插件 ${name} 已启用`)
  } catch (error) {
    ElMessage.error(`启用插件失败: ${error.message}`)
  }
}

// 禁用插件
const disablePlugin = async (name: string) => {
  try {
    await disablePluginAction(name)
    ElMessage.success(`插件 ${name} 已禁用`)
  } catch (error) {
    ElMessage.error(`禁用插件失败: ${error.message}`)
  }
}

// 处理插件操作
const handlePluginAction = async (command: string) => {
  const [action, pluginName] = command.split(':')
  
  try {
    switch (action) {
      case 'reload':
        await reloadPlugin(pluginName)
        ElMessage.success(`插件 ${pluginName} 已重载`)
        break
        
      case 'config':
        // 打开插件配置页面
        ElMessage.info('插件配置功能开发中...')
        break
        
      case 'uninstall':
        await ElMessageBox.confirm(
          `确定要卸载插件 ${pluginName} 吗？此操作不可恢复。`,
          '确认卸载',
          { type: 'warning' }
        )
        await uninstallPlugin(pluginName)
        ElMessage.success(`插件 ${pluginName} 已卸载`)
        break
    }
  } catch (error) {
    if (error !== 'cancel') {
      ElMessage.error(`操作失败: ${error.message}`)
    }
  }
}

// 刷新插件列表
const refreshPlugins = async () => {
  try {
    await initializePlugins()
    ElMessage.success('插件列表已刷新')
  } catch (error) {
    ElMessage.error(`刷新失败: ${error.message}`)
  }
}

// 处理文件选择
const handleFileSelect = (file: any) => {
  // file selected for upload
}

// 安装插件
const installPlugin = async () => {
  installing.value = true
  
  try {
    switch (installMethod.value) {
      case 'file':
        ElMessage.info('文件安装功能开发中...')
        break
        
      case 'url':
        if (!installForm.value.url) {
          ElMessage.warning('请输入插件URL')
          return
        }
        ElMessage.info('URL安装功能开发中...')
        break
        
      case 'dev':
        if (!installForm.value.devPath) {
          ElMessage.warning('请输入插件目录路径')
          return
        }
        ElMessage.info('开发模式安装功能开发中...')
        break
    }
    
    showInstallDialog.value = false
  } catch (error) {
    ElMessage.error(`安装失败: ${error.message}`)
  } finally {
    installing.value = false
  }
}

// 格式化日期
const formatDate = (dateStr: string) => {
  return new Date(dateStr).toLocaleString('zh-CN')
}

// 获取依赖状态标签类型
const getDependencyTagType = (status: string) => {
  switch (status) {
    case 'installed':
      return 'success'
    case 'installing':
      return 'warning'
    case 'failed':
      return 'danger'
    default:
      return 'info'
  }
}

// 获取依赖状态文本
const getDependencyStatusText = (status: string) => {
  switch (status) {
    case 'installed':
      return '已安装'
    case 'installing':
      return '安装中'
    case 'failed':
      return '失败'
    case 'pending':
      return '等待中'
    default:
      return '未知'
  }
}

// 获取安装状态标签类型
const getInstallStatusTagType = (status: string) => {
  switch (status) {
    case 'installing':
      return 'warning'
    case 'failed':
      return 'danger'
    case 'pending':
      return 'info'
    default:
      return 'success'
  }
}

// 获取安装状态文本
const getInstallStatusText = (status: string) => {
  switch (status) {
    case 'installing':
      return '安装中'
    case 'installed':
      return '已安装'
    case 'failed':
      return '安装失败'
    case 'pending':
      return '等待安装'
    default:
      return '未知状态'
  }
}

onMounted(() => {
  // 页面加载时刷新插件列表
  refreshPlugins()
})
</script>

<style scoped>
.plugin-manager {
  padding: 1.5rem;
}

.page-header {
  margin-bottom: 2rem;
}

.page-header h1 {
  font-size: 1.75rem;
  font-weight: 600;
  color: #1f2937;
  margin: 0 0 0.5rem 0;
}

.page-header p {
  color: #6b7280;
  margin: 0;
}

.stats-cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
  margin-bottom: 2rem;
}

.stat-card {
  background: white;
  border-radius: 8px;
  padding: 1.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  display: flex;
  align-items: center;
  gap: 1rem;
}

.stat-icon {
  width: 48px;
  height: 48px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.5rem;
}

.stat-icon.total {
  background: #eff6ff;
  color: #3b82f6;
}

.stat-icon.enabled {
  background: #f0fdf4;
  color: #22c55e;
}

.stat-icon.disabled {
  background: #fef2f2;
  color: #ef4444;
}

.stat-content h3 {
  font-size: 1.5rem;
  font-weight: 600;
  color: #1f2937;
  margin: 0 0 0.25rem 0;
}

.stat-content p {
  color: #6b7280;
  margin: 0;
  font-size: 0.875rem;
}

.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.5rem;
  gap: 1rem;
}

.toolbar-left {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.toolbar-right {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.plugin-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.plugin-card {
  background: white;
  border-radius: 8px;
  padding: 1.5rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1rem;
}

.plugin-info {
  flex: 1;
}

.plugin-header {
  display: flex;
  align-items: center;
  gap: 1rem;
  margin-bottom: 0.5rem;
}

.plugin-name {
  font-size: 1.1rem;
  font-weight: 600;
  color: #1f2937;
  margin: 0;
}

.plugin-version {
  background: #f3f4f6;
  color: #6b7280;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 500;
}

.plugin-description {
  color: #6b7280;
  margin: 0 0 1rem 0;
  line-height: 1.5;
}

.plugin-meta {
  display: flex;
  gap: 1rem;
  margin-bottom: 0.75rem;
  font-size: 0.875rem;
  color: #9ca3af;
}

.plugin-hooks {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.hooks-label {
  font-size: 0.875rem;
  color: #6b7280;
  font-weight: 500;
}

.hook-tag {
  font-size: 0.75rem;
}

.plugin-dependencies {
  margin-top: 0.75rem;
}

.deps-label {
  font-size: 0.875rem;
  color: #6b7280;
  font-weight: 500;
  display: block;
  margin-bottom: 0.5rem;
}

.dependency-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.25rem;
}

.dependency-tag {
  font-size: 0.75rem;
}

.plugin-install-status {
  margin-top: 0.75rem;
}

.plugin-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-shrink: 0;
}

.install-plugin-form {
  margin: 1rem 0;
}

@media (max-width: 768px) {
  .toolbar {
    flex-direction: column;
    align-items: stretch;
  }
  
  .toolbar-left {
    flex-direction: column;
    align-items: stretch;
  }
  
  .plugin-card {
    flex-direction: column;
    align-items: stretch;
  }
  
  .plugin-actions {
    justify-content: flex-end;
  }
}
</style>