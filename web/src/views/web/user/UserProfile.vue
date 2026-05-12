<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="user-profile">
    <div class="container">
      <div class="user-center-layout">
        <!-- 侧边导航 -->
        <WebUserSidebar :user-info="userInfo" @logout="handleLogout" />

        <!-- 主要内容区域 -->
        <main class="main-content">
          <div class="content-header">
            <h1>{{ t('profile_title') }}</h1>
            <p>{{ t('profile_subtitle') }}</p>
          </div>

          <!-- 个人信息表单 -->
          <div class="profile-form-section">
            <div class="section-header">
              <div class="section-icon section-icon-blue">
                <el-icon><User /></el-icon>
              </div>
              <div>
                <h2>{{ t('profile_basic') }}</h2>
                <p>{{ t('profile_basic_desc') }}</p>
              </div>
            </div>

            <el-form :model="profileForm" :rules="profileRules" ref="profileFormRef" label-width="100px" class="profile-form">
              <!-- 头像上传 -->
              <el-form-item :label="t('profile_avatar')">
                <div class="avatar-upload">
                  <div class="current-avatar">
                    <img v-if="profileForm.avatar" :src="resolveResourcePath(profileForm.avatar)" alt="头像" />
                    <div v-else class="avatar-placeholder">
                      <el-icon><User /></el-icon>
                    </div>
                  </div>
                  <div class="upload-actions">
                    <ResourcePicker v-model="profileForm.avatar" accept="image/*" placeholder="头像 URL" />
                    <p class="upload-tip">{{ t('profile_avatar_tip') }}</p>
                  </div>
                </div>
              </el-form-item>

              <el-form-item :label="t('profile_username')" prop="userName">
                <el-input v-model="profileForm.userName" disabled class="disabled-input">
                  <template #suffix>
                    <el-tooltip :content="t('profile_username_tip')" placement="top">
                      <el-icon><InfoFilled /></el-icon>
                    </el-tooltip>
                  </template>
                </el-input>
              </el-form-item>

              <el-form-item :label="t('profile_name')" prop="name">
                <el-input v-model="profileForm.name" :placeholder="t('profile_name_ph')" />
              </el-form-item>

              <el-form-item :label="t('profile_email')" prop="email">
                <el-input v-model="profileForm.email" :placeholder="t('profile_email_ph')" />
              </el-form-item>
              <!-- 邮箱验证码（邮箱变更时需要） -->
              <el-form-item v-if="needEmailVerify && emailChanged" label="邮箱验证码" prop="emailCode" class="verify-code-item">
                <div class="verify-code-row">
                  <el-input v-model="profileForm.emailCode" placeholder="请输入邮箱验证码" maxlength="6" class="verify-code-input" />
                  <el-button :disabled="emailCooldown > 0 || !profileForm.email" @click="sendEmailCode" class="verify-code-btn">
                    {{ emailCooldown > 0 ? `${emailCooldown}s` : '发送验证码' }}
                  </el-button>
                </div>
                <p class="verify-tip">邮箱已变更，验证码将发送至新邮箱</p>
              </el-form-item>

              <el-form-item :label="t('profile_phone')" prop="phone">
                <el-input v-model="profileForm.phone" :placeholder="t('profile_phone_ph')" />
              </el-form-item>
              <!-- 手机验证码（手机变更时需要） -->
              <el-form-item v-if="needPhoneVerify && phoneChanged" label="手机验证码" prop="phoneCode" class="verify-code-item">
                <div class="verify-code-row">
                  <el-input v-model="profileForm.phoneCode" placeholder="请输入手机验证码" maxlength="6" class="verify-code-input" />
                  <el-button :disabled="phoneCooldown > 0 || !profileForm.phone" @click="sendPhoneCode" class="verify-code-btn">
                    {{ phoneCooldown > 0 ? `${phoneCooldown}s` : '发送验证码' }}
                  </el-button>
                </div>
                <p class="verify-tip">手机号已变更，验证码将发送至新手机号</p>
              </el-form-item>

              <el-form-item :label="t('profile_bio')">
                <el-input
                  v-model="profileForm.bio"
                  type="textarea"
                  :rows="4"
                  :placeholder="t('profile_bio_ph')"
                  maxlength="200"
                  show-word-limit
                />
              </el-form-item>

              <el-form-item>
                <el-button type="primary" @click="handleSaveProfile" :loading="saving">
                  {{ t('profile_save') }}
                </el-button>
                <el-button @click="handleResetProfile">{{ t('profile_reset') }}</el-button>
              </el-form-item>
            </el-form>
          </div>

          <!-- 密码修改 -->
          <div class="password-section">
            <div class="section-header">
              <div class="section-icon section-icon-orange">
                <el-icon><Lock /></el-icon>
              </div>
              <div>
                <h2>{{ t('profile_chpwd') }}</h2>
                <p>{{ t('profile_chpwd_desc') }}</p>
              </div>
            </div>

            <el-form :model="passwordForm" :rules="passwordRules" ref="passwordFormRef" label-width="100px" class="password-form">
              <el-form-item :label="t('profile_cur_pwd')" prop="currentPassword">
                <el-input
                  v-model="passwordForm.currentPassword"
                  type="password"
                  :placeholder="t('profile_cur_pwd_ph')"
                  show-password
                />
              </el-form-item>

              <el-form-item :label="t('profile_new_pwd')" prop="newPassword">
                <el-input
                  v-model="passwordForm.newPassword"
                  type="password"
                  :placeholder="t('profile_new_pwd_ph')"
                  show-password
                />
              </el-form-item>

              <el-form-item :label="t('profile_confirm_pwd')" prop="confirmPassword">
                <el-input
                  v-model="passwordForm.confirmPassword"
                  type="password"
                  :placeholder="t('profile_confirm_pwd_ph')"
                  show-password
                />
              </el-form-item>

              <el-form-item>
                <el-button type="primary" @click="handleChangePassword" :loading="changingPassword">
                  {{ t('profile_chpwd') }}
                </el-button>
                <el-button @click="handleResetPassword">{{ t('profile_reset') }}</el-button>
              </el-form-item>
            </el-form>
          </div>


        </main>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  User, InfoFilled, Lock
} from '@element-plus/icons-vue'
import { useWebAuthStore } from '../../../stores/webAuth'
import { getCurrentUser, updateProfile, changePassword } from '../../../api/user'
import ResourcePicker from '../../../components/ResourcePicker.vue'
import { resolveResourcePath } from '../../../utils/resourceUrl'
import { t } from '@/utils/lang'
import { useSystemStore } from '../../../stores/system'
import { sendVerificationCode } from '../../../api/auth'

