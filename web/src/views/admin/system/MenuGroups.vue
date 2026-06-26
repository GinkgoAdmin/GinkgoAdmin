<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="page-container menu-groups-page">
    <div class="menu-groups-layout">
      <!-- 左栏：菜单组列表 -->
      <div class="menu-groups-sidebar">
        <div class="sidebar-header">
          <h3>导航菜单</h3>
          <el-button type="primary" size="small" :icon="Plus" @click="handleAddGroup">新建</el-button>
        </div>
        <el-scrollbar class="sidebar-body">
          <div v-if="groupLoading" class="sidebar-loading">
            <el-skeleton :rows="4" animated />
          </div>
          <div v-else-if="groups.length === 0" class="sidebar-empty">
            <el-empty description="暂无菜单组" :image-size="80" />
          </div>
          <div v-else class="group-list">
            <div
              v-for="group in groups"
              :key="group.id"
              class="group-card"
              :class="{ active: currentGroupId === group.id }"
              @click="selectGroup(group)"
            >
              <div class="group-card-main">
                <div class="group-card-title">
                  <el-icon v-if="group.isSystem" :size="14" color="#e6a23c"><Lock /></el-icon>
                  <span>{{ group.name }}</span>
                </div>
                <div class="group-card-meta">
                  <el-tag size="small" type="info" effect="plain">{{ group.slug }}</el-tag>
                  <span class="item-count">{{ group.itemCount }} 项</span>
                </div>
                <div class="group-card-info">
                  <span v-if="group.location" class="info-tag">{{ group.location }}</span>
                  <span v-if="group.clientType" class="info-tag">{{ group.clientType }}</span>
                  <span v-if="group.version" class="info-tag">{{ group.version }}</span>
                </div>
              </div>
              <div class="group-card-actions" @click.stop>
                <el-switch v-model="group.enabled" size="small" @change="(v: boolean) => toggleGroupEnabled(group, v)" />
                <el-dropdown trigger="click" @command="(cmd: string) => handleGroupCommand(cmd, group)">
                  <el-button type="primary" link size="small" :icon="MoreFilled" />
                  <template #dropdown>
                    <el-dropdown-menu>
                      <el-dropdown-item command="export">导出菜单</el-dropdown-item>
                      <el-dropdown-item command="import">导入菜单</el-dropdown-item>
                      <el-dropdown-item command="edit" divided>编辑</el-dropdown-item>
                      <el-dropdown-item v-if="!group.isSystem" command="delete">
                        <span style="color: var(--el-color-danger)">删除</span>
                      </el-dropdown-item>
                    </el-dropdown-menu>
                  </template>
                </el-dropdown>
              </div>
            </div>
          </div>
        </el-scrollbar>
      </div>

      <!-- 右栏：菜单项编辑区 -->
      <div class="menu-items-main">
        <template v-if="currentGroup">
          <div class="items-header">
            <div class="items-header-info">
              <h3>{{ currentGroup.name }}</h3>
              <span class="slug-text">{{ currentGroup.slug }}</span>
              <el-tag v-if="currentGroup.version" size="small" effect="plain">{{ currentGroup.version }}</el-tag>
            </div>
            <div class="items-header-actions">
              <el-button type="primary" size="small" :icon="Plus" @click="handleAddItem()">添加菜单项</el-button>
              <el-button size="small" :icon="Download" @click="handleExportGroup()">导出菜单</el-button>
              <el-button size="small" :icon="Upload" @click="triggerMenuImport">导入菜单</el-button>
              <el-button size="small" :icon="Download" @click="showImportDialog = true">从系统菜单导入</el-button>
            </div>
          </div>
          
          <el-scrollbar class="items-body">
            <div v-if="itemLoading" class="items-loading">
              <el-skeleton :rows="6" animated />
            </div>
            <div v-else-if="items.length === 0" class="items-empty">
              <el-empty description="暂未添加菜单项，点击上方按钮开始添加">
                <el-button type="primary" @click="handleAddItem()">添加第一个菜单项</el-button>
              </el-empty>
            </div>
            <div v-else class="items-tree">
              <!-- 表头 -->
              <div class="tree-table-header">
                <span class="col-title">标题</span>
                <span class="col-link-type">链接类型</span>
                <span class="col-url">链接地址</span>
                <span class="col-target">打开方式</span>
                <span class="col-perm">权限编码</span>
                <span v-if="canSetUniappHome" class="col-uniapp-home">框架首页</span>
                <span class="col-enabled">启用</span>
                <span class="col-actions">操作</span>
              </div>
              <el-tree
                ref="dragTreeRef"
                :data="items"
                node-key="id"
                :props="{ label: 'title', children: 'children' }"
                default-expand-all
                draggable
                :allow-drop="allowDrop"
                @node-drop="handleNodeDrop"
                class="drag-tree"
              >
                <template #default="{ node, data }">
                  <div class="tree-row" :class="{ 'tree-row-disabled': !data.enabled }">
                    <span class="col-title tree-row-title">
                      <el-icon class="drag-handle"><Rank /></el-icon>
                      <el-icon v-if="data.icon" :size="14"><component :is="data.icon" /></el-icon>
                      <img v-else-if="data.image" :src="resolveResourcePath(data.image)" class="item-image" />
                      <span class="item-title">{{ data.title }}</span>
                      <el-tag v-if="data.badge" :type="data.badgeType || 'danger'" size="small" effect="dark" class="item-badge">{{ data.badge }}</el-tag>
                    </span>
                    <span class="col-link-type">
                      <el-tag :type="linkTypeTagType(data.linkType)" size="small" effect="plain">{{ linkTypeText(data.linkType) }}</el-tag>
                    </span>
                    <span class="col-url" :title="data.url || data.refMenuName || ''">
                      <template v-if="data.linkType === 'SystemMenu' && data.refMenuName">
                        <el-icon :size="12"><Link /></el-icon> {{ data.refMenuName }}
                      </template>
                      <template v-else>{{ data.url || '-' }}</template>
                    </span>
                    <span class="col-target">
                      <el-tag size="small" :type="data.target === '_blank' ? 'warning' : 'info'" effect="plain">
                        {{ data.target === '_blank' ? '新窗口' : '当前' }}
                      </el-tag>
                    </span>
                    <span class="col-perm">
                      <code v-if="data.permissionCode" class="perm-code">{{ data.permissionCode }}</code>
                      <span v-else class="text-muted">-</span>
                    </span>
                    <span v-if="canSetUniappHome" class="col-uniapp-home">
                      <el-tooltip
                        :content="data.url ? '设为 UNIAPP 启动后默认打开的首页' : '请先配置链接地址'"
                        placement="top"
                      >
                        <el-switch
                          :model-value="!!data.isUniappHome"
                          size="small"
                          :disabled="!data.url"
                          @click.stop
                          @change="(v: boolean) => toggleUniappHome(data, v)"
                        />
                      </el-tooltip>
                    </span>
                    <span class="col-enabled">
                      <el-switch v-model="data.enabled" size="small" @click.stop @change="(v: boolean) => toggleItemEnabled(data, v)" />
                    </span>
                    <span class="col-actions">
                      <el-button type="primary" link size="small" @click.stop="handleEditItem(data)">编辑</el-button>
                      <el-button type="primary" link size="small" @click.stop="handleAddItem(data.id)">子项</el-button>
                      <el-button type="danger" link size="small" @click.stop="handleDeleteItem(data)">删除</el-button>
                    </span>
                  </div>
                </template>
              </el-tree>
              <div v-if="sortDirty" class="sort-save-bar">
                <span>拖拽排序已更改</span>
                <el-button type="primary" size="small" :loading="sortSaving" @click="saveSortOrder">保存排序</el-button>
                <el-button size="small" @click="cancelSort">撤销</el-button>
              </div>
            </div>
          </el-scrollbar>
        </template>
        <template v-else>
          <div class="no-group-selected">
            <el-empty description="请从左侧选择一个菜单组，或新建菜单组" :image-size="120" />
          </div>
        </template>
      </div>
    </div>

    <!-- 菜单组编辑对话框 -->
    <el-dialog v-model="groupDialogVisible" :title="groupDialogTitle" width="560px" :close-on-click-modal="false">
      <el-form :model="groupForm" :rules="groupFormRules" ref="groupFormRef" label-width="100px">
        <el-form-item label="名称" prop="name">
          <el-input v-model="groupForm.name" placeholder="如：前端导航、页脚链接" />
        </el-form-item>
        <el-form-item label="标识 (Slug)" prop="slug">
          <el-input v-model="groupForm.slug" placeholder="如：frontend-nav, footer（唯一标识，仅允许小写字母、数字和连字符）" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="groupForm.description" type="textarea" :rows="2" placeholder="菜单组用途说明" />
        </el-form-item>
        <el-form-item label="展示位置">
          <el-select v-model="groupForm.location" clearable placeholder="选择或输入展示位置" allow-create filterable style="width: 100%">
            <el-option label="站点头部 (site-header)" value="site-header" />
            <el-option label="站点页脚 (site-footer)" value="site-footer" />
            <el-option label="移动端底栏 (mobile-tabbar)" value="mobile-tabbar" />
            <el-option label="侧边栏 (sidebar)" value="sidebar" />
          </el-select>
        </el-form-item>
        <el-form-item label="适用终端">
          <el-select v-model="groupForm.clientType" clearable placeholder="选择适用终端" allow-create filterable style="width: 100%">
            <el-option label="WEB 管理端" value="WEB_ADMIN" />
            <el-option label="WEB 前台" value="WEB_PORTAL" />
            <el-option label="移动端" value="UNIAPP" />
            <el-option label="桌面端" value="WPF" />
          </el-select>
        </el-form-item>
        <el-form-item label="版本标识">
          <el-input v-model="groupForm.version" placeholder="如：v1, v2, beta" />
        </el-form-item>
        <el-form-item label="最大层级">
          <el-input-number v-model="groupForm.maxDepth" :min="0" :max="10" />
          <span class="form-hint">0 表示不限制嵌套层级</span>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="groupDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="groupSaving" @click="saveGroup">确定</el-button>
      </template>
    </el-dialog>

    <!-- 菜单项编辑对话框 -->
    <el-dialog v-model="itemDialogVisible" :title="itemDialogTitle" width="660px" :close-on-click-modal="false">
      <el-form :model="itemForm" :rules="itemFormRules" ref="itemFormRef" label-width="100px">
        <el-form-item label="标题" prop="title">
          <el-input v-model="itemForm.title" placeholder="显示标题" />
        </el-form-item>
        <el-form-item label="上级菜单项">
          <el-tree-select
            v-model="itemForm.parentId"
            :data="parentItemOptions"
            node-key="id"
            :props="{ label: 'title', children: 'children' }"
            placeholder="无（顶级菜单项）"
            check-strictly
            filterable
            clearable
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="链接类型" prop="linkType">
          <el-radio-group v-model="itemForm.linkType" @change="onLinkTypeChange">
            <el-radio value="Custom">自定义链接</el-radio>
            <el-radio value="SystemMenu">系统菜单</el-radio>
            <el-radio value="External">外部链接</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item v-if="itemForm.linkType === 'SystemMenu'" label="系统菜单">
          <el-tree-select
            v-model="itemForm.refMenuId"
            :data="sysMenuTree"
            node-key="id"
            :props="{ label: 'name', children: 'children' }"
            placeholder="选择系统菜单"
            check-strictly
            filterable
            clearable
            style="width: 100%"
            @change="onSysMenuSelect"
          />
        </el-form-item>
        <el-form-item v-if="itemForm.linkType !== 'SystemMenu'" label="链接地址">
          <el-input v-model="itemForm.url" :placeholder="itemForm.linkType === 'External' ? 'https://...' : '/path'" />
        </el-form-item>
        <el-form-item label="打开方式">
          <el-radio-group v-model="itemForm.target">
            <el-radio value="_self">当前窗口</el-radio>
            <el-radio value="_blank">新窗口</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item label="副标题">
          <el-input v-model="itemForm.subtitle" placeholder="可选的描述文字" />
        </el-form-item>
        <el-form-item label="图标">
          <BootstrapIconPicker v-model="itemForm.icon" />
        </el-form-item>
        <el-form-item label="图片">
          <ResourcePicker v-model="itemForm.image" accept="image/*" placeholder="输入图片URL 或点击附件库选择" />
        </el-form-item>
        <el-form-item label="可见权限">
          <el-select
            v-model="itemForm.permissionCode"
            clearable
            filterable
            placeholder="留空表示所有人可见"
            style="width: 100%"
          >
            <el-option
              v-for="perm in permissionOptions"
              :key="perm.code"
              :value="perm.code"
              :label="`${perm.name}（${perm.code}）`"
            >
              <div class="perm-option">
                <span>{{ perm.name }}</span>
                <code class="perm-option-code">{{ perm.code }}</code>
              </div>
            </el-option>
          </el-select>
          <div class="form-hint-block">仅拥有该权限的角色可看到此菜单项</div>
        </el-form-item>
        <el-form-item label="角标">
          <el-row :gutter="12">
            <el-col :span="12">
              <el-input v-model="itemForm.badge" placeholder="如 New, Hot" />
            </el-col>
            <el-col :span="12">
              <el-select v-model="itemForm.badgeType" clearable placeholder="角标类型">
                <el-option label="主色" value="primary" />
                <el-option label="成功" value="success" />
                <el-option label="警告" value="warning" />
                <el-option label="危险" value="danger" />
                <el-option label="信息" value="info" />
              </el-select>
            </el-col>
          </el-row>
        </el-form-item>
        <el-form-item label="CSS 类名">
          <el-input v-model="itemForm.cssClass" placeholder="自定义样式类" />
        </el-form-item>
        <el-form-item label="排序号">
          <el-input-number v-model="itemForm.order" :min="0" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="itemDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="itemSaving" @click="saveItem">确定</el-button>
      </template>
    </el-dialog>

    <!-- 从系统菜单导入对话框 -->
    <el-dialog v-model="showImportDialog" title="从系统菜单导入" width="560px" :close-on-click-modal="false">
      <p class="import-hint">勾选要导入的系统菜单，将自动创建为当前菜单组的菜单项并关联。</p>
      <el-tree
        ref="importTreeRef"
        :data="sysMenuTree"
        :props="{ label: 'name', children: 'children' }"
        node-key="id"
        show-checkbox
        check-strictly
        default-expand-all
        class="import-tree"
      />
      <template #footer>
        <el-button @click="showImportDialog = false">取消</el-button>
        <el-button type="primary" :loading="importing" @click="handleImport">导入选中项</el-button>
      </template>
    </el-dialog>

    <input
      ref="menuImportFileRef"
      type="file"
      accept="application/json,.json"
      style="display: none"
      @change="onMenuImportFileChange"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, nextTick } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { FormInstance, FormRules } from 'element-plus'
