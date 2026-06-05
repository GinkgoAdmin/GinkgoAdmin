/**
 * 主框架通用组件导出
 * 这些组件可供所有插件和页面使用
 */

// 地址选择组件 - 中国省市区三级联动
export { default as AddressPicker } from './AddressPicker.vue'
export type { AddressValue } from './AddressPicker.vue'

// 经纬度选择组件 - 基于 Leaflet + OpenStreetMap
export { default as LocationPicker } from './LocationPicker.vue'
export type { LocationValue, LocationPickerTileProvider } from './LocationPicker.vue'

// 多图片上传组件（支持从附件库选择）
export { default as ImageUploader } from './ImageUploader.vue'

// 文件选择器组件
export { default as FileSelector } from './FileSelector.vue'

// 图标相关组件
export { default as RemixIcon } from './RemixIcon.vue'
export { default as IconPicker } from './IconPicker.vue'

// 数据表格组件
export { default as DataTable } from './DataTable/index.vue'

// wangEditor 富文本编辑器组件（内置默认编辑器）
export { default as WangEditor } from './WangEditor.vue'

// 编辑器适配器类型与工具函数
export type { EditorAdapterProps, EditorAdapterEmits, EditorAdapterExposed, EditorConfig } from './editor-adapter'
export { validateEditorConfig } from './editor-adapter'