const router = useRouter()
const webAuth = useWebAuthStore()
const system = useSystemStore()
const userInfo = computed(() => webAuth.userInfo)
const profileFormRef = ref()
const passwordFormRef = ref()
const saving = ref(false)
const changingPassword = ref(false)

// 个人信息表单（需要在 computed/函数之前声明）
const profileForm = reactive({
  userName: '',
  name: '',
  email: '',
  phone: '',
  bio: '',
  avatar: '',
  emailCode: '',
  phoneCode: ''
})

// ---------- 注册模式 & 验证码逻辑 ----------
const regMode = computed(() => system.registrationMode || 'free')
const needEmailVerify = computed(() => ['email_code', 'both_code'].includes(regMode.value))
const needPhoneVerify = computed(() => ['phone_code', 'both_code'].includes(regMode.value))

// 记录服务端原始值，用于判断是否发生变更
const originalEmail = ref('')
const originalPhone = ref('')
const emailChanged = computed(() =>
  needEmailVerify.value && profileForm.email.trim() !== '' && profileForm.email.trim().toLowerCase() !== originalEmail.value.toLowerCase()
)
const phoneChanged = computed(() =>
  needPhoneVerify.value && profileForm.phone.trim() !== '' && profileForm.phone.trim() !== originalPhone.value
)

// 验证码倒计时
const emailCooldown = ref(0)
const phoneCooldown = ref(0)
let emailTimer: ReturnType<typeof setInterval> | null = null
let phoneTimer: ReturnType<typeof setInterval> | null = null

function startCooldown(type: 'email' | 'phone', seconds: number) {
  const cooldown = type === 'email' ? emailCooldown : phoneCooldown
  cooldown.value = seconds
  const timer = setInterval(() => {
    cooldown.value--
    if (cooldown.value <= 0) clearInterval(timer)
  }, 1000)
  if (type === 'email') emailTimer = timer
  else phoneTimer = timer
}

function sendEmailCode() {
  if (!profileForm.email || emailCooldown.value > 0) return
  startCooldown('email', 60)
  // purpose=3 (BindEmail), channel=0 (Email)
  sendVerificationCode({ target: profileForm.email.trim(), purpose: 3, channel: 0 })
    .then(res => ElMessage.success(res.message || '验证码已发送'))
    .catch((e: any) => ElMessage.error(e?.message || '发送失败'))
}

function sendPhoneCode() {
  if (!profileForm.phone || phoneCooldown.value > 0) return
  startCooldown('phone', 60)
  // purpose=4 (BindPhone), channel=1 (SMS)
  sendVerificationCode({ target: profileForm.phone.trim(), purpose: 4, channel: 1 })
    .then(res => ElMessage.success(res.message || '验证码已发送'))
    .catch((e: any) => ElMessage.error(e?.message || '发送失败'))
}

