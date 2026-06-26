import type { PermissionTreeNode } from '../../../api/role'

/**
 * 角色编辑器「功能权限」树勾选联动的纯逻辑工具。
 *
 * 规则：
 * - 勾选节点：自动勾选全部下级，并向上勾选全部祖先（子权限需有菜单/目录入口）
 * - 取消节点：仅取消当前节点及其下级，不联动取消上级（上级菜单可独立保留）
 */

/** 递归收集某节点下全部子孙权限 Id */
export function collectPermissionDescendantIds(node: PermissionTreeNode): string[] {
  const ids: string[] = []
  const walk = (nodes?: PermissionTreeNode[]) => {
    for (const n of nodes || []) {
      ids.push(String(n.id))
      if (n.children?.length) walk(n.children)
    }
  }
  walk(node.children)
  return ids
}

/**
 * 在权限树中查找 targetId 的祖先链（不含 targetId 自身），自顶向下返回。
 */
export function findPermissionAncestorIds(
  nodes: PermissionTreeNode[],
  targetId: string,
  ancestors: string[] = []
): string[] | null {
  for (const node of nodes) {
    const nodeId = String(node.id)
    if (nodeId === String(targetId)) return ancestors
    if (node.children?.length) {
      const found = findPermissionAncestorIds(node.children, targetId, [...ancestors, nodeId])
      if (found) return found
    }
  }
  return null
}

/**
 * 功能权限树勾选联动：
 * - 勾选：当前节点 + 全部下级 + 全部上级
 * - 取消：仅当前节点 + 全部下级（上级保留）
 */
export function applyPermissionCheckCascade(
  tree: PermissionTreeNode[],
  currentCheckedKeys: string[],
  node: PermissionTreeNode,
  isChecking: boolean
): string[] {
  const nodeId = String(node.id)
  if (!isChecking) {
    const removeIds = new Set<string>([nodeId, ...collectPermissionDescendantIds(node)])
    return currentCheckedKeys.map(String).filter(id => !removeIds.has(id))
  }

  const next = new Set(currentCheckedKeys.map(String))
  next.add(nodeId)
  for (const id of collectPermissionDescendantIds(node)) {
    next.add(id)
  }
  for (const id of findPermissionAncestorIds(tree, nodeId) || []) {
    next.add(id)
  }
  return [...next]
}
