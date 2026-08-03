// ============================================================
//  YZH 前端框架 —— 保存 / 删除 前置校验 + 二次确认
//  作用：把「业务页面通用的防呆逻辑」从业务 Vue 中抽到基类层，
//        业务页只在 lifecycles 里声明额外约束即可。
// ============================================================
import type { IYZHHttpProxy } from './YZHBaseApiClient';

export class YZHEditGuard {
  constructor(private readonly proxy: IYZHHttpProxy) {}

  // —— 行级删除：默认二次确认 ——
  async confirmDeleteOne(rowLabel: string | number = ''): Promise<boolean> {
    const msg = rowLabel ? `确认删除「${rowLabel}」？` : '确认删除当前行？';
    return this._confirm(msg);
  }

  // —— 批量删除：默认二次确认 + 数量 ——
  async confirmDeleteBatch(count: number): Promise<boolean> {
    if (!count) {
      this.proxy.$message?.warning?.('请先选择要删除的行');
      return false;
    }
    return this._confirm(`确认删除所选 ${count} 条数据？此操作不可撤销。`);
  }

  // —— 新增 / 修改 字段必填快速校验（配合 Vol form 表单自身校验使用）——
  checkRequired(main: any, requiredFields: string[]): string | null {
    const missing: string[] = [];
    for (const f of requiredFields) {
      const v = main?.[f];
      if (v === null || v === undefined || v === '') missing.push(f);
    }
    if (!missing.length) return null;
    const msg = '以下字段必填：' + missing.join('、');
    this.proxy.$message?.error?.(msg);
    return msg;
  }

  private async _confirm(msg: string): Promise<boolean> {
    try {
      if (this.proxy.$message?.confirm) {
        await this.proxy.$message.confirm(msg);
        return true;
      }
      return window.confirm(msg);
    } catch (_) {
      return false;
    }
  }
}
