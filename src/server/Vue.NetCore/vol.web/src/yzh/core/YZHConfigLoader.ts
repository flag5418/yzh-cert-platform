// ============================================================
//  YZH Framework —— 配置加载器
//
//  简化版：直接使用 options.js 配置，不再从数据库加载
//  组件会回退到 options.js 中的配置
// ============================================================

import type { IYzhPageUIConfig } from '../types/YZHV3Config'



/**
 * 加载页面配置
 * 
 * 简化版：直接返回 null，让组件使用 options.js 配置
 * 如果后续需要数据库驱动配置，可以在此处实现
 */
export async function loadPageConfig(
  pageKey: string,
  apiPrefix?: string,
  options?: { useCache?: boolean; forceRefresh?: boolean }
): Promise<IYzhPageUIConfig | null> {
  // 直接返回 null，组件会使用 options.js 中的配置
  return null
}



/** 清除指定页面的缓存 */
export function clearPageConfigCache(pageKey: string): void {
  configCache.delete(pageKey)
  loadingPromises.delete(pageKey)
}

/** 清除所有配置缓存 */
export function clearAllConfigCache(): void {
  configCache.clear()
  loadingPromises.clear()
}

/** 获取缓存的配置（同步，不触发加载） */
export function getCachedConfig(pageKey: string): IYzhPageUIConfig | undefined {
  return configCache.get(pageKey)
}
