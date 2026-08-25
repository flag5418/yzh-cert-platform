import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface UserInfo {
  userId?: string | number
  userName?: string
  realName?: string
  avatar?: string
}

const TOKEN_KEY = 'token'

/**
 * 用户 Store：token + 用户信息，token 与 localStorage 双向同步
 */
export const useUserStore = defineStore('user', () => {
  const token = ref<string>(localStorage.getItem(TOKEN_KEY) || '')
  const userInfo = ref<UserInfo>({})

  function setToken(value: string): void {
    token.value = value
    localStorage.setItem(TOKEN_KEY, value)
  }

  function setUserInfo(info: UserInfo): void {
    userInfo.value = info
  }

  function logout(): void {
    token.value = ''
    userInfo.value = {}
    localStorage.removeItem(TOKEN_KEY)
  }

  return { token, userInfo, setToken, setUserInfo, logout }
})
