/**
 * YzhForm 统一导出
 */
export { default as YzhForm } from './YzhForm.vue'
export type { YzhFieldType, YzhFormField } from './YzhForm.vue'
// 兼容命名空间别名
export type YzhFormFieldV4 = import('./YzhForm.vue').YzhFormField
