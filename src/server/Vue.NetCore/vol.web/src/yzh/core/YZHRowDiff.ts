// ============================================================
//  YZH Framework V3.1 —— 行级 CRUD 增量更新算法
//  纯 TS 函数，不依赖 Vue / Vol，可单独单测
//  目标：新增/修改/删除成功后，直接操作当前页内存 rows 数组
//
//  V3.1 更新：
//  - 新增 safeGetKey() 防御性 key 提取（兼容 Vue proxy / 大小写差异）
//  - replaceByKey / removeByKeys 均使用 safeGetKey 做 key 匹配
// ============================================================

import type { IYZHEntitySchema, SortOrder } from '../types/YZHEntitySchema'

// ———————————————— 0. 防御性 key 提取 ————————————————
/**
 * 从一行数据中安全提取主键值
 * 兼容以下场景：
 *  1. Vue Proxy 包装导致直接属性访问异常 → 用 Object.keys() 遍历
 *  2. 大小写不匹配（后端返回 code vs 前端期望 Code）→ 忽略大小写
 *  3. 字段名完全不同（如用 Id 替代 Code）→ 按优先级尝试多个候选字段
 */
export function safeGetKey(row: any, keyField: string): string | undefined {
  if (!row) return undefined

  // 方式 1：直接访问（最快路径）
  const directVal = row[keyField]
  if (directVal !== undefined && directVal !== null) {
    return String(directVal)
  }

  // 方式 2：大小写不敏感匹配（Vue proxy 可能改变属性名大小写）
  const targetLower = keyField.toLowerCase()
  const keys = Object.keys(row)
  for (const k of keys) {
    if (k.toLowerCase() === targetLower && row[k] !== undefined && row[k] !== null) {
      return String(row[k])
    }
  }

  // 方式 3：常见候选字段（Id / id 作为兜底）
  // 当 Code 不存在时，有些场景会用 Id 作为 fallback
  for (const candidate of ['id', 'ID', 'Id', 'code', 'Code']) {
    if (candidate.toLowerCase() === targetLower) continue // 已经试过了
    for (const k of keys) {
      if (k.toLowerCase() === candidate.toLowerCase() && row[k] !== undefined && row[k] !== null) {
        // 只在明确有值时才使用候选字段
        return String(row[k])
      }
    }
  }

  return undefined
}

// ———————————————— 1. 修改：按主键原地替换（splice） ————————————————
// 直接在原数组上操作，不创建新数组
// Vue 响应式能检测到 splice 导致的数组变化
export function replaceByKey<TKey, TEntity extends object>(
  rows: TEntity[],
  updatedRow: TEntity,
  keyField: keyof TEntity & string
): { index: number; replaced: boolean } {
  const targetKey = safeGetKey(updatedRow as any, keyField)
  if (!targetKey || targetKey === 'undefined' || targetKey === 'null') {
    console.warn('[YZHRowDiff] replaceByKey: 目标行的 keyField="' + keyField + '" 值为空:', updatedRow)
    return { index: -1, replaced: false }
  }

  let idx = -1
  for (let i = 0; i < rows.length; i++) {
    const rowKey = safeGetKey(rows[i] as any, keyField)
    if (rowKey === targetKey) {
      idx = i
      break
    }
  }
  if (idx < 0) {
    console.warn(`[YZHRowDiff] replaceByKey: 未找到 key="${targetKey}" 的行(共${rows.length}行), keyField="${keyField}"`)
    // 打印当前所有行的 key 值便于调试
    if (rows.length <= 5) {
      rows.forEach((r, i) => console.warn(`  [${i}] key=${safeGetKey(r as any, keyField)}, rawKeys=${Object.keys(r).filter(k => k.length < 10).join(',')}`))
    }
    return { index: -1, replaced: false }
  }
  // 原地 splice 替换：Vue 响应式系统会检测到变化
  rows.splice(idx, 1, updatedRow)
  return { index: idx, replaced: true }
}

// ———————————————— 2. 删除：按主键数组原地移除（splice） ————————————————
// 直接在原数组上操作，从后往前 splice 避免索引偏移
export function removeByKeys<TKey, TEntity extends object>(
  rows: TEntity[],
  deletedKeys: TKey[],
  keyField: keyof TEntity & string
): { removed: number } {
  // 🔧 强制转为 string，防止 undefined 导致 toLowerCase 报错
  const kf = String(keyField || '')
  console.log(`[YZHRowDiff] 🗑️ removeByKeys 调用: keyField="${kf}", deletedKeys=`, deletedKeys, `, rows.length=${rows?.length}`)
  
  if (!deletedKeys || !deletedKeys.length) return { removed: 0 }
  const keySet = new Set((deletedKeys as any[]).map(k => String(k)))
  console.log(`[YZHRowDiff] 🗑️ keySet=[${[...keySet].join(',')}], typeof keyField=${typeof keyField}`)
  
  let removed = 0
  // 从后往前遍历，splice 不会影响未处理元素的索引
  for (let i = rows.length - 1; i >= 0; i--) {
    const rowKey = safeGetKey(rows[i] as any, kf)
    if (rowKey && keySet.has(rowKey)) {
      rows.splice(i, 1)
      removed++
    }
  }
  if (removed === 0 && rows.length > 0) {
    console.warn(`[YZHRowDiff] removeByKeys: 0 行被删除! 目标keys=[${[...keySet].join(',')}], keyField="${kf}"`)
    // 打印当前所有行的 key 值便于调试
    if (rows.length <= 5) {
      rows.forEach((r, i) => console.warn(`  [${i}] key=${safeGetKey(r as any, kf)}, rawKeys=${Object.keys(r).filter(k => k.length < 10).join(',')}`))
    }
  }
  console.log(`[YZHRowDiff] ✅ removeByKeys 完成: removed=${removed}, 剩余rows=${rows.length}`)
  return { removed }
}

// ———————————————— 3. 新增：按当前排序原地插入正确位置（splice） ————————————————
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
): { index: number } {
  let i = 0
  for (; i < rows.length; i++) {
    if (cmpAny((newRow as any)[sortField], (rows[i] as any)[sortField], sortOrder) <= 0) break
  }
  // 原地 splice 插入
  rows.splice(i, 0, newRow)
  return { index: i }
}

// ———————————————— 4. 包装类 ————————————————
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
