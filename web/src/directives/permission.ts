import { Directive, DirectiveBinding } from 'vue'
import { useMenuStore } from '../stores/menu'
import { useAuthStore } from '../stores/auth'

/**
 * 判断当前登录用户是否为超级管理员。
 * 唯一权威来源：数据库 ginkgo_Sys_Role.IsSuperAdmin=1。
 * 后端 /auth/login 与 /auth/refresh 都会把该结果以 isSuperAdmin 字段返回，
 * 前端登录后写入 useAuthStore().isSuperAdmin 并持久化到 localStorage。
 * 不再依赖角色编码字符串（如 "ADMIN"）做匹配，避免角色编码巧合导致的越权或漏判。
 */
function isSuperAdmin(): boolean {
  try {
    const auth = useAuthStore()
    return !!auth.isSuperAdmin
  } catch {
    return false
  }
}

/** 权限校验：超级管理员直接放行；否则按按钮权限码匹配 */
function check(value: unknown): boolean {
  if (!value) return true
  if (isSuperAdmin()) return true
  const menuStore = useMenuStore()
  return menuStore.hasButtonPermission(value as string)
}

// 权限指令
export const permission: Directive = {
  mounted(el: HTMLElement, binding: DirectiveBinding) {
    if (!check(binding.value)) {
      // 没有权限则隐藏元素
      el.style.display = 'none'
    }
  },

  updated(el: HTMLElement, binding: DirectiveBinding) {
    if (!check(binding.value)) {
      el.style.display = 'none'
    } else {
      el.style.display = ''
    }
  }
}

// 权限检查函数（用于在组件中使用）
export function hasPermission(buttonCode: string): boolean {
  if (!buttonCode) return true
  if (isSuperAdmin()) return true
  const menuStore = useMenuStore()
  return menuStore.hasButtonPermission(buttonCode)
}