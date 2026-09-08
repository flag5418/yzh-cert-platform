import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface UserInfo {
  token: string
  userId: number
  userCode: string
  userName: string
  userTrueName: string
  roleId: number
  email?: string
  phone?: string
  nickname?: string
}

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

  function clearToken() {
    token.value = ''
    userInfo.value = null
    roles.value = []
    localStorage.removeItem('YZH_TOKEN')
  }

  return { token, userInfo, roles, isAuthenticated, setToken, setUserInfo, clearToken }
})
