import { describe, expect, it } from 'vitest'
import {
  filterSearchableTree,
  matchesSearchableTreeNodeDeep
} from '../treeSearch.utils'

const tree = [
  {
    name: 'Tenant',
    route: 'tenant',
    code: 'tenant',
    children: [
      {
        name: 'Tenant List',
        route: '/tenant/list',
        code: 'tenant:list',
        children: [
          {
            name: 'View',
            route: 'tenant/list/button/view',
            code: 'tenant:list:view',
            children: [
              {
                name: 'My Tenant Api',
                route: 'tenant/list/api/my',
                code: 'tenant:list:view:my:api',
                resource: '/api/v1/tenants/my'
              }
            ]
          }
        ]
      }
    ]
  }
]

describe('tree search utils', () => {
  it('filters tree by route and keeps ancestors', () => {
    const result = filterSearchableTree(tree, 'tenant/list/api/my')

    expect(result).toHaveLength(1)
    expect(result[0].children?.[0].children?.[0].children?.[0].name).toBe('My Tenant Api')
  })

  it('matches descendants by code or resource', () => {
    expect(matchesSearchableTreeNodeDeep(tree[0], 'tenant:list:view:my:api')).toBe(true)
    expect(matchesSearchableTreeNodeDeep(tree[0], '/api/v1/tenants/my')).toBe(true)
    expect(matchesSearchableTreeNodeDeep(tree[0], 'missing')).toBe(false)
  })
})
