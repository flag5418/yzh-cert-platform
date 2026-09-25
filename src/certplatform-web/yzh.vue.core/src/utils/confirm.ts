/**
 * ElMessageBox.confirm 的「取消不是错误」包装（铁律 F-4）
 *
 * 规则：确认 → true；取消 / 关闭 / ESC → false（**不抛异常、不提示**）。
 * 调用方必须写 `if (!ok) return`，不得把 false 当作业务失败。
 *
 * 权威：docs/10-YZH架构/23-前后端信封统一改造计划-V1.md §三 F-4
 */

import { ElMessageBox } from 'element-plus'

/**
 * @returns true=用户点确定；false=用户取消/关闭（静默）
 */
export async function confirmOrFalse(
  message: string,
  title: string,
  options?: {
    type?: 'warning' | 'info' | 'error' | 'success'
    confirmButtonText?: string
    cancelButtonText?: string
    distinguishCancelAndClose?: boolean
  },
): Promise<boolean> {
  try {
    await ElMessageBox.confirm(message, title, {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      ...options,
    })
    return true
  } catch {
    // 'cancel' | 'close' | 'prompt' —— 一律视为「用户不想做」，静默
    return false
  }
}
