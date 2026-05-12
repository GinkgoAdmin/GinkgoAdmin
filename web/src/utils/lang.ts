/**
 * 主框架多语言工具函数
 * 提供全局语言状态管理、解析、翻译等核心能力
 * 所有插件应从此模块导入多语言功能，而非各自实现
 */
import { ref, watch, computed } from 'vue'

// ====================================================================
// 语言代码映射（数据库存储代码 ↔ URL 短代码）
// ====================================================================
const langCodeMap: Record<string, string> = {
  'zh-CN': 'zh',
  'en': 'en',
  'ja': 'ja',
  'ko': 'ko',
  'fr': 'fr',
  'de': 'de',
  'es': 'es',
  'pt': 'pt',
  'ru': 'ru',
  'ar': 'ar',
}
const reverseLangCodeMap: Record<string, string> = Object.fromEntries(
  Object.entries(langCodeMap).map(([k, v]) => [v, k])
)

/** 数据库代码转 URL 短代码 */
export function toUrlCode(dbCode: string): string {
  return langCodeMap[dbCode] || dbCode.split('-')[0]
}

/** URL 短代码转数据库代码 */
export function toDbCode(urlCode: string): string {
  return reverseLangCodeMap[urlCode] || urlCode
}

// ====================================================================
// 语言配置类型
// ====================================================================
export interface LangItem {
  code: string       // 数据库代码，如 zh-CN, en
  urlCode: string    // URL 短代码，如 zh, en
  label: string      // 显示名称
  flag: string       // 国旗 emoji
  required: boolean  // 是否必填
}

// ====================================================================
// 全局响应式状态（单例）
// ====================================================================
const STORAGE_KEY = 'ginkgo:locale'

// 默认语言列表（当后端配置尚未加载时使用）
const defaultLangs: LangItem[] = [
  { code: 'zh-CN', urlCode: 'zh', label: '中文', flag: '🇨🇳', required: true },
  { code: 'en', urlCode: 'en', label: 'English', flag: '🇺🇸', required: false },
]

// 多语言是否启用（从后端配置加载）
const multiLangEnabled = ref(true)

// 可用语言列表（从后端配置加载）
const availableLangs = ref<LangItem[]>([...defaultLangs])

// 默认语言代码（数据库代码）
const defaultLang = ref('zh-CN')

// 当前选中的语言（数据库代码）
const savedLang = typeof localStorage !== 'undefined' ? localStorage.getItem(STORAGE_KEY) : null
const currentLang = ref<string>(savedLang || 'zh-CN')

// 当前语言 URL 短代码
const currentUrlCode = computed(() => toUrlCode(currentLang.value))

// 持久化到 localStorage
watch(currentLang, (lang) => {
  if (typeof localStorage !== 'undefined') {
    localStorage.setItem(STORAGE_KEY, lang)
  }
})

// ====================================================================
// 全局翻译触发器
// ====================================================================
const translateAllTrigger = ref(0)

/** 触发整页翻译（由 AdminLangSwitcher 调用） */
export function triggerTranslateAll() {
  translateAllTrigger.value++
}

/** 获取整页翻译触发器（由 LangInput 监听） */
export function useTranslateAllTrigger() {
  return translateAllTrigger
}

// ====================================================================
// 语言状态 API
// ====================================================================

/** 获取多语言是否启用 */
export function isMultiLangEnabled(): boolean {
  return multiLangEnabled.value
}

/** 获取多语言启用状态的响应式引用 */
export function useMultiLangEnabled() {
  return multiLangEnabled
}

/** 获取可用语言列表 */
export function getAvailableLangs(): LangItem[] {
  return availableLangs.value
}

/** 获取可用语言列表的响应式引用 */
export function useAvailableLangs() {
  return availableLangs
}

/** 获取默认语言代码 */
export function getDefaultLang(): string {
  return defaultLang.value
}

/** 获取默认语言 URL 短代码 */
export function getDefaultUrlCode(): string {
  return toUrlCode(defaultLang.value)
}

/** 获取当前语言代码（数据库代码） */
export function getCurrentLang(): string {
  return currentLang.value
}

/** 获取当前语言的响应式引用 */
export function useLangRef() {
  return currentLang
}

