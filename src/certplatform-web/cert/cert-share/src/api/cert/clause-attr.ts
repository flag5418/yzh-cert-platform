/**
 * 条款属性（属性池 + 条款配置）API
 *
 * ★ 后端：
 *   - 属性池 L0：`/api/Admin/Cert/ClauseAttrDef`（`YzhControllerBase<ClauseAttrDef>`）
 *   - 条款配置 L1：`/api/Admin/Cert/ClauseAttr`（`YzhControllerBase<ClauseAttr>`）
 *
 * ★ CRUD 全走内核约定端点（`/config` `/filter` `/add` `/update` `/delete` `/toggle-valid`），
 *   本文件只放**两个非内核端点**：
 *   1. 左树「标准 → 叶子条款」—— 树数据源是 `cert_iso_clause`，不是本实体
 *   2. 属性下拉选项 —— 给配置页的「属性」选择器
 *
 * ⛔ 设计约束（`docs/70-V2设计/24-条款标准化-属性池与条款配置-V2.md`）：
 *   - 属性池 Code 是人可读驼峰（`Artifacts`），不是 GUID
 *   - `ValueSource` 只在条款配置上有，只 `fixed` / `reference` 两档
 */
import { yzhApi } from '@yzh-core'
import type { ApiResponse, TreeItemDto } from '@yzh-core'
import { unwrap } from '@yzh-core'

/** 属性池控制器路由 */
export const CLAUSE_ATTR_DEF_BASE = '/api/Admin/Cert/ClauseAttrDef'

/** 条款属性配置控制器路由 */
export const CLAUSE_ATTR_BASE = '/api/Admin/Cert/ClauseAttr'

/** 属性下拉选项（只含启用的属性） */
export interface ClauseAttrOption {
  Code: string
  AttrName: string
  ValueType: string
  DictCode?: string | null
  Remark?: string | null
}

/**
 * 左树：标准 → 叶子条款（只列叶子 —— 配置粒度默认叶子，合并是例外）。
 *
 * <p>为什么只列叶子：非叶子条款本身不产生检查要求，给它配属性配了也不会被消费。
 * 208 条叶子平铺在标准下，按 ClauseNumber 排序天然按章节聚集。</p>
 */
export async function getClauseAttrTree(): Promise<TreeItemDto[]> {
  const res = await yzhApi.post<ApiResponse<TreeItemDto[]>>(
    `${CLAUSE_ATTR_BASE}/tree/root`,
    {},
  )
  return unwrap(res, [])
}

/** 属性池下拉选项 */
export async function getClauseAttrOptions(): Promise<ClauseAttrOption[]> {
  const res = await yzhApi.get<ApiResponse<ClauseAttrOption[]>>(
    `${CLAUSE_ATTR_DEF_BASE}/options`,
  )
  return unwrap(res, [])
}

/** 每个条款已配属性数（给树节点打角标，可选） */
export async function getClauseAttrCounts(): Promise<Record<string, number>> {
  const res = await yzhApi.post<ApiResponse<Record<string, number>>>(
    `${CLAUSE_ATTR_BASE}/tree/counts`,
    {},
  )
  return unwrap(res, {})
}
