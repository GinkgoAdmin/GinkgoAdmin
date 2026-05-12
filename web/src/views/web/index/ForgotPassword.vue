<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="web-forgot-page">
    <div class="auth-scene">
      <div class="scene-bg">
        <CommonHeroBg />
      </div>

      <div class="auth-container">
        <!-- 左侧：品牌 + 动画 -->
        <div class="auth-brand">
          <div class="brand-content">
            <div class="brand-logo">
              <svg viewBox="0 0 20 20" fill="currentColor"><path d="M10 2L3 7v11h4v-6h6v6h4V7l-7-5z" /></svg>
            </div>
            <h1 class="brand-title">GinkgoAdmin</h1>
            <p class="brand-slogan">找回您的密码</p>
            <p class="brand-desc">我们将通过您绑定的邮箱发送验证码，<br>帮助您安全地重置密码。</p>
            <div class="brand-features">
              <div class="brand-feat"><i class="ri-mail-send-line"></i><span>邮箱验证码验证</span></div>
              <div class="brand-feat"><i class="ri-timer-line"></i><span>验证码15分钟有效</span></div>
              <div class="brand-feat"><i class="ri-shield-check-line"></i><span>端到端安全加密</span></div>
            </div>
          </div>
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

        <!-- 右侧：找回密码表单 -->
        <div class="auth-card">
          <!-- 步骤指示器 -->
          <div class="step-indicator">
            <div :class="['step-dot', { active: step >= 0, done: step > 0 }]">
              <span class="step-num">{{ step > 0 ? '✓' : '1' }}</span>
            </div>
            <div :class="['step-line', { active: step >= 1 }]"></div>
            <div :class="['step-dot', { active: step >= 1, done: step > 1 }]">
              <span class="step-num">{{ step > 1 ? '✓' : '2' }}</span>
            </div>
            <div :class="['step-line', { active: step >= 2 }]"></div>
            <div :class="['step-dot', { active: step >= 2 }]">
              <span class="step-num">3</span>
            </div>
          </div>
          <div class="step-labels">
            <span :class="{ active: step === 0 }">验证账户</span>
            <span :class="{ active: step === 1 }">输入验证码</span>
            <span :class="{ active: step === 2 }">重置密码</span>
          </div>

          <!-- Step 0: 输入账户 -->
          <div v-if="step === 0" class="step-panel">
            <h2 class="card-title">验证您的账户</h2>
            <p class="card-desc">请输入您的邮箱地址或用户名</p>

            <el-form :model="accountForm" :rules="accountRules" ref="accountRef" label-width="0" class="auth-form">
              <el-form-item prop="account">
                <el-input v-model="accountForm.account" placeholder="邮箱地址或用户名" clearable size="large" class="auth-input"
                  @keyup.enter="onCheckAccount">
                  <template #prefix>
                    <el-icon class="input-icon"><User /></el-icon>
                  </template>
                </el-input>
              </el-form-item>

              <el-form-item class="submit-section">
                <button type="button" class="auth-btn" :disabled="loading" @click="onCheckAccount">
                  <i v-if="loading" class="ri-loader-4-line auth-btn-spin"></i>
                  <span v-if="!loading">下一步</span>
                  <span v-else>验证中...</span>
                </button>
              </el-form-item>
            </el-form>

            <div v-if="noContactInfo" class="no-contact-notice">
              <i class="ri-error-warning-line"></i>
              <div>
                <p class="notice-title">无法自助找回密码</p>
                <p class="notice-desc">该账户未绑定邮箱或手机号，请联系系统管理员重置密码。</p>
              </div>
            </div>
          </div>

          <!-- Step 1: 选择渠道 + 发送验证码 + 输入验证码 -->
          <div v-if="step === 1" class="step-panel">
            <h2 class="card-title">输入验证码</h2>

            <div class="channel-options">
              <div v-if="contactInfo.hasEmail" :class="['channel-card', { selected: channel === 'email' }]"
                @click="channel = 'email'">
                <i class="ri-mail-line"></i>
                <div>
                  <p class="ch-label">邮箱验证</p>
                  <p class="ch-value">{{ contactInfo.maskedEmail }}</p>
                </div>
              </div>
              <div v-if="contactInfo.hasPhone" :class="['channel-card', { selected: channel === 'phone' }]"
                @click="channel = 'phone'">
                <i class="ri-smartphone-line"></i>
                <div>
                  <p class="ch-label">手机验证</p>
                  <p class="ch-value">{{ contactInfo.maskedPhone }}</p>
                </div>
              </div>
            </div>

            <button v-if="!codeSent" type="button" class="auth-btn auth-btn--outline" :disabled="loading" @click="onSendCode">
              <i v-if="loading" class="ri-loader-4-line auth-btn-spin"></i>
              <span v-if="!loading"><i class="ri-send-plane-line"></i> 发送验证码</span>
              <span v-else>发送中...</span>
            </button>

            <div v-if="codeSent" class="code-sent-tip">
              <i class="ri-checkbox-circle-line"></i>
              <span>验证码已发送至 {{ channel === 'email' ? contactInfo.maskedEmail : contactInfo.maskedPhone }}</span>
            </div>

            <div v-if="codeSent" class="code-input-group">
              <input v-for="(_, idx) in codeDigits" :key="idx" ref="codeInputRefs"
                v-model="codeDigits[idx]" type="text" inputmode="numeric" maxlength="1"
                class="code-cell" @input="onCodeInput(idx)" @keydown="onCodeKeydown(idx, $event)"
                @paste="onCodePaste($event)" />
            </div>

            <div v-if="codeSent" class="resend-row">
              <span class="resend-text">没有收到？</span>
              <button v-if="countdown <= 0" type="button" class="resend-btn" @click="onSendCode">重新发送</button>
              <span v-else class="resend-timer">{{ countdown }}s 后可重发</span>
            </div>

            <div v-if="codeSent" class="submit-section">
              <button type="button" class="auth-btn" :disabled="loading || codeValue.length < 6" @click="onVerifyCode">
                <i v-if="loading" class="ri-loader-4-line auth-btn-spin"></i>
                <span v-if="!loading">验证</span>
                <span v-else>验证中...</span>
              </button>
            </div>
          </div>

          <!-- Step 2: 设置新密码 -->
          <div v-if="step === 2" class="step-panel">
            <div class="success-icon-wrap">
              <i class="ri-shield-keyhole-line"></i>
            </div>
            <h2 class="card-title" style="text-align:center">设置新密码</h2>
            <p class="card-desc" style="text-align:center">请输入您的新密码，至少8位且包含字母和数字</p>

            <el-form :model="resetForm" :rules="resetRules" ref="resetRef" label-width="0" class="auth-form">
              <el-form-item prop="newPassword">
                <el-input v-model="resetForm.newPassword" type="password" placeholder="新密码（至少8位）" show-password size="large" class="auth-input">
                  <template #prefix>
                    <el-icon class="input-icon"><Lock /></el-icon>
                  </template>
                </el-input>
              </el-form-item>
              <el-form-item prop="confirmPassword">
                <el-input v-model="resetForm.confirmPassword" type="password" placeholder="确认新密码" show-password size="large"
                  class="auth-input" @keyup.enter="onResetPassword">
                  <template #prefix>
                    <el-icon class="input-icon"><Lock /></el-icon>
                  </template>
                </el-input>
              </el-form-item>

              <div class="pwd-strength">
                <div class="pwd-bar">
                  <div :class="['pwd-seg', pwdStrength >= 1 ? 'seg-weak' : '']"></div>
                  <div :class="['pwd-seg', pwdStrength >= 2 ? 'seg-medium' : '']"></div>
                  <div :class="['pwd-seg', pwdStrength >= 3 ? 'seg-strong' : '']"></div>
                </div>
                <span class="pwd-label">{{ pwdStrengthLabel }}</span>
              </div>

              <el-form-item class="submit-section">
                <button type="button" class="auth-btn" :disabled="loading" @click="onResetPassword">
                  <i v-if="loading" class="ri-loader-4-line auth-btn-spin"></i>
                  <span v-if="!loading">重置密码</span>
                  <span v-else>重置中...</span>
                </button>
              </el-form-item>
            </el-form>
          </div>

          <div class="auth-divider"><span>或</span></div>
          <div class="auth-footer">
            <span>记起密码了？</span>
            <router-link to="/web/login" class="auth-link">返回登录</router-link>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, computed, nextTick } from 'vue'
