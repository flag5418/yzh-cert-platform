import { readonly, ref } from 'vue'

export type YzhTheme = 'light' | 'dark'

/** 本地缓存键：与管理端 / 专家端共用（同源部署下两端各自读同一键） */
export const YZH_THEME_STORAGE_KEY = 'yzh-theme'

const DEFAULT_THEME: YzhTheme = 'light'

function readStoredTheme(): YzhTheme {
  try {
    const v = localStorage.getItem(YZH_THEME_STORAGE_KEY)
    if (v === 'light' || v === 'dark') return v
  } catch {
    /* 隐私模式 / 存储被禁用：落回默认亮色 */
  }
  return DEFAULT_THEME
}

const theme = ref<YzhTheme>(readStoredTheme())

function applyToDom(next: YzhTheme) {
  if (typeof document === 'undefined') return
  // Element Plus 暗色（theme-chalk/dark/css-vars.css）与 tokens.css 的 html.dark 覆盖层
  // 都挂在这个类上，切换它即整体换肤。
  document.documentElement.classList.toggle('dark', next === 'dark')
}

/** 应用启动时调用一次（main.ts 内、mount 之前），回放缓存主题，避免首屏闪烁 */
export function initTheme() {
  applyToDom(theme.value)
  return theme.value
}

export function setTheme(next: YzhTheme) {
  theme.value = next
  applyToDom(next)
  try {
    localStorage.setItem(YZH_THEME_STORAGE_KEY, next)
  } catch {
    /* 写入失败不阻断切换：本次会话仍生效，仅不持久化 */
  }
}

export function toggleTheme() {
  setTheme(theme.value === 'dark' ? 'light' : 'dark')
}

export function useTheme() {
  return { theme: readonly(theme), setTheme, toggleTheme, initTheme }
}
