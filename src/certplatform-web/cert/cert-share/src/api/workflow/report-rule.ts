import { yzhApi } from '@yzh-core/api/client'
import type {
  ReportTemplate,
  ReportSection,
} from '@share/types/cert'

const API_PREFIX = '/api/ReportDefinition'

// ========================================================
// 模板 CRUD
// ========================================================

/** 按上下文查询模板 */
export function getTemplateByContext(params: {
  orgCode: string
  standardCode: string
  phaseCode: string
}) {
  return yzhApi.get<ReportTemplate>(
    `${API_PREFIX}/template/context`,
    params
  )
}

/** 保存模板（创建/更新） */
export function saveTemplate(data: Record<string, any>) {
  return yzhApi.post<ReportTemplate>(
    `${API_PREFIX}/template/save`,
    data
  )
}

/** 上传模板文件 */
export function uploadTemplateFile(
  file: File,
  params: { orgCode: string; standardCode: string; phaseCode: string }
) {
  const formData = new FormData()
  formData.append('file', file)
  return yzhApi.post<{ path: string; fileName: string; size: number }>(
    `${API_PREFIX}/template/upload`,
    formData,
    { params }
  )
}

/** 删除模板 */
export function deleteTemplate(id: number) {
  return yzhApi.post<boolean>(
    `${API_PREFIX}/template/delete`,
    null,
    { params: { id } }
  )
}

// ========================================================
// 章节 CRUD
// ========================================================

/** 按模板编码查询章节列表 */
export function getSectionList(reportCode: string) {
  return yzhApi.get<ReportSection[]>(
    `${API_PREFIX}/section/list`,
    { reportCode }
  )
}

/** 保存章节（创建/更新） */
export function saveSection(data: Record<string, any>) {
  return yzhApi.post<ReportSection>(
    `${API_PREFIX}/section/save`,
    data
  )
}

/** 删除章节 */
export function deleteSection(id: number) {
  return yzhApi.post<boolean>(
    `${API_PREFIX}/section/delete`,
    null,
    { params: { id } }
  )
}
