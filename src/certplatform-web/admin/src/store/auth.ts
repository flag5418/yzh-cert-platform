import { defineStore } from 'pinia'
import { ref } from 'vue'

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string>(localStorage.getItem('YZH_TOKEN') || '')
  const userInfo = ref<any>(null)
  const roles = ref<string[]>([])

  const isAuthenticated = () => !!token.value

  function setToken(t: string) {
    token.value = t
    localStorage.setItem('YZH_TOKEN', t)
  }

  function clearToken() {
    token.value = ''
    userInfo.value = null
    roles.value = []
    localStorage.removeItem('YZH_TOKEN')
  }

  return { token, userInfo, roles, isAuthenticated, setToken, clearToken }
})
