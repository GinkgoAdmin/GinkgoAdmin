<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="web-register-page">
    <div class="auth-scene">
      <!-- 背景装饰 -->
      <div class="scene-bg">
        <CommonHeroBg />
      </div>

      <div class="auth-container">
        <!-- 左侧：品牌口号 + 动画 -->
        <div class="auth-brand">
          <div class="brand-content">
            <div class="brand-logo">
              <svg viewBox="0 0 20 20" fill="currentColor"><path d="M10 2L3 7v11h4v-6h6v6h4V7l-7-5z" /></svg>
            </div>
            <h1 class="brand-title">GinkgoAdmin</h1>
            <p class="brand-slogan">加入我们，开启精彩旅程</p>
            <p class="brand-desc">创建您的专属账户，即刻体验<br>强大的企业级管理能力。</p>
            <div class="brand-features">
              <div class="brand-feat"><i class="ri-shield-check-line"></i><span>企业级安全</span></div>
              <div class="brand-feat"><i class="ri-speed-line"></i><span>极致性能</span></div>
              <div class="brand-feat"><i class="ri-plug-line"></i><span>插件化架构</span></div>
            </div>
          </div>
          <!-- 动画装饰 -->
          <div class="brand-anim">
            <div class="anim-ring anim-ring--1"></div>
            <div class="anim-ring anim-ring--2"></div>
            <div class="anim-ring anim-ring--3"></div>
            <div class="anim-dot anim-dot--1"></div>
            <div class="anim-dot anim-dot--2"></div>
            <div class="anim-dot anim-dot--3"></div>
            <div class="anim-dot anim-dot--4"></div>
          </div>
        </div>

        <!-- 右侧：注册表单 -->
        <div class="auth-card">
          <h2 class="card-title">创建账户</h2>
          <p class="card-desc">填写以下信息完成注册</p>

          <div v-if="regMode === 'disabled'" class="disabled-notice">
            <i class="ri-error-warning-line"></i>
            <p>当前已关闭注册，请联系管理员或稍后再试。</p>
          </div>

          <el-form v-else :model="form" :rules="rules" ref="formRef" label-width="0" class="auth-form">
            <!-- 用户名（仅自由注册模式显示，邮箱/手机模式自动用邮箱/手机作为帐户） -->
            <el-form-item v-if="showUserName" prop="userName">
              <el-input v-model="form.userName" placeholder="请输入用户名" clearable size="large" class="auth-input">
                <template #prefix>
                  <el-icon class="input-icon"><User /></el-icon>
                </template>
              </el-input>
            </el-form-item>

            <!-- 邮箱（email / email_code / both_code 模式必填，free 模式可选） -->
            <el-form-item v-if="showEmail" prop="email">
              <el-input v-model="form.email" :placeholder="needEmail ? '请输入邮箱地址' : '请输入邮箱地址（可选）'" clearable size="large" class="auth-input">
                <template #prefix>
                  <el-icon class="input-icon"><Message /></el-icon>
                </template>
              </el-input>
            </el-form-item>

            <!-- 邮箱验证码 -->
            <el-form-item v-if="needEmailCode" prop="emailCode">
              <div class="code-input-row">
                <el-input v-model="form.emailCode" placeholder="邮箱验证码" clearable size="large" class="auth-input" maxlength="6" />
                <button type="button" class="send-code-btn" :disabled="emailCooldown > 0 || !form.email || emailLocked" @click="sendEmailCode">
                  {{ emailBtnText }}
                </button>
              </div>
            </el-form-item>

            <!-- 手机号（phone / phone_code / both_code 模式必填） -->
            <el-form-item v-if="showPhone" prop="phone">
              <el-input v-model="form.phone" placeholder="请输入手机号" clearable size="large" class="auth-input">
                <template #prefix>
                  <el-icon class="input-icon"><Iphone /></el-icon>
                </template>
              </el-input>
            </el-form-item>

            <!-- 手机验证码 -->
            <el-form-item v-if="needPhoneCode" prop="phoneCode">
              <div class="code-input-row">
                <el-input v-model="form.phoneCode" placeholder="短信验证码" clearable size="large" class="auth-input" maxlength="6" />
                <button type="button" class="send-code-btn" :disabled="phoneCooldown > 0 || !form.phone || phoneLocked" @click="sendPhoneCode">
                  {{ phoneBtnText }}
                </button>
              </div>
            </el-form-item>

            <!-- 密码 -->
            <el-form-item prop="password">
              <el-input v-model="form.password" type="password" placeholder="请输入密码（至少8位）" show-password size="large" class="auth-input">
                <template #prefix>
                  <el-icon class="input-icon"><Lock /></el-icon>
                </template>
              </el-input>
            </el-form-item>

            <!-- 确认密码 -->
            <el-form-item prop="confirmPassword">
              <el-input v-model="form.confirmPassword" type="password" placeholder="请再次输入密码" show-password size="large"
                class="auth-input" @keyup.enter="onSubmit">
                <template #prefix>
                  <el-icon class="input-icon"><Lock /></el-icon>
                </template>
              </el-input>
            </el-form-item>

            <el-form-item class="submit-section">
              <button type="button" class="auth-btn" :disabled="loading" @click="onSubmit">
                <i v-if="loading" class="ri-loader-4-line auth-btn-spin"></i>
                <span v-if="!loading">创建账户</span>
                <span v-else>创建中...</span>
              </button>
            </el-form-item>
          </el-form>

          <div class="auth-divider"><span>或</span></div>

          <div class="auth-footer">
            <span>已有账户？</span>
            <router-link to="/web/login" class="auth-link">立即登录</router-link>
          </div>

          <p class="auth-trust">
            <i class="ri-shield-check-line"></i>
            采用企业级安全架构，您的数据受到严格保护
          </p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, computed, onUnmounted } from 'vue'
