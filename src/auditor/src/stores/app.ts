import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

export type ThemeMode = 'light' | 'dark'

const STORAGE_KEY = 'auditor-theme-mode'

function loadThemeMode(): ThemeMode {
  const saved = localStorage.getItem(STORAGE_KEY)
  return saved === 'dark' ? 'dark' : 'light'
}

/**
 * 应用级 Store：主题模式（light/dark），持久化到 localStorage
 */
export const useAppStore = defineStore('app', () => {
  const themeMode = ref<ThemeMode>(loadThemeMode())

  watch(
    themeMode,
    (mode) => {
      localStorage.setItem(STORAGE_KEY, mode)
      document.documentElement.setAttribute('data-theme', mode)
    },
    { immediate: true }
  )

  function toggleTheme(): void {
    themeMode.value = themeMode.value === 'light' ? 'dark' : 'light'
  }

  function setThemeMode(mode: ThemeMode): void {
    themeMode.value = mode
  }

  return { themeMode, toggleTheme, setThemeMode }
})
