/**
 * SystemDictionary API - 数据字典管理（左树右表）
 *
 * 后端：YZH.Core.Web/Controllers/System/DictionaryController.cs → /api/System/Dictionary
 *
 * 设计原则（与 organization.ts 一致）：
 * - 公共契约类型从 @yzh-core 导入
 * - 不维护业务 TS 实体，数据传输使用 Record<string, any>
 * - UI 由后端 EntityConfig / TreeConfig 驱动
 * - yzhApi 原样透传：返回 ApiResponse<T>，业务侧自行取 res.data
 *
 * 架构铁律：关联一律走 Code。所有入参都是记录 Code，不是 Id、也不是 DicNo。
 */

import {
  yzhApi,
  type ApiResponse,
  type TreeItemDto,
  type PagedData,
} from '@yzh-core'

const BASE = '/api/System/Dictionary'

// ========================================================
// 字典下拉选项（后端 DictOptionDto）
// ========================================================

export interface DictOption {
  /** 选项值 = 记录 Code（随机唯一、稳定，业务侧按 Code 关联） */
  Value: string
  /** 显示文本 */
  Label: string
  /** 标签颜色（可选） */
  Color?: string | null
}

// ========================================================
// 配置
// ========================================================

/** 获取左树右表完整配置（TableConfig + TreeConfig + TreeFormConfig） */
export function getDictTreeConfig(): Promise<ApiResponse<any>> {
  return yzhApi.get(`${BASE}/treepconfig`)
}

// ========================================================
// 树接口 - 字典 / 分类
// ========================================================

/** 加载根节点 */
export function getDictTreeRoot(): Promise<ApiResponse<TreeItemDto[]>> {
  return yzhApi.post(`${BASE}/tree/root`, {})
}

/** 懒加载子节点 */
export function getDictTreeChildren(
  parentCode: string,
  level: number,
): Promise<ApiResponse<TreeItemDto[]>> {
  return yzhApi.post(`${BASE}/tree/children`, { ParentCode: parentCode, Level: level })
}

/** 新增字典 / 分类 */
export function addDictTreeNode(data: Record<string, any>): Promise<ApiResponse<TreeItemDto>> {
  return yzhApi.post(`${BASE}/tree/add`, data)
}

/** 修改字典 / 分类 */
export function updateDictTreeNode(data: Record<string, any>): Promise<ApiResponse<TreeItemDto>> {
  return yzhApi.post(`${BASE}/tree/update`, data)
}

/** 删除字典 / 分类（软删除，级联子节点） */
export function deleteDictTreeNodes(codes: string[]): Promise<ApiResponse<string>> {
  return yzhApi.post(`${BASE}/tree/delete`, codes)
}

/** 启用 / 禁用字典 / 分类 */
export function toggleDictTreeNodeValid(
  code: string,
): Promise<ApiResponse<{ Code: string; IsValid: number }>> {
  return yzhApi.post(`${BASE}/tree/toggle-valid`, { Code: code })
}

// ========================================================
// 字典项接口（右表 CRUD）
// ========================================================

/** 分页查询字典项（Filters 需带 { Field:'DicCode', Value: 字典Code }） */
export function getDictItemPage(params: {
  Page: number
  PageSize: number
  SortField?: string
  SortOrder?: string
  Filters?: Array<{ Field: string; Operator: string; Value: any }>
}): Promise<ApiResponse<PagedData<any>>> {
  return yzhApi.post(`${BASE}/filter`, params)
}

/** 新增字典项 */
export function addDictItem(data: Record<string, any>): Promise<ApiResponse<any>> {
  return yzhApi.post(`${BASE}/add`, data)
}

/** 修改字典项 */
export function updateDictItem(data: Record<string, any>): Promise<ApiResponse<any>> {
  return yzhApi.post(`${BASE}/update`, data)
}

/** 删除字典项（软删除） */
export function deleteDictItems(codes: string[]): Promise<ApiResponse<object>> {
  return yzhApi.post(`${BASE}/delete`, codes)
}

/** 启用 / 禁用字典项 */
export function toggleDictItemValid(
  code: string,
): Promise<ApiResponse<{ Code: string; IsValid: number }>> {
  return yzhApi.post(`${BASE}/toggle-valid`, { Code: code })
}

// ========================================================
// 字典消费（供业务页面取下拉数据）
// ========================================================

/**
 * 取某个字典下的全部字典项
 * · value = 字典项 Code
 * · label = 显示文本
 * · 自动过滤已禁用（IsValid=0）与已软删除的记录
 */
export function getDictItems(code: string): Promise<ApiResponse<DictOption[]>> {
  return yzhApi.get(`${BASE}/items/${encodeURIComponent(code)}`)
}

/**
 * 取某个分类下的全部字典
 * · value = 字典 Code
 * · label = 字典名称
 * · 自动过滤已禁用（IsValid=0）与已软删除的记录
 */
export function getDictionariesByCategory(
  categoryCode: string,
): Promise<ApiResponse<DictOption[]>> {
  return yzhApi.get(`${BASE}/category/${encodeURIComponent(categoryCode)}/dictionaries`)
}