import CommonHeroBg from './components/CommonHeroBg.vue'
import { User, Lock } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { useRouter } from 'vue-router'
import { checkAccountContact, forgotPasswordStart, forgotPasswordReset } from '../../../api/auth'
import type { CheckAccountContactOutput } from '../../../api/auth'

const router = useRouter()
const step = ref(0)
const loading = ref(false)
const noContactInfo = ref(false)
const codeSent = ref(false)
const channel = ref<'email' | 'phone'>('email')
const countdown = ref(0)
let countdownTimer: ReturnType<typeof setInterval> | null = null

const contactInfo = reactive<CheckAccountContactOutput>({
  found: false, hasEmail: false, hasPhone: false, maskedEmail: null, maskedPhone: null
})

// Step 0: 账户表单
const accountForm = reactive({ account: '' })
const accountRef = ref()
const accountRules = { account: [{ required: true, message: '请输入邮箱或用户名', trigger: 'blur' }] }

// Step 1: 验证码
const codeDigits = ref(['', '', '', '', '', ''])
const codeInputRefs = ref<HTMLInputElement[]>([])
const codeValue = computed(() => codeDigits.value.join(''))

// Step 2: 重置密码
const resetForm = reactive({ newPassword: '', confirmPassword: '' })
const resetRef = ref()
const resetRules = {
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    { min: 8, message: '密码至少需要8位', trigger: 'blur' },
    { pattern: /^(?=.*[A-Za-z])(?=.*\d)/, message: '需包含字母和数字', trigger: 'blur' }
  ],
  confirmPassword: [
    { required: true, message: '请确认新密码', trigger: 'blur' },
    {
      validator: (_: any, value: string, cb: any) => {
        if (value !== resetForm.newPassword) cb(new Error('两次输入的密码不一致'))
        else cb()
      }, trigger: 'blur'
    }
  ]
}

