import type { SearchOption } from '../../../components/DataTable/types'

export type LogResultKind = 'normal' | 'error' | 'unknown'

export interface SystemLogRow {
  id: string
  action: string
  resource?: string
  moduleCN?: string
  featureCN?: string
  result?: string
  reviewCN?: string
  dataJson?: string
  createdAt: string
  userName?: string | null
  displayName?: string | null
  email?: string | null
  phone?: string | null
  ip?: string
  userAgent?: string
  elapsedMs?: number | null
}

export interface LogResultMeta {
  kind: LogResultKind
  label: string
  tagType: '' | 'success' | 'warning' | 'danger' | 'info'
  dotClass: string
}

export interface LogFilterOptions {
  features: SearchOption[]
  types: SearchOption[]
}

const normalKeywords = ['ok', 'success', 'successful', '正常', '成功']
const errorKeywords = ['error', 'fail', 'failed', 'exception', '异常', '失败']

function normalizeText(value?: string | null) {
  return String(value || '').trim().toLowerCase()
}

export function getLogResultMeta(result?: string | null): LogResultMeta {
  const normalized = normalizeText(result)

  if (!normalized) {
    return { kind: 'unknown', label: '未知', tagType: 'info', dotClass: 'is-info' }
  }

  if (normalKeywords.some(keyword => normalized.includes(keyword))) {
    return { kind: 'normal', label: '正常', tagType: 'success', dotClass: 'is-success' }
  }

  if (errorKeywords.some(keyword => normalized.includes(keyword))) {
    return { kind: 'error', label: '错误', tagType: 'danger', dotClass: 'is-danger' }
  }

  return { kind: 'unknown', label: result || '未知', tagType: 'warning', dotClass: 'is-warning' }
}

export function buildLogPreviewText(row: SystemLogRow) {
  const parsedDataMessage = (() => {
    if (!row.dataJson?.trim()) return ''
    try {
      const parsed = JSON.parse(row.dataJson)
      return String(parsed?.message || parsed?.error || parsed?.detail || parsed?.msg || '').trim()
    } catch {
      return row.dataJson.trim()
    }
  })()

  if (getLogResultMeta(row.result).kind === 'error' && parsedDataMessage) {
    return parsedDataMessage
  }

  if (row.reviewCN?.trim()) {
    return row.reviewCN.trim()
  }

  if (parsedDataMessage) {
    return parsedDataMessage
  }

  const pieces = [row.featureCN, row.resource].filter(Boolean)
  if (pieces.length > 0) {
    return pieces.join(' / ')
  }

  return row.action || '暂无内容'
}

export function buildLogFilterOptions(rows: SystemLogRow[]): LogFilterOptions {
  const featureValues = Array.from(
    new Set(rows.map(item => String(item.featureCN || '').trim()).filter(Boolean))
  ).sort((left, right) => left.localeCompare(right, 'zh-CN'))

  const typeValues = Array.from(
    new Set(
      rows
        .map(item => getLogResultMeta(item.result).kind)
        .filter((item): item is Exclude<LogResultKind, 'unknown'> => item !== 'unknown')
    )
  )

  return {
    features: featureValues.map(item => ({ label: item, value: item })),
    types: typeValues.map(item => ({
      label: item === 'normal' ? '正常' : '错误',
      value: item
    }))
  }
}

export function formatLogTime(value?: string) {
  if (!value) return ''
  try {
    return new Date(value).toLocaleString('zh-CN')
  } catch {
    return value
  }
}

export function safeFormatJson(value?: string) {
  if (!value?.trim()) return ''
  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return value
  }
}
