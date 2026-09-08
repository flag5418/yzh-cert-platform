/**
 * YzhApiClient - 基于 fetch 封装的 HTTP 客户端
 *
 * 特性：
 * - 自动注入 Token
 * - vol 后端响应格式兼容（{ status, msg, rows, total, data }）
 * - 401 自动跳登录
 * - TypeScript 泛型支持
 */

export interface ApiResponse<T = any> {
  /** vol 格式：0=成功；新格式：200=成功 */
  status: number
  msg: string | null
  message?: string
  code?: number | string
  data?: T
  rows?: T[]
  total?: number
  [key: string]: any
}

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
  /** 是否使用 vol 格式（默认 true） */
  legacy?: boolean
}

const TOKEN_KEY = 'YZH_TOKEN'

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (v: string) => localStorage.setItem(TOKEN_KEY, v),
  clear: () => localStorage.removeItem(TOKEN_KEY)
}

export class YzhApiClient {
  private baseURL: string
  private getToken: () => string | null
  private onUnauthorized?: () => void
  private onError?: (err: Error) => void
  private legacy: boolean

  constructor(options: YzhApiClientOptions) {
    this.baseURL = options.baseURL.replace(/\/$/, '')
    this.getToken = options.getToken || (() => tokenStore.get())
    this.onUnauthorized = options.onUnauthorized
    this.onError = options.onError
    this.legacy = options.legacy ?? true
  }

  /**
   * 通用请求方法
   */
  async request<T = any>(url: string, options: RequestOptions = {}): Promise<T> {
    const { method = 'POST', params, body, headers = {}, requireAuth = true, raw = false } = options

    // 处理 URL：GET 走 query，其他走 body
    let finalUrl = url
    const fetchOptions: RequestInit = {
      method,
      headers: {
        'Content-Type': 'application/json',
        ...headers
      }
    }

    if (requireAuth !== false) {
      const token = this.getToken()
      if (token) {
        ;(fetchOptions.headers as Record<string, string>)['Authorization'] = `Bearer ${token}`
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
      // vol 风格：POST + JSON body 作为参数
      fetchOptions.body = JSON.stringify(params)
    }

    try {
      const res = await fetch(this.baseURL + finalUrl, fetchOptions)

      // 401 未授权
      if (res.status === 401) {
        tokenStore.clear()
        this.onUnauthorized?.()
        throw new Error('登录已过期，请重新登录')
      }

      if (raw) {
        return (await res.json()) as T
      }

      const json: ApiResponse<T> = await res.json()
      return this.normalizeResponse(json)
    } catch (e: any) {
      this.onError?.(e)
      throw e
    }
  }

  /**
   * 忽略大小写获取对象属性（兼容 Vol JsonNormal 返回 PascalCase）
   */
  private pick(json: Record<string, any>, ...keys: string[]): any {
    for (const k of keys) {
      // 先精确匹配，再忽略大小写
      if (json[k] !== undefined) return json[k]
      const lower = k.toLowerCase()
      for (const jk of Object.keys(json)) {
        if (jk.toLowerCase() === lower) return json[jk]
      }
    }
    return undefined
  }

  /**
   * PascalCase → camelCase
   * DepartmentName → departmentName
   */
  private toCamelCase(str: string): string {
    return str.charAt(0).toLowerCase() + str.slice(1)
  }

  /**
   * 递归转换对象的 key 为 camelCase（用于 Vol JsonNormal 返回 PascalCase 实体）
   * 仅在检测到第一个 key 的首字母大写时转换，避免误伤已是 camelCase 的数据
   */
  private normalizeKeys(obj: any): any {
    if (obj === null || obj === undefined || typeof obj !== 'object') return obj
    if (Array.isArray(obj)) return obj.map((item) => this.normalizeKeys(item))
    const keys = Object.keys(obj)
    if (keys.length === 0) return obj
    // 启发式：若任意 key 首字母大写 → 需要转 camelCase
    const hasPascal = keys.some((k) => k.length > 0 && k[0] >= 'A' && k[0] <= 'Z')
    if (!hasPascal) return obj
    const out: Record<string, any> = {}
    for (const k of keys) {
      out[this.toCamelCase(k)] = this.normalizeKeys(obj[k])
    }
    return out
  }

  /**
   * 归一化 vol 响应为新格式
   *  - vol JsonNormal 返回 PascalCase：{ Status, Msg, Rows, Total, Data }
   *  - vol Json 返回 camelCase：{ status, msg, rows, total, data }
   */
  private normalizeResponse<T>(json: ApiResponse<T>): T {
    if (!this.legacy) {
      return this.normalizeKeys(this.pick(json, 'data') ?? json) as T
    }

    // vol 格式判断：Status 可能为 true/PascalCase 或 0/camelCase 表示成功
    const status = this.pick(json, 'status', 'code')
    const isSuccess = status === true || status === 0 || status === undefined
    if (isSuccess === false || status === 401) {
      const message = (this.pick(json, 'message', 'msg', 'error') as string) || '请求失败'
      const err = new Error(message)
      ;(err as any).response = json
      throw err
    }

    // 兼容返回结构：优先 Data，其次 rows
    const data = this.pick(json, 'data')
    if (data !== undefined && data !== null) {
      return this.normalizeKeys(data) as T
    }
    const rows = this.pick(json, 'rows')
    if (rows !== undefined) {
      return { rows: this.normalizeKeys(rows), total: this.pick(json, 'total') || 0 } as T
    }
    return this.normalizeKeys(json) as T
  }

  get<T = any>(
    url: string,
    params?: Record<string, any>,
    options?: Omit<RequestOptions, 'method' | 'params'>
  ) {
    return this.request<T>(url, { ...options, method: 'GET', params })
  }

  post<T = any>(url: string, body?: any, options?: Omit<RequestOptions, 'method' | 'body'>) {
    return this.request<T>(url, { ...options, method: 'POST', body })
  }

  put<T = any>(url: string, body?: any, options?: Omit<RequestOptions, 'method' | 'body'>) {
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
        ...(token ? { Authorization: `Bearer ${token}` } : {})
      },
      body: JSON.stringify(body)
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
        ...(token ? { Authorization: `Bearer ${token}` } : {})
      }
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
        ...(token ? { Authorization: `Bearer ${token}` } : {})
      },
      body: formData
    })
    if (res.status === 401) {
      tokenStore.clear()
      this.onUnauthorized?.()
      throw new Error('登录已过期，请重新登录')
    }
    const json: ApiResponse<T> = await res.json()
    return this.normalizeResponse(json)
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
    // 401 时清理并提示
    console.warn('[YzhApi] 401 未授权，请重新登录')
  },
  legacy: true
})