import { Plus, Delete, Download, Upload, MoreFilled, Lock, Link, Rank } from '@element-plus/icons-vue'
import {
  getMenuGroups, getMenuGroupDetail, createMenuGroup, updateMenuGroup, deleteMenuGroup,
  getMenuGroupItems, createMenuGroupItem, updateMenuGroupItem, deleteMenuGroupItem,
  batchDeleteMenuGroupItems, importFromSystemMenu, sortMenuGroupItems,
  setMenuGroupItemUniappHome, exportMenuGroup, importMenuGroup,
  type MenuGroupListItem, type MenuGroupItemNode,
  type CreateMenuGroupInput, type UpdateMenuGroupInput,
  type CreateMenuGroupItemInput, type UpdateMenuGroupItemInput,
  type MenuGroupItemSortInput, type MenuGroupExportPackage
} from '@/api/menuGroup'
import { getAdminMenusTree, type AdminMenuNode } from '@/api/menu'
import BootstrapIconPicker from '@/components/BootstrapIconPicker.vue'
import ResourcePicker from '@/components/ResourcePicker.vue'
import { resolveResourcePath } from '@/utils/resourceUrl'

// ===== 菜单组状态 =====
const groups = ref<MenuGroupListItem[]>([])
const currentGroupId = ref<string>('')
const currentGroup = computed(() => groups.value.find(g => g.id === currentGroupId.value))
/** 仅 default-uniapp 默认 UNIAPP 菜单组可设置框架启动首页 */
const canSetUniappHome = computed(() => {
  const g = currentGroup.value
  if (!g) return false
  return g.isDefault
    && g.slug === 'default-uniapp'
    && (g.clientType || '').split(',').map(s => s.trim().toUpperCase()).includes('UNIAPP')
})
const groupLoading = ref(false)

