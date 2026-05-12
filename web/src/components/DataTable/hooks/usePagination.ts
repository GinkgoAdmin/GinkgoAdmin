import { computed } from 'vue'

export function usePagination(pagination?: { total: number; page: number; pageSize: number; pageSizes?: number[] }, emit?: any) {
  const page = computed({ get:()=> pagination?.page || 1, set:(v:number)=> emit?.('page-change', v) })
  const pageSize = computed({ get:()=> pagination?.pageSize || 20, set:(v:number)=> emit?.('size-change', v) })
  const pageSizes = computed(()=> pagination?.pageSizes || [10,20,50,100])
  return { page, pageSize, pageSizes }
}


