import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse, PagedData } from '@yzh-core'
import type { CertStandardFamily } from '../../types/cert'

/**
 * 标准族管理 API（体系 → 族 → 版本 三层中的「族」层）
 *
 * 后端：`CertPlatform.Admin/Controllers/Foundation/CertStandardFamilyController.cs`
 *       （`YzhControllerBase<CertStandardFamily>`，路由 `/api/Admin/Foundation/CertStandardFamily`）
 * 页面：`cert-admin/src/pages/foundation/standard-manage/`
 */

/** 获取标准族分页列表 */
export async function getCertStandardFamilyPage(
  params: any,
): Promise<ApiResponse<PagedData<CertStandardFamily>>> {
  return yzhApi.post<ApiResponse<PagedData<CertStandardFamily>>>(
    '/api/Admin/Foundation/CertStandardFamily/filter',
    params,
  )
}

/** 获取标准族列表（不限分页，供左树组树 / 版本表单下拉选择） */
export async function getCertStandardFamilyList(
  params: Record<string, any> = {},
): Promise<ApiResponse<PagedData<CertStandardFamily>>> {
  return yzhApi.post<ApiResponse<PagedData<CertStandardFamily>>>(
    '/api/Admin/Foundation/CertStandardFamily/filter',
    { Page: 1, PageSize: 1000, ...params },
  )
}

/** 获取标准族页面配置（EntityConfig） */
export async function getCertStandardFamilyConfig(): Promise<ApiResponse<any>> {
  return yzhApi.get<ApiResponse<any>>(
    '/api/Admin/Foundation/CertStandardFamily/config',
  )
}

/** 新增标准族 */
export async function addCertStandardFamily(
  data: Partial<CertStandardFamily>,
): Promise<ApiResponse<CertStandardFamily>> {
  return yzhApi.post<ApiResponse<CertStandardFamily>>(
    '/api/Admin/Foundation/CertStandardFamily/add',
    data,
  )
}

/** 修改标准族 */
export async function updateCertStandardFamily(
  data: Partial<CertStandardFamily>,
): Promise<ApiResponse<CertStandardFamily>> {
  return yzhApi.post<ApiResponse<CertStandardFamily>>(
    '/api/Admin/Foundation/CertStandardFamily/update',
    data,
  )
}

/** 删除标准族（软删；不级联删版本） */
export async function deleteCertStandardFamily(
  codes: string[],
): Promise<ApiResponse<object>> {
  return yzhApi.post<ApiResponse<object>>(
    '/api/Admin/Foundation/CertStandardFamily/delete',
    codes,
  )
}

/** 切换启用/停用 */
export async function toggleCertStandardFamilyValid(
  code: string,
): Promise<ApiResponse<{ Code: string; IsValid: number }>> {
  return yzhApi.post<ApiResponse<{ Code: string; IsValid: number }>>(
    '/api/Admin/Foundation/CertStandardFamily/toggle-valid',
    { Code: code },
  )
}
