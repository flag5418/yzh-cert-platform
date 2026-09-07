import http from '@yzh-core/utils/http'
import type { ApiResponse as HttpApiResponse } from '@yzh-core/utils/http'

// 登录请求参数
export interface LoginParams {
  userName: string
  password: string
  captcha?: string
  uuid?: string
}

// 登录响应（双键设计：Id + Code）
export interface LoginResult {
  token: string
  userId: number
  userCode: string
  userName: string
  userTrueName?: string
  roleId?: number
}

// 验证码响应
export interface CaptchaData {
  img: string  // base64 图片
  uuid: string
}

// 当前用户信息
export interface CurrentUser {
  userId: number
  userName: string
  userTrueName: string
  roleId: number
  roleName: string
  orgId?: number
  orgCode?: string
}

/**
 * 用户登录
 * 后端 API: POST /api/Auth/login
 * 请求: { userName, password, captcha?, uuid? }
 */
export async function login(params: LoginParams): Promise<HttpApiResponse<LoginResult>> {
  return http.post('/Auth/login', {
    userName: params.userName,
    password: params.password,
    captcha: params.captcha || '',
    uuid: params.uuid || ''
  })
}

/**
 * 获取登录验证码
 * 后端 API: GET /api/Auth/captcha
 * 返回: { img: base64图片, uuid: 验证码ID }
 */
export async function getCaptcha(): Promise<HttpApiResponse<CaptchaData>> {
  return http.get('/Auth/captcha')
}

/**
 * 获取当前登录用户信息
 */
export async function getCurrentUser(): Promise<HttpApiResponse<CurrentUser>> {
  return http.get('/Auth/getCurrentUser')
}

/**
 * 健康检查
 */
export async function ping(): Promise<HttpApiResponse<string>> {
  return http.get('/Auth/ping')
}
