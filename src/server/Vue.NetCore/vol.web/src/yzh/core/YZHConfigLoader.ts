// ============================================================
//  YZH Framework V3.0 —— 数据库配置加载器（V2.6 Store 优先模式）
//
//  加载优先级：
//  1. Vuex Store (YZHConfigStore) — 登录后全量加载，同步读取，零延迟
//  2. 单页 API 请求 — Store 未命中时的降级方案
//
//  职责：
//  - 为 YzhCrudTable / YzhCrudV3 提供统一的配置获取接口
//  - 内部自动优先从 Store 读取，降级为网络请求
//  - 数据格式转换（后端 camelCase → 前端标准格式）
//
//  V2.6 修复（2026-08-07）：
//  - 修复 Vue3 子组件 onMounted 先于父组件执行的时序问题
//    （Index.vue 的 yzhConfig/init 尚未完成时，子组件已在读取 Store）
//  - 修复降级 _fetchConfig 不使用 Vol http 模块导致 baseURL 和 JWT token 缺失
// ============================================================

import type { IYzhPageUIConfig, IYzhPageMeta, IYzhFieldConfig } from '../types/YZHV3Config'
// 导入 Vol 框架的 http 模块（自动处理 baseURL 和 JWT Token）
import http from '@/api/http'

/** 配置缓存 (pageKey => config) — 用于降级场景 */
const configCache = new Map<string, IYzhPageUIConfig>()

/** 正在加载的 Promise（防止并发重复请求） */
const loadingPromises = new Map<string, Promise<IYzhPageUIConfig>>()

/**
 * 默认 API 基础路径
 */
const DEFAULT_API_PREFIX = '/api/yzh-page-config'

/**
 * 获取 Vuex store 实例（懒加载）
 */
function getStore(): any {
  try {
    // Vue3 + Vuex4: app.config.globalProperties.$store
    const app = document.querySelector('#app')?.__vue_app__
    if (app?.config?.globalProperties?.$store) {
      return app.config.globalProperties.$store
    }
    // 兼容：window 上直接挂载
    if ((window as any).$store) return (window as any).$store
  } catch { /* 非 Vuex 环境 */ }
  return null
}

/**
 * V2.6 新增：等待 Vuex Store 就绪
 *
 * 解决 Vue3 组件挂载时序问题：
 *   子组件 onMounted → 父组件 onMounted
 *   YzhCrudTable（子）的 loadDbPageConfig 先于 Index.vue（父）的 yzhConfig/init 执行
 *   导致 Store 尚未初始化时子组件就尝试读取配置
 *
 * 此函数轮询 Store 的 loaded 状态，最多等待 maxWaitMs 毫秒
 */
async function waitForStoreReady(maxWaitMs = 5000): Promise<any> {
  const store = getStore()
  if (!store) return null

  // 如果 Store 已就绪或有缓存数据，立即返回
  if (store.state?.yzhConfig?.loaded || Object.keys(store.state?.yzhConfig?.configs || {}).length > 0) {
    return store
  }

  // 轮询等待（每 100ms 检查一次）
  const start = Date.now()
  while (Date.now() - start < maxWaitMs) {
    await new Promise((r) => setTimeout(r, 100))
    if (store.state?.yzhConfig?.loaded) {
      return store
    }
    // 如果 Store 加载出错，不再等待，降级为单页请求
    if (store.state?.yzhConfig?.error) {
      console.warn('[YZHConfigLoader] Store 加载出错，降级为单页请求')
      return store
    }
  }

  console.warn(`[YZHConfigLoader] Store 等待超时（${maxWaitMs}ms），降级为单页请求`)
  return store
}

/**
 * 从数据库加载页面 UI 配置（V2.5：Store 优先模式）
 *
 * @param pageKey 页面唯一标识 (如 'ISOStandard')
 * @returns 完整的页面 UI 配置
 *
 * 加载策略：
 *   1. 先查 Vuex Store（同步，<1ms）
 *   2. Store 未命中 → 降级为单页 API 请求
 *   3. API 也失败 → 抛出异常
 */
