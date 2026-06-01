import type { GrantableMenuItem, GrantableItemNode } from '../../../api/menuGroup'

/**
 * 角色编辑器「业务入口授权」分区的纯逻辑工具。
 *
 * 这些函数从 Roles.vue 中抽离，便于在不挂载组件的前提下对核心数据流做单元测试：
 * - 收集「需授权」入口项 Id（保存时精确过滤的依据）
 * - 判定入口树节点是否禁用勾选（公共可见项禁用）
 * - 从各棵入口树已勾选 key 中收集需授权项（全量覆盖提交用）
 * - 终端类型中文标签与入口图标归一化
 *
 * 注意：雪花 Id（id / parentId）统一为字符串，禁止 Number() 转换。
 */

/**
 * 收集所有「需授权（requireGrant=true）」入口项的 Id 集合。
 * 递归遍历各端默认组下的入口树，作为保存时精确过滤的依据。
 */
export function collectRequireGrantItemIds(groups: GrantableMenuItem[]): Set<string> {
  const ids = new Set<string>()
  const walk = (nodes?: GrantableItemNode[]) => {
    (nodes || []).forEach(n => {
      if (n.requireGrant) ids.add(n.id)
      if (n.children?.length) walk(n.children)
    })
  }
  ;(groups || []).forEach(g => walk(g.items))
  return ids
}

/**
 * 判定业务入口树节点是否禁用勾选：
 * requireGrant===false（公共可见、无需勾选）的项禁用勾选。
 */
export function isPortalGrantNodeDisabled(node: GrantableItemNode): boolean {
  return node.requireGrant === false
}

/**
 * 从各棵入口树已勾选的 key 中，仅保留「需授权」项（全量覆盖提交用），并去重。
 *
 * @param checkedKeyGroups 各棵入口树各自已勾选的 key 列表
 * @param requireGrantIds 需授权入口项 Id 集合（由 collectRequireGrantItemIds 得到）
 * @returns 去重后的需授权入口项 Id 列表（雪花 Id 字符串）
 */
export function collectGrantedRequireGrantIds(
  checkedKeyGroups: string[][],
  requireGrantIds: Set<string>
): string[] {
  const result = new Set<string>()
  for (const keys of checkedKeyGroups) {
    for (const k of keys || []) {
      // 仅提交需授权项；公共可见项（requireGrant=false）无需授权，禁止勾选
      const id = String(k)
      if (requireGrantIds.has(id)) result.add(id)
    }
  }
  return [...result]
}

/** 终端类型中文标签 */
export function clientTypeLabel(clientType?: string): string {
  const map: Record<string, string> = {
    UNIAPP: '移动端',
    WEB_PORTAL: 'WEB前台',
    WPF: '桌面端'
  }
  return map[(clientType || '').toUpperCase()] || (clientType || '未知')
}

/** 入口图标归一化：直接使用声明的图标类（兼容 bi / ri 等图标库），缺省给通用图标 */
export function normalizeIcon(icon?: string | null): string {
  const v = (icon || '').trim()
  return v || 'bi bi-grid'
}
