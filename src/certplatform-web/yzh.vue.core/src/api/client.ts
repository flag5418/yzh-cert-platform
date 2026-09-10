/**
 * YzhApiClient - 基于 fetch 封装的 HTTP 客户端
 *
 * 原则：原样透传
 * - ApiResponse 顶层 camelCase（YZH.Core.Stand 显式 [JsonPropertyName]）
 *   → 前端 res.data / res.message / res.success
 * - 业务实体 PascalCase（与 C# 属性、数据库列名一致）
 *   → 前端 res.data.Items, row.Code 等
 * - NewEntity/Schema 字典 key 是 camelCase（YZH.Core.Stand/EntitySchemaHelper 反射 ToCamelCase）
 * - 不做大小写来回转换
 * - 前端高频契约（ApiResponse/TreeItemDto/FilterRequest/EntityConfigDto）已在 share/types/contracts.ts 定义
 * - 业务实体类型前端不维护，用 any 兜底
 *
 * 特性：
 * - 自动注入 Token
 * - 401 自动跳登录
 * - TypeScript 泛型支持
 */

// 重导出高频契约（向后兼容，业务可直接 from '@yzh-core/api/client' 引入）
export type { ApiResponse } from '../types/contracts'

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH'
  params?: Record<string, any>
  body?: any
  headers?: Record<string, string>
  signal?: AbortSignal
  /** 是否需要鉴权（默认 true） */
  requireAuth?: boolean
  /** 自定义 raw response（不解析） */
  raw?: boolean
}

export interface YzhApiClientOptions {
  baseURL: string
  getToken?: () => string | null
  onUnauthorized?: () => void
  onError?: (err: Error) => void
}

const TOKEN_KEY = 'YZH_TOKEN'

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (v: string) => localStorage.setItem(TOKEN_KEY, v),
  clear: () => localStorage.removeItem(TOKEN_KEY),
}

export class YzhApiClient {
  private baseURL: string
  private getToken: () => string | null
  private onUnauthorized?: () => void
  private onError?: (err: Error) => void

  constructor(options: YzhApiClientOptions) {
    this.baseURL = options.baseURL.replace(/\/$/, '')
    this.getToken = options.getToken || (() => tokenStore.get())
    this.onUnauthorized = options.onUnauthorized
    this.onError = options.onError
  }

  /**
   * 通用请求方法
   * 原样透传：返回后端 JSON，不做 key 转换
   */
  async request<T = any>(
    url: string,
    options: RequestOptions = {},
  ): Promise<T> {
    const {
      method = 'POST',
      params,
      body,
      headers = {},
      requireAuth = true,
      raw = false,
    } = options

    let finalUrl = url
    const fetchOptions: RequestInit = {
      method,
      headers: {
        'Content-Type': 'application/json',
        ...headers,
      },
    }

    if (requireAuth !== false) {
      const token = this.getToken()
      if (token) {
        ;(fetchOptions.headers as Record<string, string>)['Authorization'] =
          `Bearer ${token}`
      }
    }

    if (method === 'GET' && params) {
      const qs = new URLSearchParams()
      Object.entries(params).forEach(([k, v]) => {
        if (v === undefined || v === null) return
        qs.append(k, String(v))
      })
      const q = qs.toString()
      if (q) finalUrl += (url.includes('?') ? '&' : '?') + q
    } else if (body !== undefined) {
      fetchOptions.body = JSON.stringify(body)
    } else if (params) {
      fetchOptions.body = JSON.stringify(params)
    }

    try {
      const res = await fetch(this.baseURL + finalUrl, fetchOptions)

      if (res.status === 401) {
        tokenStore.clear()
        this.onUnauthorized?.()
        throw new Error('登录已过期，请重新登录')
      }

      // 解析 JSON（无论状态码）
      const json = (await res.json()) as T

      // 非 200 响应：抛出异常，携带后端错误信息
      if (!res.ok) {
        const msg = (json as any)?.message || (json as any)?.msg || `请求失败 (${res.status})`
        const err = new Error(msg)
        ;(err as any).status = res.status
        ;(err as any).data = json
        throw err
      }

      // 原样返回后端 JSON
      return json
    } catch (e: any) {
      this.onError?.(e)
      throw e
    }
  }

  get<T = any>(
    url: string,
    params?: Record<string, any>,
    options?: Omit<RequestOptions, 'method' | 'params'>,
  ) {
    return this.request<T>(url, { ...options, method: 'GET', params })
  }

  post<T = any>(
    url: string,
    body?: any,
    options?: Omit<RequestOptions, 'method' | 'body'>,
  ) {
    return this.request<T>(url, { ...options, method: 'POST', body })
  }

  put<T = any>(
    url: string,
    body?: any,
    options?: Omit<RequestOptions, 'method' | 'body'>,
  ) {
    return this.request<T>(url, { ...options, method: 'PUT', body })
  }

  delete<T = any>(url: string, options?: Omit<RequestOptions, 'method'>) {
    return this.request<T>(url, { ...options, method: 'DELETE' })
  }

  /**
   * POST 下载文件（导出）
   */
  async download(url: string, body: any, filename: string): Promise<void> {
    const token = this.getToken()
    const res = await fetch(this.baseURL + url, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify(body),
    })
    if (res.status === 401) {
      tokenStore.clear()
      this.onUnauthorized?.()
      throw new Error('登录已过期，请重新登录')
    }
    if (!res.ok) {
      const json = await res.json().catch(() => ({}))
      throw new Error(json.message || json.msg || '下载失败')
    }
    const blob = await res.blob()
    this.triggerDownload(blob, filename)
  }

  /**
   * GET 下载文件（模板下载）
   */
  async downloadGet(url: string, filename: string): Promise<void> {
    const token = this.getToken()
    const res = await fetch(this.baseURL + url, {
      method: 'GET',
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
    })
    if (res.status === 401) {
      tokenStore.clear()
      this.onUnauthorized?.()
      throw new Error('登录已过期，请重新登录')
    }
    if (!res.ok) {
      const json = await res.json().catch(() => ({}))
      throw new Error(json.message || json.msg || '下载失败')
    }
    const blob = await res.blob()
    this.triggerDownload(blob, filename)
  }

  /**
   * 上传文件（导入）
   */
  async upload<T = any>(url: string, formData: FormData): Promise<T> {
    const token = this.getToken()
    const res = await fetch(this.baseURL + url, {
      method: 'POST',
      headers: {
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: formData,
    })
    if (res.status === 401) {
      tokenStore.clear()
      this.onUnauthorized?.()
      throw new Error('登录已过期，请重新登录')
    }
    // 原样返回后端 JSON
    return (await res.json()) as T
  }

  /**
   * 触发浏览器下载
   */
  private triggerDownload(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  }
}

/**
 * 默认实例（业务可直接使用）
 */
export const yzhApi = new YzhApiClient({
  baseURL: (import.meta as any).env?.VITE_API_BASE || 'http://127.0.0.1:9992',
  onUnauthorized: () => {
    console.warn('[YzhApi] 401 未授权，请重新登录')
  },
})