/** 获取当前 URL 短代码的计算属性 */
export function useCurrentUrlCode() {
  return currentUrlCode
}

/**
 * 切换语言
 * @param lang 可以是数据库代码(zh-CN)或 URL 短代码(zh)
 */
export function switchLang(lang: string) {
  // 尝试 URL 短代码转换
  const dbCode = reverseLangCodeMap[lang] || lang
  const found = availableLangs.value.find(l => l.code === dbCode)
  if (found) {
    currentLang.value = dbCode
  }
}

/**
 * 验证语言代码是否合法（支持 URL 短代码和数据库代码）
 */
export function isValidLang(code: string): boolean {
  const dbCode = reverseLangCodeMap[code] || code
  return availableLangs.value.some(l => l.code === dbCode)
}

/**
 * 初始化/更新语言配置（由 useLanguageStore 调用）
 */
export function setLangConfig(config: {
  enabled: boolean
  langs: LangItem[]
  defaultLang: string
}) {
  multiLangEnabled.value = config.enabled
  availableLangs.value = config.langs.length > 0 ? config.langs : [...defaultLangs]
  defaultLang.value = config.defaultLang || 'zh-CN'

  // 如果当前语言不在可用列表中，切换到默认语言
  if (!availableLangs.value.find(l => l.code === currentLang.value)) {
    currentLang.value = defaultLang.value
  }
}

// ====================================================================
// 多语言解析函数
// ====================================================================

/**
 * 解析多语言 JSON 字符串，返回当前语言的值
 * 支持格式：{"zh-CN":"中文内容","en":"English"} 或 纯字符串
 * 如果当前语言没有内容，按回退规则查找
 */
export function parseLang(v: any): string {
  if (!v) return ''
  // 如果已经是对象（如区块组件接收到的已解析 props），直接按语言取值
  if (typeof v === 'object' && v !== null) {
    if (v[currentLang.value]) return v[currentLang.value]
    if (v[defaultLang.value]) return v[defaultLang.value]
    if (v['zh-CN']) return v['zh-CN']
    const vals = Object.values(v)
    return vals.length > 0 ? String(vals[0]) : ''
  }
  // 字符串类型：尝试 JSON 解析
  if (typeof v !== 'string') return String(v)
  try {
    // 移除导致解析报错的非法控制字符（换行、回车等）
    const sanitized = v.replace(/[\n\r\t]/g, ' ')
    const o = JSON.parse(sanitized)
    if (typeof o === 'string') return o
    if (typeof o !== 'object' || o === null) return v
    // 优先当前语言
    if (o[currentLang.value]) return o[currentLang.value]
    // 回退到默认语言
    if (o[defaultLang.value]) return o[defaultLang.value]
    // 回退到 zh-CN
    if (o['zh-CN']) return o['zh-CN']
    // 回退到第一个有值的
    const vals = Object.values(o)
    return vals.length > 0 ? String(vals[0]) : v
  } catch {
    return v
  }
}

/**
 * 解析多语言 JSON 中的图片 URL
 */
export function parseImg(v: string | null | undefined): string {
  if (!v) return ''
  try {
    const sanitized = v.replace(/[\n\r\t]/g, ' ')
    const o = JSON.parse(sanitized)
    if (typeof o === 'string') return o
    return o[currentLang.value] || o[defaultLang.value] || o['zh-CN'] || Object.values(o)[0] || ''
  } catch {
    return v
  }
}

/**
 * 获取多语言 JSON 中指定语言的值
 */
export function getLangValue(json: string | null | undefined, lang: string): string {
  if (!json) return ''
  try {
    const sanitized = json.replace(/[\n\r\t]/g, ' ')
    const o = JSON.parse(sanitized)
    if (typeof o === 'string') return o
    return o[lang] || ''
  } catch {
    return json
  }
}

