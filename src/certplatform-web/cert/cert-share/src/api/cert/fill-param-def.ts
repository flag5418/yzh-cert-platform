/**
 * 企业资料参数 API（后台管理）
 *
 * ★ 数据模型（2026-10-09 更新）：
 *   `cert_fill_param_def` 是一个**按标准管理的简单字典** ——
 *   左树 = [通用] + 体系(系统) → 族 → 标准 三层，
 *   每条参数唯一归属一真实节点（`StandardCode=''</>=ISOStandard.Code`(GUID)）；
 *   `OrgCode` / `StageCode` 服务端强制恒空串（不分机构、不分阶段）。
 *
 * ★ CRUD 全走内核约定端点（`controllerName = 'Admin/Cert/FillParamDef'`）：
 *   `/treepconfig` 以外的配置走 `/config`（本页 logic 覆写 loadConfig，见下）、
 *   `/filter` `/add` `/update` `/delete` `/toggle-valid` —— 前端**零手写 CRUD**。
 *
 * ★ 左树三层数据源（跨控制器拼接，由本页 logic 的 `loadTreeRoot` 并行拉取）：
 *   1. 体系(系统) = `iso_category` 字典 → `GET /api/System/Dictionary/items/by-no/iso_category`
 *   2. 族       = `cert_standard_family` → `POST /api/Admin/Foundation/CertStandardFamily/filter`
 *   3. 标准     = `ISOStandardTreeTable/tree/root` → `POST .../tree/root`
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
 * 左上树数据源 #3：ISO 标准扁平列表（ISOStandardTreeTable 的 root 端点）。
 *
 * <p>返回的每个 TreeItemDto 已通过后端 `MapToTreeItem` 注入 Extra：
 * `StandardCode` / `VersionYear` / `Category` / `FamilyCode` / `Description`。</p>
 * <p>⚠️ 返回的列表不含「通用参数」根，也不含体系/族层级 —— 由本页
 * `loadTreeRoot` + `system-family-tree.ts` 组装成三层。</p>
 */
export async function getIsoStandardTree(): Promise<TreeItemDto[]> {
  const res = await yzhApi.post<ApiResponse<TreeItemDto[]>>(
    '/api/Admin/Foundation/ISOStandardTreeTable/tree/root',
    {},
  )
  return unwrap(res, [])
}