// ===== 菜单项状态 =====
const items = ref<MenuGroupItemNode[]>([])
const itemLoading = ref(false)
const selectedItemIds = ref<string[]>([])

// ===== 系统菜单树（用于导入和关联选择） =====
const sysMenuTree = ref<AdminMenuNode[]>([])

// ===== 上级菜单项选项（排除自身及子项防止循环） =====
const parentItemOptions = computed(() => {
  if (!editingItemId.value) return items.value
  // 编辑时排除自身及其所有后代
  const excludeIds = new Set<string>()
  function collectIds(nodes: MenuGroupItemNode[], collecting: boolean) {
    for (const n of nodes) {
      if (n.id === editingItemId.value || collecting) {
        excludeIds.add(n.id)
        if (n.children) collectIds(n.children, true)
      } else {
        if (n.children) collectIds(n.children, false)
      }
    }
  }
  collectIds(items.value, false)
  function filterTree(nodes: MenuGroupItemNode[]): MenuGroupItemNode[] {
    return nodes
      .filter(n => !excludeIds.has(n.id))
      .map(n => ({ ...n, children: n.children ? filterTree(n.children) : undefined }))
  }
  return filterTree(items.value)
})

// ===== 权限编码下拉选项（从系统菜单树中提取） =====
interface PermOption { code: string; name: string }
const permissionOptions = computed<PermOption[]>(() => {
  const result: PermOption[] = []
  const seen = new Set<string>()
  function collect(nodes: AdminMenuNode[]) {
    for (const n of nodes) {
      if (n.code && !seen.has(n.code)) {
        seen.add(n.code)
        result.push({ code: n.code, name: n.name })
      }
      if (n.children) collect(n.children)
    }
  }
  collect(sysMenuTree.value)
  return result.sort((a, b) => a.code.localeCompare(b.code))
})

