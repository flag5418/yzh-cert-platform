import { yzhApi, tokenStore } from './client'

/**
 * 认证 API —— **全端唯一实现**（admin / auditor / core 原子登录页共用）
 *
 * 历史：曾存在本文件（裸 fetch、无验证码）与 cert-share 双实现，
 * 2026-09-24 系统底座化迁移 P1 合并——cert-share 版降级为 re-export 或直接指向本文件。
 *
 * - 后端路由：`POST /api/User/login`
 *   （`AuthController` 挂 `[Route("api/User")]` + `[HttpPost("login")]`，**不是** `/api/Auth/login`）
 * - 请求 PascalCase / 响应外层 ApiResponse 为 camelCase（已登记例外 E1）、内层 data 为 PascalCase
 */

// 登录请求参数
export interface LoginParams {
  userName: string
  password: string
  captcha?: string
  uuid?: string
}

// 登录响应（PascalCase，与 C# 属性逐字一致）
export interface LoginResult {
  Token: string
  UserCode: string
  UserName: string
  UserTrueName?: string
  RoleCode?: string
}

// 验证码响应
// ⚠️ 该接口返回**裸匿名对象** `{ img, uuid }`（无 ApiResponse 信封）——已登记例外 E6，
//    camelCase 字段直接在 res 顶层。改字段名需同步后端 UserController.GetVierificationCode。
export interface CaptchaData {
  img: string  // base64 图片
  uuid: string
}

// 当前用户信息（个人中心）—— 字段与 `CurrentUserInfoDto` / `Sys_User` 的 C# 属性名逐字一致
export interface CurrentUser {
  UserCode: string
  UserName: string
  UserTrueName: string
  Gender?: number | null
  PhoneNo?: string | null
  Email?: string | null
  Address?: string | null
  Remark?: string | null
  HeadImageUrl?: string | null
  OrgCode?: string | null
  OrgName?: string | null
  RoleCode?: string | null
  RoleName?: string | null
  CreateTime?: string
  LastLoginDate?: string | null
}

// 修改本人资料请求（全字段可选 —— 只更新显式传入的字段）
export interface UpdateUserInfoParams {
  UserTrueName?: string
  Gender?: number | null
  Remark?: string | null
  HeadImageUrl?: string
  Email?: string
  PhoneNo?: string
}

/**
 * 用户登录
 * 后端 API: POST /api/User/login
 * DEBUG 构建下后端跳过验证码校验，本地可直接登录
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
 * 获取登录验证码
 * 后端 API: GET /api/User/getVierificationCode —— 返回裸对象 `{ img, uuid }`
 */
export async function getCaptcha(): Promise<any> {
  return yzhApi.get('/api/User/getVierificationCode')
}

/**
 * 获取当前登录用户信息（个人中心）
 * 后端 API: POST /api/User/getCurrentUserInfo
 * 登录响应只给 Token/UserCode/UserName/UserTrueName/RoleCode，刷新会丢资料字段，须此接口回填
 */
export async function getCurrentUser(): Promise<any> {
  return yzhApi.post('/api/User/getCurrentUserInfo', {})
}

/**
 * 修改本人资料（个人中心）
 * 后端 API: POST /api/User/updateUserInfo —— 仅更新显式传入的字段
 */
export async function updateUserInfo(params: UpdateUserInfoParams): Promise<any> {
  return yzhApi.post('/api/User/updateUserInfo', params)
}

/**
 * 修改本人密码（自助改密，需校验旧密码）
 * 后端 API: POST /api/User/modifyPwd —— 请求体 PascalCase `{ OldPwd, NewPwd }`
 * 改密成功后后端递增 SSO 版本号 → 本人旧 Token 立即失效，需重新登录
 */
export async function modifyPwd(oldPwd: string, newPwd: string): Promise<any> {
  return yzhApi.post('/api/User/modifyPwd', { OldPwd: oldPwd, NewPwd: newPwd })
}

/** 健康检查 */
export async function ping(): Promise<any> {
  return yzhApi.get('/api/User/ping')
}

/** 登出（清 Token；会话级状态由调用方清 —— 菜单缓存见 useMenuTree.clearMenus） */
export function logout(): void {
  tokenStore.clear()
}

export function isAuthenticated(): boolean {
  return !!tokenStore.get()
}
