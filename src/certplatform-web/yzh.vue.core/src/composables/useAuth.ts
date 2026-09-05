import { ref, computed } from 'vue'
import { tokenStore } from '../api/client'

export function useAuth() {
  const token = ref<string>(tokenStore.get() || '')
  const userInfo = ref<any>(null)

  const isAuthenticated = computed(() => !!token.value)

  function setToken(newToken: string) {
    token.value = newToken
    tokenStore.set(newToken)
  }

  function clearToken() {
    token.value = ''
    userInfo.value = null
    tokenStore.clear()
  }

  function login(username: string, password: string) {
    // 实际调用会在页面层实现
    return Promise.resolve()
  }

  function logout() {
    clearToken()
  }

  return {
    token,
    userInfo,
    isAuthenticated,
    setToken,
    clearToken,
    login,
    logout
  }
}