// ===== 菜单组对话框 =====
const groupDialogVisible = ref(false)
const groupDialogTitle = ref('新建菜单组')
const editingGroupId = ref<string>('')
const groupSaving = ref(false)
const groupFormRef = ref<FormInstance>()
const groupForm = reactive<CreateMenuGroupInput & { maxDepth: number; enabled: boolean }>({
  name: '', slug: '', description: '', location: '', clientType: '', version: '', maxDepth: 3, enabled: true
})
const groupFormRules: FormRules = {
  name: [{ required: true, message: '请输入菜单组名称', trigger: 'blur' }],
  slug: [
    { required: true, message: '请输入菜单组标识', trigger: 'blur' },
    { pattern: /^[a-z0-9][a-z0-9-]*$/, message: '仅允许小写字母、数字和连字符', trigger: 'blur' }
  ]
}

// ===== 菜单项对话框 =====
const itemDialogVisible = ref(false)
const itemDialogTitle = ref('添加菜单项')
const editingItemId = ref<string>('')
const itemParentId = ref<string | null>(null)
const itemSaving = ref(false)
const itemFormRef = ref<FormInstance>()
const itemForm = reactive<CreateMenuGroupItemInput & { enabled: boolean; order: number }>({
  title: '', linkType: 'Custom', url: '', target: '_self', parentId: null,
  refMenuId: null, icon: '', image: '', subtitle: '', permissionCode: '',
  cssClass: '', badge: '', badgeType: '', extraData: '', order: 0, enabled: true
})
const itemFormRules: FormRules = {
  title: [{ required: true, message: '请输入标题', trigger: 'blur' }],
  linkType: [{ required: true, message: '请选择链接类型', trigger: 'change' }]
}

// ===== 导入对话框 =====
const showImportDialog = ref(false)
const importTreeRef = ref<any>(null)
const importing = ref(false)

// ===== 拖拽排序 =====
const dragTreeRef = ref<any>(null)
const sortDirty = ref(false)
const sortSaving = ref(false)

function allowDrop(draggingNode: any, dropNode: any, type: string) {
  // 允许前后插入和内部放置（改变父级）
  return true
}

function handleNodeDrop() {
  // 标记排序已变动，等待用户确认保存
  sortDirty.value = true
}

function collectSortData(nodes: MenuGroupItemNode[], parentId: string | null): MenuGroupItemSortInput[] {
  const result: MenuGroupItemSortInput[] = []
  nodes.forEach((node, index) => {
    result.push({ id: node.id, parentId, order: index })
    if (node.children && node.children.length > 0) {
      result.push(...collectSortData(node.children, node.id))
    }
  })
  return result
}

async function saveSortOrder() {
  if (!currentGroupId.value) return
  sortSaving.value = true
  try {
    const sortData = collectSortData(items.value, null)
    await sortMenuGroupItems(currentGroupId.value, sortData)
    sortDirty.value = false
    ElMessage.success('排序保存成功')
    await loadItems()
  } catch (e: any) {
    ElMessage.error(e.message || '保存排序失败')
  } finally {
    sortSaving.value = false
  }
}

async function cancelSort() {
  sortDirty.value = false
  await loadItems()
}

// ===== 加载 =====

onMounted(async () => {
  await loadGroups()
  await loadSysMenuTree()
})