onUnmounted(() => {
  if (emailTimer) clearInterval(emailTimer)
  if (phoneTimer) clearInterval(phoneTimer)
})

// 密码修改表单
const passwordForm = reactive({
  currentPassword: '',
  newPassword: '',
  confirmPassword: ''
})



// 表单验证规则
import WebUserSidebar from './components/WebUserSidebar.vue'

const profileRules = {
  name: [
    { required: true, message: t('v_name_required'), trigger: 'blur' },
    { min: 2, max: 20, message: t('v_name_length'), trigger: 'blur' }
  ],
  email: [
    { type: 'email', message: t('v_email_format'), trigger: 'blur' }
  ],
  phone: [
    { pattern: /^1[3-9]\d{9}$/, message: t('v_phone_format'), trigger: 'blur' }
  ]
}

const passwordRules = {
  currentPassword: [
    { required: true, message: t('v_cur_pwd_required'), trigger: 'blur' }
  ],
  newPassword: [
    { required: true, message: t('v_new_pwd_required'), trigger: 'blur' },
    { min: 6, max: 20, message: t('v_pwd_length'), trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: '请确认新密码', trigger: 'blur' },
    {
      validator: (rule: any, value: string, callback: Function) => {
        if (value !== passwordForm.newPassword) {
          callback(new Error(t('v_pwd_mismatch')))
        } else {
          callback()
        }
      },
      trigger: 'blur'
    }
  ]
}



// 从服务端加载最新个人资料
const loadProfile = async () => {
  try {
    const data = await getCurrentUser()
    profileForm.userName = data.userName
    profileForm.name = (data as any).displayName || ''
    profileForm.email = (data as any).email || ''
    profileForm.phone = (data as any).phone || ''
    // 记录原始值用于变更检测
    originalEmail.value = profileForm.email
    originalPhone.value = profileForm.phone
    // 清空验证码
    profileForm.emailCode = ''
    profileForm.phoneCode = ''
    // 从服务端数据同步 bio(introduction) 与 avatar
    profileForm.bio = (data as any).introduction || ''
    profileForm.avatar = (data as any).avatar || ''
  } catch (e) {
    ElMessage.error(t('profile_load_fail'))
  }
}

// 头像通过 ResourcePicker 组件选择，v-model 自动写入 profileForm.avatar

// 保存个人信息
const handleSaveProfile = async () => {
  try {
    await profileFormRef.value?.validate()
    saving.value = true

    // 验证码前端校验
    if (needEmailVerify.value && emailChanged.value && !profileForm.emailCode.trim()) {
      ElMessage.warning('邮箱已变更，请输入邮箱验证码')
      saving.value = false
      return
    }
    if (needPhoneVerify.value && phoneChanged.value && !profileForm.phoneCode.trim()) {
      ElMessage.warning('手机号已变更，请输入手机验证码')
      saving.value = false
      return
    }

    const payload: any = { displayName: profileForm.name }
    if (typeof profileForm.email === 'string' && profileForm.email.trim() !== '') payload.email = profileForm.email
    if (typeof profileForm.phone === 'string' && profileForm.phone.trim() !== '') payload.phone = profileForm.phone
    if (typeof profileForm.avatar === 'string' && profileForm.avatar.trim() !== '') payload.avatar = profileForm.avatar
    if (typeof profileForm.bio === 'string' && profileForm.bio.trim() !== '') payload.introduction = profileForm.bio
    // 携带验证码
    if (emailChanged.value && profileForm.emailCode.trim()) payload.emailCode = profileForm.emailCode.trim()
    if (phoneChanged.value && profileForm.phoneCode.trim()) payload.phoneCode = profileForm.phoneCode.trim()

    await updateProfile(payload)

    // 同步到本地存储（尽量以服务端数据为准，这里仅做本地展示用）
    webAuth.updateUserInfo({
      name: profileForm.name,
      email: payload.email,
      phone: payload.phone,
      bio: profileForm.bio,
      avatar: profileForm.avatar
    })

    ElMessage.success(t('profile_save_ok'))
    // 重新加载以确保与服务端一致
    await loadProfile()
  } catch (e) {
    ElMessage.error((e as any)?.message || t('profile_save_fail'))
  } finally {
    saving.value = false
  }
}

// 重置个人信息表单
const handleResetProfile = async () => {
  await loadProfile()
  ElMessage.info(t('profile_reset_ok'))
}

// 修改密码（调用后端 /api/v1/users/me/password）
const handleChangePassword = async () => {
  try {
    await passwordFormRef.value?.validate()
    changingPassword.value = true

    await changePassword({
      oldPassword: passwordForm.currentPassword,
      newPassword: passwordForm.newPassword,
    })

    ElMessage.success(t('profile_chpwd_ok'))

    // 清空表单
    passwordForm.currentPassword = ''
    passwordForm.newPassword = ''
    passwordForm.confirmPassword = ''
  } catch (e: any) {
    ElMessage.error(e?.message || t('profile_chpwd_fail'))
  } finally {
    changingPassword.value = false
  }
}