// ====================================================================
// 静态 UI 文案翻译
// ====================================================================
const uiTexts: Record<string, Record<string, string>> = {
  'home': { 'zh-CN': '首页', 'en': 'Home' },
  'search': { 'zh-CN': '搜索', 'en': 'Search' },
  'contact_us': { 'zh-CN': '联系我们', 'en': 'Contact Us' },
  'learn_more': { 'zh-CN': '了解更多', 'en': 'Learn More' },
  'recommended': { 'zh-CN': '推荐阅读', 'en': 'Featured' },
  'recommended_desc': { 'zh-CN': '精选优质内容，不容错过', 'en': 'Selected premium content' },
  'latest': { 'zh-CN': '最新文章', 'en': 'Latest Articles' },
  'latest_desc': { 'zh-CN': '了解最新动态', 'en': 'Stay updated' },
  'pinned': { 'zh-CN': '置顶', 'en': 'Pinned' },
  'quick_links': { 'zh-CN': '快速链接', 'en': 'Quick Links' },
  'more': { 'zh-CN': '更多', 'en': 'More' },
  'about_us': { 'zh-CN': '关于我们', 'en': 'About Us' },
  'services': { 'zh-CN': '服务支持', 'en': 'Services' },
  'privacy': { 'zh-CN': '隐私政策', 'en': 'Privacy Policy' },
  'no_articles': { 'zh-CN': '暂无文章，敬请期待', 'en': 'No articles yet, stay tuned' },
  'views': { 'zh-CN': '浏览', 'en': 'views' },
  'likes': { 'zh-CN': '点赞', 'en': 'likes' },
  'search_placeholder': { 'zh-CN': '输入关键词搜索...', 'en': 'Search keywords...' },
  'search_results': { 'zh-CN': '共找到 {n} 条结果', 'en': '{n} results found' },
  'your_name': { 'zh-CN': '您的姓名', 'en': 'Your Name' },
  'your_email': { 'zh-CN': '您的邮箱', 'en': 'Your Email' },
  'your_phone': { 'zh-CN': '联系电话', 'en': 'Phone' },
  'message': { 'zh-CN': '留言内容', 'en': 'Message' },
  'submit': { 'zh-CN': '提交', 'en': 'Submit' },
  'send_success': { 'zh-CN': '发送成功', 'en': 'Sent Successfully' },
  'send_success_desc': { 'zh-CN': '我们会尽快回复您', 'en': 'We will reply soon' },
  'comments': { 'zh-CN': '评论', 'en': 'Comments' },
  'write_comment': { 'zh-CN': '写评论...', 'en': 'Write a comment...' },
  'post_comment': { 'zh-CN': '发表评论', 'en': 'Post Comment' },
  'paid_content': { 'zh-CN': '付费内容', 'en': 'Premium Content' },
  'hot_articles': { 'zh-CN': '热门文章', 'en': 'Hot Articles' },
  'tags': { 'zh-CN': '标签', 'en': 'Tags' },
  'not_found': { 'zh-CN': '页面未找到', 'en': 'Page Not Found' },
  'back_home': { 'zh-CN': '返回首页', 'en': 'Back to Home' },

  // ===== 登录页 =====
  'login_platform': { 'zh-CN': 'Ginkgo平台', 'en': 'Ginkgo Platform' },
  'login_welcome': { 'zh-CN': '欢迎回来', 'en': 'Welcome Back' },
  'login_subtitle': { 'zh-CN': '登录您的账户以继续使用', 'en': 'Sign in to your account to continue' },
  'login_username': { 'zh-CN': '用户名 / 邮箱 / 手机号', 'en': 'Username / Email / Phone' },
  'login_password': { 'zh-CN': '密码', 'en': 'Password' },
  'login_remember': { 'zh-CN': '记住我', 'en': 'Remember me' },
  'login_forgot': { 'zh-CN': '忘记密码？', 'en': 'Forgot password?' },
  'login_btn': { 'zh-CN': '登录', 'en': 'Sign In' },
  'login_loading': { 'zh-CN': '登录中...', 'en': 'Signing in...' },
  'login_success': { 'zh-CN': '登录成功', 'en': 'Login successful' },
  'login_no_account': { 'zh-CN': '还没有账户？', 'en': "Don't have an account?" },
  'login_register': { 'zh-CN': '立即注册', 'en': 'Register now' },
  'login_closed': { 'zh-CN': '当前已关闭注册', 'en': 'Registration is closed' },
  'login_username_required': { 'zh-CN': '请输入用户名、邮箱或手机号', 'en': 'Please enter username, email or phone' },
  'login_password_required': { 'zh-CN': '请输入密码', 'en': 'Please enter password' },
  'login_third_party_fail': { 'zh-CN': '第三方登录失败', 'en': 'Third-party login failed' },
  'login_error': { 'zh-CN': '登录过程中发生错误', 'en': 'An error occurred during login' },

  // ===== 用户侧栏 =====
  'sidebar_center': { 'zh-CN': '个人中心', 'en': 'Dashboard' },
  'sidebar_profile': { 'zh-CN': '资料管理', 'en': 'Profile' },
  'sidebar_notifications': { 'zh-CN': '我的通知', 'en': 'Notifications' },
  'sidebar_logs': { 'zh-CN': '操作日志', 'en': 'Activity Log' },
  'sidebar_logout': { 'zh-CN': '退出登录', 'en': 'Sign Out' },
  'role_admin': { 'zh-CN': '管理员', 'en': 'Admin' },
  'role_user': { 'zh-CN': '普通用户', 'en': 'User' },
  'role_demo': { 'zh-CN': '演示用户', 'en': 'Demo' },
  'role_visitor': { 'zh-CN': '访客', 'en': 'Visitor' },
  'role_default': { 'zh-CN': '用户', 'en': 'Member' },

  // ===== 个人中心 =====
  'uc_title': { 'zh-CN': '个人中心', 'en': 'Dashboard' },
  'uc_welcome': { 'zh-CN': '欢迎回来，{name}！', 'en': 'Welcome back, {name}!' },
  'uc_login_days': { 'zh-CN': '登录天数', 'en': 'Login Days' },
  'uc_last_login': { 'zh-CN': '最后登录', 'en': 'Last Login' },
  'uc_op_count': { 'zh-CN': '操作次数', 'en': 'Operations' },
  'uc_fav_count': { 'zh-CN': '收藏数量', 'en': 'Favorites' },
  'uc_quick_actions': { 'zh-CN': '快速操作', 'en': 'Quick Actions' },
  'uc_edit_profile': { 'zh-CN': '编辑资料', 'en': 'Edit Profile' },
  'uc_edit_profile_desc': { 'zh-CN': '更新个人信息和头像', 'en': 'Update your personal info and avatar' },
  'uc_view_logs': { 'zh-CN': '查看日志', 'en': 'View Logs' },
  'uc_view_logs_desc': { 'zh-CN': '查看最近的操作记录', 'en': 'View recent activity history' },
  'uc_download': { 'zh-CN': '下载中心', 'en': 'Downloads' },
  'uc_download_desc': { 'zh-CN': '获取最新版本和工具', 'en': 'Get latest versions and tools' },
  'uc_docs': { 'zh-CN': '文档中心', 'en': 'Documentation' },
  'uc_docs_desc': { 'zh-CN': '查看使用文档和教程', 'en': 'View guides and tutorials' },
  'uc_recent': { 'zh-CN': '最近活动', 'en': 'Recent Activity' },
  'uc_login_system': { 'zh-CN': '登录系统', 'en': 'System Login' },
  'uc_logout_system': { 'zh-CN': '退出登录', 'en': 'System Logout' },
  'uc_create_op': { 'zh-CN': '创建/提交操作', 'en': 'Create/Submit' },
  'uc_update_op': { 'zh-CN': '更新数据', 'en': 'Update Data' },
  'uc_delete_op': { 'zh-CN': '删除数据', 'en': 'Delete Data' },
  'uc_other_op': { 'zh-CN': '查看或其他操作', 'en': 'View or Other' },

  // ===== 资料管理 =====
  'profile_title': { 'zh-CN': '资料管理', 'en': 'Profile Settings' },
  'profile_subtitle': { 'zh-CN': '管理您的个人信息和密码安全', 'en': 'Manage your personal info and password security' },
  'profile_basic': { 'zh-CN': '基本信息', 'en': 'Basic Information' },
  'profile_basic_desc': { 'zh-CN': '更新您的基本个人信息', 'en': 'Update your basic personal info' },
  'profile_avatar': { 'zh-CN': '头像', 'en': 'Avatar' },
  'profile_upload_avatar': { 'zh-CN': '上传头像', 'en': 'Upload Avatar' },
  'profile_avatar_tip': { 'zh-CN': '支持 JPG、PNG 格式，文件大小不超过 2MB', 'en': 'JPG, PNG format, max 2MB' },
  'profile_avatar_selected': { 'zh-CN': '已选择头像', 'en': 'Avatar selected' },
  'profile_username': { 'zh-CN': '用户名', 'en': 'Username' },
  'profile_username_tip': { 'zh-CN': '用户名不可修改', 'en': 'Username cannot be changed' },
  'profile_name': { 'zh-CN': '姓名', 'en': 'Name' },
  'profile_name_ph': { 'zh-CN': '请输入您的姓名', 'en': 'Enter your name' },
  'profile_email': { 'zh-CN': '邮箱', 'en': 'Email' },
  'profile_email_ph': { 'zh-CN': '请输入邮箱地址', 'en': 'Enter email address' },
  'profile_phone': { 'zh-CN': '手机号', 'en': 'Phone' },
  'profile_phone_ph': { 'zh-CN': '请输入手机号码', 'en': 'Enter phone number' },
  'profile_bio': { 'zh-CN': '个人简介', 'en': 'Bio' },
  'profile_bio_ph': { 'zh-CN': '简单介绍一下自己...', 'en': 'Tell us about yourself...' },
  'profile_save': { 'zh-CN': '保存修改', 'en': 'Save Changes' },
  'profile_reset': { 'zh-CN': '重置', 'en': 'Reset' },
  'profile_save_ok': { 'zh-CN': '个人信息保存成功', 'en': 'Profile saved successfully' },
  'profile_save_fail': { 'zh-CN': '保存失败', 'en': 'Save failed' },
  'profile_load_fail': { 'zh-CN': '加载个人资料失败', 'en': 'Failed to load profile' },
  'profile_reset_ok': { 'zh-CN': '已重置为原始信息', 'en': 'Reset to original info' },
  'profile_chpwd': { 'zh-CN': '修改密码', 'en': 'Change Password' },
  'profile_chpwd_desc': { 'zh-CN': '定期更新密码以保护账户安全', 'en': 'Update password regularly for account security' },
  'profile_cur_pwd': { 'zh-CN': '当前密码', 'en': 'Current Password' },
  'profile_cur_pwd_ph': { 'zh-CN': '请输入当前密码', 'en': 'Enter current password' },
  'profile_new_pwd': { 'zh-CN': '新密码', 'en': 'New Password' },
  'profile_new_pwd_ph': { 'zh-CN': '请输入新密码', 'en': 'Enter new password' },
  'profile_confirm_pwd': { 'zh-CN': '确认密码', 'en': 'Confirm Password' },
  'profile_confirm_pwd_ph': { 'zh-CN': '请再次输入新密码', 'en': 'Re-enter new password' },
  'profile_chpwd_ok': { 'zh-CN': '密码修改成功', 'en': 'Password changed successfully' },
  'profile_chpwd_fail': { 'zh-CN': '密码修改失败', 'en': 'Password change failed' },
  'v_name_required': { 'zh-CN': '请输入姓名', 'en': 'Name is required' },
  'v_name_length': { 'zh-CN': '姓名长度在 2 到 20 个字符', 'en': 'Name must be 2-20 characters' },
  'v_email_format': { 'zh-CN': '请输入正确的邮箱地址', 'en': 'Please enter a valid email' },
  'v_phone_format': { 'zh-CN': '请输入正确的手机号码', 'en': 'Please enter a valid phone number' },
  'v_cur_pwd_required': { 'zh-CN': '请输入当前密码', 'en': 'Current password is required' },
  'v_new_pwd_required': { 'zh-CN': '请输入新密码', 'en': 'New password is required' },
  'v_pwd_length': { 'zh-CN': '密码长度在 6 到 20 个字符', 'en': 'Password must be 6-20 characters' },
  'v_confirm_pwd': { 'zh-CN': '请确认新密码', 'en': 'Please confirm password' },
  'v_pwd_mismatch': { 'zh-CN': '两次输入的密码不一致', 'en': 'Passwords do not match' },

  // ===== 操作日志 =====
  'logs_title': { 'zh-CN': '操作日志', 'en': 'Activity Log' },
  'logs_subtitle': { 'zh-CN': '查看您的账户操作记录和活动历史', 'en': 'View your account activity and history' },
  'logs_type': { 'zh-CN': '日志类型', 'en': 'Log Type' },
  'logs_all': { 'zh-CN': '全部', 'en': 'All' },
  'logs_login': { 'zh-CN': '登录', 'en': 'Login' },
  'logs_operation': { 'zh-CN': '操作', 'en': 'Operation' },
  'logs_setting': { 'zh-CN': '设置', 'en': 'Settings' },
  'logs_security': { 'zh-CN': '安全', 'en': 'Security' },
  'logs_other': { 'zh-CN': '其他', 'en': 'Other' },
  'logs_date_range': { 'zh-CN': '至', 'en': 'to' },
  'logs_start_date': { 'zh-CN': '开始日期', 'en': 'Start Date' },
  'logs_end_date': { 'zh-CN': '结束日期', 'en': 'End Date' },
  'logs_refresh': { 'zh-CN': '刷新', 'en': 'Refresh' },
  'logs_export': { 'zh-CN': '导出', 'en': 'Export' },
  'logs_empty': { 'zh-CN': '暂无日志记录', 'en': 'No logs found' },
  'logs_refreshed': { 'zh-CN': '日志已刷新', 'en': 'Logs refreshed' },
  'logs_refresh_fail': { 'zh-CN': '刷新失败', 'en': 'Refresh failed' },
  'logs_load_fail': { 'zh-CN': '加载日志失败', 'en': 'Failed to load logs' },
  'logs_export_na': { 'zh-CN': '导出功能暂未开放，敬请期待', 'en': 'Export feature coming soon' },
  'logs_exporting': { 'zh-CN': '正在导出当前筛选条件下的日志...', 'en': 'Exporting logs under current filter...' },
  'logs_export_success': { 'zh-CN': '日志已导出 ({n} 条)', 'en': '{n} logs exported' },
  'logs_export_empty': { 'zh-CN': '当前筛选条件下没有可导出的日志', 'en': 'No logs match the current filter' },
  'logs_export_fail': { 'zh-CN': '导出失败，请稍后重试', 'en': 'Export failed, please try again' },
  'logs_success': { 'zh-CN': '成功', 'en': 'Success' },
  'logs_failed': { 'zh-CN': '失败', 'en': 'Failed' },
  'logs_warning': { 'zh-CN': '警告', 'en': 'Warning' },
  'logs_unknown': { 'zh-CN': '未知', 'en': 'Unknown' },
  'time_min_ago': { 'zh-CN': '{n}分钟前', 'en': '{n}m ago' },
  'time_hour_ago': { 'zh-CN': '{n}小时前', 'en': '{n}h ago' },
  'time_day_ago': { 'zh-CN': '{n}天前', 'en': '{n}d ago' },
  'time_just_now': { 'zh-CN': '刚刚', 'en': 'Just now' },

  // ===== 通知 =====
  'notify_title': { 'zh-CN': '我的通知', 'en': 'Notifications' },
  'notify_subtitle': { 'zh-CN': '点击通知可查看正文与附件', 'en': 'Click to view details and attachments' },
  'notify_all': { 'zh-CN': '全部', 'en': 'All' },
  'notify_unread': { 'zh-CN': '未读', 'en': 'Unread' },
  'notify_read': { 'zh-CN': '已读', 'en': 'Read' },
  'notify_mark_all': { 'zh-CN': '全部标记为已读', 'en': 'Mark all as read' },
  'notify_mark_read': { 'zh-CN': '标记已读', 'en': 'Mark read' },
  'notify_marked': { 'zh-CN': '已标记为已读', 'en': 'Marked as read' },
  'notify_mark_all_ok': { 'zh-CN': '已全部标记为已读', 'en': 'All marked as read' },
  'notify_mark_fail': { 'zh-CN': '标记已读失败', 'en': 'Failed to mark as read' },
  'notify_mark_all_fail': { 'zh-CN': '批量标记已读失败', 'en': 'Batch mark failed' },
  'notify_load_fail': { 'zh-CN': '加载通知失败', 'en': 'Failed to load notifications' },
  'notify_detail_fail': { 'zh-CN': '加载通知详情失败', 'en': 'Failed to load notification details' },
  'notify_empty': { 'zh-CN': '暂无通知', 'en': 'No notifications' },
  'notify_content': { 'zh-CN': '通知内容', 'en': 'Content' },
  'notify_no_content': { 'zh-CN': '暂无内容', 'en': 'No content' },
  'notify_attachments': { 'zh-CN': '附件', 'en': 'Attachments' },
  'notify_loading': { 'zh-CN': '正在加载详情...', 'en': 'Loading details...' },
  'notify_image': { 'zh-CN': '图片', 'en': 'Image' },
  'notify_video': { 'zh-CN': '视频', 'en': 'Video' },
  'notify_audio': { 'zh-CN': '音频', 'en': 'Audio' },
  'notify_document': { 'zh-CN': '文档', 'en': 'Document' },
  'notify_download': { 'zh-CN': '下载', 'en': 'Download' },
  'notify_play': { 'zh-CN': '点击播放', 'en': 'Click to play' },
  'notify_load_fail_img': { 'zh-CN': '加载失败', 'en': 'Load failed' },
  'notify_close': { 'zh-CN': '关闭', 'en': 'Close' },
  'notify_unnamed': { 'zh-CN': '未命名文件', 'en': 'Unnamed file' },
  'notify_unknown_size': { 'zh-CN': '未知大小', 'en': 'Unknown size' },

  // ===== 通用 =====
  'confirm': { 'zh-CN': '确定', 'en': 'Confirm' },
  'cancel': { 'zh-CN': '取消', 'en': 'Cancel' },
  'tip': { 'zh-CN': '提示', 'en': 'Notice' },
  'confirm_logout': { 'zh-CN': '确定要退出登录吗？', 'en': 'Are you sure you want to sign out?' },
  'logged_out': { 'zh-CN': '已退出登录', 'en': 'Signed out' },
}