async function loadGroups() {
  groupLoading.value = true
  try {
    groups.value = await getMenuGroups()
    if (groups.value.length > 0 && !currentGroupId.value) {
      selectGroup(groups.value[0])
    }
  } catch (e: any) {
    ElMessage.error('加载菜单组失败: ' + (e.message || '未知错误'))
  } finally {
    groupLoading.value = false
  }
}

async function loadItems() {
  if (!currentGroupId.value) return
  itemLoading.value = true
  try {
    items.value = await getMenuGroupItems(currentGroupId.value)
  } catch (e: any) {
    ElMessage.error('加载菜单项失败: ' + (e.message || '未知错误'))
  } finally {
    itemLoading.value = false
  }
}

async function loadSysMenuTree() {
  try {
    sysMenuTree.value = await getAdminMenusTree()
  } catch { /* 静默 */ }
}

function selectGroup(group: MenuGroupListItem) {
  currentGroupId.value = group.id
  loadItems()
}

// ===== 菜单组操作 =====

function handleAddGroup() {
  editingGroupId.value = ''
  groupDialogTitle.value = '新建菜单组'
  Object.assign(groupForm, { name: '', slug: '', description: '', location: '', clientType: '', version: '', maxDepth: 3, enabled: true })
  groupDialogVisible.value = true
  nextTick(() => groupFormRef.value?.clearValidate())
}

async function handleEditGroup(group: MenuGroupListItem) {
  editingGroupId.value = group.id
  groupDialogTitle.value = '编辑菜单组'
  try {
    const detail = await getMenuGroupDetail(group.id)
    Object.assign(groupForm, {
      name: detail.name, slug: detail.slug, description: detail.description || '',
      location: detail.location || '', clientType: detail.clientType || '',
      version: detail.version || '', maxDepth: detail.maxDepth, enabled: detail.enabled
    })
  } catch {
    Object.assign(groupForm, {
      name: group.name, slug: group.slug, description: group.description || '',
      location: group.location || '', clientType: group.clientType || '',
      version: group.version || '', maxDepth: group.maxDepth, enabled: group.enabled
    })
  }
  groupDialogVisible.value = true
  nextTick(() => groupFormRef.value?.clearValidate())
}

async function saveGroup() {
  const valid = await groupFormRef.value?.validate().catch(() => false)
  if (!valid) return
  groupSaving.value = true
  try {
    if (editingGroupId.value) {
      await updateMenuGroup(editingGroupId.value, {
        name: groupForm.name, slug: groupForm.slug, description: groupForm.description,
        location: groupForm.location, clientType: groupForm.clientType,
        version: groupForm.version, maxDepth: groupForm.maxDepth, enabled: groupForm.enabled
      })
      ElMessage.success('更新成功')
    } else {
      await createMenuGroup({
        name: groupForm.name, slug: groupForm.slug, description: groupForm.description,
        location: groupForm.location, clientType: groupForm.clientType,
        version: groupForm.version, maxDepth: groupForm.maxDepth
      })
      ElMessage.success('创建成功')
    }
    groupDialogVisible.value = false
    await loadGroups()
  } catch (e: any) {
    ElMessage.error(e.message || '操作失败')
  } finally {
    groupSaving.value = false
  }
}

async function toggleGroupEnabled(group: MenuGroupListItem, val: boolean) {
  try {
    await updateMenuGroup(group.id, {
      name: group.name, slug: group.slug, description: group.description,
      location: group.location, clientType: group.clientType,
      version: group.version, maxDepth: group.maxDepth, enabled: val
    })
  } catch {
    group.enabled = !val
  }
}

function handleGroupCommand(cmd: string, group: MenuGroupListItem) {
  if (cmd === 'edit') handleEditGroup(group)
  else if (cmd === 'export') handleExportGroup(group)
  else if (cmd === 'import') handleImportGroup(group)
  else if (cmd === 'delete') handleDeleteGroup(group)
}

const menuImportFileRef = ref<HTMLInputElement | null>(null)
const menuImportTargetGroupId = ref('')

function downloadJsonFile(data: unknown, filename: string) {
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  URL.revokeObjectURL(url)
}

async function handleExportGroup(group?: MenuGroupListItem) {
  const target = group || currentGroup.value
  if (!target) {
    ElMessage.warning('请先选择要导出的菜单组')
    return
  }
  try {
    const pkg = await exportMenuGroup(target.id)
    const slug = pkg.group?.slug || target.slug || 'menu-group'
    const date = new Date().toISOString().slice(0, 10)
    downloadJsonFile(pkg, `menu-group-${slug}-${date}.json`)
    ElMessage.success(`已导出「${pkg.group?.name || target.name}」共 ${pkg.items?.length ?? 0} 项`)
  } catch (e: any) {
    ElMessage.error(e?.message || '导出失败')
  }
}

function handleImportGroup(group?: MenuGroupListItem) {
  const target = group || currentGroup.value
  if (!target) {
    ElMessage.warning('请先选择要导入到的菜单组')
    return
  }
  menuImportTargetGroupId.value = target.id
  if (currentGroupId.value !== target.id) selectGroup(target)
  triggerMenuImport()
}

function triggerMenuImport() {
  if (!currentGroupId.value) {
    ElMessage.warning('请先选择要导入到的菜单组')
    return
  }
  menuImportTargetGroupId.value = currentGroupId.value
  menuImportFileRef.value?.click()
}

