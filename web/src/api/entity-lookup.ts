import http from '@/api/http'

/** 通用实体查询接口（用于 EntityPicker 组件的兜底数据源） */
export const entityLookup = (params: {
  table: string
  valueField?: string
  labelField?: string
  keyword?: string
  page?: number
  pageSize?: number
}) => http.get('/system/entity-lookup', { params })
