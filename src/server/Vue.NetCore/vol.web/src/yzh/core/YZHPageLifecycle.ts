// ============================================================
//  YZH Framework V2.0 —— 生命周期接口 + 默认空钩子工厂 + runGuard
// ============================================================

import type {
  ILifecycleAdd,
  ILifecycleDelete,
  ILifecycleIO,
  ILifecycleLoad,
  ILifecycleRow,
  ILifecycleUpdate,
  IYZHContext,
  IYZHPageLifecycle,
  YZHGuardResult
} from '../types/YZHLifecycles'

export type {
  ILifecycleAdd,
  ILifecycleDelete,
  ILifecycleIO,
  ILifecycleLoad,
  ILifecycleRow,
  ILifecycleUpdate,
  IYZHContext,
  IYZHPageLifecycle,
  YZHGuardResult
}

/** 返回全空的默认生命周期，业务页可以只实现自己关心的几个 */
export function createDefaultLifecycles<TKey, TEntity>(): Partial<
  IYZHPageLifecycle<TKey, TEntity>
> {
  return {
    onLoadBefore: undefined,
    onLoadAfter: undefined,
    onAddBefore: undefined,
    onAddSaveBefore: undefined,
    onAddSaveAfter: undefined,
    onUpdateBefore: undefined,
    onUpdateSaveBefore: undefined,
    onUpdateSaveAfter: undefined,
    onDeleteBefore: undefined,
    onDeleteAfter: undefined,
    onImportBefore: undefined,
    onImportAfter: undefined,
    onExportBefore: undefined,
    onExportAfter: undefined,
    onRowSelect: undefined,
    onRowClick: undefined,
    onRowDbClick: undefined,
    onEditModeChange: undefined
  }
}

/** 安全调用 guard 函数，返回 boolean */
export async function runGuard(
  fn: ((...a: any[]) => any) | undefined,
  args: any[]
): Promise<boolean> {
  if (typeof fn !== 'function') return true
  const r = await fn(...args)
  return r !== false
}