import CommonHeroBg from './components/CommonHeroBg.vue'
import { ElMessage } from 'element-plus'
import { useRouter } from 'vue-router'
import { User, Message, Lock, Iphone } from '@element-plus/icons-vue'
import { register, sendVerificationCode } from '../../../api/auth'
import { useSystemStore } from '../../../stores/system'

const router = useRouter()
const system = useSystemStore()

// ---------- 注册模式 ----------
const regMode = computed(() => system.registrationMode || 'free')
const showEmail = computed(() => ['email_code', 'both_code'].includes(regMode.value))
const showPhone = computed(() => ['phone_code', 'both_code'].includes(regMode.value))
const needEmail = computed(() => ['email_code', 'both_code'].includes(regMode.value))
const needPhone = computed(() => ['phone_code', 'both_code'].includes(regMode.value))
const needEmailCode = computed(() => ['email_code', 'both_code'].includes(regMode.value))
const needPhoneCode = computed(() => ['phone_code', 'both_code'].includes(regMode.value))
const showUserName = computed(() => regMode.value === 'free')

// ---------- 表单 ----------
const form = reactive({
  userName: '',
  email: '',
  phone: '',
  emailCode: '',
  phoneCode: '',
  password: '',
  confirmPassword: ''
})
const formRef = ref()
const loading = ref(false)

// ---------- 验证码倒计时 + 发送次数限制（3次/30分钟锁定） ----------
const MAX_SEND_COUNT = 3
const LOCK_SECONDS = 30 * 60 // 30 分钟

const emailCooldown = ref(0)
const phoneCooldown = ref(0)
const emailSendCount = ref(0)
const phoneSendCount = ref(0)
const emailLocked = ref(false)
const phoneLocked = ref(false)
const emailLockRemain = ref(0)
const phoneLockRemain = ref(0)
let emailTimer: ReturnType<typeof setInterval> | null = null
let phoneTimer: ReturnType<typeof setInterval> | null = null
let emailLockTimer: ReturnType<typeof setInterval> | null = null
let phoneLockTimer: ReturnType<typeof setInterval> | null = null

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

function startLock(type: 'email' | 'phone') {
  const locked = type === 'email' ? emailLocked : phoneLocked
  const remain = type === 'email' ? emailLockRemain : phoneLockRemain
  locked.value = true
  remain.value = LOCK_SECONDS
  const timer = setInterval(() => {
    remain.value--
    if (remain.value <= 0) {
      clearInterval(timer)
      locked.value = false
      if (type === 'email') emailSendCount.value = 0
      else phoneSendCount.value = 0
    }
  }, 1000)
  if (type === 'email') emailLockTimer = timer
  else phoneLockTimer = timer
}

const emailBtnText = computed(() => {
  if (emailLocked.value) {
    const m = Math.floor(emailLockRemain.value / 60)
    const s = emailLockRemain.value % 60
    return `${m}:${s.toString().padStart(2, '0')} 后可发送`
  }
  if (emailCooldown.value > 0) return `${emailCooldown.value}s`
  return emailSendCount.value > 0 ? `发送验证码(${MAX_SEND_COUNT - emailSendCount.value})` : '发送验证码'
})

