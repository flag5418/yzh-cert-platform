// ============================================================
//  YZH Framework V3.0 —— CRUD 增量同步 orchestrator
//
//  架构原则：
//  - YZH 完全自包含，不依赖 Vol 的数据管理
//  - 数据流：API → incSync → pageRows (Ref) → el-table
//  - 所有操作使用**原地 splice**，直接修改 pageRows.value 数组
//  - Vue 响应式系统自动检测 splice 变化并更新 el-table
//  - 不创建新数组、不替换引用、不使用快照守卫
//
//  适用场景：
//  ✅ 新增 → splice 插入新行到排序正确位置
//  ✅ 修改 → splice 原地替换匹配行（用 String() 比较 key 兼容 proxy）
//  ✅ 删除 → splice 从后往前移除匹配行 + 更新 total
// ============================================================

import type { IYZHEntitySchema, SortOrder } from '../types/YZHEntitySchema'
import { insertByOrder, removeByKeys, replaceByKey } from '../core/YZHRowDiff'
import { computed, type Ref } from 'vue'

export interface IYZHIncrementSyncOptions<TKey, TEntity extends object> {
  enabled: Ref<boolean>
  schema: IYZHEntitySchema<TKey, TEntity>
  pageRows: Ref<TEntity[]>
  currentSortField?: Ref<keyof TEntity & string>
  currentSortOrder?: Ref<SortOrder>
}

export interface IYZHPager {
  /** 当前页码（支持 Ref / reactive / 普通 number） */
  page: Ref<number> | { value: number } | number
  /** 每页大小 */
  size: Ref<number> | { value: number } | number
  /** 总记录数 */
  total: Ref<number> | { value: number } | number
}

/**
 * 增量同步结果
 * affected=false 时调用方应回退到 loadData() 全量刷新
 */
export interface IIncrementResult {
  /** 操作是否实际生效 */
  affected: boolean
  /** 受影响的行数（insert/replace=1, remove=N） */
  count: number
}

export function useYZHIncrementSync<TKey, TEntity extends object>(
  opts: IYZHIncrementSyncOptions<TKey, TEntity>
) {
  const { enabled, schema, pageRows } = opts

  // 排序上下文
  function _sortCtx() {
    const sf = (opts.currentSortField?.value as any) || schema.defaultSortField
    const so = (opts.currentSortOrder?.value as any) || schema.defaultSortOrder
    return { sf: sf as keyof TEntity & string, so: so as SortOrder }
  }

  // ———————————————— setRows（全量加载后调用） ————————————————
  function setRows(rows: TEntity[]) {
    pageRows.value = rows
  }

  // ———————————————— 1. 新增：按排序 splice 插入 ————————————————
  const applyInsert = async (newRow: TEntity): Promise<IIncrementResult> => {
    if (!enabled.value) return { affected: false, count: 0 }

    try {
      const { sf, so } = _sortCtx()
      const { index } = insertByOrder(pageRows.value, newRow, sf, so)
      console.log(`[incSync] applyInsert: 在位置 ${index} splice 插入新行, key=${(newRow as any)[schema.keyField]}`)
      return { affected: true, count: 1 }
    } catch (e: any) {
      console.error('[incSync] applyInsert 异常:', e?.message || e)
      return { affected: false, count: 0 }
    }
  }

  // ———————————————— 2. 修改：按主键 splice 原地替换 ————————————————
  const applyReplace = async (updatedRow: TEntity): Promise<IIncrementResult> => {
    if (!enabled.value) return { affected: false, count: 0 }

    try {
      const { index, replaced } = replaceByKey(pageRows.value, updatedRow, schema.keyField)
      if (replaced) {
        console.log(`[incSync] applyReplace: 在位置 ${index} splice 替换行, key=${(updatedRow as any)[schema.keyField]}`)
        return { affected: true, count: 1 }
      } else {
        console.warn(`[incSync] applyReplace: 未找到 key=${(updatedRow as any)[schema.keyField]} 的行，需全量刷新`)
        return { affected: false, count: 0 }
      }
    } catch (e: any) {
      console.error('[incSync] applyReplace 异常:', e?.message || e)
      return { affected: false, count: 0 }
    }
  }

  // ———————————————— 3. 删除：按主键数组 splice 移除 ————————————————
  const applyRemove = async (deletedKeys: TKey[], pager?: IYZHPager): Promise<IIncrementResult> => {
    if (!enabled.value) return { affected: false, count: 0 }
    if (!deletedKeys?.length) return { affected: false, count: 0 }

    try {
      const { removed } = removeByKeys(pageRows.value, deletedKeys, schema.keyField)

      if (removed > 0) {
        // 更新 total
        if (pager) {
          const currentTotal = typeof pager.total === 'number' ? pager.total : pager.total.value
          const newTotal = Math.max(0, currentTotal - removed)
          if (typeof pager.total === 'number') {
            (pager as any).total = newTotal
          } else {
            pager.total.value = newTotal
          }
        }
        console.log(`[incSync] applyRemove: splice 移除 ${removed} 条, keys=${JSON.stringify(deletedKeys)}, 剩余 ${pageRows.value.length} 条`)
        return { affected: true, count: removed }
      } else {
        console.warn(`[incSync] applyRemove: 当前页未找到匹配行, keys=${JSON.stringify(deletedKeys)}, 当前行数=${pageRows.value?.length}`)
        if (pageRows.value?.length) {
          const existingKeys = pageRows.value.map((r) => `( ${(r as any)[schema.keyField]} )`)
          console.warn(`[incSync] applyRemove: 当前行 keys: [${existingKeys.join(', ')}]`)
          console.warn(`[incSync] applyRemove: 目标删除 keys: [${deletedKeys.join(', ')}]`)
        }
        return { affected: false, count: 0 }
      }
    } catch (e: any) {
      console.error('[incSync] applyRemove 异常:', e?.message || e)
      return { affected: false, count: 0 }
    }
  }

  return {
    enabled: computed(() => enabled.value),
    setRows,
    applyInsert,
    applyReplace,
    applyRemove,
  }
}
