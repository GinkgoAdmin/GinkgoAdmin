import { ref } from 'vue'

export function useSearch<T extends Record<string, any> = Record<string, any>>(initial?: T) {
  const model = ref<T>({ ...(initial as any) })
  function reset() {
    Object.keys(model.value || {}).forEach(k => (model.value as any)[k] = undefined)
  }
  return { model, reset }
}


