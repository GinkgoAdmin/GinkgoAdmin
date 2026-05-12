<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="page-container">
    <div class="page-header">
      <div class="page-title">
        <h1>个人资料</h1>
        <p>管理您的个人信息和账户设置</p>
      </div>
    </div>

    <el-row :gutter="24">
      <!-- 左侧：个人信息 -->
      <el-col :xs="24" :lg="16">
        <el-card class="profile-card">
          <template #header>
            <div class="card-header">
              <h3>基本信息</h3>
            </div>
          </template>

          <el-form :model="profileForm" :rules="profileRules" ref="profileFormRef" label-width="100px">
            <el-form-item label="用户名">
              <el-input v-model="userInfo.userName" disabled />
            </el-form-item>

            <el-form-item label="显示名称" prop="displayName">
              <el-input v-model="profileForm.displayName" placeholder="请输入显示名称" />
            </el-form-item>

            <el-form-item label="邮箱" prop="email">
              <el-input v-model="profileForm.email" placeholder="请输入邮箱地址" />
            </el-form-item>

            <el-form-item label="手机号" prop="phone">
              <el-input v-model="profileForm.phone" placeholder="请输入手机号" />
            </el-form-item>


            <el-form-item label="个人简介" prop="introduction">
              <el-input
                v-model="profileForm.introduction"
                type="textarea"
                :rows="4"
                maxlength="1000"
                show-word-limit
                placeholder="请输入个人简介"
              />
            </el-form-item>

            <el-form-item label="创建时间">
              <el-input v-model="userInfo.createdAt" disabled />
            </el-form-item>

            <el-form-item>
              <el-button type="primary" @click="handleUpdateProfile" :loading="updating">
                保存修改
              </el-button>
              <el-button @click="resetForm">
                重置
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>
      </el-col>

      <!-- 右侧：头像和快捷操作 -->
      <el-col :xs="24" :lg="8">
        <el-card class="avatar-card">
          <div class="avatar-section">
            <el-avatar :size="120" class="user-avatar" :src="resolveResourcePath(profileForm.avatar || userInfo.avatar || '')">
              {{ userInfo.displayName?.charAt(0) || userInfo.userName?.charAt(0) || 'U' }}
            </el-avatar>
            <div style="margin-top:12px;">
              <ResourcePicker v-model="profileForm.avatar" accept="image/*" placeholder="头像 URL" />
              <div style="color:#6b7280;font-size:12px;margin-top:6px;">支持 JPG、PNG 格式，建议 2MB 以内</div>
            </div>
            <h3 style="margin-top:12px;">{{ userInfo.displayName || userInfo.userName }}</h3>
            <p class="user-status">
              <el-tag :type="userInfo.enabled ? 'success' : 'danger'" size="small">
                {{ userInfo.enabled ? '正常' : '禁用' }}
              </el-tag>
            </p>
          </div>

          <div class="quick-actions">
            <el-button type="primary" :icon="Key" @click="showChangePasswordDialog = true" block>
              修改密码
            </el-button>
            <el-button :icon="Bell" @click="goToNotifications" block>
              我的通知
              <el-badge :value="unreadCount" :hidden="unreadCount === 0" class="notification-badge" />
            </el-button>
            <el-button :icon="Document" @click="goToLogs" block>
              我的日志
            </el-button>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 修改密码对话框 -->
    <el-dialog v-model="showChangePasswordDialog" title="修改密码" width="400px">
      <el-form :model="passwordForm" :rules="passwordRules" ref="passwordFormRef" label-width="80px">
        <el-form-item label="旧密码" prop="oldPassword">
          <el-input v-model="passwordForm.oldPassword" type="password" show-password />
        </el-form-item>

        <el-form-item label="新密码" prop="newPassword">
          <el-input v-model="passwordForm.newPassword" type="password" show-password />
        </el-form-item>

        <el-form-item label="确认密码" prop="confirmPassword">
          <el-input v-model="passwordForm.confirmPassword" type="password" show-password />
        </el-form-item>
      </el-form>

      <template #footer>
        <el-button @click="showChangePasswordDialog = false">取消</el-button>
        <el-button type="primary" @click="handleChangePassword" :loading="changingPassword">
          确认修改
        </el-button>
      </template>
    </el-dialog>

  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Key, Bell, Document } from '@element-plus/icons-vue'
import {
  getCurrentUser,
  updateProfile,
  changePassword,
  getUnreadNotificationCount,
  type UserInfo,
  type UpdateProfileInput,
  type ChangePasswordInput
} from '../../../api/user'
import ResourcePicker from '../../../components/ResourcePicker.vue'
import { resolveResourcePath } from '../../../utils/resourceUrl'


const router = useRouter()

// 用户信息
const userInfo = ref<UserInfo>({
  id: '',
  userName: '',
  displayName: '',
  email: '',
  phone: '',
  enabled: true,
  createdAt: ''
})

// 个人资料表单
const profileForm = reactive<UpdateProfileInput>({
  displayName: '',
  avatar: '',
  introduction: '',
  email: '',
  phone: ''
})

const profileFormRef = ref()
const updating = ref(false)

// 头像通过 ResourcePicker 组件选择，v-model 自动写入 profileForm.avatar

// 修改密码
const showChangePasswordDialog = ref(false)
const passwordForm = reactive<ChangePasswordInput & { confirmPassword: string }>({
  oldPassword: '',
  newPassword: '',
  confirmPassword: ''
})
const passwordFormRef = ref()
const changingPassword = ref(false)

