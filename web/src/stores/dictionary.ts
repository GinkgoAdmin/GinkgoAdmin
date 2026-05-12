import { defineStore } from 'pinia'
import { ref } from 'vue'
import { getDictionariesByCodes, DictItemListItem } from '../api/dictionary'

export const useDictionaryStore = defineStore('dictionary', () => {
  const dictData = ref<Record<string, DictItemListItem[]>>({})
  const loadingCodes = ref<Set<string>>(new Set())

  const fetchDictionaries = async (codes: string[]) => {
    // 过滤掉已经加载过，正在加载中，或空值的 codes
    const missingCodes = codes.filter(
      code => code && !dictData.value[code] && !loadingCodes.value.has(code)
    )

    if (missingCodes.length === 0) return

    // 标记为正在加载
    missingCodes.forEach(code => loadingCodes.value.add(code))

    try {
      const result = await getDictionariesByCodes(missingCodes)
      
      // 合并数据
      for (const [code, items] of Object.entries(result)) {
        dictData.value[code] = items || []
      }
      
      // 对于请求了但后端没返回的 code，也要给个空数组，避免重复请求
      missingCodes.forEach(code => {
        if (!dictData.value[code]) {
          dictData.value[code] = []
        }
      })
    } catch (error) {
      console.error('获取字典数据失败:', error)
      // 如果失败，取消 loading 标记以便重试
      missingCodes.forEach(code => loadingCodes.value.delete(code))
    }
  }

  // 获取特定字典（支持响应式使用）
  const getDict = (code: string): DictItemListItem[] => {
    return dictData.value[code] || []
  }

  return {
    dictData,
    loadingCodes,
    fetchDictionaries,
    getDict
  }
})
