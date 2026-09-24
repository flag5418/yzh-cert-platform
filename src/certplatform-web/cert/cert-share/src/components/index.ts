/**
 * Share 业务组件出口
 *
 * 注：三个纯展示组件（CertConvertBadge/CertPageHeader/CertStatusBar）已加 lang="ts"，
 * vue-tsc 可正常推导类型；若新增零逻辑 SFC 请保持 <script setup lang="ts">。
 */
export { default as CertBizTree } from './CertBizTree.vue'
export { default as CertDirectoryTree } from './CertDirectoryTree.vue'
export { default as CertConvertBadge } from './CertConvertBadge.vue'
export { default as CertPageHeader } from './CertPageHeader.vue'
export { default as CertPagePlaceholder } from './CertPagePlaceholder.vue'
export { default as CertStatusBar } from './CertStatusBar.vue'
export { default as YzhFolderUpload } from './YzhFolderUpload.vue'

// ⛔ 重型组件**不要**放进本 barrel（勿加回来）
//    `WorkflowDesigner` / `ExecutionResultPanel` 依赖 logicflow，体积约 366 KB JS + 25 KB CSS。
//    一旦挂在本 barrel 上，**任何** App 只要 `import { 某个小组件 } from '@share/components'`
//    就会把设计器整包拖进自己的产物 —— 专家端因此凭空多了 390 KB。
//    它们的正确用法是**直接路径导入**（管理端即如此）：
//      import WorkflowDesigner from '@share/components/workflow/WorkflowDesigner.vue'
//    判定标准：组件若依赖第三方重型库（logicflow / echarts / monaco 等），一律走直接路径。