export async function loadPageConfig(
  pageKey: string,
  apiPrefix: string = DEFAULT_API_PREFIX,
  options?: { useCache?: boolean; forceRefresh?: boolean }
): Promise<IYzhPageUIConfig> {
  const { useCache = true, forceRefresh = false } = options ?? {}

  // ====== V2.6：优先从 Vuex Store 读取（含等待就绪机制）======
  if (!forceRefresh) {
    // 等待 Store 就绪（解决 Vue3 子组件先于父组件 mount 的时序问题）
    const store = await waitForStoreReady()
    if (store) {
      const cached = store.getters['yzhConfig/getConfig']?.(pageKey)
      if (cached) {
        // 命中 Store 缓存，转换为标准格式后返回
        console.log(`[YZHConfigLoader] 📦 Store 命中: ${pageKey}`)
        const transformed = transformStoreData(cached)
        // 同时写入本地缓存（备用）
        if (useCache) configCache.set(pageKey, transformed)
        return transformed
      }
      // Store 已加载完成但没有此页面 → 降级为单页请求
      if (store.getters['yzhConfig/isReady']) {
        console.warn(`[YZHConfigLoader] ⚠️ Store 中无 ${pageKey} 配置，降级为单页请求`)
      }
    }
  }

  // ====== 原有逻辑：内存缓存 + 单页请求 ======
  if (useCache && !forceRefresh && configCache.has(pageKey)) {
    return configCache.get(pageKey)!
  }

  if (!forceRefresh && loadingPromises.has(pageKey)) {
    return loadingPromises.get(pageKey)!
  }

  // 发起单页请求（降级方案）
  const promise = _fetchConfig(pageKey, apiPrefix)
    .then(config => {
      if (useCache) configCache.set(pageKey, config)
      return config
    })
    .catch(err => {
      console.error(`[YZHConfigLoader] 加载配置失败: ${pageKey}`, err)
      throw err
    })
    .finally(() => {
      loadingPromises.delete(pageKey)
    })

  loadingPromises.set(pageKey, promise)
  return promise
}

/**
 * 从 Vuex Store 的原始数据转换为前端标准格式
 * Store 数据格式：{ pageMeta: {...}, fieldConfigs: [...] }
 * 目标格式：IYzhPageUIConfig { pageMeta: IYzhPageMeta, fieldConfigs: IYzhFieldConfig[] }
 */
function transformStoreData(storeData: any): IYzhPageUIConfig {
  if (!storeData) throw new Error('Store 配置数据为空')

  let { pageMeta, fieldConfigs } = storeData

  // 确保 visibleButtons 是数组
  if (pageMeta && !Array.isArray(pageMeta.visibleButtons)) {
    try {
      pageMeta = { ...pageMeta, visibleButtons: JSON.parse(pageMeta.visibleButtons || '[]') }
    } catch {
      pageMeta = { ...pageMeta, visibleButtons: ['add', 'refresh', 'batchDelete', 'columnSetting'] }
    }
  }

  // 处理字段配置（后端返回的是 PascalCase，需要转换）
  const processedFields: IYzhFieldConfig[] = (fieldConfigs || []).map((f: any) => ({
    // 后端 DTO 返回 PascalCase → 前端 camelCase
    fieldName: (f.fieldName || f.FieldName) || '',
    fieldAlias: (f.fieldAlias || f.FieldAlias) || (f.fieldName || f.FieldName) || '',
    xsFlag: f.xsFlag ?? f.XsFlag ?? true,
    columnSxh: f.columnSxh ?? f.ColumnSxh ?? 0,
    columnTitle: (f.columnTitle ?? f.ColumnTitle) || '',
    columnWidth: f.columnWidth ?? f.ColumnWidth ?? 120,
    sortable: f.sortable ?? f.Sortable ?? true,
    align: f.align ?? f.Align ?? 'left',
    showOverflow: f.showOverflow ?? f.ShowOverflow ?? true,

    bcFlag: f.bcFlag ?? f.BcFlag ?? true,
    formTitle: (f.formTitle ?? f.FormTitle) || (f.columnTitle || f.ColumnTitle) || '',
    controlType: ((f.controlType || f.ControlType) || 'input').toLowerCase(),
    required: f.required ?? f.Required ?? false,
    maxlength: f.maxlength ?? f.MaxLength ?? 0,
    placeholder: (f.placeholder ?? f.Placeholder) || '',
    defaultValue: f.defaultValue ?? f.DefaultValue ?? '',

    gridRow: f.gridRow ?? f.GridRow ?? 0,
    gridCol: f.gridCol ?? f.GridCol ?? 0,
    gridRowSpan: f.gridRowSpan ?? f.GridRowSpan ?? 1,
    gridColSpan: f.gridColSpan ?? f.GridColSpan ?? 1,

    dataKey: (f.dataKey ?? f.DataKey) || null,
    groupIndex: f.groupIndex ?? f.GroupIndex ?? 0,

    searchFlag: f.searchFlag ?? f.SearchFlag ?? false,
    searchTitle: (f.searchTitle ?? f.SearchTitle) || '',
    searchPlaceholder: (f.searchPlaceholder ?? f.SearchPlaceholder) || '',
    searchControlType: (((f.searchControlType || f.SearchControlType) || (f.controlType || f.ControlType)) || 'input').toLowerCase(),
    searchWidth: f.searchWidth ?? f.SearchWidth ?? 180,
  })) as IYzhFieldConfig[]

  return { pageMeta: pageMeta as IYzhPageMeta, fieldConfigs: processedFields }
}

