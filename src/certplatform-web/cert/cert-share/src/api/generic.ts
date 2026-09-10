/**
 * 通用 CRUD API（泛型接口）
 *
 * 适用于所有继承 YzhControllerBase 的控制器
 * 前端只需指定 controllerName，无需手写每个业务接口
 */
import { yzhApi, type ApiResponse, type FilterRequest, type PagedResult } from '@yzh-core'

/**
 * 泛型分页查询
 * POST /api/{controller}/filter
 */
export async function getEntityPage<T = Record<string, any>>(
  controllerName: string,
  params: FilterRequest,
): Promise<PagedResult<T>> {
  const res = await yzhApi.post<{ data: PagedResult<T> }>(
    `/api/${controllerName}/filter`,
    params,
  )
  return res.data
}

/**
 * 泛型新增
 * POST /api/{controller}/add
 */
export async function addEntity<T = Record<string, any>>(
  controllerName: string,
  entity: Record<string, any>,
): Promise<T> {
  const res = await yzhApi.post<{ data: T }>(`/api/${controllerName}/add`, entity)
  return res.data
}

/**
 * 泛型更新
 * POST /api/{controller}/update
 */
export async function updateEntity<T = Record<string, any>>(
  controllerName: string,
  entity: Record<string, any>,
): Promise<T> {
  const res = await yzhApi.post<{ data: T }>(`/api/${controllerName}/update`, entity)
  return res.data
}

/**
 * 泛型删除（批量删除）
 * POST /api/{controller}/delete
 */
export async function deleteEntities(
  controllerName: string,
  codes: string[],
): Promise<void> {
  await yzhApi.post(`/api/${controllerName}/delete`, codes)
}

/**
 * 泛型获取配置
 * GET /api/{controller}/config
 */
export async function getEntityConfig<T = Record<string, any>>(
  controllerName: string,
): Promise<T> {
  const res = await yzhApi.get<{ data: T }>(`/api/${controllerName}/config`)
  return res.data
}

/**
 * 泛型导出
 * POST /api/{controller}/export
 */
export async function exportEntities(
  controllerName: string,
  params: FilterRequest,
): Promise<Blob> {
  const res = await yzhApi.post<Blob>(
    `/api/${controllerName}/export`,
    params,
    { responseType: 'blob' } as any,
  )
  return res
}

/**
 * 泛型导入
 * POST /api/{controller}/import
 */
export async function importEntities(
  controllerName: string,
  file: File,
): Promise<{ success: number; fail: number }> {
  const formData = new FormData()
  formData.append('file', file)
  const res = await yzhApi.post<{ data: { success: number; fail: number } }>(
    `/api/${controllerName}/import`,
    formData,
    { headers: { 'Content-Type': 'multipart/form-data' } } as any,
  )
  return res.data
}