function parseMenuImportPackage(raw: unknown): MenuGroupExportPackage {
  const obj = raw as MenuGroupExportPackage
  if (!obj || typeof obj !== 'object' || !obj.group?.slug || !obj.group?.name) {
    throw new Error('文件格式无效：需包含 group.slug 与 group.name')
  }
  if (!Array.isArray(obj.items)) obj.items = []
  if (obj.formatVersion != null && obj.formatVersion !== 1) {
    throw new Error(`不支持的格式版本：${obj.formatVersion}`)
  }
  return obj
}

async function onMenuImportFileChange(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return

  const groupId = menuImportTargetGroupId.value || currentGroupId.value
  if (!groupId) {
    ElMessage.warning('请先选择要导入到的菜单组')
    return
  }

  try {
    const text = await file.text()
    const pkg = parseMenuImportPackage(JSON.parse(text))
    const target = groups.value.find(g => g.id === groupId)
    const slug = pkg.group.slug
    const itemCount = pkg.items.length

    let confirmMsg = `将把 ${itemCount} 个菜单项导入到「${target?.name || '当前菜单组'}」`
    if (target && target.slug !== slug) {
      confirmMsg += `\n\n注意：文件来自「${pkg.group.name}」（${slug}），与当前组（${target.slug}）标识不同，将仅导入菜单项结构。`
    }
    confirmMsg += '\n\n导入将全量替换当前组下已有菜单项，是否继续？'

    await ElMessageBox.confirm(confirmMsg, '导入确认', {
      type: 'warning',
      confirmButtonText: '覆盖导入',
      cancelButtonText: '取消',
    })

    const result = await importMenuGroup(groupId, pkg)
    ElMessage.success(`导入成功：新增 ${result.itemsCreated} 项，替换前删除 ${result.itemsDeleted} 项`)
    if (currentGroupId.value === groupId) await loadItems()
    await loadGroups()
  } catch (err: any) {
    if (err !== 'cancel' && err?.message !== 'cancel') {
      ElMessage.error(err?.message || '导入失败')
    }
  }
}

async function handleDeleteGroup(group: MenuGroupListItem) {
  if (group.isSystem) { ElMessage.warning('系统内置菜单组不可删除'); return }
  try {
    await ElMessageBox.confirm(`确定删除菜单组"${group.name}"？该组下所有菜单项将一并删除。`, '确认', { type: 'warning' })
    await deleteMenuGroup(group.id)
    ElMessage.success('删除成功')
    if (currentGroupId.value === group.id) currentGroupId.value = ''
    await loadGroups()
  } catch { /* 取消 */ }
}

// ===== 菜单项操作 =====

function handleAddItem(parentId?: string) {
  editingItemId.value = ''
  itemParentId.value = parentId || null
  itemDialogTitle.value = parentId ? '添加子菜单项' : '添加菜单项'
  Object.assign(itemForm, {
    title: '', linkType: 'Custom', url: '', target: '_self', parentId: parentId || null,
    refMenuId: null, icon: '', image: '', subtitle: '', permissionCode: '',
    cssClass: '', badge: '', badgeType: '', extraData: '', order: 0, enabled: true
  })
  itemDialogVisible.value = true
  nextTick(() => itemFormRef.value?.clearValidate())
}

async function handleEditItem(row: MenuGroupItemNode) {
  editingItemId.value = row.id
  itemDialogTitle.value = '编辑菜单项'
  Object.assign(itemForm, {
    title: row.title, linkType: row.linkType, url: row.url || '', target: row.target,
    parentId: row.parentId || null, refMenuId: row.refMenuId || null,
    icon: row.icon || '', image: row.image || '', subtitle: row.subtitle || '',
    permissionCode: row.permissionCode || '', cssClass: row.cssClass || '',
    badge: row.badge || '', badgeType: row.badgeType || '', extraData: row.extraData || '',
    order: row.order, enabled: row.enabled
  })
  itemDialogVisible.value = true
  nextTick(() => itemFormRef.value?.clearValidate())
}

async function saveItem() {
  const valid = await itemFormRef.value?.validate().catch(() => false)
  if (!valid) return
  if (!currentGroupId.value) return
  itemSaving.value = true
  try {
    const data: CreateMenuGroupItemInput & { enabled: boolean; order: number } = { ...itemForm }
    if (editingItemId.value) {
      await updateMenuGroupItem(currentGroupId.value, editingItemId.value, data as UpdateMenuGroupItemInput)
      ElMessage.success('更新成功')
    } else {
      await createMenuGroupItem(currentGroupId.value, data)
      ElMessage.success('创建成功')
    }
    itemDialogVisible.value = false
    await loadItems()
    await loadGroups() // 刷新项数量
  } catch (e: any) {
    ElMessage.error(e.message || '操作失败')
  } finally {
    itemSaving.value = false
  }
}

async function handleDeleteItem(row: MenuGroupItemNode) {
  try {
    await ElMessageBox.confirm(`确定删除菜单项"${row.title}"？其子项也将被删除。`, '确认', { type: 'warning' })
    await deleteMenuGroupItem(currentGroupId.value, row.id)
    ElMessage.success('删除成功')
    await loadItems()
    await loadGroups()
  } catch { /* 取消 */ }
}

async function handleBatchDeleteItems() {
  if (selectedItemIds.value.length === 0) return
  try {
    await ElMessageBox.confirm(`确定删除选中的 ${selectedItemIds.value.length} 个菜单项？`, '确认', { type: 'warning' })
    await batchDeleteMenuGroupItems(currentGroupId.value, selectedItemIds.value)
    ElMessage.success('批量删除成功')
    selectedItemIds.value = []
    await loadItems()
    await loadGroups()
  } catch { /* 取消 */ }
}

