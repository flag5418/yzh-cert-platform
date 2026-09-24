import { ref } from 'vue'
import { tokenStore } from '../api/client'
import type { CurrentUser } from '../api/auth'

/**
 * 登录态 —— 模块级单例（admin / auditor 宿主 pinia 薄适配的唯一真源）
 *
 * ⚠️ 命名铁律（`项目全局规则.md` §16.9 ③′）：
 *    `UserInfo` 直接承接 `/api/User/login` 与 `/api/User/getCurrentUserInfo` 的响应 `data`，
 *    字段名必须与后端 `Sys_User` / `CurrentUserInfoDto` 的 C# 属性名逐字一致（PascalCase）。
 *
 * ⚠️ Token 持久化走 `tokenStore`（key `YZH_TOKEN`），与 `yzhApi` 自动注入的 getToken 同源 ——
 *    双写不会分叉；退出登录必须 `clearToken()`。
 *
 * 状态是**模块级单例**：同一应用内所有调用方（含 core 原子登录页与宿主 store）共享一份。
 */

export type UserInfo = CurrentUser & { Token?: string }

const token = ref<string>(tokenStore.get() || '')
const userInfo = ref<UserInfo | null>(null)
const roles = ref<string[]>([])

const isAuthenticated = () => !!token.value

function setToken(t: string) {
  token.value = t
  tokenStore.set(t)
}

function setUserInfo(info: UserInfo) {
  userInfo.value = info
}

/** 合并式更新（个人中心保存后局部刷新，避免整体覆盖丢失未提交字段） */
function patchUserInfo(patch: Partial<UserInfo>) {
  userInfo.value = { ...(userInfo.value || {}), ...patch } as UserInfo
}

function setRoles(list: string[]) {
  roles.value = list
}

function clearToken() {
  token.value = ''
  userInfo.value = null
  roles.value = []
  tokenStore.clear()
}

export function useAuthState() {
  return {
    token,
    userInfo,
    roles,
    isAuthenticated,
    setToken,
    setUserInfo,
    patchUserInfo,
    setRoles,
    clearToken,
  }
}
