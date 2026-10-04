/**
 * ElMessageBox.confirm 的「取消不是错误」包装（铁律 F-4）
 *
 * 规则：确认 → true；取消 / 关闭 / ESC → false（**不抛异常、不提示**）。
 * 调用方必须写 `if (!ok) return`，不得把 false 当作业务失败。
 *
 * 权威：docs/10-YZH架构/23-前后端信封统一改造计划-V1.md §三 F-4
 */

import { ElMessageBox } from 'element-plus'

/** 用户在确认弹窗上的三态选择（需区分「取消按钮」与「X / Esc」时用） */
export type ConfirmChoice = 'confirm' | 'cancel' | 'close'

/**
 * 三态确认（需区分取消 / 关闭时的唯一写法）
 *
 * @returns 'confirm'=确定；'cancel'=取消按钮；'close'=X / Esc / 点遮罩
 *   —— 三者都不抛异常。
 */
export async function confirmChoice(
  message: string,
  title: string,
  options?: {
    type?: 'warning' | 'info' | 'error' | 'success'
    confirmButtonText?: string
    cancelButtonText?: string
    distinguishCancelAndClose?: boolean
  },
): Promise<ConfirmChoice> {
  try {
    await ElMessageBox.confirm(message, title, {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      ...options,
    })
    return 'confirm'
  } catch (action) {
    // 'cancel' | 'close' | 'prompt' —— 一律是「用户不想做」，不是业务失败
    return action === 'cancel' ? 'cancel' : 'close'
  }
}

/**
 * 二态确认（绝大多数场景的唯一写法）
 *
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
  return (await confirmChoice(message, title, options)) === 'confirm'
}
