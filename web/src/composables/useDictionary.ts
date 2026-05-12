import { computed, onMounted } from 'vue'
import { useDictionaryStore } from '../stores/dictionary'
import { DictItemListItem } from '../api/dictionary'

/**
 * 快速获取全局缓存字典数据的 Vue 组合式函数
 * @param codes 用逗号分隔的单个字符串，或字典的 Code 数组。例如: useDictionary('gender', 'status')
 * @returns 包含各字典响应式引用（Computed）的对象，同时附带 store 的辅助方法。
 * 
 * 使用方式：
 * const { dicts } = useDictionary('SYS_LANGUAGES', 'city')
 * console.log(dicts.value.SYS_LANGUAGES) // 字典项数组
 * // 或者解构单独获取：
 * const { SYS_LANGUAGES, city } = useDictionary('SYS_LANGUAGES', 'city')
 * // SYS_LANGUAGES.value 是一个数组：[{ itemKey: 'zh-CN', itemValue: '简体中文' }]
 */
export function useDictionary(...codes: string[]) {
  const dictStore = useDictionaryStore()

  // 发起获取网络请求（带缓存控制）
  onMounted(() => {
    if (codes && codes.length > 0) {
      dictStore.fetchDictionaries(codes)
    }
  })

  // 以方便解构的方式返回 computed
  const result: Record<string, import('vue').ComputedRef<DictItemListItem[]>> = {}
  
  codes.forEach(code => {
    // 允许通过解构出与字典码同名的变量获取
    result[code] = computed(() => dictStore.getDict(code))
  })

  // 同时也暴露出统一的 dicts 字典树集合，方便某些场景遍历
  const dicts = computed(() => {
    const combined: Record<string, DictItemListItem[]> = {}
    codes.forEach(code => {
      combined[code] = dictStore.getDict(code)
    })
    return combined
  })

  return {
    ...result,
    dicts,
    fetchDictionaries: dictStore.fetchDictionaries
  }
}
