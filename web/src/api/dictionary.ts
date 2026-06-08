import http from './http'

export interface DictionaryCategoryListItem {
  id: string
  code: string
  name: string
  nameI18n?: string | null
  category: string
  sourceType?: string
  enabled: boolean
  description?: string | null
  descriptionI18n?: string | null
  extraJson?: string | null
}

export interface DictionaryCategoryDetail extends DictionaryCategoryListItem {}

export interface CreateDictionaryCategoryInput {
  code: string
  name: string
  nameI18n?: string | null
  category: string
  sourceType?: string
  enabled: boolean
  description?: string | null
  descriptionI18n?: string | null
  extraJson?: string | null
}

export type UpdateDictionaryCategoryInput = Partial<CreateDictionaryCategoryInput>

export interface DictItemListItem {
  id: string
  itemKey: string
  itemValue: string
  valueI18n?: string | null
  order?: number
  enabled: boolean
  parentId?: string | null
}

export interface DictionaryItemDetail extends DictItemListItem {
  categoryId: string
}

export interface CreateDictItemInput {
  categoryId: string
  itemKey: string
  itemValue: string
  valueI18n?: string | null
  order?: number
  enabled: boolean
  parentId?: string | null
}

export type UpdateDictItemInput = Partial<CreateDictItemInput>

// Categories
export async function getDictionaryCategories(keyword = ''): Promise<DictionaryCategoryListItem[]> {
  // Try paged endpoint with large page size to get all
  const url = keyword
    ? `/v1/dictionaries/categories?page=1&pageSize=1000&keyword=${encodeURIComponent(keyword)}`
    : `/v1/dictionaries/categories?page=1&pageSize=1000`
  const res = await http.get<any>(url)
  const data = (res && res.data) ?? res
  if (Array.isArray(data)) return data as DictionaryCategoryListItem[]
  if (data && Array.isArray(data.items)) return data.items as DictionaryCategoryListItem[]
  if (res && Array.isArray((res as any).items)) return (res as any).items as DictionaryCategoryListItem[]
  return []
}

export async function getDictionaryCategoryDetail(id: string): Promise<DictionaryCategoryDetail> {
  const res = await http.get<any, DictionaryCategoryDetail | { data?: DictionaryCategoryDetail }>(`/v1/dictionaries/categories/${id}`)
  return (res as any)?.data ?? (res as DictionaryCategoryDetail)
}

export async function createDictionaryCategory(input: CreateDictionaryCategoryInput): Promise<string> {
  const res = await http.post<any, string | { data?: string }>(`/v1/dictionaries/categories`, input)
  return typeof res === 'string' ? res : ((res as any)?.data as string)
}

export async function updateDictionaryCategory(id: string, input: UpdateDictionaryCategoryInput): Promise<void> {
  await http.put(`/v1/dictionaries/categories/${id}`, input)
}

export async function deleteDictionaryCategory(id: string): Promise<void> {
  await http.delete(`/v1/dictionaries/categories/${id}`)
}

// Items
export async function getDictionaryItems(categoryId: string): Promise<DictItemListItem[]> {
  const url = `/v1/dictionaries/items?page=1&pageSize=2000&categoryId=${encodeURIComponent(categoryId)}`
  const res = await http.get<any>(url)
  const data = (res && res.data) ?? res
  if (Array.isArray(data)) return data as DictItemListItem[]
  if (data && Array.isArray(data.items)) return data.items as DictItemListItem[]
  if (res && Array.isArray((res as any).items)) return (res as any).items as DictItemListItem[]
  return []
}

export async function getDictionaryItemDetail(id: string): Promise<DictionaryItemDetail> {
  const res = await http.get<any, DictionaryItemDetail | { data?: DictionaryItemDetail }>(`/v1/dictionaries/items/${id}`)
  return (res as any)?.data ?? (res as DictionaryItemDetail)
}

export async function createDictionaryItem(input: CreateDictItemInput): Promise<string> {
  const res = await http.post<any, string | { data?: string }>(`/v1/dictionaries/items`, input)
  return typeof res === 'string' ? res : ((res as any)?.data as string)
}

export async function updateDictionaryItem(id: string, input: UpdateDictItemInput): Promise<void> {
  await http.put(`/v1/dictionaries/items/${id}`, input)
}

export async function deleteDictionaryItem(id: string): Promise<void> {
  await http.delete(`/v1/dictionaries/items/${id}`)
}

export async function getDictionariesByCodes(codes: string[]): Promise<Record<string, DictItemListItem[]>> {
  if (!codes || codes.length === 0) return {}
  const url = `/v1/dictionaries/by-codes?codes=${encodeURIComponent(codes.join(','))}`
  const res = await http.get<any, Record<string, DictItemListItem[]> | { data?: Record<string, DictItemListItem[]> }>(url)
  return (res as any)?.data ?? res
}

/** 导出包：分类 + 全部条目 */
export interface DictionaryExportCategory {
  code: string
  name: string
  nameI18n?: string | null
  category?: string | null
  sourceType?: string | null
  enabled: boolean
  description?: string | null
  descriptionI18n?: string | null
  extraJson?: string | null
  module?: string
}

export interface DictionaryExportItem {
  itemKey: string
  itemValue: string
  valueI18n?: string | null
  order?: number
  enabled?: boolean
  parentItemKey?: string | null
  extraJson?: string | null
}

export interface DictionaryExportPackage {
  formatVersion: number
  exportedAt: string
  category: DictionaryExportCategory
  items: DictionaryExportItem[]
}

export interface DictionaryImportResult {
  categoryId: string
  categoryCode: string
  createdCategory: boolean
  itemsCreated: number
  itemsUpdated: number
  itemsDeleted: number
}

export async function exportDictionaryCategory(categoryId: string): Promise<DictionaryExportPackage> {
  const res = await http.get<any, DictionaryExportPackage | { data?: DictionaryExportPackage }>(
    `/v1/dictionaries/categories/${encodeURIComponent(categoryId)}/export`,
  )
  return (res as any)?.data ?? (res as DictionaryExportPackage)
}

export async function importDictionaryCategory(
  pkg: DictionaryExportPackage,
  overwriteIfExists = true,
): Promise<DictionaryImportResult> {
  const res = await http.post<any, DictionaryImportResult | { data?: DictionaryImportResult }>(
    `/v1/dictionaries/import?overwriteIfExists=${overwriteIfExists ? 'true' : 'false'}`,
    pkg,
  )
  return (res as any)?.data ?? (res as DictionaryImportResult)
}
