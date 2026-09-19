import { yzhApi } from '@yzh-core/api/client'

// 登录请求参数
export interface LoginParams {
  userName: string
  password: string
  captcha?: string
  uuid?: string
}

// 登录响应（Code 关联设计）
// 注意：后端使用 PascalCase JSON 序列化，字段名与 C# 属性名一致
export interface LoginResult {
  Token: string
  UserCode: string
  UserName: string
  UserTrueName?: string
  RoleCode?: string
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
  roleCode: string
  roleName: string
  orgId?: number
  orgCode?: string
}

/**
 * 用户登录（YZH.Core 新架构接口）
 * 后端 API: POST /api/User/login
 */
export async function login(params: LoginParams): Promise<any> {
  return yzhApi.post('/api/User/login', {
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
export async function getCaptcha(): Promise<any> {
  return yzhApi.get('/api/User/getVierificationCode')
}

/**
 * 获取当前登录用户信息
 */
export async function getCurrentUser(): Promise<any> {
  return yzhApi.get('/api/User/getCurrentUserInfo')
}

/**
 * 健康检查
 */
export async function ping(): Promise<any> {
  return yzhApi.get('/api/User/ping')
}
