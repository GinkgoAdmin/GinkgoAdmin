/**
 * 角色编辑器「功能权限」树勾选联动测试。
 *
 * 规则：
 * - 勾选上级：自动勾选全部下级
 * - 取消全部下级：上级仍可独立保留
 * - 勾选下级：自动向上勾选祖先
 */
import { describe, it, expect } from 'vitest'
import {
  applyPermissionCheckCascade,
  collectPermissionDescendantIds,
  findPermissionAncestorIds
} from '../rolePermission.utils'
import type { PermissionTreeNode } from '../../../../api/role'

function buildPermissionTree(): PermissionTreeNode[] {
  return [
    {
      id: 'dir-1',
      name: '系统管理',
      type: 'Directory',
      children: [
        {
          id: 'menu-dict',
          name: '数据字典',
          type: 'Item',
          code: 'sys:dicts',
          children: [
            {
              id: 'btn-add',
              name: '分类：新增',
              type: 'Button',
              code: 'sys:dicts:cat:add',
              children: [
                { id: 'api-add', name: '创建分类接口', type: 'Api', code: 'sys:dicts:cat:add:api' }
              ]
            },
            {
              id: 'btn-edit',
              name: '分类：编辑',
              type: 'Button',
              code: 'sys:dicts:cat:edit',
              children: [
                { id: 'api-edit', name: '更新分类接口', type: 'Api', code: 'sys:dicts:cat:edit:api' }
              ]
            }
          ]
        }
      ]
    }
  ]
}

describe('rolePermission.utils', () => {
  const tree = buildPermissionTree()
  const menuDict = tree[0].children![0]
  const btnAdd = menuDict.children![0]

  it('collectPermissionDescendantIds 应收集全部下级', () => {
    expect(collectPermissionDescendantIds(menuDict)).toEqual([
      'btn-add',
      'api-add',
      'btn-edit',
      'api-edit'
    ])
  })

  it('findPermissionAncestorIds 应返回祖先链', () => {
    expect(findPermissionAncestorIds(tree, 'api-add')).toEqual(['dir-1', 'menu-dict', 'btn-add'])
  })

  it('勾选上级菜单应自动全选下级', () => {
    const next = applyPermissionCheckCascade(tree, [], menuDict, true)
    expect(next).toEqual([
      'menu-dict',
      'btn-add',
      'api-add',
      'btn-edit',
      'api-edit',
      'dir-1'
    ])
  })

  it('取消全部下级后上级菜单仍可保留', () => {
    const allChecked = applyPermissionCheckCascade(tree, [], menuDict, true)
    let current = allChecked

    current = applyPermissionCheckCascade(tree, current, btnAdd, false)
    current = applyPermissionCheckCascade(tree, current, menuDict.children![1], false)

    expect(current).toEqual(['menu-dict', 'dir-1'])
  })

  it('勾选下级按钮应自动向上勾选菜单与目录', () => {
    const next = applyPermissionCheckCascade(tree, [], btnAdd, true)
    expect(next.sort()).toEqual(['btn-add', 'api-add', 'menu-dict', 'dir-1'].sort())
  })

  it('取消上级菜单应取消全部下级但不影响更上级以外的节点', () => {
    const checked = applyPermissionCheckCascade(tree, [], menuDict, true)
    const next = applyPermissionCheckCascade(tree, checked, menuDict, false)
    expect(next).toEqual(['dir-1'])
  })
})