// 密码强度
const pwdStrength = computed(() => {
  const p = resetForm.newPassword
  if (!p) return 0
  let s = 0
  if (p.length >= 8) s++
  if (/[A-Za-z]/.test(p) && /\d/.test(p)) s++
  if (p.length >= 12 && /[^A-Za-z0-9]/.test(p)) s++
  return s
})
const pwdStrengthLabel = computed(() => ['', '弱', '中等', '强'][pwdStrength.value] || '')

// Step 0: 检查账户
async function onCheckAccount() {
  try {
    await accountRef.value?.validate()
    loading.value = true
    noContactInfo.value = false
    const res = await checkAccountContact(accountForm.account)
    Object.assign(contactInfo, res)

    if (!res.found || (!res.hasEmail && !res.hasPhone)) {
      noContactInfo.value = true
      return
    }
    channel.value = res.hasEmail ? 'email' : 'phone'
    step.value = 1
  } catch (e: any) {
    if (e?.message) ElMessage.error(e.message)
  } finally {
    loading.value = false
  }
}

// Step 1: 发送验证码
async function onSendCode() {
  try {
    loading.value = true
    await forgotPasswordStart({ account: accountForm.account, channel: channel.value })
    codeSent.value = true
    ElMessage.success('验证码已发送')
    startCountdown()
  } catch (e: any) {
    if (e?.message) ElMessage.error(e.message)
  } finally {
    loading.value = false
  }
}

