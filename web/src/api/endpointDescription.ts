import http from './http'

/**
 * 接口注释（业务说明）数据。
 * 由后端 [EndpointDescription] 标注产出，专供运维/监控/审计等横切页面反查。
 */
export interface EndpointDescriptionItem {
  method?: string | null
  path?: string | null
  description?: string | null
  category?: string | null
  template?: string | null
  fromController?: boolean
}

export interface EndpointDescriptionQuery {
  method?: string | null
  path: string
}

/**
 * 单条查询接口注释。后端找不到时返回 null。
 */
export const resolveEndpointDescription = (query: EndpointDescriptionQuery) =>
  http.get<any, EndpointDescriptionItem | null>('/endpoint-descriptions/resolve', {
    params: { method: query.method ?? '', path: query.path }
  })

/**
 * 批量查询接口注释。返回数组顺序与入参一致；某条无注释时该条目的 description 为空。
 */
export const resolveEndpointDescriptions = (items: EndpointDescriptionQuery[]) =>
  http.post<any, EndpointDescriptionItem[]>('/endpoint-descriptions/batch', { items })

/**
 * 进程内缓存：在一次会话中尽量只为同一个 path 请求一次。
 * key = `${method}|${path}` （method 为空使用 *）。
 */
const cache = new Map<string, EndpointDescriptionItem | null>()
const inflight = new Map<string, Promise<EndpointDescriptionItem | null>>()

function makeKey(method: string | null | undefined, path: string): string {
  return `${(method || '*').toUpperCase()}|${path}`
}

/**
 * 带缓存的批量解析。对未缓存的条目去后端一次性查询，返回与入参等长的结果。
 *
 * 调用方拿到结果后只需检查每项的 description：有则显示，无则不显示。
 */
export async function resolveEndpointDescriptionsCached(
  items: EndpointDescriptionQuery[]
): Promise<(EndpointDescriptionItem | null)[]> {
  if (!items || items.length === 0) return []

  // 第一遍：命中缓存的直接占位，未命中的收集起来批量查
  const result: (EndpointDescriptionItem | null)[] = new Array(items.length).fill(null)
  const pending: { idx: number; key: string; q: EndpointDescriptionQuery }[] = []

  for (let i = 0; i < items.length; i++) {
    const q = items[i]
    if (!q || !q.path) continue
    const key = makeKey(q.method, q.path)
    if (cache.has(key)) {
      result[i] = cache.get(key) ?? null
    } else {
      pending.push({ idx: i, key, q })
    }
  }

  if (pending.length === 0) return result

  // 合并 inflight：相同 key 的同时进行的请求不再重复
  const toFetch: EndpointDescriptionQuery[] = []
  const toFetchKeys: string[] = []
  for (const p of pending) {
    if (!inflight.has(p.key)) {
      toFetch.push(p.q)
      toFetchKeys.push(p.key)
    }
  }

  let batchPromise: Promise<EndpointDescriptionItem[]> | null = null
  if (toFetch.length > 0) {
    batchPromise = resolveEndpointDescriptions(toFetch)
    // 把同一批内每个 key 都映射到这个 promise
    toFetchKeys.forEach((k, idx) => {
      const single = batchPromise!
        .then(arr => {
          const item = arr?.[idx] ?? null
          const stored: EndpointDescriptionItem | null = item && item.description ? item : null
          cache.set(k, stored)
          return stored
        })
        .catch(() => {
          cache.set(k, null)
          return null
        })
        .finally(() => {
          inflight.delete(k)
        })
      inflight.set(k, single)
    })
  }

  // 等待所有 pending 项的 inflight 完成
  await Promise.all(pending.map(p => inflight.get(p.key) ?? Promise.resolve(null)))
  for (const p of pending) {
    result[p.idx] = cache.get(p.key) ?? null
  }
  return result
}

/**
 * 仅供测试或排错使用：清空进程内缓存。
 */
export function clearEndpointDescriptionCache(): void {
  cache.clear()
  inflight.clear()
}
