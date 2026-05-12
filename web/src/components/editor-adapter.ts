/** 编辑器适配器 Props 接口 */
export interface EditorAdapterProps {
  modelValue: string
  placeholder?: string
  disabled?: boolean
  readonly?: boolean
  height?: number | string
  toolbar?: 'minimal' | 'basic' | 'full'
  config?: Record<string, any>
  editorId?: string
}

/** 编辑器适配器 Emits 接口 */
export interface EditorAdapterEmits {
  (e: 'update:modelValue', value: string): void
  (e: 'editor-ready', editor: any): void
  (e: 'editor-change', value: string, editor: any): void
}

/** 编辑器适配器暴露方法接口 */
export interface EditorAdapterExposed {
  getHTML: () => string
  getText: () => string
  isEmpty: () => boolean
  reset: () => void
  focus: () => void
  blur: () => void
}

/** 编辑器配置对象 */
export interface EditorConfig {
  toolbar?: 'minimal' | 'basic' | 'full'
  height?: number | string
  placeholder?: string
  readOnly?: boolean
  customConfig?: Record<string, any>
}

/** 有效的工具栏键名 */
const VALID_TOOLBAR_KEYS: readonly string[] = ['minimal', 'basic', 'full'] as const

/** 验证 EditorConfig，返回错误信息数组，空数组表示验证通过 */
export function validateEditorConfig(config: unknown): string[] {
  const errors: string[] = []
  if (typeof config !== 'object' || config === null) {
    errors.push('配置必须是一个非空对象')
    return errors
  }
  const c = config as Record<string, unknown>
  if (c.toolbar !== undefined && !VALID_TOOLBAR_KEYS.includes(c.toolbar as string)) {
    errors.push(`无效的工具栏键名 "${c.toolbar}"，有效值为: ${VALID_TOOLBAR_KEYS.join(', ')}`)
  }
  return errors
}
