/**
 * WEB 角色编辑器「业务入口授权」分区 UI/数据流测试。
 *
 * 测试范围（对应任务 19.1，需求 8.3 / 8.5 / 8.6）：
 *  1. 进入面板加载可授权入口：调用 getGrantableItems，按各端默认组渲染入口树；
 *  2. 进入角色回填勾选：getRoleItemPermissions 返回的已授权项被回填为勾选；
 *  3. RequireGrant=0（公共可见）项禁用勾选：用户无法勾选、也不会被纳入提交集合；
 *  4. 保存全量覆盖：setRoleItemPermissions 收到当前勾选的「需授权」项 Id 全集（去重、仅需授权项）。
 *
 * 说明：主框架 web 测试环境为 vitest（environment: node），未安装
 * @vue/test-utils / jsdom，因此不挂载完整组件，而是 mock API 模块并复用
 * Roles.vue 抽离到 rolePortalGrant.utils.ts 的真实数据流逻辑进行验证；
 * 并以 FakeElTree 忠实模拟 el-tree（node-key=id、check-strictly、disabled、
 * setCheckedKeys/getCheckedKeys）。浅色/深色主题以静态断言「admin-dark」样式钩子存在为证据，
 * 完整视觉主题验证为手动测试范畴。
 *
 * 雪花 Id 全程以字符串处理，断言时不做 Number() 转换。
 */
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { resolve, dirname } from 'node:path'

// ===== Mock API 模块：提供受控数据，避免加载 http/axios =====
const getGrantableItems = vi.fn()
const getRoleItemPermissions = vi.fn()
const setRoleItemPermissions = vi.fn()

vi.mock('../../../../api/menuGroup', () => ({
  getGrantableItems: (...args: any[]) => getGrantableItems(...args),
  getRoleItemPermissions: (...args: any[]) => getRoleItemPermissions(...args),
  setRoleItemPermissions: (...args: any[]) => setRoleItemPermissions(...args)
}))

import {
  collectRequireGrantItemIds,
  isPortalGrantNodeDisabled,
  collectGrantedRequireGrantIds,
  clientTypeLabel,
  normalizeIcon
} from '../rolePortalGrant.utils'
import type { GrantableMenuItem, GrantableItemNode } from '../../../../api/menuGroup'

// 经 mock 后再取出，供模拟器调用（与 Roles.vue 一致的 API 表面）
const api = await import('../../../../api/menuGroup')

// ===== 测试夹具：一个默认 UNIAPP 组，含一个需授权项与一个公共可见项 =====
function buildGroups(): GrantableMenuItem[] {
  return [
    {
      clientType: 'UNIAPP',
      // 雪花 Id 以字符串表达（超过 JS 数字精度）
      groupId: '7300000000000000001',
      groupName: '默认移动端',
      items: [
        {
          id: '7300000000000000101',
          parentId: null,
          title: '事件办理',
          icon: 'ri-mic-line',
          requireGrant: true, // 需授权
          module: 'Ginkgo.Module.SmartCommunity',
          order: 1
        },
        {
          id: '7300000000000000102',
          parentId: null,
          title: '智慧社区',
          icon: 'ri-community-line',
          requireGrant: false, // 公共可见、无需勾选
          module: 'Ginkgo.Module.SmartCommunity',
          order: 2
        }
      ]
    }
  ]
}

/**
 * FakeElTree：忠实模拟 Roles.vue 中 el-tree 的相关行为：
 *  - node-key = "id"
 *  - check-strictly（父子不联动）
 *  - props.disabled 控制节点是否可被用户勾选
 *  - setCheckedKeys：仅勾选树内存在的 key（不存在的 key 被忽略）
 *  - getCheckedKeys：返回当前已勾选 key
 *  - userToggle：模拟用户点击复选框；disabled 节点拒绝勾选
 */
class FakeElTree {
  private byId = new Map<string, GrantableItemNode>()
  private checked = new Set<string>()
  constructor(
    items: GrantableItemNode[],
    private disabledFn: (n: GrantableItemNode) => boolean
  ) {
    const walk = (nodes: GrantableItemNode[]) => {
      for (const n of nodes) {
        this.byId.set(n.id, n)
        if (n.children?.length) walk(n.children)
      }
    }
    walk(items)
  }
  setCheckedKeys(keys: string[]) {
    this.checked = new Set(keys.filter(k => this.byId.has(String(k))).map(String))
  }
  getCheckedKeys(_leafOnly = false): string[] {
    return [...this.checked]
  }
  /** 模拟用户点击勾选某节点；disabled（公共可见）节点拒绝勾选，返回是否成功 */
  userToggle(id: string, checked: boolean): boolean {
    const node = this.byId.get(id)
    if (!node) return false
    if (this.disabledFn(node)) return false // 公共可见项禁用，无法勾选
    if (checked) this.checked.add(id)
    else this.checked.delete(id)
    return true
  }
}

/**
 * PortalGrantSim：镜像 Roles.vue 业务入口授权分区的数据流方法
 * （loadGrantableItems / loadRoleItemPermissions / collectGrantedItemIds / save），
 * 复用真实的 rolePortalGrant.utils 纯逻辑与 mock API。
 */
