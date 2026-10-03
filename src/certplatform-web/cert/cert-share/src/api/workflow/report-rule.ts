/**
 * ★ 报告章节定义 API（★2026-09-29 去主表化，D34/D35）
 *
 * 【★ 改造说明】
 *   删除了全部「报告主表」端点（模板查询/保存/上传/删除）：
 *     - getTemplateByContext ❌
 *     - saveTemplate        ❌
 *     - uploadTemplateFile  ❌
 *     - deleteTemplate      ❌
 *     - getSectionList      ❌（按 reportCode 查，已无 ReportCode 列）
 *
 *   章节改为按三元组（OrgCode + StandardCode + PhaseCode）直接定位，
 *   与 NC 规则定义（cert_validation_rule）完全对称。
 *
 * 【★ 命名铁律】
 *   DB 列名 = C# 属性名 = TS 字段名 = PascalCase。
 *   本文件所有 data/params 的**键**用 PascalCase（对应实体属性）；
 *   查询串键名 orgCode/standardCode/phaseCode 对应后端 C# 形参名，保持 camelCase。
 */
import { yzhApi } from '@yzh-core/api/client'
import type { ReportSection } from '@share/types/cert'

const API_PREFIX = '/api/Admin/Workflow/ReportDefinition'

// ========================================================
// 章节 CRUD（★唯一保留的能力）
// ========================================================

/** ★ 按三元组查询章节列表（★去 JOIN，章节自带归属） */
export function getSectionsByContext(params: {
  orgCode: string
  standardCode: string
  phaseCode: string
}) {
  return yzhApi.get<ReportSection[]>(
    `${API_PREFIX}/section/by-context`,
    params
  )
}

/** 按章节 Code 查询单条 */
export function getSectionDetail(code: string) {
  return yzhApi.get<ReportSection>(
    `${API_PREFIX}/section/detail`,
    { code }
  )
}

/** 保存章节（创建/更新，★按 Code 判定：准则 A） */
export function saveSection(data: Partial<ReportSection> & Record<string, any>) {
  return yzhApi.post<ReportSection>(
    `${API_PREFIX}/section/save`,
    data
  )
}

/** 删除章节（软删，准则 A：业务键 Code） */
export function deleteSection(code: string) {
  return yzhApi.post<boolean>(
    `${API_PREFIX}/section/delete`,
    null,
    { params: { code } }
  )
}

/** ★ 批量启停（替代原「模板级启停」能力，D34 删除主表后的等价手段） */
export function batchToggleSections(params: {
  orgCode: string
  standardCode: string
  phaseCode: string
  isValid: 0 | 1
}) {
  return yzhApi.post<{ Affected: number }>(
    `${API_PREFIX}/section/batch-toggle`,
    null,
    { params }
  )
}
