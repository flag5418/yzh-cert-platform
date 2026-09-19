import { tokenStore } from './client'

export interface LoginResult {
  token: string
  userName: string
  userTrueName?: string
  roleCode?: string
}

/**
 * 登录 API
 * 后端: POST /api/Auth/login
 * 请求: { userName: string, password: string }
 * 响应: { success: true, data: { token, userName, userTrueName, roleCode }, message: "登录成功" }
 */
export async function login(username: string, password: string): Promise<LoginResult> {
  const response = await fetch('/api/Auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ UserName: username, Password: password })
  })

  const data = await response.json()

  if (!response.ok || data?.success === false) {
    throw new Error(data?.message || data?.msg || '登录失败')
  }

  const token = data?.data?.token
  if (!token) {
    throw new Error('登录响应缺少 token')
  }

  tokenStore.set(token)
  return {
    token,
    userName: data.data.userName,
    userTrueName: data.data.userTrueName,
    roleCode: data.data.roleCode
  }
}

export function logout(): void {
  tokenStore.clear()
}

export function isAuthenticated(): boolean {
  return !!tokenStore.get()
}
