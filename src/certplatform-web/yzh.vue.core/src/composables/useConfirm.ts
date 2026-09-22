/**
 * useConfirm - 通用确认弹窗（C-G1）
 *
 * ElMessageBox.confirm 的轻封装：
 * - 文案由调用方传入，组件/工具层不内置业务语义
 * - resolve(true) 确认 / resolve(false) 取消，调用方无需 try/catch
 *
 * 用法：
 *   const { confirm } = useConfirm()
 *   if (await confirm({ message: `确定删除【${name}】？`, title: '删除确认' })) { ... }
 */
import { ElMessageBox } from 'element-plus'

export interface ConfirmOptions {
  message: string
  title?: string
  confirmButtonText?: string
  cancelButtonText?: string
  type?: 'warning' | 'info' | 'success' | 'error'
}

export function useConfirm() {
  async function confirm(options: ConfirmOptions): Promise<boolean> {
    try {
      await ElMessageBox.confirm(options.message, options.title ?? '操作确认', {
        type: options.type ?? 'warning',
        confirmButtonText: options.confirmButtonText ?? '确定',
        cancelButtonText: options.cancelButtonText ?? '取消',
      })
      return true
    } catch {
      return false
    }
  }

  return { confirm }
}
