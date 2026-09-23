/**
 * 菜单 API —— 已迁移至共享层 `@share/api/system/menu`
 *
 * 迁移原因：`cert-auditor`（专家端）也需要同一套菜单接口与 `buildTree`，
 * 原先实现在 `cert-admin` 本地，导致「同一后端契约两份前端实现」。
 *
 * 本文件仅作**转出口**，保持既有 `@/api/system/menu` 导入路径不变。
 * 新增代码请直接 `from '@share/api/system/menu'`。
 */
export * from '@share/api/system/menu'