class PortalGrantSim {
  grantableGroups: GrantableMenuItem[] = []
  grantTrees = new Map<string, FakeElTree>()
  currentRoleId = ''

  get requireGrantItemIds(): Set<string> {
    return collectRequireGrantItemIds(this.grantableGroups)
  }

  async loadGrantableItems() {
    this.grantableGroups = await api.getGrantableItems()
    this.grantTrees.clear()
    for (const g of this.grantableGroups) {
      this.grantTrees.set(g.groupId, new FakeElTree(g.items, isPortalGrantNodeDisabled))
    }
    if (this.currentRoleId) await this.loadRoleItemPermissions(this.currentRoleId)
  }

  async loadRoleItemPermissions(roleId: string) {
    let granted: string[] = []
    try {
      granted = await api.getRoleItemPermissions(roleId)
    } catch {
      granted = []
    }
    const set = new Set(granted)
    this.grantTrees.forEach(t => t.setCheckedKeys([...set]))
  }

  collectGrantedItemIds(): string[] {
    const groups: string[][] = []
    this.grantTrees.forEach(t => groups.push(t.getCheckedKeys(false)))
    return collectGrantedRequireGrantIds(groups, this.requireGrantItemIds)
  }

  async save(roleId: string) {
    await api.setRoleItemPermissions({ roleId, menuGroupItemIds: this.collectGrantedItemIds() })
  }
}

describe('角色编辑器业务入口授权 - 数据流', () => {
  beforeEach(() => {
    getGrantableItems.mockReset()
    getRoleItemPermissions.mockReset()
    setRoleItemPermissions.mockReset()
  })

  it('进入面板加载可授权入口：调用 getGrantableItems 并按默认组渲染入口树（需求 8.5）', async () => {
    getGrantableItems.mockResolvedValue(buildGroups())
    const sim = new PortalGrantSim()

    await sim.loadGrantableItems()

    // 调用了可授权入口接口
    expect(getGrantableItems).toHaveBeenCalledTimes(1)
    // 按默认组渲染：一个 UNIAPP 默认组，含两个入口项
    expect(sim.grantableGroups).toHaveLength(1)
    expect(sim.grantableGroups[0].clientType).toBe('UNIAPP')
    expect(sim.grantableGroups[0].items.map(i => i.title)).toEqual(['事件办理', '智慧社区'])
    // 每个默认组对应一棵入口树
    expect(sim.grantTrees.has('7300000000000000001')).toBe(true)
  })

  it('进入角色回填勾选：getRoleItemPermissions 返回的已授权项被回填为勾选（需求 8.3）', async () => {
    getGrantableItems.mockResolvedValue(buildGroups())
    // 该角色已授权「事件办理」（需授权项）
    getRoleItemPermissions.mockResolvedValue(['7300000000000000101'])

    const sim = new PortalGrantSim()
    sim.currentRoleId = '7300000000000000900'
    await sim.loadGrantableItems()

    expect(getRoleItemPermissions).toHaveBeenCalledWith('7300000000000000900')
    const tree = sim.grantTrees.get('7300000000000000001')!
    expect(tree.getCheckedKeys()).toContain('7300000000000000101')
    // 回填的 Id 仍为字符串
    expect(typeof tree.getCheckedKeys()[0]).toBe('string')
  })

  it('回填忽略不存在于树内的已授权 Id（避免脏数据）', async () => {
    getGrantableItems.mockResolvedValue(buildGroups())
    getRoleItemPermissions.mockResolvedValue(['7300000000000000101', '9999999999999999999'])

    const sim = new PortalGrantSim()
    sim.currentRoleId = '7300000000000000900'
    await sim.loadGrantableItems()

    const tree = sim.grantTrees.get('7300000000000000001')!
    expect(tree.getCheckedKeys()).toEqual(['7300000000000000101'])
  })

  it('RequireGrant=0 项禁用勾选：判定为禁用且用户无法勾选、不进入提交集合（需求 8.6）', async () => {
    getGrantableItems.mockResolvedValue(buildGroups())
    const sim = new PortalGrantSim()
    await sim.loadGrantableItems()

    const groups = sim.grantableGroups
    const requireGrantNode = groups[0].items[0] // 事件办理 requireGrant=true
    const publicNode = groups[0].items[1] // 智慧社区 requireGrant=false

    // 节点禁用判定：公共可见项禁用，需授权项不禁用
    expect(isPortalGrantNodeDisabled(publicNode)).toBe(true)
    expect(isPortalGrantNodeDisabled(requireGrantNode)).toBe(false)

    const tree = sim.grantTrees.get('7300000000000000001')!
    // 用户尝试勾选公共可见项 -> 被拒绝
    expect(tree.userToggle(publicNode.id, true)).toBe(false)
    expect(tree.getCheckedKeys()).not.toContain(publicNode.id)
    // 用户勾选需授权项 -> 允许
    expect(tree.userToggle(requireGrantNode.id, true)).toBe(true)

    // 即使公共可见项被误置为勾选，收集逻辑也只会保留需授权项
    expect(sim.collectGrantedItemIds()).toEqual(['7300000000000000101'])
  })

  it('保存全量覆盖：setRoleItemPermissions 收到当前勾选的需授权项 Id 全集（需求 8.3）', async () => {
    getGrantableItems.mockResolvedValue(buildGroups())
    getRoleItemPermissions.mockResolvedValue([]) // 初始无授权
    setRoleItemPermissions.mockResolvedValue(undefined)

    const sim = new PortalGrantSim()
    sim.currentRoleId = '7300000000000000900'
    await sim.loadGrantableItems()

    // 用户勾选需授权项「事件办理」
    const tree = sim.grantTrees.get('7300000000000000001')!
    tree.userToggle('7300000000000000101', true)

    await sim.save('7300000000000000900')

    expect(setRoleItemPermissions).toHaveBeenCalledTimes(1)
    expect(setRoleItemPermissions).toHaveBeenCalledWith({
      roleId: '7300000000000000900',
      menuGroupItemIds: ['7300000000000000101']
    })
  })

  it('保存全量覆盖：取消勾选后提交空集合（覆盖既有授权）', async () => {
    getGrantableItems.mockResolvedValue(buildGroups())
    getRoleItemPermissions.mockResolvedValue(['7300000000000000101']) // 初始已授权
    setRoleItemPermissions.mockResolvedValue(undefined)

    const sim = new PortalGrantSim()
    sim.currentRoleId = '7300000000000000900'
    await sim.loadGrantableItems()

    // 取消勾选「事件办理」
    const tree = sim.grantTrees.get('7300000000000000001')!
    tree.userToggle('7300000000000000101', false)

    await sim.save('7300000000000000900')

    expect(setRoleItemPermissions).toHaveBeenCalledWith({
      roleId: '7300000000000000900',
      menuGroupItemIds: []
    })
  })
})

