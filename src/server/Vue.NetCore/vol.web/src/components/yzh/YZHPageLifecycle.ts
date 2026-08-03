// ============================================================
//  YZH 前端框架 —— 生命周期接口（与 types/yzh 对齐，直接重导出 + 业务默认实现）
//  作用：在 components/yzh 域内提供一份可直接 import 的类型 + 空钩子工厂
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
} from '@/types/yzh/YZHLifecycles'

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

/** 返回 13+ 全空的默认生命周期，业务页可以只实现自己关心的几个 */
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

/** 安全调用一个可能是布尔或 Promise<boolean> 或 void 的 guard，返回 boolean */
export async function runGuard(
  fn: ((...a: any[]) => any) | undefined,
  args: any[]
): Promise<boolean> {
  if (typeof fn !== 'function') return true
  const r = await fn(...args)
  return r !== false
}