const phoneBtnText = computed(() => {
  if (phoneLocked.value) {
    const m = Math.floor(phoneLockRemain.value / 60)
    const s = phoneLockRemain.value % 60
    return `${m}:${s.toString().padStart(2, '0')} 后可发送`
  }
  if (phoneCooldown.value > 0) return `${phoneCooldown.value}s`
  return phoneSendCount.value > 0 ? `发送验证码(${MAX_SEND_COUNT - phoneSendCount.value})` : '发送验证码'
})

onUnmounted(() => {
  if (emailTimer) clearInterval(emailTimer)
  if (phoneTimer) clearInterval(phoneTimer)
  if (emailLockTimer) clearInterval(emailLockTimer)
  if (phoneLockTimer) clearInterval(phoneLockTimer)
})

function sendEmailCode() {
  if (!form.email || emailCooldown.value > 0 || emailLocked.value) return
  if (emailSendCount.value >= MAX_SEND_COUNT) {
    startLock('email')
    ElMessage.warning('已达到最大发送次数，请 30 分钟后再试')
    return
  }
  emailSendCount.value++
  startCooldown('email', 60)
  if (emailSendCount.value >= MAX_SEND_COUNT) {
    startLock('email')
  }
  sendVerificationCode({ target: form.email.trim(), purpose: 2, channel: 0 })
    .then(res => ElMessage.success(res.message || '验证码已发送'))
    .catch((e: any) => ElMessage.error(e?.message || '发送失败'))
}

function sendPhoneCode() {
  if (!form.phone || phoneCooldown.value > 0 || phoneLocked.value) return
  if (phoneSendCount.value >= MAX_SEND_COUNT) {
    startLock('phone')
    ElMessage.warning('已达到最大发送次数，请 30 分钟后再试')
    return
  }
  phoneSendCount.value++
  startCooldown('phone', 60)
  if (phoneSendCount.value >= MAX_SEND_COUNT) {
    startLock('phone')
  }
  sendVerificationCode({ target: form.phone.trim(), purpose: 2, channel: 1 })
    .then(res => ElMessage.success(res.message || '验证码已发送'))
    .catch((e: any) => ElMessage.error(e?.message || '发送失败'))
}

// ---------- 动态验证规则 ----------
const rules = computed(() => ({
  userName: showUserName.value ? [
    { required: true, message: '请输入用户名', trigger: 'blur' },
    { min: 2, max: 64, message: '用户名长度应为 2-64 个字符', trigger: 'blur' },
    { pattern: /^[a-zA-Z0-9_\u4e00-\u9fa5]+$/, message: '用户名只能包含字母、数字、下划线和中文', trigger: 'blur' }
  ] : [],
  email: [
    ...(needEmail.value ? [{ required: true, message: '请输入邮箱地址', trigger: 'blur' }] : []),
    { type: 'email' as const, message: '请输入正确的邮箱格式', trigger: 'blur' }
  ],
  phone: [
    ...(needPhone.value ? [{ required: true, message: '请输入手机号', trigger: 'blur' }] : []),
    { pattern: /^1[3-9]\d{9}$/, message: '请输入正确的手机号', trigger: 'blur' }
  ],
  emailCode: needEmailCode.value ? [{ required: true, message: '请输入邮箱验证码', trigger: 'blur' }] : [],
  phoneCode: needPhoneCode.value ? [{ required: true, message: '请输入手机验证码', trigger: 'blur' }] : [],
  password: [
    { required: true, message: '请输入密码', trigger: 'blur' },
    { min: 8, message: '密码至少需要 8 个字符', trigger: 'blur' },
    { pattern: /^(?=.*[A-Za-z])(?=.*\d)/, message: '密码必须包含至少一个字母和一个数字', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: '请再次输入密码', trigger: 'blur' },
    {
      validator: (_: any, value: string, cb: any) => {
        if (value !== form.password) cb(new Error('两次输入的密码不一致'))
        else cb()
      }, trigger: 'blur'
    }
  ]
}))