/**
 * 注册额外的 UI 翻译文案（插件可调用以扩展）
 */
export function registerUiTexts(texts: Record<string, Record<string, string>>) {
  Object.assign(uiTexts, texts)
}

/**
 * 获取静态 UI 文案翻译
 */
export function t(key: string, params?: Record<string, any>): string {
  const texts = uiTexts[key]
  if (!texts) return key
  let text = texts[currentLang.value] || texts[defaultLang.value] || texts['zh-CN'] || key
  if (params) {
    Object.keys(params).forEach(k => {
      text = text.replace(`{${k}}`, String(params[k]))
    })
  }
  return text
}

// ====================================================================
// 翻译 API 工具
// ====================================================================

// MyMemory API 语言代码映射
const translateCodeMap: Record<string, string> = {
  'zh-CN': 'zh',
  'en': 'en',
  'ja': 'ja',
  'ko': 'ko',
  'fr': 'fr',
  'de': 'de',
  'es': 'es',
  'pt': 'pt',
  'ru': 'ru',
  'ar': 'ar',
}

/**
 * 调用翻译 API 翻译文本
 */
export async function translateText(text: string, fromCode: string, toCode: string): Promise<string | null> {
  const fromApi = translateCodeMap[fromCode] || fromCode
  const toApi = translateCodeMap[toCode] || toCode
  try {
    const url = `https://api.mymemory.translated.net/get?q=${encodeURIComponent(text)}&langpair=${fromApi}|${toApi}`
    const res = await fetch(url)
    const data = await res.json()
    if (data.responseStatus === 200 && data.responseData?.translatedText) {
      return data.responseData.translatedText
    }
  } catch (e) {
    console.warn(`翻译 ${fromCode} -> ${toCode} 失败:`, e)
  }
  return null
}

// ====================================================================
// URL 工具
// ====================================================================

/**
 * 为前台链接添加语言前缀
 * 如果多语言未启用，返回原路径
 * @param path 原始路径，如 /web/docs
 * @param lang 可选，指定语言代码
 */
export function langUrl(path: string, lang?: string): string {
  if (!multiLangEnabled.value) return path
  const urlCode = lang ? toUrlCode(lang) : currentUrlCode.value
  // 如果路径已有语言前缀则替换
  const cleaned = path.replace(/^\/[a-z]{2}(?:\/|$)/, '/')
  return `/${urlCode}${cleaned.startsWith('/') ? '' : '/'}${cleaned}`
}