// 重置密码表单
const handleResetPassword = () => {
  passwordForm.currentPassword = ''
  passwordForm.newPassword = ''
  passwordForm.confirmPassword = ''
}



// 处理退出登录
const handleLogout = async () => {
  try {
    await ElMessageBox.confirm(t('confirm_logout'), t('tip'), {
      confirmButtonText: t('confirm'),
      cancelButtonText: t('cancel'),
      type: 'warning'
    })

    webAuth.logout()
    ElMessage.success(t('logged_out'))
    router.push('/web')
  } catch (e) {
    // 用户取消退出
  }
}

onMounted(async () => {
  webAuth.initFromStorage()
  if (!webAuth.isAuthenticated) {
    router.push('/web/login')
    return
  }
  // 确保系统配置已加载（获取注册模式）
  if (!system.loaded) await system.loadPublicConfig()
  loadProfile()
})
</script>

<style scoped>
.user-profile {
  min-height: calc(100vh - 4rem - 200px);
  background: #f8fafc;
  padding: 2rem 0;
}

.container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 0 1rem;
}

.user-center-layout {
  display: grid;
  grid-template-columns: 280px 1fr;
  gap: 2rem;
  align-items: start;
}

/* 主要内容区域 - 统一白色卡片容器 */
.main-content {
  background: white;
  border-radius: 16px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.06);
  padding: 2rem;
  display: flex;
  flex-direction: column;
  gap: 0;
}

.content-header {
  margin-bottom: 2rem;
}

.content-header h1 {
  font-size: 1.75rem;
  font-weight: 700;
  color: #1e293b;
  margin: 0 0 0.5rem 0;
}

.content-header p {
  color: #64748b;
  margin: 0;
  font-size: 1rem;
}

/* 表单区域 */
.profile-form-section,
.password-section {
  padding: 1.5rem 0;
  border-top: 1px solid #f1f5f9;
}

.profile-form-section:first-of-type {
  border-top: none;
}

.section-header {
  margin-bottom: 1.5rem;
  display: flex;
  align-items: center;
  gap: 1rem;
}

.section-icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.15rem;
  color: white;
  flex-shrink: 0;
}

.section-icon-blue {
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  box-shadow: 0 4px 12px rgba(59,130,246,.25);
}

.section-icon-orange {
  background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
  box-shadow: 0 4px 12px rgba(249,115,22,.25);
}

.section-header h2 {
  font-size: 1.2rem;
  font-weight: 600;
  color: #1e293b;
  margin: 0 0 0.25rem 0;
}

.section-header p {
  color: #64748b;
  margin: 0;
  font-size: .875rem;
}

/* 头像上传 */
.avatar-upload {
  display: flex;
  align-items: center;
  gap: 1.5rem;
}

.current-avatar {
  width: 80px;
  height: 80px;
  border-radius: 50%;
  overflow: hidden;
  background: #f3f4f6;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 3px solid #e5e7eb;
  transition: border-color .2s;
}

.current-avatar:hover {
  border-color: #3b82f6;
}

.current-avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.current-avatar .avatar-placeholder {
  color: #9ca3af;
  font-size: 2rem;
}

.upload-actions {
  flex: 1;
}

.upload-tip {
  margin: 0.5rem 0 0 0;
  color: #6b7280;
  font-size: 0.875rem;
}

/* 表单样式 */
.profile-form :deep(.el-form-item__label),
.password-form :deep(.el-form-item__label) {
  font-weight: 500;
  color: #374151;
}

.disabled-input :deep(.el-input__wrapper) {
  background: #f9fafb;
  cursor: not-allowed;
}



/* 响应式设计 */
@media (max-width: 1024px) {
  .user-center-layout {
    grid-template-columns: 1fr;
    gap: 1.5rem;
  }
}

@media (max-width: 768px) {
  .user-profile {
    padding: 1rem 0;
  }

  .main-content {
    padding: 1.5rem;
  }

  .avatar-upload {
    flex-direction: column;
    align-items: center;
    text-align: center;
  }

  .setting-item {
    flex-direction: column;
    align-items: flex-start;
    gap: 1rem;
  }
}

/* 验证码行 */
.verify-code-item {
  margin-top: -8px;
}

.verify-code-row {
  display: flex;
  gap: 8px;
  width: 100%;
}

.verify-code-input {
  flex: 1;
}

.verify-code-btn {
  flex-shrink: 0;
  min-width: 110px;
}

.verify-tip {
  margin: 4px 0 0 0;
  font-size: 0.8rem;
  color: #f59e0b;
  line-height: 1.4;
}
</style>