async function toggleItemEnabled(row: MenuGroupItemNode, val: boolean) {
  try {
    await updateMenuGroupItem(currentGroupId.value, row.id, {
      title: row.title, linkType: row.linkType, url: row.url, target: row.target,
      parentId: row.parentId, refMenuId: row.refMenuId, icon: row.icon, image: row.image,
      subtitle: row.subtitle, permissionCode: row.permissionCode, cssClass: row.cssClass,
      badge: row.badge, badgeType: row.badgeType, extraData: row.extraData,
      order: row.order, enabled: val
    })
  } catch {
    row.enabled = !val
  }
}

async function toggleUniappHome(row: MenuGroupItemNode, val: boolean) {
  if (!currentGroupId.value || !row.url) return
  try {
    await setMenuGroupItemUniappHome(currentGroupId.value, row.id, val)
    ElMessage.success(val ? '已设为 UNIAPP 框架首页' : '已取消 UNIAPP 框架首页')
    await loadItems()
  } catch (e: any) {
    ElMessage.error(e.message || '设置失败')
  }
}

function onItemSelectionChange(rows: MenuGroupItemNode[]) {
  selectedItemIds.value = rows.map(r => r.id)
}

function onLinkTypeChange() {
  if (itemForm.linkType !== 'SystemMenu') {
    itemForm.refMenuId = null
  }
  if (itemForm.linkType === 'SystemMenu') {
    itemForm.url = ''
  }
}

function onSysMenuSelect(id: string) {
  // 从系统菜单树中查找并自动填充标题和链接
  const menu = findMenuById(sysMenuTree.value, id)
  if (menu) {
    if (!itemForm.title) itemForm.title = menu.name
    itemForm.url = menu.route || menu.webRouteUrl || ''
    if (menu.icon) itemForm.icon = menu.icon
  }
}

function findMenuById(tree: AdminMenuNode[], id: string): AdminMenuNode | null {
  for (const node of tree) {
    if (node.id === id) return node
    if (node.children) {
      const found = findMenuById(node.children, id)
      if (found) return found
    }
  }
  return null
}

// ===== 导入 =====

async function handleImport() {
  const checked = importTreeRef.value?.getCheckedKeys() as string[]
  if (!checked || checked.length === 0) { ElMessage.warning('请至少选择一个菜单'); return }
  importing.value = true
  try {
    await importFromSystemMenu(currentGroupId.value, checked)
    ElMessage.success(`成功导入 ${checked.length} 个菜单项`)
    showImportDialog.value = false
    await loadItems()
    await loadGroups()
  } catch (e: any) {
    ElMessage.error(e.message || '导入失败')
  } finally {
    importing.value = false
  }
}

// ===== 辅助 =====

function linkTypeText(type: string) {
  switch (type) {
    case 'SystemMenu': return '系统菜单'
    case 'External': return '外部链接'
    default: return '自定义'
  }
}

function linkTypeTagType(type: string) {
  switch (type) {
    case 'SystemMenu': return 'warning'
    case 'External': return 'danger'
    default: return 'primary'
  }
}
</script>

<style scoped>
.menu-groups-page {
  height: 100%;
}

.menu-groups-layout {
  display: flex;
  gap: 16px;
  height: calc(100vh - 130px);
  min-height: 500px;
}

/* 左栏 */
.menu-groups-sidebar {
  width: 320px;
  min-width: 280px;
  background: var(--el-bg-color);
  border-radius: 8px;
  border: 1px solid var(--el-border-color-lighter);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.sidebar-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 16px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.sidebar-header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.sidebar-body {
  flex: 1;
  overflow: hidden;
}

.sidebar-loading, .sidebar-empty {
  padding: 24px 16px;
}

.group-list {
  padding: 8px;
}

.group-card {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding: 12px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  margin-bottom: 4px;
  border: 1px solid var(--el-border-color-lighter);
  background: var(--el-bg-color);
}

.group-card:hover {
  background: var(--el-fill-color-light);
  border-color: var(--el-border-color);
}

.group-card.active {
  background: var(--el-color-primary-light-9);
  border-color: var(--el-color-primary);
  box-shadow: 0 0 0 1px var(--el-color-primary-light-5);
}

.group-card-main {
  flex: 1;
  min-width: 0;
}

.group-card-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-bottom: 6px;
}

.group-card-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.item-count {
  font-size: 12px;
  color: var(--el-text-color-regular);
}

.group-card-info {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
}

.info-tag {
  font-size: 11px;
  color: var(--el-text-color-secondary);
  background: var(--el-fill-color-light);
  padding: 2px 8px;
  border-radius: 3px;
  border: 1px solid var(--el-border-color-extra-light);
}

.group-card-actions {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
  margin-left: 8px;
}

/* 右栏 */
.menu-items-main {
  flex: 1;
  min-width: 0;
  background: var(--el-bg-color);
  border-radius: 8px;
  border: 1px solid var(--el-border-color-lighter);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.items-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 16px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  flex-wrap: wrap;
  gap: 8px;
}

.items-header-info {
  display: flex;
  align-items: center;
  gap: 8px;
}

.items-header-info h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.slug-text {
  font-size: 12px;
  color: var(--el-text-color-placeholder);
  font-family: monospace;
}