async function onSubmit() {
  try {
    if (regMode.value === 'disabled') {
      ElMessage.warning('当前已关闭注册')
      return
    }
    await formRef.value?.validate()
    loading.value = true

    const autoUserName = showUserName.value ? form.userName : (form.email || form.phone || '')
    await register({
      userName: autoUserName,
      displayName: autoUserName,
      email: form.email || undefined,
      phone: form.phone || undefined,
      password: form.password,
      confirmPassword: form.confirmPassword,
      emailCode: form.emailCode || undefined,
      phoneCode: form.phoneCode || undefined,
    })

    ElMessage.success('注册成功！请登录您的账户')
    router.replace('/web/login')
  } catch (e: any) {
    if (e?.message) {
      ElMessage.error(e.message)
    } else if (e?.response?.data?.message) {
      ElMessage.error(e.response.data.message)
    } else {
      ElMessage.error('注册失败，请稍后重试')
    }
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
/* ===== 页面变量 ===== */
.web-register-page {
  --ab: #f0f4f8;
  --as: #ffffff;
  --as2: #f1f5f9;
  --ab2: rgba(0,0,0,0.06);
  --at: #0f172a;
  --at2: #475569;
  --at3: #94a3b8;
  --ap: #3b82f6;
  --apl: #60a5fa;
}
:global(.dark) .web-register-page {
  --ab: #0b0f1a;
  --as: #141926;
  --as2: #1c2235;
  --ab2: rgba(255,255,255,0.06);
  --at: #f1f5f9;
  --at2: #94a3b8;
  --at3: #64748b;
  --ap: #60a5fa;
  --apl: #93bbfd;
}

/* ===== 全屏场景 ===== */
.auth-scene {
  position: relative;
  min-height: calc(100vh - 4rem);
  display: flex; align-items: center; justify-content: center;
  padding: 5rem 1.5rem 4rem;
  background: linear-gradient(160deg, #0f172a 0%, #1e293b 50%, #0f172a 100%);
  overflow: hidden;
}
.scene-bg { position: absolute; inset: 0; pointer-events: none; }
.auth-container {
  position: relative; z-index: 2;
  display: flex; align-items: stretch; gap: 4rem;
  max-width: 1040px; width: 100%;
}
.auth-brand {
  flex: 1; position: relative;
  display: flex; flex-direction: column; justify-content: center;
  min-height: 480px;
}
.brand-content { position: relative; z-index: 2; }
.brand-logo {
  width: 64px; height: 64px; border-radius: 18px;
  background: linear-gradient(135deg, #3b82f6, #2563eb);
  display: flex; align-items: center; justify-content: center;
  box-shadow: 0 8px 32px rgba(59,130,246,0.35);
  margin-bottom: 1.75rem; animation: logoFloat 6s ease-in-out infinite;
}
.brand-logo svg { width: 32px; height: 32px; color: #fff; }
@keyframes logoFloat { 0%, 100% { transform: translateY(0); } 50% { transform: translateY(-8px); } }
.brand-title {
  font-size: 2.5rem; font-weight: 800; color: #fff;
  letter-spacing: -0.5px; margin: 0 0 0.5rem;
  background: linear-gradient(135deg, #ffffff 30%, #93c5fd 100%);
  -webkit-background-clip: text; -webkit-text-fill-color: transparent; background-clip: text;
}
.brand-slogan { font-size: 1.35rem; font-weight: 600; color: rgba(255,255,255,0.85); margin: 0 0 0.75rem; line-height: 1.5; }
.brand-desc { font-size: 0.95rem; color: rgba(255,255,255,0.45); margin: 0 0 2rem; line-height: 1.7; }
.brand-features { display: flex; flex-direction: column; gap: 0.75rem; }
.brand-feat { display: flex; align-items: center; gap: 0.6rem; font-size: 0.9rem; color: rgba(255,255,255,0.65); }
.brand-feat i {
  font-size: 1.15rem; color: #60a5fa; width: 32px; height: 32px;
  display: flex; align-items: center; justify-content: center;
  border-radius: 8px; background: rgba(59,130,246,0.12); flex-shrink: 0;
}
.brand-anim { position: absolute; inset: 0; pointer-events: none; z-index: 1; }
.anim-ring { position: absolute; border-radius: 50%; border: 1.5px solid rgba(59,130,246,0.12); }
.anim-ring--1 { width: 320px; height: 320px; top: 50%; left: 50%; transform: translate(-50%, -50%); animation: ringPulse 8s ease-in-out infinite; }
.anim-ring--2 { width: 240px; height: 240px; top: 50%; left: 50%; transform: translate(-50%, -50%); border-color: rgba(139,92,246,0.1); animation: ringPulse 8s ease-in-out 2s infinite; }
.anim-ring--3 { width: 160px; height: 160px; top: 50%; left: 50%; transform: translate(-50%, -50%); border-color: rgba(6,182,212,0.1); animation: ringPulse 8s ease-in-out 4s infinite; }
@keyframes ringPulse { 0%, 100% { opacity: 0.4; transform: translate(-50%, -50%) scale(1); } 50% { opacity: 0.8; transform: translate(-50%, -50%) scale(1.08); } }
.anim-dot { position: absolute; border-radius: 50%; background: rgba(59,130,246,0.6); box-shadow: 0 0 12px rgba(59,130,246,0.4); }
.anim-dot--1 { width: 6px; height: 6px; top: 15%; left: 70%; animation: dotDrift1 12s linear infinite; }
.anim-dot--2 { width: 4px; height: 4px; top: 75%; left: 20%; background: rgba(139,92,246,0.6); animation: dotDrift2 15s linear infinite; }
.anim-dot--3 { width: 5px; height: 5px; top: 35%; left: 85%; background: rgba(6,182,212,0.5); animation: dotDrift3 10s linear infinite; }
.anim-dot--4 { width: 4px; height: 4px; top: 60%; left: 65%; animation: dotDrift4 14s linear infinite; }
@keyframes dotDrift1 { 0% { transform: translate(0,0); opacity: 0; } 10% { opacity: 1; } 90% { opacity: 1; } 100% { transform: translate(-60px,80px); opacity: 0; } }
@keyframes dotDrift2 { 0% { transform: translate(0,0); opacity: 0; } 10% { opacity: 1; } 90% { opacity: 1; } 100% { transform: translate(50px,-70px); opacity: 0; } }
@keyframes dotDrift3 { 0% { transform: translate(0,0); opacity: 0; } 10% { opacity: 1; } 90% { opacity: 1; } 100% { transform: translate(-40px,60px); opacity: 0; } }
@keyframes dotDrift4 { 0% { transform: translate(0,0); opacity: 0; } 10% { opacity: 1; } 90% { opacity: 1; } 100% { transform: translate(30px,-50px); opacity: 0; } }
.auth-card {
  width: 420px; flex-shrink: 0; background: var(--as);
  border-radius: 24px; padding: 2.75rem 2.5rem;
  box-shadow: 0 20px 60px rgba(0,0,0,0.25), 0 4px 12px rgba(0,0,0,0.1);
  border: 1px solid rgba(255,255,255,0.08); animation: cardSlideIn 0.6s ease;
}
:global(.dark) .auth-card { box-shadow: 0 20px 60px rgba(0,0,0,0.5), 0 4px 12px rgba(0,0,0,0.2); border-color: rgba(255,255,255,0.06); }
@keyframes cardSlideIn { from { opacity: 0; transform: translateX(30px); } to { opacity: 1; transform: translateX(0); } }
.card-title { font-size: 1.5rem; font-weight: 700; color: var(--at); margin: 0 0 0.35rem; }
.card-desc { font-size: 0.9rem; color: var(--at3); margin: 0 0 1.75rem; }
.disabled-notice {
  display: flex; align-items: center; gap: 0.75rem; padding: 1.25rem;
  background: rgba(245,158,11,0.08); border-radius: 14px; border: 1px solid rgba(245,158,11,0.15);
}
.disabled-notice i { font-size: 1.5rem; color: #f59e0b; flex-shrink: 0; }
.disabled-notice p { color: #92400e; margin: 0; font-size: 0.9rem; font-weight: 500; }
:global(.dark) .disabled-notice { background: rgba(245,158,11,0.06); }
:global(.dark) .disabled-notice p { color: #fbbf24; }
.auth-form { display: flex; flex-direction: column; gap: 1rem; }
.auth-input :deep(.el-input__wrapper) {
  border-radius: 12px; border: 1.5px solid var(--ab2);
  transition: all 0.25s; padding: 13px 16px; background: var(--as2); box-shadow: none;
}
.auth-input :deep(.el-input__wrapper:hover) { border-color: rgba(59,130,246,0.35); background: var(--as); box-shadow: 0 0 0 3px rgba(59,130,246,0.06); }
.auth-input :deep(.el-input__wrapper.is-focus) { border-color: var(--ap); background: var(--as); box-shadow: 0 0 0 3px rgba(59,130,246,0.12); }
.input-icon { color: var(--at3); margin-right: 6px; }
.submit-section { margin: 0.25rem 0 0; }
.code-input-row { display: flex; gap: 10px; width: 100%; align-items: stretch; }
.code-input-row .auth-input { flex: 1; }
.send-code-btn {
  flex-shrink: 0; padding: 0 22px; border-radius: 12px;
  border: none; background: linear-gradient(135deg, #3b82f6, #2563eb); color: #fff;
  font-size: 0.88rem; font-weight: 600; cursor: pointer; white-space: nowrap;
  transition: all 0.25s; letter-spacing: 0.3px; box-shadow: 0 2px 8px rgba(37,99,235,0.25);
}
.send-code-btn:hover:not(:disabled) { background: linear-gradient(135deg, #2563eb, #1d4ed8); box-shadow: 0 4px 14px rgba(37,99,235,0.35); transform: translateY(-1px); }
.send-code-btn:active:not(:disabled) { transform: translateY(0); box-shadow: 0 1px 4px rgba(37,99,235,0.2); }
.send-code-btn:disabled { opacity: 0.55; cursor: not-allowed; background: linear-gradient(135deg, #94a3b8, #64748b); box-shadow: none; }
.auth-btn {
  width: 100%; height: 50px; border-radius: 12px; border: none; cursor: pointer;
  font-size: 0.95rem; font-weight: 600; letter-spacing: 0.3px;
  display: inline-flex; align-items: center; justify-content: center; gap: 0.5rem;
  transition: all 0.3s; position: relative; overflow: hidden;
  background: linear-gradient(135deg, #3b82f6, #2563eb); color: #fff;
  box-shadow: 0 4px 16px rgba(59,130,246,0.3);
}
.auth-btn::after { content: ''; position: absolute; inset: 0; background: linear-gradient(90deg, transparent, rgba(255,255,255,0.15), transparent); transform: translateX(-100%); }
.auth-btn:hover:not(:disabled) { transform: translateY(-2px); box-shadow: 0 8px 28px rgba(59,130,246,0.45); }
.auth-btn:hover:not(:disabled)::after { animation: shine 0.7s ease; }
.auth-btn:active:not(:disabled) { transform: translateY(0); }
.auth-btn:disabled { opacity: 0.55; cursor: not-allowed; }
@keyframes shine { to { transform: translateX(100%); } }
.auth-btn-spin { animation: spin 0.8s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
.auth-divider { display: flex; align-items: center; gap: 1rem; margin: 1.5rem 0; color: var(--at3); font-size: 0.8rem; }
.auth-divider::before, .auth-divider::after { content: ''; flex: 1; height: 1px; background: var(--ab2); }
.auth-footer { text-align: center; font-size: 0.88rem; color: var(--at3); }
.auth-link { color: var(--ap); text-decoration: none; font-weight: 600; margin-left: 4px; transition: color 0.2s; }
.auth-link:hover { color: var(--apl); }
.auth-trust { display: flex; align-items: center; justify-content: center; gap: 6px; margin-top: 1.5rem; font-size: 0.78rem; color: var(--at3); }
.auth-trust i { color: #10b981; font-size: 0.95rem; }
:deep(.el-form-item.is-error .el-input__wrapper) { border-color: #ef4444 !important; box-shadow: 0 0 0 3px rgba(239,68,68,0.08) !important; }
:deep(.el-form-item__error) { font-size: 0.78rem; color: #ef4444; margin-top: 4px; }
@media (max-width: 900px) {
  .auth-container { flex-direction: column; align-items: center; gap: 2.5rem; }
  .auth-brand { min-height: auto; text-align: center; align-items: center; }
  .brand-features { flex-direction: row; flex-wrap: wrap; justify-content: center; }
  .brand-desc br { display: none; }
  .brand-anim { display: none; }
  .auth-card { width: 100%; max-width: 440px; }
}
@media (max-width: 480px) {
  .auth-scene { padding: 3.5rem 1rem 3rem; }
  .auth-card { padding: 2rem 1.5rem; border-radius: 18px; }
  .brand-title { font-size: 1.75rem; }
  .brand-slogan { font-size: 1.1rem; }
  .brand-features { gap: 0.5rem; }
  .brand-feat { font-size: 0.82rem; }
}
</style>
