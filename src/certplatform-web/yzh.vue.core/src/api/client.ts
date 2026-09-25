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

/**
 * L2 HTTP 层的兜底文案（只覆盖 401/403/404/500 四种基础设施错误，22 §三矩阵）。
 * 响应体有 err/message 时优先用响应体；空响应体（403 中间件短路、502 网关页）才落到这里。
 */
const STATUS_FALLBACK: Record<number, string> = {
  401: '登录已过期，请重新登录',
  403: '无权限执行该操作',
  404: '接口不存在',
  500: '服务器内部错误，请稍后重试',
}

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (v: string) => localStorage.setItem(TOKEN_KEY, v),
  clear: () => localStorage.removeItem(TOKEN_KEY),
}

export class YzhApiClient {
  /** 服务根地址（文件下载等场景需要读取） */
  public baseURL: string
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
   * 动态配置客户端（宿主 main.ts 启动时调用 `configureYzhApi`）
   * baseURL 缺省为 ''（相对路径 /api/*）—— dev 由宿主 vite proxy 承接、prod 同源承接；
   * 跨域部署才由宿主显式注入绝对地址。core 永不硬编码地址（守卫 R11）。
   */
  configure(options: Partial<YzhApiClientOptions>): void {
    if (options.baseURL !== undefined) {
      this.baseURL = options.baseURL.replace(/\/$/, '')
    }
    if (options.getToken) this.getToken = options.getToken
    if (options.onUnauthorized !== undefined) this.onUnauthorized = options.onUnauthorized
    if (options.onError !== undefined) this.onError = options.onError
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
      raw: _raw = false,
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

    // 构建查询参数（GET 或 POST+body 时 params 都追加到 URL）
    if (params) {
      // 防御：get(url, params) 的第二个参数本身就是查询对象，
      // 但容易照着 axios 的写法再包一层 { params: {...} }。
      // 不拦的话会把整个对象 String() 成 "[object Object]"，
      // 变成 `?params=[object Object]` —— 请求能发出、后端却 400，
      // 而且类型检查发现不了（params 是 Record<string, any>）。
      // 这里自动解包并给出告警，避免这类故障静默发生。
      let flatParams: Record<string, any> = params
      const keys = Object.keys(params)
      const nested = (params as any).params
      if (keys.length === 1 && keys[0] === 'params' && nested && typeof nested === 'object') {
        console.warn(
          '[YzhApi] 查询参数多包了一层 params（应为 get(url, { a, b }) 而非 get(url, { params: { a, b } })），已自动解包：',
          nested,
        )
        flatParams = nested
      }

      const qs = new URLSearchParams()
      Object.entries(flatParams).forEach(([k, v]) => {
        if (v === undefined || v === null) return
        qs.append(k, String(v))
      })
      const q = qs.toString()
      if (q) finalUrl += (url.includes('?') ? '&' : '?') + q
    }

    // 请求体
    if (body !== undefined) {
      fetchOptions.body = JSON.stringify(body)
    } else if (method !== 'GET' && !params) {
      // 无 body 无 params 的 POST，发空 JSON
      fetchOptions.body = '{}'
    }

    try {
      const res = await fetch(this.baseURL + finalUrl, fetchOptions)

      if (res.status === 401) {
        tokenStore.clear()
        this.onUnauthorized?.()
        // 带 status 抛出：宿主 onError 据此做 401 副作用（D6 只做副作用、不弹提示）
        const err = new Error('登录已过期，请重新登录')
        ;(err as any).status = 401
        throw err
      }

      // 解析 JSON（无论状态码）；响应体不是 JSON（网关 HTML / 空体）时不抛 SyntaxError，
      // 返回 null 交给上层 expectOk 统一报错，避免裸 "Unexpected token <" 流到用户面前
      const json = (await res.json().catch(() => null)) as T

      // 非 200 响应：抛出异常，携带后端错误信息（err 优先，P0 起失败文本在 err）
      if (!res.ok) {
        const body = json as any
        const msg =
          body?.err ||
          body?.message ||
          body?.msg ||
          // 401/403 可能是空响应体（中间件直接短路），按状态码兜底文案
          STATUS_FALLBACK[res.status] ||
          `请求失败 (${res.status})`
        const err = new Error(msg)
        ;(err as any).status = res.status
        ;(err as any).data = body
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
   * GET 二进制内容（带鉴权）——用于预览场景
   *
   * 背景：`<iframe src>` / `<img src>` 无法携带 Authorization 头（本平台 JWT 走 Header），
   * 直接渲染受保护的文件流必然 401。必须先带 Token 取回 Blob，再用 ObjectURL 渲染。
   *
   * @param url    相对路径
   * @param params 查询参数（追加到 URL）
   * @returns      Blob（MIME 取自响应头，缺失时调用方按魔数兜底）
   * @throws       401 / 业务错误：抛出带 status 的 Error（错误信息优先取后端 JSON 的 message）
   */
  async getBlob(url: string, params?: Record<string, any>): Promise<Blob> {
    const token = this.getToken()
    let finalUrl = url
    if (params) {
      const qs = new URLSearchParams()
      Object.entries(params).forEach(([k, v]) => {
        if (v === undefined || v === null) return
        qs.append(k, String(v))
      })
      const q = qs.toString()
      if (q) finalUrl += (url.includes('?') ? '&' : '?') + q
    }

    const res = await fetch(this.baseURL + finalUrl, {
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

    const contentType = res.headers.get('content-type') || ''

    // 后端业务失败时返回 JSON（如 { code: 400, message }），需取出 message 而不是当成文件
    if (contentType.includes('application/json')) {
      const json = await res.json().catch(() => ({} as any))
      const err = new Error(json?.err || json?.message || json?.msg || `请求失败 (${res.status})`)
      ;(err as any).status = res.status
      throw err
    }

    if (!res.ok) {
      const err = new Error(`请求失败 (${res.status})`)
      ;(err as any).status = res.status
      throw err
    }

    return await res.blob()
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
      throw new Error(json.err || json.message || json.msg || '下载失败')
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
      throw new Error(json.err || json.message || json.msg || '下载失败')
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
 *
 * baseURL 缺省 '' → 相对路径 `/api/*`：
 * - dev：宿主 vite proxy（cert-admin / cert-auditor 均已配 `/api → 9992`）
 * - prod：同源部署 / nginx
 * 跨域部署时由宿主 main.ts 调 `configureYzhApi({ baseURL })` 注入（值来自宿主 env/配置）。
 */
export const yzhApi = new YzhApiClient({
  baseURL: '',
  onUnauthorized: () => {
    console.warn('[YzhApi] 401 未授权，请重新登录')
  },
})

/**
 * 宿主注入后台地址/钩子（契约：core 零硬编码地址，项目决定「对谁说」）
 * @example // 宿主 main.ts
 * import { configureYzhApi } from '@yzh-core/api/client'
 * configureYzhApi({ baseURL: import.meta.env.VITE_API_BASE ?? '' })
 */
export function configureYzhApi(options: Partial<YzhApiClientOptions>): void {
  yzhApi.configure(options)
}