function startCountdown() {
  countdown.value = 60
  if (countdownTimer) clearInterval(countdownTimer)
  countdownTimer = setInterval(() => {
    countdown.value--
    if (countdown.value <= 0 && countdownTimer) {
      clearInterval(countdownTimer)
      countdownTimer = null
    }
  }, 1000)
}

// 验证码输入逻辑
function onCodeInput(idx: number) {
  const v = codeDigits.value[idx]
  if (v && !/^\d$/.test(v)) {
    codeDigits.value[idx] = ''
    return
  }
  if (v && idx < 5) {
    nextTick(() => codeInputRefs.value[idx + 1]?.focus())
  }
}
function onCodeKeydown(idx: number, e: KeyboardEvent) {
  if (e.key === 'Backspace' && !codeDigits.value[idx] && idx > 0) {
    nextTick(() => codeInputRefs.value[idx - 1]?.focus())
  }
}
function onCodePaste(e: ClipboardEvent) {
  e.preventDefault()
  const text = (e.clipboardData?.getData('text') || '').replace(/\D/g, '').slice(0, 6)
  for (let i = 0; i < 6; i++) {
    codeDigits.value[i] = text[i] || ''
  }
  const focusIdx = Math.min(text.length, 5)
  nextTick(() => codeInputRefs.value[focusIdx]?.focus())
}

// Step 1→2: 验证通过
async function onVerifyCode() {
  if (codeValue.value.length < 6) {
    ElMessage.warning('请输入完整的6位验证码')
    return
  }
  step.value = 2
}

