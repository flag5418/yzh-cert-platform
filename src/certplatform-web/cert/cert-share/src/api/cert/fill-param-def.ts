/**
 * 企业资料参数 API（后台管理）
 *
 * ★ 数据模型（2026-10-06 重定义，见 26-核心菜单功能设计 §3.1 裁决）：
 *   `cert_fill_param_def` 是一个**按标准管理的简单字典** —— 左树 = [通用] + 各ISO标准，
 *   每条参数唯一归属一节点（`StandardCode=''</>=ISOStandard.Code`）；
 *   `OrgCode` / `StageCode` 服务端强制恒空串（不分机构、不分阶段）。
 *
 * ★ CRUD 全走内核约定端点（`controllerName = 'Admin/Cert/FillParamDef'`）：
 *   `/treepconfig` 以外的配置走 `/config`（本页 logic 覆写 loadConfig，见下）、
 *   `/filter` `/add` `/update` `/delete` `/toggle-valid` —— 前端**零手写 CRUD**。
 *
 * ★ 唯一自定义取数：左树数据源是 ISO 标准树（另一实体）——
 *   `getIsoStandardTree()` → `POST /api/Admin/Foundation/ISOStandardTreeTable/tree/root`。
 *
 * ⛔ 原自定义端点 `scopes` / `enterprise-attrs` / `effective` 已随作用域模型删除
 *   （后端同批移除；ApiCode 集合变化，部署后须重跑 ApiSync 并重关联角色-接口）。
 */
import { yzhApi } from '@yzh-core'
import type { ApiResponse, TreeItemDto } from '@yzh-core'
import { unwrap } from '@yzh-core'

/** 本实体控制器路由（内核 `controllerName` = 相对段，此常量供直连端点用） */
export const FILL_PARAM_DEF_BASE = '/api/Admin/Cert/FillParamDef'

/**
 * ★ 左树数据源：ISO 标准树（单层，ISOStandardTreeTable 的 root 端点）。
 *
 * <p>⚠️ 返回的 `tree/root` 不含「通用参数」根 —— 由本页 logic 在 `afterTreeLoaded`
 * 里前置插入 `Code=''` 的真实节点（⛔ 不能标 `NodeType:'virtual'`，否则内核
 * `isVirtualNode` 判定后不注入树过滤，右表会变成全量）。</p>
 */
export async function getIsoStandardTree(): Promise<TreeItemDto[]> {
  const res = await yzhApi.post<ApiResponse<TreeItemDto[]>>(
    '/api/Admin/Foundation/ISOStandardTreeTable/tree/root',
    {},
  )
  return unwrap(res, [])
}