describe('角色编辑器业务入口授权 - 纯逻辑工具', () => {
  it('collectRequireGrantItemIds 递归收集所有 requireGrant=true 项 Id', () => {
    const groups: GrantableMenuItem[] = [
      {
        clientType: 'UNIAPP',
        groupId: 'g1',
        groupName: 'G1',
        items: [
          { id: 'a', parentId: null, title: 'A', requireGrant: true, module: 'm', order: 1, children: [
            { id: 'a1', parentId: 'a', title: 'A1', requireGrant: false, module: 'm', order: 1 },
            { id: 'a2', parentId: 'a', title: 'A2', requireGrant: true, module: 'm', order: 2 }
          ] },
          { id: 'b', parentId: null, title: 'B', requireGrant: false, module: 'm', order: 2 }
        ]
      }
    ]
    const ids = collectRequireGrantItemIds(groups)
    expect([...ids].sort()).toEqual(['a', 'a2'])
  })

  it('collectGrantedRequireGrantIds 去重并仅保留需授权项', () => {
    const requireGrant = new Set(['a', 'a2'])
    const checkedGroups = [
      ['a', 'b', 'a'], // b 为公共可见，应被剔除；a 重复应去重
      ['a2', 'x'] // x 不在需授权集合，剔除
    ]
    expect(collectGrantedRequireGrantIds(checkedGroups, requireGrant).sort()).toEqual(['a', 'a2'])
  })

  it('clientTypeLabel 返回中文终端标签', () => {
    expect(clientTypeLabel('UNIAPP')).toBe('移动端')
    expect(clientTypeLabel('web_portal')).toBe('WEB前台')
    expect(clientTypeLabel('WPF')).toBe('桌面端')
    expect(clientTypeLabel('')).toBe('未知')
  })

  it('normalizeIcon 缺省给通用图标', () => {
    expect(normalizeIcon('ri-mic-line')).toBe('ri-mic-line')
    expect(normalizeIcon('')).toBe('bi bi-grid')
    expect(normalizeIcon(null)).toBe('bi bi-grid')
  })
})

describe('角色编辑器业务入口授权 - 浅色/深色主题适配（静态钩子断言）', () => {
  // 读取 Roles.vue 源码，断言业务入口授权分区具备深色主题样式钩子（admin-dark）。
  // 完整视觉主题验证为手动测试范畴，此处仅核验主题适配钩子存在。
  const rolesVuePath = resolve(dirname(fileURLToPath(import.meta.url)), '../Roles.vue')
  const source = readFileSync(rolesVuePath, 'utf-8')

  it('业务入口授权分区存在 admin-dark 深色主题样式钩子', () => {
    expect(source).toContain('.admin-dark .portal-grant-group')
    expect(source).toContain('.admin-dark .portal-grant-tree')
    expect(source).toContain('.admin-dark .portal-node-label')
  })

  it('深色与浅色样式钩子文案为简体中文且无乱码', () => {
    // 命中分区注释，确保 UTF-8 中文正常（出现乱码时此断言会失败）
    expect(source).toContain('业务入口授权')
    expect(source).toContain('公共可见、无需勾选')
  })
})
