<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="web-login-page">
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
            <h1 class="brand-title">{{ system.siteName || 'GinkgoAdmin' }}</h1>
            <p class="brand-slogan">{{ t('login_welcome') }}</p>
            <p class="brand-desc">开源的 AI 开发交付底座，<br>帮助团队快速构建可复用、收益更高的高效模型。</p>
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

        <!-- 右侧：登录表单 -->
        <div class="auth-card">
          <h2 class="card-title">账户登录</h2>
          <p class="card-desc">请输入您的账户信息</p>

          <el-form :model="form" :rules="rules" ref="formRef" label-width="0" class="auth-form">
            <el-form-item prop="userName">
              <el-input v-model="form.userName" :placeholder="t('login_username')" clearable size="large" class="auth-input">
                <template #prefix>
                  <el-icon class="input-icon"><User /></el-icon>
                </template>
              </el-input>
            </el-form-item>

            <el-form-item prop="password">
              <el-input v-model="form.password" type="password" :placeholder="t('login_password')" show-password size="large"
                class="auth-input" @keyup.enter="onSubmit">
                <template #prefix>
                  <el-icon class="input-icon"><Lock /></el-icon>
                </template>
              </el-input>
            </el-form-item>

            <div class="form-options">
              <el-checkbox v-model="form.rememberMe" class="remember-me">{{ t('login_remember') }}</el-checkbox>
              <router-link to="/web/forgot-password" class="forgot-link">{{ t('login_forgot') }}</router-link>
            </div>

            <el-form-item class="submit-section">
              <button type="button" class="auth-btn" :disabled="loading" @click="onSubmit">
                <i v-if="loading" class="ri-loader-4-line auth-btn-spin"></i>
                <span v-if="!loading">{{ t('login_btn') }}</span>
                <span v-else>{{ t('login_loading') }}</span>
              </button>
            </el-form-item>
          </el-form>

          <!-- 插件注入的登录操作 -->
          <PluginSlot name="login-actions" :context="{ page: 'web-login' }" @plugin-event="handlePluginEvent" />

          <div class="auth-divider"><span>或</span></div>

          <div class="auth-footer">
            <span>{{ t('login_no_account') }}</span>
            <router-link v-if="registrationEnabled" to="/web/register" class="auth-link">{{ t('login_register') }}</router-link>
            <span v-else class="auth-link--disabled">{{ t('login_closed') }}</span>
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
import { reactive, ref, onMounted, computed } from 'vue'
import CommonHeroBg from './components/CommonHeroBg.vue'
import { User, Lock } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { useRouter, useRoute } from 'vue-router'
import { useWebAuthStore } from '../../../stores/webAuth'
import { useSystemStore } from '../../../stores/system'
import { useMenuStore } from '../../../stores/menu'
import { useLanguageStore } from '../../../stores/language'
import { login as apiLogin } from '../../../api/auth'
import { getCurrentUser } from '../../../api/user'
import { usePlugins } from '../../../composables/usePlugins'
import { t } from '@/utils/lang'
import PluginSlot from '../../../components/PluginSlot.vue'

const router = useRouter()
const route = useRoute()
const webAuth = useWebAuthStore()
const system = useSystemStore()
const menuStore = useMenuStore()
const languageStore = useLanguageStore()
const registrationEnabled = computed(() => system.registrationMode !== 'disabled' && system.registrationEnabled)
const { executeHook } = usePlugins()

const form = reactive({
  userName: '',
  password: '',
  rememberMe: false
})
const formRef = ref()
const loading = ref(false)

const rules = {
  userName: [{ required: true, message: t('login_username_required'), trigger: 'blur' }],
  password: [{ required: true, message: t('login_password_required'), trigger: 'blur' }]
}