// Step 2: 重置密码
async function onResetPassword() {
  try {
    await resetRef.value?.validate()
    loading.value = true
    await forgotPasswordReset({ account: accountForm.account, token: codeValue.value, newPassword: resetForm.newPassword })
    ElMessage.success('密码已重置成功，请使用新密码登录')
    router.replace('/web/login')
  } catch (e: any) {
    if (e?.message) ElMessage.error(e.message)
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
/* ===== 页面变量 ===== */
.web-forgot-page {
  --ab: #f0f4f8; --as: #ffffff; --as2: #f1f5f9; --ab2: rgba(0,0,0,0.06);
  --at: #0f172a; --at2: #475569; --at3: #94a3b8; --ap: #3b82f6; --apl: #60a5fa;
}
:global(.dark) .web-forgot-page {
  --ab: #0b0f1a; --as: #141926; --as2: #1c2235; --ab2: rgba(255,255,255,0.06);
  --at: #f1f5f9; --at2: #94a3b8; --at3: #64748b; --ap: #60a5fa; --apl: #93bbfd;
}
.auth-scene {
  position: relative; min-height: calc(100vh - 4rem);
  display: flex; align-items: center; justify-content: center;
  padding: 5rem 1.5rem 4rem;
  background: linear-gradient(160deg, #0f172a 0%, #1e293b 50%, #0f172a 100%);
  overflow: hidden;
}
.scene-bg { position: absolute; inset: 0; pointer-events: none; }
.auth-container {
  position: relative; z-index: 2;
  display: flex; align-items: stretch; gap: 4rem;
  max-width: 1060px; width: 100%;
}
.auth-brand {
  flex: 1; position: relative;
  display: flex; flex-direction: column; justify-content: center; min-height: 480px;
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
  font-size: 2.5rem; font-weight: 800; letter-spacing: -0.5px; margin: 0 0 0.5rem;
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
  width: 440px; flex-shrink: 0; background: var(--as);
  border-radius: 24px; padding: 2.5rem 2.25rem;
  box-shadow: 0 20px 60px rgba(0,0,0,0.25), 0 4px 12px rgba(0,0,0,0.1);
  border: 1px solid rgba(255,255,255,0.08); animation: cardSlideIn 0.6s ease;
}
:global(.dark) .auth-card { box-shadow: 0 20px 60px rgba(0,0,0,0.5), 0 4px 12px rgba(0,0,0,0.2); border-color: rgba(255,255,255,0.06); }
@keyframes cardSlideIn { from { opacity: 0; transform: translateX(30px); } to { opacity: 1; transform: translateX(0); } }
.step-indicator { display: flex; align-items: center; justify-content: center; gap: 0; margin-bottom: 0.5rem; }
.step-dot {
  width: 32px; height: 32px; border-radius: 50%;
  display: flex; align-items: center; justify-content: center;
  font-size: 0.8rem; font-weight: 600;
  background: var(--as2); color: var(--at3);
  border: 2px solid var(--ab2); transition: all 0.3s;
}
.step-dot.active { background: var(--ap); color: #fff; border-color: var(--ap); }
.step-dot.done { background: #10b981; color: #fff; border-color: #10b981; }
.step-num { line-height: 1; }
.step-line { width: 60px; height: 2px; background: var(--ab2); transition: background 0.3s; }
.step-line.active { background: var(--ap); }
.step-labels {
  display: flex; justify-content: space-between; margin-bottom: 1.75rem;
  font-size: 0.75rem; color: var(--at3); padding: 0 0.25rem;
}
.step-labels span.active { color: var(--ap); font-weight: 600; }
.step-panel { animation: panelFade 0.35s ease; }
@keyframes panelFade { from { opacity: 0; transform: translateY(10px); } to { opacity: 1; transform: translateY(0); } }
.card-title { font-size: 1.35rem; font-weight: 700; color: var(--at); margin: 0 0 0.3rem; }
.card-desc { font-size: 0.88rem; color: var(--at3); margin: 0 0 1.5rem; }
.no-contact-notice {
  display: flex; align-items: flex-start; gap: 0.75rem; padding: 1.25rem;
  background: rgba(245,158,11,0.08); border-radius: 14px;
  border: 1px solid rgba(245,158,11,0.15); margin-top: 1rem;
}
.no-contact-notice > i { font-size: 1.5rem; color: #f59e0b; flex-shrink: 0; margin-top: 2px; }
.notice-title { color: #92400e; margin: 0 0 4px; font-size: 0.9rem; font-weight: 600; }
.notice-desc { color: #a16207; margin: 0; font-size: 0.82rem; line-height: 1.5; }
:global(.dark) .no-contact-notice { background: rgba(245,158,11,0.06); }
:global(.dark) .notice-title { color: #fbbf24; }
:global(.dark) .notice-desc { color: #d97706; }
.channel-options { display: flex; gap: 0.75rem; margin-bottom: 1.25rem; }
.channel-card {
  flex: 1; display: flex; align-items: center; gap: 0.75rem;
  padding: 1rem; border-radius: 14px; cursor: pointer;
  background: var(--as2); border: 2px solid var(--ab2); transition: all 0.25s;
}
.channel-card:hover { border-color: rgba(59,130,246,0.3); }
.channel-card.selected { border-color: var(--ap); background: rgba(59,130,246,0.06); box-shadow: 0 0 0 3px rgba(59,130,246,0.1); }
.channel-card i { font-size: 1.5rem; color: var(--ap); }
.ch-label { font-size: 0.82rem; font-weight: 600; color: var(--at); margin: 0; }
.ch-value { font-size: 0.78rem; color: var(--at3); margin: 2px 0 0; }
.code-sent-tip {
  display: flex; align-items: center; gap: 0.5rem;
  padding: 0.75rem 1rem; border-radius: 10px; margin-bottom: 1.25rem;
  background: rgba(16,185,129,0.08); color: #059669;
  font-size: 0.82rem; font-weight: 500;
}
.code-sent-tip i { font-size: 1.1rem; }
:global(.dark) .code-sent-tip { background: rgba(16,185,129,0.06); color: #34d399; }
.code-input-group { display: flex; gap: 0.5rem; justify-content: center; margin-bottom: 1rem; }
.code-cell {
  width: 48px; height: 56px; text-align: center;
  font-size: 1.5rem; font-weight: 700; color: var(--at);
  border: 2px solid var(--ab2); border-radius: 12px;
  background: var(--as2); outline: none; transition: all 0.2s;
}
.code-cell:focus { border-color: var(--ap); box-shadow: 0 0 0 3px rgba(59,130,246,0.12); background: var(--as); }
.resend-row {
  display: flex; align-items: center; justify-content: center; gap: 0.5rem;
  margin-bottom: 1.25rem; font-size: 0.82rem;
}
.resend-text { color: var(--at3); }
.resend-btn { background: none; border: none; color: var(--ap); cursor: pointer; font-weight: 600; font-size: 0.82rem; padding: 0; }
.resend-btn:hover { text-decoration: underline; }
.resend-timer { color: var(--at3); }
.success-icon-wrap { display: flex; justify-content: center; margin-bottom: 1rem; }
.success-icon-wrap i {
  font-size: 2.5rem; color: var(--ap);
  width: 64px; height: 64px; display: flex; align-items: center; justify-content: center;
  border-radius: 50%; background: rgba(59,130,246,0.08);
}
.pwd-strength { display: flex; align-items: center; gap: 0.75rem; margin-bottom: 0.5rem; }
.pwd-bar { display: flex; gap: 4px; flex: 1; }
.pwd-seg { height: 4px; flex: 1; border-radius: 2px; background: var(--ab2); transition: background 0.3s; }
.seg-weak { background: #ef4444; }
.seg-medium { background: #f59e0b; }
.seg-strong { background: #10b981; }
.pwd-label { font-size: 0.75rem; color: var(--at3); min-width: 2rem; }
.auth-form { display: flex; flex-direction: column; gap: 1.1rem; }
.auth-input :deep(.el-input__wrapper) {
  border-radius: 12px; border: 1.5px solid var(--ab2);
  transition: all 0.25s; padding: 13px 16px; background: var(--as2); box-shadow: none;
}
.auth-input :deep(.el-input__wrapper:hover) { border-color: rgba(59,130,246,0.35); background: var(--as); box-shadow: 0 0 0 3px rgba(59,130,246,0.06); }
.auth-input :deep(.el-input__wrapper.is-focus) { border-color: var(--ap); background: var(--as); box-shadow: 0 0 0 3px rgba(59,130,246,0.12); }
.input-icon { color: var(--at3); margin-right: 6px; }
.submit-section { margin: 0.25rem 0 0; }
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
.auth-btn--outline { background: transparent; color: var(--ap); border: 2px solid var(--ap); box-shadow: none; margin-bottom: 1.25rem; }
.auth-btn--outline:hover:not(:disabled) { background: rgba(59,130,246,0.06); box-shadow: none; transform: none; }
.auth-btn--outline::after { display: none; }
@keyframes shine { to { transform: translateX(100%); } }
.auth-btn-spin { animation: spin 0.8s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
.auth-divider { display: flex; align-items: center; gap: 1rem; margin: 1.5rem 0; color: var(--at3); font-size: 0.8rem; }
.auth-divider::before, .auth-divider::after { content: ''; flex: 1; height: 1px; background: var(--ab2); }
.auth-footer { text-align: center; font-size: 0.88rem; color: var(--at3); }
.auth-link { color: var(--ap); text-decoration: none; font-weight: 600; margin-left: 4px; transition: color 0.2s; }
.auth-link:hover { color: var(--apl); }
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
  .code-cell { width: 40px; height: 48px; font-size: 1.25rem; }
  .step-line { width: 36px; }
}
</style>
