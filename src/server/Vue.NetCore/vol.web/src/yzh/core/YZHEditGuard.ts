// ============================================================
//  YZH Framework V2.0 —— 保存 / 删除 前置校验 + 二次确认
// ============================================================

import type { IYZHHttpProxy } from './YZHBaseApiClient'

export class YZHEditGuard {
  constructor(private readonly proxy: IYZHHttpProxy) {}

  async confirmDeleteOne(rowLabel: string | number = ''): Promise<boolean> {
    const msg = rowLabel ? `确认删除「${rowLabel}」？` : '确认删除当前行？'
    return this._confirm(msg)
  }

  async confirmDeleteBatch(count: number): Promise<boolean> {
    if (!count) {
      this.proxy.$message?.warning?.('请先选择要删除的行')
      return false
    }
    return this._confirm(`确认删除所选 ${count} 条数据？此操作不可撤销。`)
  }

  checkRequired(main: any, requiredFields: string[]): string | null {
    const missing: string[] = []
    for (const f of requiredFields) {
      const v = main?.[f]
      if (v === null || v === undefined || v === '') missing.push(f)
    }
    if (!missing.length) return null
    const msg = '以下字段必填：' + missing.join('、')
    this.proxy.$message?.error?.(msg)
    return msg
  }

  private async _confirm(msg: string): Promise<boolean> {
    try {
      if (this.proxy.$message?.confirm) {
        await this.proxy.$message.confirm(msg)
        return true
      }
      return window.confirm(msg)
    } catch (_) {
      return false
    }
  }
}
