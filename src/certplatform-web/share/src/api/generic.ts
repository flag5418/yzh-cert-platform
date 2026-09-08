/**
 * 通用 CRUD API（泛型接口）
 *
 * 适用于所有继承 YzhControllerBase 的控制器
 * 前端只需指定 controllerName，无需手写每个业务接口
 */
import { yzhApi } from '@yzh-core/api/client'
import type { PageParams, PagedResult, FilterRequest } from '@share/types/contracts'

/**
 * 泛型分页查询
 * GET /api/{controller}/filter
 */
export async function getEntityPage(
  controllerName: string,
  params: PageParams
): Promise<PagedResult<Record<string, any>>> {
  const res = await yzhApi.post(`/api/${controllerName}/filter`, params)
  return res.data
}

/**
 * 泛型新增
 * POST /api/{controller}/add
 */
export async function addEntity(
  controllerName: string,
  entity: Record<string, any>
): Promise<Record<string, any>> {
  const res = await yzhApi.post(`/api/${controllerName}/add`, entity)
  return res.data
}

/**
 * 泛型更新
 * PUT /api/{controller}/update
 */
export async function updateEntity(
  controllerName: string,
  entity: Record<string, any>
): Promise<Record<string, any>> {
  const res = await yzhApi.put(`/api/${controllerName}/update`, entity)
  return res.data
}

/**
 * 泛型删除（批量删除）
 * DELETE /api/{controller}/delete
 */
export async function deleteEntities(
  controllerName: string,
  codes: string[]
): Promise<void> {
  await yzhApi.post(`/api/${controllerName}/delete`, { codes })
}

/**
 * 泛型获取配置
 * GET /api/{controller}/config
 */
export async function getEntityConfig(
  controllerName: string
): Promise<Record<string, any>> {
  const res = await yzhApi.get(`/api/${controllerName}/config`)
  return res.data
}

/**
 * 泛型导出
 * POST /api/{controller}/export
 */
export async function exportEntities(
  controllerName: string,
  params: FilterRequest
): Promise<Blob> {
  const res = await yzhApi.post(`/api/${controllerName}/export`, params, { responseType: 'blob' })
  return res.data
}

/**
 * 泛型导入
 * POST /api/{controller}/import
 */
export async function importEntities(
  controllerName: string,
  file: File
): Promise<{ success: number; fail: number }> {
  const formData = new FormData()
  formData.append('file', file)
  const res = await yzhApi.post(`/api/${controllerName}/import`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' }
  })
  return res.data
}
