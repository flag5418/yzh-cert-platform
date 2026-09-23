import { tokenStore } from './client'

/**
 * 登录响应 data —— 后端 PascalCase（YZH 命名铁律：C# 属性名 = JSON 字段名）
 * 对应 YZH.Core.Web/Controllers/AuthController.cs → LoginResponse
 */
export interface LoginResult {
  Token: string
  UserCode: string
  UserName: string
  UserTrueName?: string
  RoleCode?: string
}

/**
 * 登录 API
 *
 * - 后端路由：`POST /api/User/login`
 *   （`AuthController` 挂 `[Route("api/User")]` + `[HttpPost("login")]`，**不是** `/api/Auth/login`）
 * - 请求体：`{ UserName, Password, Captcha?, Uuid? }` —— PascalCase
 * - 响应体：`{ success, message, data: { Token, UserCode, UserName, UserTrueName, RoleCode }, code, timestamp }`
 *   → 外层 ApiResponse 为 camelCase（已登记例外 E1），内层 data 为 PascalCase
 * - DEBUG 构建下后端跳过验证码校验，本地可直接登录
 *
 * 注：本文件与 `cert-share/src/api/auth.ts` 是同一后端契约的两处实现。
 * 业务页面（cert-admin 登录页）走的是 cert-share 那份；此处保留供框架层/其他端复用。
 */
export async function login(username: string, password: string): Promise<LoginResult> {
  const response = await fetch('/api/User/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ UserName: username, Password: password })
  })

  const data = await response.json()

  if (!response.ok || data?.success === false) {
    throw new Error(data?.message || data?.msg || '登录失败')
  }

  const token = data?.data?.Token
  if (!token) {
    throw new Error('登录响应缺少 Token（请确认读取的是 PascalCase 的 data.Token）')
  }

  tokenStore.set(token)
  return {
    Token: token,
    UserCode: data.data.UserCode,
    UserName: data.data.UserName,
    UserTrueName: data.data.UserTrueName,
    RoleCode: data.data.RoleCode
  }
}

export function logout(): void {
  tokenStore.clear()
}

export function isAuthenticated(): boolean {
  return !!tokenStore.get()
}
