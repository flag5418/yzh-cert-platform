import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { CurrentUser } from '@share/api/auth'

/**
 * 当前登录用户信息
 *
 * ⚠️ 命名铁律（`项目全局规则.md` §16.9 ③′）：
 *    本模型**直接承接** `/api/User/login` 与 `/api/User/getCurrentUserInfo` 的响应 `data`，
 *    字段名必须与后端 `Sys_User` / `CurrentUserInfoDto` 的 C# 属性名逐字一致（PascalCase）。
 *    禁止为了「前端风格」改成 camelCase —— 那会导致读到 `undefined` 且不报错。
 *
 *    唯一非 `Sys_User` 列字段是 `Token`（对应 `Sys_User.Token`，由登录响应下发）。
 */
export type UserInfo = CurrentUser & { Token?: string }

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string>(localStorage.getItem('YZH_TOKEN') || '')
  const userInfo = ref<UserInfo | null>(null)
  const roles = ref<string[]>([])

  const isAuthenticated = () => !!token.value

  function setToken(t: string) {
    token.value = t
    localStorage.setItem('YZH_TOKEN', t)
  }

  function setUserInfo(info: UserInfo) {
    userInfo.value = info
  }

  /** 合并式更新（用于个人中心保存后局部刷新，避免整体覆盖丢失未提交字段） */
  function patchUserInfo(patch: Partial<UserInfo>) {
    userInfo.value = { ...(userInfo.value || {}), ...patch } as UserInfo
  }

  function clearToken() {
    token.value = ''
    userInfo.value = null
    roles.value = []
    localStorage.removeItem('YZH_TOKEN')
  }

  return { token, userInfo, roles, isAuthenticated, setToken, setUserInfo, patchUserInfo, clearToken }
})
