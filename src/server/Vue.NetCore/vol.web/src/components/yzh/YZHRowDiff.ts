// ============================================================
//  YZH 前端框架 —— 行级 CRUD 增量更新算法
//  参考：YZH 方案 V1.0 §5.0 ✅ CRUD 增量刷新
//  目标：新增 / 修改 / 删除成功后，不再调用 gridRef.search() 全表 reload，
//        而是直接操作当前页内存 rows 数组，响应速度从 300~1200ms → <50ms
//  设计：纯 TS 函数，不依赖 Vue / Vol，可单独单测。
// ============================================================
import type { IYZHEntitySchema, SortOrder } from '@/types/yzh/YZHEntitySchema'

// ———————————————— 1. 修改：按主键替换 ————————————————
export function replaceByKey<TKey, TEntity extends object>(
  rows: TEntity[],
  updatedRow: TEntity,
  keyField: keyof TEntity & string
): { rows: TEntity[]; index: number } {
  const idx = rows.findIndex((r) => (r as any)[keyField] === (updatedRow as any)[keyField])
  if (idx < 0) return { rows, index: -1 }
  const next = rows.slice()
  next.splice(idx, 1, updatedRow)
  return { rows: next, index: idx }
}

// ———————————————— 2. 删除：按主键数组移除 ————————————————
export function removeByKeys<TKey, TEntity extends object>(
  rows: TEntity[],
  deletedKeys: TKey[],
  keyField: keyof TEntity & string
): { rows: TEntity[]; removed: number } {
  if (!deletedKeys || !deletedKeys.length) return { rows, removed: 0 }
  const set = new Set(deletedKeys as any[])
  const next: TEntity[] = []
  let removed = 0
  for (const r of rows) {
    if (set.has((r as any)[keyField])) removed++
    else next.push(r)
  }
  return { rows: next, removed }
}

// ———————————————— 3. 新增：按当前排序插入正确位置 ————————————————
function cmpAny(a: any, b: any, order: SortOrder): number {
  const av = a ?? null
  const bv = b ?? null
  if (av === bv) return 0
  if (av === null) return order === 'asc' ? -1 : 1
  if (bv === null) return order === 'asc' ? 1 : -1
  let c = 0
  if (av instanceof Date && bv instanceof Date) c = av.getTime() - bv.getTime()
  else if (typeof av === 'number' && typeof bv === 'number') c = av - bv
  else
    c = String(av).localeCompare(String(bv), 'zh-Hans-CN', { numeric: true, sensitivity: 'base' })
  return order === 'asc' ? c : -c
}

export function insertByOrder<TEntity extends object>(
  rows: TEntity[],
  newRow: TEntity,
  sortField: keyof TEntity & string,
  sortOrder: SortOrder
): { rows: TEntity[]; index: number } {
  const next = rows.slice()
  let i = 0
  for (; i < next.length; i++) {
    if (cmpAny((newRow as any)[sortField], (next[i] as any)[sortField], sortOrder) <= 0) break
  }
  next.splice(i, 0, newRow)
  return { rows: next, index: i }
}

// ———————————————— 4. 包装：泛型 Schema 类，省得业务方传 keyField / sort ————————————————
export class YZHRowDiff<TKey, TEntity extends object> {
  constructor(private readonly schema: IYZHEntitySchema<TKey, TEntity>) {}
  insert(rows: TEntity[], newRow: TEntity) {
    return insertByOrder(rows, newRow, this.schema.defaultSortField, this.schema.defaultSortOrder)
  }
  replace(rows: TEntity[], updatedRow: TEntity) {
    return replaceByKey(rows, updatedRow, this.schema.keyField)
  }
  remove(rows: TEntity[], deletedKeys: TKey[]) {
    return removeByKeys(rows, deletedKeys, this.schema.keyField)
  }
}
