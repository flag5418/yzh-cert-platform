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
// ⚠️ 该接口返回的是**裸匿名对象**（`new { img, uuid }`），**没有** ApiResponse 信封，
//    所以字段直接在 res 顶层，且是 camelCase —— 属 §16.9 例外清单之外的第 6 类：
//    「未包信封的裸 JSON 响应」。改字段名需同步后端 UserController.GetVierificationCode。
export interface CaptchaData {
  img: string  // base64 图片
  uuid: string
}

// 当前用户信息（个人中心）
// 字段名与后端 `CurrentUserInfoDto` / `Sys_User` 的 C# 属性名逐字一致（§16.9 铁律）
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
  Remark?: string
  HeadImageUrl?: string
  Email?: string
  PhoneNo?: string
}

/**
 * 用户登录（YZH.Core 新架构接口）
 * 后端 API: POST /api/User/login
 * 请求 PascalCase / 响应 data 亦为 PascalCase（外层 ApiResponse 为 camelCase）
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
 * 返回裸对象 `{ img, uuid }`（无 ApiResponse 信封）
 */
export async function getCaptcha(): Promise<any> {
  return yzhApi.get('/api/User/getVierificationCode')
}

/**
 * 获取当前登录用户信息（个人中心）
 * 后端 API: POST /api/User/getCurrentUserInfo（Vol 兼容路由，同时接受 GET）
 * 响应：ApiResponse 信封，`data` 为 PascalCase 的 CurrentUser
 *
 * 用途：页面刷新后回填「个人中心」与顶栏用户名 —— 登录响应只给
 * Token/UserCode/UserName/UserTrueName/RoleCode，不含 Email/PhoneNo/Gender 等资料字段，
 * 且未持久化，刷新即丢。
 */
export async function getCurrentUser(): Promise<any> {
  return yzhApi.post('/api/User/getCurrentUserInfo', {})
}

/**
 * 修改本人资料（个人中心）
 * 后端 API: POST /api/User/updateUserInfo
 * 请求体 PascalCase；仅更新显式传入的字段，未传字段保持库中原值。
 */
export async function updateUserInfo(params: UpdateUserInfoParams): Promise<any> {
  return yzhApi.post('/api/User/updateUserInfo', params)
}

/**
 * 修改本人密码（自助改密，需校验旧密码）
 * 后端 API: POST /api/User/modifyPwd
 * 请求体 PascalCase：`{ OldPwd, NewPwd }`
 * ⚠️ 历史项目用的是 camelCase `oldPwd`/`newPwd`；新架构统一 PascalCase（§16.9 铁律）。
 * 改密成功后后端会递增 SSO 版本号 → 本人旧 Token 立即失效，需重新登录。
 */
export async function modifyPwd(oldPwd: string, newPwd: string): Promise<any> {
  return yzhApi.post('/api/User/modifyPwd', { OldPwd: oldPwd, NewPwd: newPwd })
}

/**
 * 健康检查
 */
export async function ping(): Promise<any> {
  return yzhApi.get('/api/User/ping')
}
