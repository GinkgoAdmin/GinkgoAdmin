export interface SearchableTreeNode {
  name?: string | null
  route?: string | null
  code?: string | null
  resource?: string | null
  children?: SearchableTreeNode[]
}

function normalizeKeyword(keyword?: string | null) {
  return String(keyword || '').trim().toLowerCase()
}

function matchesNodeFields(node: SearchableTreeNode, keyword: string) {
  return [node.name, node.route, node.code, node.resource]
    .some(value => String(value || '').toLowerCase().includes(keyword))
}

export function matchesSearchableTreeNodeDeep(node: SearchableTreeNode, keyword?: string | null): boolean {
  const normalized = normalizeKeyword(keyword)
  if (!normalized) return true

  if (matchesNodeFields(node, normalized)) return true
  return (node.children || []).some(child => matchesSearchableTreeNodeDeep(child, normalized))
}

export function filterSearchableTree<T extends SearchableTreeNode>(nodes: T[], keyword?: string | null): T[] {
  const normalized = normalizeKeyword(keyword)
  if (!normalized) return nodes

  return nodes
    .map(node => {
      const children = filterSearchableTree((node.children || []) as T[], normalized)
      if (matchesNodeFields(node, normalized) || children.length > 0) {
        return { ...node, children } as T
      }
      return null
    })
    .filter((node): node is T => node !== null)
}
