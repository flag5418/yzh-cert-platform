/**
 * API 错误边界（决策 D6 / 铁律 F-3）
 *
 * 两条规则，缺一不可：
 * 1. `client.ts` 的 `onError` **不发任何提示**，只做 401 副作用（清 Token 已在 client 内，
 *    这里负责跳登录）。提示一律「谁 catch 谁弹」—— 否则与 catch 方的弹窗形成双弹。
 * 2. 未被任何 catch 接住的 Promise 拒绝（页面 handler 漏 try/catch、异步事件回调）
 *    由 `unhandledrejection` 兜底弹一条，避免「点了没反应」的静默失败。
 *    已被 `reportError` / catch 方处理过的错误带 `handled` 标记，此处**跳过**（防双弹）。
 *
 * 权威：docs/10-YZH架构/23-前后端信封统一改造计划-V1.md §三 F-3 / §二 D6
 */

import { ElMessage } from 'element-plus'
import { configureYzhApi } from './client'

export interface YzhApiErrorBoundaryOptions {
  /** 后端地址（缺省 '' 走 vite proxy / 同源） */
  baseURL?: string
  /** 401 后的跳转动作（缺省 location 跳 /login） */
  onUnauthorized?: () => void
}

let rejectionFallbackInstalled = false

/**
 * 未捕获 Promise 拒绝兜底：一条错误提示（只弹一次，`handled` 标记防双弹）
 */
function installUnhandledRejectionFallback(): void {
  if (rejectionFallbackInstalled || typeof window === 'undefined') return
  rejectionFallbackInstalled = true

  window.addEventListener('unhandledrejection', (event) => {
    const reason = event.reason as (Error & { handled?: boolean }) | undefined | null
    // 只兜「有信息的 Error」（BizError / ApiError）；裸字符串、控制流 reject 不弹
    if (!(reason instanceof Error)) return
    // 已被 catch 方 / reportError 处理过 → 不再重复弹
    if (reason.handled === true) return
    reason.handled = true
    ElMessage.error(reason.message || '操作失败')
  })
}

/**
 * 宿主 main.ts 唯一接入点：配置 API 客户端 + 挂载全局错误兜底。
 *
 * @example cert-admin/src/main.ts
 *   installApiErrorBoundary({
 *     baseURL: (import.meta as any).env?.VITE_API_BASE ?? '',
 *     onUnauthorized: () => router.push('/login'),
 *   })
 */
export function installApiErrorBoundary(options: YzhApiErrorBoundaryOptions = {}): void {
  const goLogin =
    options.onUnauthorized ?? (() => window.location.assign('/login'))

  configureYzhApi({
    ...(options.baseURL !== undefined ? { baseURL: options.baseURL } : {}),
    // D6：onError **只做 401 副作用**（跳登录），不发任何提示。
    // 不覆写 client 的 onUnauthorized（其缺省仅 console.warn），
    // 401 的跳转统一收敛在这一处，避免重复导航。
    onError: (err: Error) => {
      if ((err as any)?.status === 401) goLogin()
    },
  })

  installUnhandledRejectionFallback()
}