.items-header-actions {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.items-body {
  flex: 1;
  overflow: hidden;
}

.items-loading, .items-empty {
  padding: 48px 24px;
}

.items-tree {
  padding: 12px;
}

/* 树形拖拽行样式 - 列宽定义 */
.col-title { flex: 1; min-width: 180px; }
.col-link-type { width: 90px; flex-shrink: 0; text-align: center; }
.col-url { width: 180px; flex-shrink: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.col-target { width: 70px; flex-shrink: 0; text-align: center; }
.col-perm { width: 110px; flex-shrink: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.col-uniapp-home { width: 72px; flex-shrink: 0; text-align: center; }
.col-enabled { width: 60px; flex-shrink: 0; text-align: center; }
.col-actions { width: 140px; flex-shrink: 0; text-align: center; }

.tree-table-header {
  display: flex;
  align-items: center;
  padding: 8px 30px 8px 24px;
  background: var(--el-fill-color-lighter);
  border-radius: 4px;
  margin-bottom: 4px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-secondary);
  gap: 8px;
}

/* el-tree 拖拽样式 */
.drag-tree :deep(.el-tree-node__content) {
  height: auto;
  min-height: 42px;
  padding: 4px 8px 4px 0 !important;
  border-radius: 4px;
  transition: background 0.15s;
}

/* 斑马纹：顶层奇偶行交替色 */
.drag-tree :deep(.el-tree-node:nth-child(odd) > .el-tree-node__content) {
  background: var(--el-fill-color-extra-light);
}

.drag-tree :deep(.el-tree-node:nth-child(even) > .el-tree-node__content) {
  background: var(--el-bg-color);
}

/* 子节点略淡 */
.drag-tree :deep(.el-tree-node .el-tree-node .el-tree-node__content) {
  background: var(--el-fill-color-blank) !important;
}

.drag-tree :deep(.el-tree-node .el-tree-node:nth-child(odd) .el-tree-node__content) {
  background: var(--el-fill-color-extra-light) !important;
}

/* 悬停高亮 */
.drag-tree :deep(.el-tree-node__content:hover) {
  background: var(--el-color-primary-light-9) !important;
}

.drag-tree :deep(.el-tree-node__expand-icon) {
  font-size: 14px;
  padding: 4px;
}

/* 行间分割线 */
.drag-tree :deep(.el-tree-node) {
  border-bottom: 1px solid var(--el-border-color-extra-light);
}

.drag-tree :deep(.el-tree-node:last-child) {
  border-bottom: none;
}

.tree-row {
  display: flex;
  align-items: center;
  flex: 1;
  gap: 8px;
  font-size: 13px;
  min-width: 0;
}

.tree-row-disabled {
  opacity: 0.5;
}

.tree-row-title {
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 0;
}

.drag-handle {
  cursor: grab;
  color: var(--el-text-color-placeholder);
  font-size: 14px;
  flex-shrink: 0;
}

.drag-handle:hover {
  color: var(--el-color-primary);
}

.drag-handle:active {
  cursor: grabbing;
}

.drag-tree :deep(.el-tree-node.is-drop-inner > .el-tree-node__content) {
  background-color: var(--el-color-primary-light-9);
  border: 1px dashed var(--el-color-primary);
  border-radius: 4px;
}

/* 排序保存栏 */
.sort-save-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 16px;
  margin-top: 8px;
  background: var(--el-color-warning-light-9);
  border: 1px solid var(--el-color-warning-light-5);
  border-radius: 6px;
  font-size: 13px;
  color: var(--el-color-warning-dark-2);
}

.no-group-selected {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

/* 菜单项表格 */
.item-title-cell {
  display: inline-flex;
  vertical-align: middle;
  align-items: center;
  gap: 6px;
  min-width: 0;
}

.item-image {
  width: 20px;
  height: 20px;
  border-radius: 3px;
  object-fit: cover;
}

.item-title {
  font-weight: 500;
}

.item-badge {
  margin-left: 4px;
}

.ref-menu-text {
  display: flex;
  align-items: center;
  gap: 4px;
  color: var(--el-color-warning);
  font-size: 13px;
}

.perm-code {
  font-size: 12px;
  padding: 2px 6px;
  background: var(--el-fill-color);
  border-radius: 3px;
  color: var(--el-text-color-regular);
}

.text-muted {
  color: var(--el-text-color-placeholder);
}

/* 对话框 */
.form-hint {
  margin-left: 8px;
  font-size: 12px;
  color: var(--el-text-color-placeholder);
}

.form-hint-block {
  width: 100%;
  margin-top: 4px;
  font-size: 12px;
  color: var(--el-text-color-placeholder);
  line-height: 1.4;
}

/* 权限选项样式 */
.perm-option {
  display: flex;
  justify-content: space-between;
  align-items: center;
  width: 100%;
}

.perm-option-code {
  font-size: 11px;
  color: var(--el-text-color-placeholder);
  font-family: monospace;
  background: var(--el-fill-color);
  padding: 1px 6px;
  border-radius: 3px;
}

.import-hint {
  margin: 0 0 12px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.import-tree {
  max-height: 400px;
  overflow-y: auto;
}

/* 图片选择 */
.image-picker-wrap {
  width: 100%;
}

.image-input-row {
  display: flex;
  gap: 8px;
  width: 100%;
}

.image-preview {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 8px;
}

.preview-img {
  width: 48px;
  height: 48px;
  border-radius: 4px;
  border: 1px solid var(--el-border-color-lighter);
  flex-shrink: 0;
}

.preview-url {
  font-size: 12px;
  color: var(--el-text-color-placeholder);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
