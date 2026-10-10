/**
 * Share 业务组件出口
 *
 * 注：三个纯展示组件（CertConvertBadge/CertPageHeader/CertStatusBar）已加 lang="ts"，
 * vue-tsc 可正常推导类型；若新增零逻辑 SFC 请保持 <script setup lang="ts">。
 */
export { default as CertBizTree } from './CertBizTree.vue'
export { default as CertDirectoryTree } from './CertDirectoryTree.vue'
// 通用机构+标准+阶段树（数据驱动，maxLevel / leafTypes 控制渲染深度与选中行为）
export { default as OrgStandardStageTree } from './OrgStandardStageTree.vue'
export type { StdStageTreeNode } from './OrgStandardStageTree.vue'
// 左树右表布局壳：OrgStandardStageTree + 表格插槽（替代 YzhTableTableLayout 的轻量方案）
export { default as OrgStageTableLayout } from './OrgStageTableLayout.vue'
export { default as CertConvertBadge } from './CertConvertBadge.vue'
export { default as CertPageHeader } from './CertPageHeader.vue'
export { default as CertPagePlaceholder } from './CertPagePlaceholder.vue'
export { default as CertStatusBar } from './CertStatusBar.vue'
// 文档在线预览（docx/xlsx/pdf/图片/文本）—— 2026-10-04 由
// cert-admin/pages/workflow/doc-extraction-rule/components 上移到 share，
// 供「文档填写规则」页（中栏预览空白模板 / 标准原始文档）复用。
// ⚠️ 依赖 `@vue-office/pdf`（中等体积），但已被两个管理端页面同时需要，
//    放进 barrel 不引入额外包（若将来只有一处用，请改回直接路径导入）。
export { default as DocPreview } from './DocPreview.vue'
// 33 号语义结果渲染器 —— 2026-10-03 由 cert-admin/pages/workflow/prompt-template/components
// 上移到 share，供 36 号「企业原始资料管理」分析结果抽屉复用（只读渲染层，⛔ 无 emit）。
export { default as SemanticResult } from './SemanticResult.vue'
export { default as YzhFolderUpload } from './YzhFolderUpload.vue'
// 2026-10-10 P2b 消息铃铛：顶栏未读消息入口，admin/auditor 共用
export { default as MessageBell } from './MessageBell/index'

// ⛔ 重型组件**不要**放进本 barrel（勿加回来）
//    `WorkflowDesigner` / `ExecutionResultPanel` 依赖 logicflow，体积约 366 KB JS + 25 KB CSS。
//    一旦挂在本 barrel 上，**任何** App 只要 `import { 某个小组件 } from '@share/components'`
//    就会把设计器整包拖进自己的产物 —— 专家端因此凭空多了 390 KB。
//    它们的正确用法是**直接路径导入**（管理端即如此）：
//      import WorkflowDesigner from '@share/components/workflow/WorkflowDesigner.vue'
//    判定标准：组件若依赖第三方重型库（logicflow / echarts / monaco 等），一律走直接路径。
