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
// 注意：后端使用 PascalCase JSON 序列化，字段名与 C# 属性名一致
export interface LoginResult {
  Token: string
  UserCode: string
  UserName: string
  UserTrueName?: string
  RoleId?: number
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
 * 用户登录（YZH.Core 新架构接口）
 * 后端 API: POST /api/User/login
 */
export async function login(params: LoginParams): Promise<HttpApiResponse<LoginResult>> {
  return http.post('/User/login', {
    UserName: params.userName,
    Password: params.password,
    Captcha: params.captcha || '',
    Uuid: params.uuid || ''
  })
}

/**
 * 获取登录验证码（YZH.Core 新架构接口）
 * 后端 API: GET /api/User/getVierificationCode
 */
export async function getCaptcha(): Promise<HttpApiResponse<CaptchaData>> {
  return http.get('/User/getVierificationCode')
}

/**
 * 获取当前登录用户信息
 */
export async function getCurrentUser(): Promise<HttpApiResponse<CurrentUser>> {
  return http.get('/User/getCurrentUserInfo')
}

/**
 * 健康检查
 */
export async function ping(): Promise<HttpApiResponse<string>> {
  return http.get('/User/ping')
}