async function onSubmit() {
  try {
    await formRef.value?.validate()
    loading.value = true

    // 调用后端登录接口
    const loginRes = await apiLogin(form.userName, form.password, 'WEB_PORTAL')

    // 先写入 token 到 store，确保后续请求带上 Authorization
    webAuth.login(loginRes.token, { userName: loginRes.userName || form.userName, name: loginRes.displayName || form.userName })

    // 登录成功后自动清空缓存，确保加载最新数据
    try {
      menuStore.clearCache()
      languageStore.clearCache()
    } catch (e) {
      // silently ignored
    }

    // 再拉取"本人"信息以更新展示
    try {
      const me = await getCurrentUser()
      webAuth.updateUserInfo({ userName: me.userName, name: me.displayName, avatar: (me as any).avatar, bio: (me as any).introduction })
    } catch {}
    ElMessage.success(t('login_success'))

    // 跳转到个人中心或重定向页面
    let redirect = (route.query.redirect as string) || '/web/user'
    try {
      while (redirect.includes('/web/login')) {
        const urlObj = new URL(redirect, window.location.origin)
        redirect = urlObj.searchParams.get('redirect') || '/web/user'
      }
    } catch {
      // ignore
    }
    if (redirect.includes('/web/login') || redirect === '/') {
      redirect = '/web/user'
    }
    router.replace(redirect)
  } catch (e: any) {
    if (e?.message) ElMessage.error(e.message)
  } finally {
    loading.value = false
  }
}

// 处理插件事件
const handlePluginEvent = (event: string, data: any) => {
  switch (event) {
    case 'third-party-login':
      handleThirdPartyLogin(data)
      break
    default:
      break
  }
}

// 处理第三方登录
// ThirdPartyLoginPanel 在弹窗成功后已经写入 useWebAuthStore 并尝试跳转，
// 这里仅做兜底缓存清理与成功提示，**不**再调用 executeHook('auth:login')，
// 否则会因为缺少 OAuth code/state 必然失败，把已登录成功的状态被错误提示覆盖。
const handleThirdPartyLogin = async (data: any) => {
  if (!data?.success || !data?.token) {
    if (data?.error) ElMessage.error(data.error)
    return
  }

  try {
    menuStore.clearCache()
    languageStore.clearCache()
  } catch { /* ignore */ }

  ElMessage.success(t('login_success'))
}

onMounted(() => {
  // 初始化插件系统（如果还没有初始化）
})
</script>

