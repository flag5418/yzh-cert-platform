import { yzhApi } from '../client'
import type { ApiResponse } from '../client'
import { unwrap } from '../../utils'

// ========================================================
// 类型定义
// ========================================================

/** 接口表实体（sys_api 表，全 PascalCase 列名） */
export interface ApiItem {
  Id: number
  Code: string
  Method: string
  Path: string
  GroupPath: string
  Name: string
  Author?: string
  /** 有效标志（1=有效 0=无效）—— 铁律九：唯一启禁契约（原 Enable 列已迁移） */
  IsValid: number
  CreateTime: string
  UpdateTime?: string
}

/** 同步结果 */
export interface SyncResult {
  Added: number
  Updated: number
  Deleted: number
  Total: number
}

// ========================================================
// API 方法
// ========================================================

/** 获取所有接口列表 */
export async function getApiList(): Promise<ApiItem[]> {
  const res = await yzhApi.get<ApiResponse<ApiItem[]>>('/api/ApiSync/list')
  return unwrap(res, [])
}

/** 触发接口扫描并同步到数据库 */
export async function syncApis(): Promise<SyncResult> {
  const res = await yzhApi.post<ApiResponse<SyncResult>>('/api/ApiSync/sync', {})
  return unwrap(res, { Added: 0, Updated: 0, Deleted: 0, Total: 0 })
}

/** 扫描接口（不保存） */
export async function scanApis(): Promise<{ Total: number; Apis: any[] }> {
  const res = await yzhApi.get<ApiResponse<{ Total: number; Apis: any[] }>>('/api/ApiSync/scan')
  return unwrap(res, { Total: 0, Apis: [] })
}

// ========================================================
// Swagger 定位（按接口直接打开 Swagger UI 中的对应操作）
// ========================================================

/** 后端地址：与 yzhApi 同一来源（缺省 '' = 相对路径，dev 走 vite proxy /swagger，prod 同源） */
const API_BASE_URL = yzhApi.baseURL

/** Swagger UI 首页 */
export const SWAGGER_UI_URL = `${API_BASE_URL}/swagger/index.html`

/** OpenAPI 文档地址 */
const SWAGGER_DOC_URL = `${API_BASE_URL}/swagger/v1/swagger.json`

/** OpenAPI 支持的请求方法 */
const HTTP_METHODS = new Set(['GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'HEAD', 'OPTIONS', 'TRACE'])

/** 统一路径写法（接口表里的 Path 不带前导斜杠，OpenAPI 里带） */
function normalizePath(path: string | undefined): string {
  return `/${(path || '').replace(/^\/+/, '')}`
}

/**
 * OpenAPI 未声明 operationId 时，Swagger UI 按「方法_路径」生成操作标识，
 * 规则与 swagger-ui 内部一致：去掉前导斜杠后，非单词字符全部替换为下划线
 */
function fallbackOperationId(method: string, path: string): string {
  return `${method.toLowerCase()}_${normalizePath(path).slice(1)}`.replace(/[^\w]/g, '_')
}

let swaggerIndexPromise: Promise<Map<string, string>> | null = null

/**
 * 建立「METHOD 路径」→「#/分组/操作」索引（只请求一次，失败后允许重试）
 * 索引来自 OpenAPI 文档本身，保证锚点与 Swagger UI 完全一致
 */
function loadSwaggerIndex(): Promise<Map<string, string>> {
  if (!swaggerIndexPromise) {
    swaggerIndexPromise = fetch(SWAGGER_DOC_URL)
      .then((res) => {
        if (!res.ok) throw new Error(`OpenAPI 文档不可用（HTTP ${res.status}）`)
        return res.json()
      })
      .then((doc: any) => {
        const index = new Map<string, string>()
        const paths = (doc?.paths ?? {}) as Record<string, Record<string, any>>
        for (const [path, operations] of Object.entries(paths)) {
          for (const [method, operation] of Object.entries(operations ?? {})) {
            const verb = method.toUpperCase()
            const tag = operation?.tags?.[0]
            if (!HTTP_METHODS.has(verb) || !tag) continue
            const operationId = operation?.operationId || fallbackOperationId(verb, path)
            index.set(
              `${verb} ${normalizePath(path)}`,
              `#/${encodeURIComponent(tag)}/${encodeURIComponent(operationId)}`,
            )
          }
        }
        return index
      })
      .catch((e) => {
        swaggerIndexPromise = null
        throw e
      })
  }
  return swaggerIndexPromise
}

/** 获取接口在 Swagger UI 中的深链；文档不可用或找不到该接口时返回 null */
export async function getSwaggerOperationUrl(
  api: Pick<ApiItem, 'Method' | 'Path'>,
): Promise<string | null> {
  try {
    const index = await loadSwaggerIndex()
    const anchor = index.get(`${(api.Method || '').toUpperCase()} ${normalizePath(api.Path)}`)
    return anchor ? `${SWAGGER_UI_URL}${anchor}` : null
  } catch {
    return null
  }
}