// 未读通知数量
const unreadCount = ref(0)

// 表单验证规则
const profileRules = {
  displayName: [
    { required: true, message: '请输入显示名称', trigger: 'blur' }
  ],
  email: [
    { type: 'email', message: '请输入正确的邮箱地址', trigger: 'blur' }
  ],
  introduction: [
    { max: 1000, message: '个人简介最多1000字', trigger: 'blur' }
  ]
}

const passwordRules = {
  oldPassword: [
    { required: true, message: '请输入旧密码', trigger: 'blur' }
  ],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 6, message: '密码长度不能少于6位', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: '请确认新密码', trigger: 'blur' },
    {
      validator: (rule: any, value: string, callback: Function) => {
        if (value !== passwordForm.newPassword) {
          callback(new Error('两次输入的密码不一致'))
        } else {
          callback()
        }
      },
      trigger: 'blur'
    }
  ]
}

// 加载用户信息
async function loadUserInfo() {
  try {
    userInfo.value = await getCurrentUser()
    // 同步到表单
    profileForm.displayName = userInfo.value.displayName
    profileForm.avatar = userInfo.value.avatar || ''
    profileForm.introduction = userInfo.value.introduction || ''
    profileForm.email = userInfo.value.email || ''
    profileForm.phone = userInfo.value.phone || ''
  } catch (error) {
    // 设置默认值以防API失败
    userInfo.value = {
      id: '1',
      userName: 'demo',
      displayName: '演示用户',
      email: 'demo@example.com',
      phone: '13800138000',
      enabled: true,
      createdAt: '2024-01-01 00:00:00'
    }
    profileForm.displayName = userInfo.value.displayName
    profileForm.avatar = userInfo.value.avatar || ''
    profileForm.introduction = userInfo.value.introduction || ''
    profileForm.email = userInfo.value.email || ''
    profileForm.phone = userInfo.value.phone || ''
    ElMessage.warning('使用演示数据，请检查API连接')
  }
}

// 加载未读通知数量
async function loadUnreadCount() {
  try {
    unreadCount.value = await getUnreadNotificationCount()
  } catch (error) {
    // silently ignored
  }
}

// 更新个人资料
async function handleUpdateProfile() {
  try {
    await profileFormRef.value?.validate()
    updating.value = true
    await updateProfile(profileForm)
    ElMessage.success('个人资料更新成功')
    await loadUserInfo() // 重新加载用户信息
  } catch (error: any) {
    if (error?.message) {
      ElMessage.error(error.message)
    }
  } finally {
    updating.value = false
  }
}

// 重置表单
function resetForm() {
  profileForm.displayName = userInfo.value.displayName
  profileForm.avatar = userInfo.value.avatar || ''
  profileForm.introduction = userInfo.value.introduction || ''
  profileForm.email = userInfo.value.email || ''
  profileForm.phone = userInfo.value.phone || ''
}

// 修改密码
async function handleChangePassword() {
  try {
    await passwordFormRef.value?.validate()
    changingPassword.value = true
    await changePassword({
      oldPassword: passwordForm.oldPassword,
      newPassword: passwordForm.newPassword
    })
    ElMessage.success('密码修改成功')
    showChangePasswordDialog.value = false
    // 重置表单
    passwordForm.oldPassword = ''
    passwordForm.newPassword = ''
    passwordForm.confirmPassword = ''
  } catch (error: any) {
    if (error?.message) {
      ElMessage.error(error.message)
    }
  } finally {
    changingPassword.value = false
  }
}

// 跳转到通知页面
function goToNotifications() {
  router.push('/admin/user/notifications')
}

// 跳转到日志页面
function goToLogs() {
  router.push('/admin/user/logs')
}

onMounted(() => {
  loadUserInfo()
  loadUnreadCount()
})
</script>

<style scoped>
.page-container {
  padding: 0;
}

.page-header {
  margin-bottom: 24px;
}

.page-title h1 {
  font-size: 24px;
  font-weight: 600;
  color: #1f2937;
  margin: 0 0 4px 0;
}

.page-title p {
  font-size: 14px;
  color: #6b7280;
  margin: 0;
}

.profile-card,
.avatar-card {
  border-radius: 12px;
  border: 1px solid #e5e7eb;
  margin-bottom: 24px;
}

.card-header h3 {
  font-size: 18px;
  font-weight: 600;
  color: #1f2937;
  margin: 0;
}

.avatar-section {
  text-align: center;
  padding: 24px 0;
  border-bottom: 1px solid #f3f4f6;
  margin-bottom: 24px;
}

.user-avatar {
  background: linear-gradient(135deg, #3b82f6, #2563eb);
  color: white;
  font-weight: 600;
  font-size: 48px;
  margin-bottom: 16px;
}

.avatar-section h3 {
  font-size: 20px;
  font-weight: 600;
  color: #1f2937;
  margin: 0 0 8px 0;
}

.user-status {
  margin: 0;
}

.quick-actions {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.notification-badge {
  margin-left: 8px;
}

/* 深色模式 */
.dark .page-title h1 {
  color: #f9fafb;
}

.dark .page-title p {
  color: #9ca3af;
}

.dark .profile-card,
.dark .avatar-card {
  background: #1f2937;
  border-color: #374151;
}

.dark .card-header h3 {
  color: #f9fafb;
}

.dark .avatar-section {
  border-bottom-color: #374151;
}

.dark .avatar-section h3 {
  color: #f9fafb;
}
</style>