/**
 * 内部实际执行 HTTP 请求（降级方案：当 Store 无数据时使用）
 *
 * V2.6 修复：使用 Vol 框架的 http 模块（自动处理 baseURL 和 JWT Token）
 * 原实现使用 window.axios（不存在）或原生 fetch（无 baseURL/无 Token），导致请求必然失败
 */
async function _fetchConfig(
  pageKey: string,
  apiPrefix: string
): Promise<IYzhPageUIConfig> {
  const url = `${apiPrefix.replace(/\/+$/, '')}/${encodeURIComponent(pageKey)}`

  // 使用 Vol 框架的 http 模块：
  // - 自动设置 baseURL（http://localhost:9991/）
  // - 自动携带 JWT Authorization header
  // - get(url, params, loading) 返回 response.data（即响应体 JSON）
  let result: any
  try {
    result = await http.get(url, null, false)
  } catch (e: any) {
    throw new Error(`[YZHConfigLoader] 网络请求失败: ${e?.message || e}`)
  }

  // result 是响应体 JSON，预期格式：{ success: true, data: { pageMeta, fieldConfigs } }
  if (!result?.success) {
    throw new Error(`[YZHConfigLoader] ${result?.message || '未知错误'}`)
  }

  return transformApiData(result.data)
}

/**
 * 转换后端 API 返回数据为前端标准格式（兼容旧格式）
 */
function transformApiData(data: any): IYzhPageUIConfig {
  // 如果数据已经是 Store 格式（有 pageMeta/fieldConfigs），走 Store 转换路径
  if (data.pageMeta && data.fieldConfigs) {
    return transformStoreData(data)
  }

  // 否则按旧格式处理（向后兼容）
  const { pageMeta, fieldConfigs } = data

  if (!Array.isArray(pageMeta?.visibleButtons)) {
    try {
      pageMeta.visibleButtons = JSON.parse(pageMeta?.visibleButtons || '[]')
    } catch {
      pageMeta.visibleButtons = ['add', 'refresh', 'batchDelete', 'columnSetting']
    }
  }

  const processedFields: IYzhFieldConfig[] = (fieldConfigs || []).map((f: any) => ({
    ...f,
    fieldAlias: f.fieldAlias || f.fieldName,
    formTitle: f.formTitle || f.columnTitle || '',
    controlType: f.controlType || 'input',
    searchControlType: f.searchControlType || f.controlType || 'input',
    searchTitle: f.searchTitle || f.formTitle || f.columnTitle || '',
  })) as IYzhFieldConfig[]

  return { pageMeta: pageMeta as IYzhPageMeta, fieldConfigs: processedFields }
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