<style scoped>
/* ===== 页面变量 ===== */
.web-login-page {
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
:global(.dark) .web-login-page {
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
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 5rem 1.5rem 4rem;
  background: linear-gradient(160deg, #0f172a 0%, #1e293b 50%, #0f172a 100%);
  overflow: hidden;
}

/* ===== 背景装饰 ===== */
.scene-bg { position: absolute; inset: 0; pointer-events: none; }

/* ===== 内容容器 ===== */
.auth-container {
  position: relative; z-index: 2;
  display: flex; align-items: stretch; gap: 4rem;
  max-width: 1040px; width: 100%;
}

/* ===== 左侧品牌区 ===== */
.auth-brand {
  flex: 1; position: relative;
  display: flex; flex-direction: column; justify-content: center;
  min-height: 420px;
}
.brand-content { position: relative; z-index: 2; }
.brand-logo {
  width: 64px; height: 64px; border-radius: 18px;
  background: linear-gradient(135deg, #3b82f6, #2563eb);
  display: flex; align-items: center; justify-content: center;
  box-shadow: 0 8px 32px rgba(59,130,246,0.35);
  margin-bottom: 1.75rem;
  animation: logoFloat 6s ease-in-out infinite;
}
.brand-logo svg { width: 32px; height: 32px; color: #fff; }
@keyframes logoFloat {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-8px); }
}
.brand-title {
  font-size: 2.5rem; font-weight: 800; color: #fff;
  letter-spacing: -0.5px; margin: 0 0 0.5rem;
  background: linear-gradient(135deg, #ffffff 30%, #93c5fd 100%);
  -webkit-background-clip: text; -webkit-text-fill-color: transparent;
  background-clip: text;
}
.brand-slogan {
  font-size: 1.35rem; font-weight: 600; color: rgba(255,255,255,0.85);
  margin: 0 0 0.75rem; line-height: 1.5;
}
.brand-desc {
  font-size: 0.95rem; color: rgba(255,255,255,0.45); margin: 0 0 2rem;
  line-height: 1.7;
}
.brand-features {
  display: flex; flex-direction: column; gap: 0.75rem;
}
.brand-feat {
  display: flex; align-items: center; gap: 0.6rem;
  font-size: 0.9rem; color: rgba(255,255,255,0.65);
}
.brand-feat i {
  font-size: 1.15rem; color: #60a5fa; width: 32px; height: 32px;
  display: flex; align-items: center; justify-content: center;
  border-radius: 8px; background: rgba(59,130,246,0.12);
  flex-shrink: 0;
}

/* ===== 左侧动画装饰 ===== */
.brand-anim {
  position: absolute; inset: 0; pointer-events: none; z-index: 1;
}
.anim-ring {
  position: absolute; border-radius: 50%;
  border: 1.5px solid rgba(59,130,246,0.12);
}
.anim-ring--1 {
  width: 320px; height: 320px; top: 50%; left: 50%;
  transform: translate(-50%, -50%);
  animation: ringPulse 8s ease-in-out infinite;
}
.anim-ring--2 {
  width: 240px; height: 240px; top: 50%; left: 50%;
  transform: translate(-50%, -50%);
  border-color: rgba(139,92,246,0.1);
  animation: ringPulse 8s ease-in-out 2s infinite;
}
.anim-ring--3 {
  width: 160px; height: 160px; top: 50%; left: 50%;
  transform: translate(-50%, -50%);
  border-color: rgba(6,182,212,0.1);
  animation: ringPulse 8s ease-in-out 4s infinite;
}
@keyframes ringPulse {
  0%, 100% { opacity: 0.4; transform: translate(-50%, -50%) scale(1); }
  50% { opacity: 0.8; transform: translate(-50%, -50%) scale(1.08); }
}
.anim-dot {
  position: absolute; border-radius: 50%;
  background: rgba(59,130,246,0.6);
  box-shadow: 0 0 12px rgba(59,130,246,0.4);
}
.anim-dot--1 { width: 6px; height: 6px; top: 15%; left: 70%; animation: dotDrift1 12s linear infinite; }
.anim-dot--2 { width: 4px; height: 4px; top: 75%; left: 20%; background: rgba(139,92,246,0.6); animation: dotDrift2 15s linear infinite; }
.anim-dot--3 { width: 5px; height: 5px; top: 35%; left: 85%; background: rgba(6,182,212,0.5); animation: dotDrift3 10s linear infinite; }
.anim-dot--4 { width: 4px; height: 4px; top: 60%; left: 65%; animation: dotDrift4 14s linear infinite; }
@keyframes dotDrift1 {
  0% { transform: translate(0, 0); opacity: 0; }
  10% { opacity: 1; }
  90% { opacity: 1; }
  100% { transform: translate(-60px, 80px); opacity: 0; }
}
@keyframes dotDrift2 {
  0% { transform: translate(0, 0); opacity: 0; }
  10% { opacity: 1; }
  90% { opacity: 1; }
  100% { transform: translate(50px, -70px); opacity: 0; }
}
@keyframes dotDrift3 {
  0% { transform: translate(0, 0); opacity: 0; }
  10% { opacity: 1; }
  90% { opacity: 1; }
  100% { transform: translate(-40px, 60px); opacity: 0; }
}
@keyframes dotDrift4 {
  0% { transform: translate(0, 0); opacity: 0; }
  10% { opacity: 1; }
  90% { opacity: 1; }
  100% { transform: translate(30px, -50px); opacity: 0; }
}

/* ===== 右侧表单卡片 ===== */
.auth-card {
  width: 420px; flex-shrink: 0;
  background: var(--as);
  border-radius: 24px; padding: 2.75rem 2.5rem;
  box-shadow: 0 20px 60px rgba(0,0,0,0.25), 0 4px 12px rgba(0,0,0,0.1);
  border: 1px solid rgba(255,255,255,0.08);
  animation: cardSlideIn 0.6s ease;
}
:global(.dark) .auth-card {
  box-shadow: 0 20px 60px rgba(0,0,0,0.5), 0 4px 12px rgba(0,0,0,0.2);
  border-color: rgba(255,255,255,0.06);
}
@keyframes cardSlideIn {
  from { opacity: 0; transform: translateX(30px); }
  to { opacity: 1; transform: translateX(0); }
}
.card-title {
  font-size: 1.5rem; font-weight: 700; color: var(--at);
  margin: 0 0 0.35rem;
}
.card-desc {
  font-size: 0.9rem; color: var(--at3); margin: 0 0 1.75rem;
}

/* ===== 表单 ===== */
.auth-form { display: flex; flex-direction: column; gap: 1.15rem; }
.auth-input :deep(.el-input__wrapper) {
  border-radius: 12px; border: 1.5px solid var(--ab2);
  transition: all 0.25s; padding: 13px 16px; background: var(--as2);
  box-shadow: none;
}
.auth-input :deep(.el-input__wrapper:hover) {
  border-color: rgba(59,130,246,0.35); background: var(--as);
  box-shadow: 0 0 0 3px rgba(59,130,246,0.06);
}
.auth-input :deep(.el-input__wrapper.is-focus) {
  border-color: var(--ap); background: var(--as);
  box-shadow: 0 0 0 3px rgba(59,130,246,0.12);
}
.input-icon { color: var(--at3); margin-right: 6px; }

.form-options {
  display: flex; justify-content: space-between; align-items: center;
  margin: -0.15rem 0 0.15rem;
}
.remember-me :deep(.el-checkbox__label) { color: var(--at3); font-size: 0.85rem; }
.forgot-link {
  color: var(--ap); font-size: 0.85rem; font-weight: 500;
  text-decoration: none; transition: color 0.2s;
}
.forgot-link:hover { color: var(--apl); }
.submit-section { margin: 0.25rem 0 0; }

/* ===== 按钮 ===== */
.auth-btn {
  width: 100%; height: 52px; border-radius: 12px; border: none; cursor: pointer;
  font-size: 1rem; font-weight: 700; letter-spacing: 0.5px;
  display: inline-flex; align-items: center; justify-content: center; gap: 0.5rem;
  transition: all 0.3s; position: relative; overflow: hidden;
  background: linear-gradient(135deg, #2563eb, #7c3aed); color: #fff;
  box-shadow: 0 6px 24px rgba(37,99,235,0.4);
}
.auth-btn::after {
  content: ''; position: absolute; inset: 0;
  background: linear-gradient(90deg, transparent, rgba(255,255,255,0.2), transparent);
  transform: translateX(-100%);
}
.auth-btn:hover:not(:disabled) {
  transform: translateY(-2px); box-shadow: 0 10px 36px rgba(37,99,235,0.55);
  background: linear-gradient(135deg, #1d4ed8, #6d28d9);
}
.auth-btn:hover:not(:disabled)::after { animation: shine 0.7s ease; }
.auth-btn:active:not(:disabled) { transform: translateY(0); }
.auth-btn:disabled { opacity: 0.55; cursor: not-allowed; }
@keyframes shine { to { transform: translateX(100%); } }
.auth-btn-spin { animation: spin 0.8s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }

/* ===== 分割线 ===== */
.auth-divider {
  display: flex; align-items: center; gap: 1rem; margin: 1.5rem 0;
  color: var(--at3); font-size: 0.8rem;
}
.auth-divider::before, .auth-divider::after {
  content: ''; flex: 1; height: 1px; background: var(--ab2);
}

/* ===== 底部 ===== */
.auth-footer {
  text-align: center; font-size: 0.88rem; color: var(--at3);
}
.auth-link {
  color: var(--ap); text-decoration: none; font-weight: 600;
  margin-left: 4px; transition: color 0.2s;
}
.auth-link:hover { color: var(--apl); }
.auth-link--disabled { color: var(--at3); margin-left: 4px; }

.auth-trust {
  display: flex; align-items: center; justify-content: center; gap: 6px;
  margin-top: 1.5rem; font-size: 0.78rem; color: var(--at3);
}
.auth-trust i { color: #10b981; font-size: 0.95rem; }

/* ===== 验证错误 ===== */
:deep(.el-form-item.is-error .el-input__wrapper) {
  border-color: #ef4444 !important; box-shadow: 0 0 0 3px rgba(239,68,68,0.08) !important;
}
:deep(.el-form-item__error) { font-size: 0.78rem; color: #ef4444; margin-top: 4px; }

/* ===== 响应式 ===== */
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
