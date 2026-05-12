<!-- GinkgoAdmin | https://www.ginkgoadmin.com | Copyright © 2026 GinkgoAdmin. All rights reserved. -->
<template>
  <div class="user-center">
    <div class="container">
      <div class="user-center-layout">
        <!-- 侧边导航（提取为组件） -->
        <WebUserSidebar :user-info="userInfo" @logout="handleLogout" />

        <!-- 主要内容区域 -->
        <main class="main-content">
          <div class="content-header">
            <h1>{{ t('uc_title') }}</h1>
            <p>{{ t('uc_welcome', { name: userInfo?.name || t('role_default') }) }}</p>
          </div>

          <!-- 统计卡片 -->
          <div class="stats-grid">
            <div class="stat-card stat-card-blue">
              <div class="stat-icon">
                <el-icon><Calendar /></el-icon>
              </div>
              <div class="stat-content">
                <h3>{{ loginDays }}</h3>
                <p>{{ t('uc_login_days') }}</p>
              </div>
            </div>

            <div class="stat-card stat-card-green">
              <div class="stat-icon">
                <el-icon><Clock /></el-icon>
              </div>
              <div class="stat-content">
                <h3>{{ lastLoginTime }}</h3>
                <p>{{ t('uc_last_login') }}</p>
              </div>
            </div>

            <div class="stat-card stat-card-orange">
              <div class="stat-icon">
                <el-icon><Document /></el-icon>
              </div>
              <div class="stat-content">
                <h3>{{ operationCount }}</h3>
                <p>{{ t('uc_op_count') }}</p>
              </div>
            </div>

            <div class="stat-card stat-card-purple">
              <div class="stat-icon">
                <el-icon><Star /></el-icon>
              </div>
              <div class="stat-content">
                <h3>{{ favoriteCount }}</h3>
                <p>{{ t('uc_fav_count') }}</p>
              </div>
            </div>
          </div>

          <!-- 快速操作 -->
          <div class="quick-actions">
            <h2>{{ t('uc_quick_actions') }}</h2>
            <div class="action-grid">
              <router-link to="/web/user/profile" class="action-card action-card-blue">
                <div class="action-icon">
                  <el-icon><Edit /></el-icon>
                </div>
                <div class="action-content">
                  <h3>{{ t('uc_edit_profile') }}</h3>
                  <p>{{ t('uc_edit_profile_desc') }}</p>
                </div>
                <el-icon class="action-arrow"><ArrowRight /></el-icon>
              </router-link>

              <router-link to="/web/user/logs" class="action-card action-card-cyan">
                <div class="action-icon">
                  <el-icon><View /></el-icon>
                </div>
                <div class="action-content">
                  <h3>{{ t('uc_view_logs') }}</h3>
                  <p>{{ t('uc_view_logs_desc') }}</p>
                </div>
                <el-icon class="action-arrow"><ArrowRight /></el-icon>
              </router-link>

              <router-link to="/web/download" class="action-card action-card-purple">
                <div class="action-icon">
                  <el-icon><Download /></el-icon>
                </div>
                <div class="action-content">
                  <h3>{{ t('uc_download') }}</h3>
                  <p>{{ t('uc_download_desc') }}</p>
                </div>
                <el-icon class="action-arrow"><ArrowRight /></el-icon>
              </router-link>

              <router-link to="/web/docs" class="action-card action-card-green">
                <div class="action-icon">
                  <el-icon><Reading /></el-icon>
                </div>
                <div class="action-content">
                  <h3>{{ t('uc_docs') }}</h3>
                  <p>{{ t('uc_docs_desc') }}</p>
                </div>
                <el-icon class="action-arrow"><ArrowRight /></el-icon>
              </router-link>
            </div>
          </div>

          <!-- 最近活动 -->
          <div class="recent-activity">
            <h2>{{ t('uc_recent') }}</h2>
            <div class="activity-list">
              <div v-if="recentActivities.length === 0" class="activity-empty">
                <el-empty :description="t('logs_empty')" :image-size="80" />
              </div>
              <div v-for="(activity, idx) in recentActivities" :key="activity.id" class="activity-item">
                <div class="activity-timeline">
                  <div class="timeline-dot" :class="activity.iconClass"></div>
                  <div v-if="idx < recentActivities.length - 1" class="timeline-line"></div>
                </div>
                <div class="activity-body">
                  <p class="activity-text">{{ activity.text }}</p>
                  <span class="activity-time">{{ activity.time }}</span>
                </div>
              </div>
            </div>
          </div>
        </main>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  House, User, Document, SwitchButton, Calendar, Clock, Star,
  Edit, View, Download, Reading, Bell, ArrowRight
} from '@element-plus/icons-vue'
import { useWebAuthStore } from '../../../stores/webAuth'
import WebUserSidebar from './components/WebUserSidebar.vue'
import { t } from '@/utils/lang'


const router = useRouter()
const webAuth = useWebAuthStore()
const userInfo = computed(() => webAuth.userInfo)

