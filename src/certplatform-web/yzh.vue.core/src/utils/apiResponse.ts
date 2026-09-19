/**
 * ApiResponse 工具函数
 *
 * 来源：role-menu.ts / role-api.ts / api.ts 重复定义
 * 统一抽取到此处
 */

import type { ApiResponse } from '../types/contracts'

/**
 * 解包 ApiResponse，失败时抛出异常
 * @param res API 返回的 ApiResponse
 * @param fallback 失败时的默认值
 * @returns 解包后的 data
 */
export function unwrap<T>(res: ApiResponse<T> | undefined, fallback: T): T {
  if (res && res.success === false) {
    throw new Error(res.message || '请求失败')
  }
  return (res?.data ?? fallback) as T
}