// 个人中心数据（从后端获取）
const loginDays = ref(0)
const operationCount = ref(0)
const favoriteCount = ref(0)
import { getWebUserCenter } from '../../../api/user'

const lastLoginTime = computed(() => {
  return new Date().toLocaleString('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit'
  })
})

const recentActivities = ref<{ id: string; icon: any; iconClass: string; text: string; time: string }[]>([])

async function loadCenterData() {
  try {
    const data = await getWebUserCenter()
    loginDays.value = data.loginDays || 0
    operationCount.value = data.operationCount || 0
    favoriteCount.value = data.favoriteCount || 0

    // 将后端最近活动映射为前端展示
    const mapIcon = (resource: string, action: string) => {
      const a = (action || '').toUpperCase()
      if (resource === '/api/auth/login') return { icon: 'User', cls: 'dot-blue', text: t('uc_login_system') }
      if (resource === '/api/auth/logout') return { icon: 'User', cls: 'dot-gray', text: t('uc_logout_system') }
      if (a === 'POST') return { icon: 'Edit', cls: 'dot-green', text: t('uc_create_op') }
      if (a === 'PUT' || a === 'PATCH') return { icon: 'Edit', cls: 'dot-orange', text: t('uc_update_op') }
      if (a === 'DELETE') return { icon: 'View', cls: 'dot-red', text: t('uc_delete_op') }
      return { icon: 'Document', cls: 'dot-cyan', text: t('uc_other_op') }
    }



    recentActivities.value = (data.recentActivities || []).map((x) => {
      const m = mapIcon(x.resource, x.action)
      return {
        id: x.id,
        icon: m.icon,
        iconClass: m.cls,
        text: x.review || x.feature || m.text,
        time: new Date(x.createdAt).toLocaleString('zh-CN', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
      }
    })
  } catch (e) {
    // silently ignored
  }
}

// 获取用户角色
const getUserRole = () => {
  if (!userInfo.value) return '访客'

  switch (userInfo.value.userName) {
    case 'admin':
      return '管理员'
    case 'user':
      return '普通用户'
    case 'demo':
      return '演示用户'
    default:
      return '用户'
  }
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

onMounted(() => {
  webAuth.initFromStorage()
  if (!webAuth.isAuthenticated) {
    router.push('/web/login')
  } else {
    loadCenterData()
  }
})
</script>

<style scoped>
.user-center {
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

/* 主要内容区域 */
.main-content {
  background: white;
  border-radius: 16px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.06);
  padding: 2rem;
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

/* 统计卡片 */
.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.875rem;
  margin-bottom: 2.5rem;
}

.stat-card {
  border-radius: 14px;
  padding: 0.95rem 0.85rem;
  display: flex;
  align-items: center;
  gap: 0.7rem;
  transition: transform 0.2s ease, box-shadow 0.2s ease;
  border: 1px solid transparent;
  min-width: 0;
}

.stat-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 8px 25px rgba(0,0,0,.08);
}

/* 各统计卡片颜色方案 */
.stat-card-blue {
  background: linear-gradient(135deg, #eff6ff 0%, #dbeafe 100%);
  border-color: #bfdbfe;
}
.stat-card-blue .stat-icon {
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
}

.stat-card-green {
  background: linear-gradient(135deg, #f0fdf4 0%, #dcfce7 100%);
  border-color: #bbf7d0;
}
.stat-card-green .stat-icon {
  background: linear-gradient(135deg, #22c55e 0%, #16a34a 100%);
}

.stat-card-orange {
  background: linear-gradient(135deg, #fff7ed 0%, #ffedd5 100%);
  border-color: #fed7aa;
}
.stat-card-orange .stat-icon {
  background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
}

.stat-card-purple {
  background: linear-gradient(135deg, #faf5ff 0%, #f3e8ff 100%);
  border-color: #e9d5ff;
}
.stat-card-purple .stat-icon {
  background: linear-gradient(135deg, #a855f7 0%, #9333ea 100%);
}

.stat-icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.15rem;
  color: white;
  flex-shrink: 0;
  box-shadow: 0 4px 12px rgba(0,0,0,.12);
}

.stat-content {
  min-width: 0;
  flex: 1;
}

.stat-content h3 {
  font-size: 1.05rem;
  font-weight: 700;
  color: #1e293b;
  margin: 0 0 0.15rem 0;
  line-height: 1.25;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.stat-content p {
  color: #64748b;
  margin: 0;
  font-size: 0.72rem;
  font-weight: 500;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* 快速操作 */
.quick-actions {
  margin-bottom: 2.5rem;
}

.quick-actions h2 {
  font-size: 1.3rem;
  font-weight: 600;
  color: #1e293b;
  margin: 0 0 1.25rem 0;
}

.action-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.875rem;
}

.action-card {
  border-radius: 14px;
  padding: 0.9rem 0.85rem;
  display: flex;
  align-items: center;
  gap: 0.7rem;
  text-decoration: none;
  color: inherit;
  transition: all 0.25s ease;
  border: 1px solid #f1f5f9;
  background: #fafbfc;
  min-width: 0;
}

.action-card:hover {
  transform: translateY(-3px);
  box-shadow: 0 10px 30px rgba(0,0,0,.08);
  border-color: transparent;
}

.action-icon {
  width: 38px;
  height: 38px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 1.05rem;
  color: white;
  flex-shrink: 0;
  transition: transform .25s ease;
}

.action-card:hover .action-icon {
  transform: scale(1.08);
}

/* 差异化操作卡片配色 */
.action-card-blue { }
.action-card-blue:hover { background: linear-gradient(135deg, #eff6ff 0%, #dbeafe 100%); }
.action-card-blue .action-icon { background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%); box-shadow: 0 4px 14px rgba(59,130,246,.3); }

.action-card-cyan { }
.action-card-cyan:hover { background: linear-gradient(135deg, #ecfeff 0%, #cffafe 100%); }
.action-card-cyan .action-icon { background: linear-gradient(135deg, #06b6d4 0%, #0891b2 100%); box-shadow: 0 4px 14px rgba(6,182,212,.3); }

.action-card-purple { }
.action-card-purple:hover { background: linear-gradient(135deg, #faf5ff 0%, #f3e8ff 100%); }
.action-card-purple .action-icon { background: linear-gradient(135deg, #a855f7 0%, #9333ea 100%); box-shadow: 0 4px 14px rgba(168,85,247,.3); }

.action-card-green { }
.action-card-green:hover { background: linear-gradient(135deg, #f0fdf4 0%, #dcfce7 100%); }
.action-card-green .action-icon { background: linear-gradient(135deg, #22c55e 0%, #16a34a 100%); box-shadow: 0 4px 14px rgba(34,197,94,.3); }

.action-content {
  flex: 1;
  min-width: 0;
}

.action-content h3 {
  font-size: 0.92rem;
  font-weight: 600;
  color: #1e293b;
  margin: 0 0 0.15rem 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.action-content p {
  color: #64748b;
  margin: 0;
  font-size: 0.72rem;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.action-arrow {
  color: #cbd5e1;
  font-size: 0.95rem;
  flex-shrink: 0;
  transition: color .2s, transform .2s;
}
.action-card:hover .action-arrow {
  color: #475569;
  transform: translateX(3px);
}

/* 最近活动 - 时间线风格 */
.recent-activity h2 {
  font-size: 1.3rem;
  font-weight: 600;
  color: #1e293b;
  margin: 0 0 1.25rem 0;
}

.activity-list {
  display: flex;
  flex-direction: column;
}

.activity-empty {
  padding: 1rem 0;
}

.activity-item {
  display: flex;
  gap: 1rem;
  position: relative;
}

.activity-timeline {
  display: flex;
  flex-direction: column;
  align-items: center;
  position: relative;
}

.timeline-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  flex-shrink: 0;
  margin-top: 6px;
  z-index: 1;
}

.timeline-line {
  width: 2px;
  flex: 1;
  min-height: 20px;
  background: #e2e8f0;
}

.dot-blue { background: #3b82f6; box-shadow: 0 0 0 3px rgba(59,130,246,.2); }
.dot-green { background: #22c55e; box-shadow: 0 0 0 3px rgba(34,197,94,.2); }
.dot-orange { background: #f97316; box-shadow: 0 0 0 3px rgba(249,115,22,.2); }
.dot-red { background: #ef4444; box-shadow: 0 0 0 3px rgba(239,68,68,.2); }
.dot-cyan { background: #06b6d4; box-shadow: 0 0 0 3px rgba(6,182,212,.2); }
.dot-gray { background: #94a3b8; box-shadow: 0 0 0 3px rgba(148,163,184,.2); }

.activity-body {
  flex: 1;
  padding-bottom: 1.25rem;
}

.activity-text {
  color: #334155;
  margin: 0 0 0.2rem 0;
  font-weight: 500;
  font-size: .925rem;
  line-height: 1.5;
}

.activity-time {
  color: #94a3b8;
  font-size: 0.8rem;
}

/* 响应式设计 */
/* 平板：侧栏堆叠到上方，卡片改为两列 */
@media (max-width: 1024px) {
  .user-center-layout {
    grid-template-columns: 1fr;
    gap: 1.5rem;
  }

  .stats-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 1rem;
  }

  .action-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 1rem;
  }

  .stat-content h3 {
    font-size: 1.25rem;
  }

  .stat-content p,
  .action-content p {
    font-size: 0.8rem;
  }

  .action-content h3 {
    font-size: 1rem;
  }

  .stat-icon {
    width: 44px;
    height: 44px;
    font-size: 1.3rem;
  }

  .action-icon {
    width: 42px;
    height: 42px;
    font-size: 1.15rem;
  }
}

/* 手机：单列 */
@media (max-width: 768px) {
  .user-center {
    padding: 1rem 0;
  }

  .main-content {
    padding: 1.5rem;
  }

  .stats-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 0.875rem;
  }

  .action-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 480px) {
  .stats-grid {
    grid-template-columns: 1fr;
  }

  .stat-card,
  .action-card {
    padding: 1rem;
  }
}
</style>