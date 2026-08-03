// ============================================================
//  YZH 单表 —— CRUD 增量同步 orchestrator
//  用户 §2 调整：删除 = 把选中行从本页列表移除（不跳页、不补拉、不调额外 HTTP），KISS 原则
//  对外 3 个入口：applyInsert / applyReplace / applyRemove
// ============================================================
import type { IYZHEntitySchema, SortOrder } from '@/types/yzh/YZHEntitySchema'
import { computed, type Ref } from 'vue'
import { YZHRowDiff, insertByOrder, removeByKeys, replaceByKey } from '../YZHRowDiff'

export interface IYZHIncrementSyncOptions<TKey, TEntity> {
  enabled: Ref<boolean>
  schema: IYZHEntitySchema<TKey, TEntity>
  pageRows: Ref<TEntity[]>
  /** 外部提供：当前排序字段（若用户手动点击列头切换了排序，这里应为最新的） */
  currentSortField?: Ref<keyof TEntity & string>
  currentSortOrder?: Ref<SortOrder>
}

export interface IYZHPager {
  page: Ref<number>
  size: Ref<number>
  total: Ref<number>
}

export function useYZHIncrementSync<TKey, TEntity extends object>(
  opts: IYZHIncrementSyncOptions<TKey, TEntity>
) {
  const { enabled, schema, pageRows, currentSortField, currentSortOrder } = opts
  const diff = new YZHRowDiff<TKey, TEntity>(schema)

  /** 上次 setRows 保存的快照：用于判断 rows 是否被 Vol 内部替换，避免 patch 错对象 */
  let lastSnapshot: TEntity[] | null = null

  function setRows(rows: TEntity[], _pager?: IYZHPager) {
    lastSnapshot = rows
    pageRows.value = rows
  }

  function _sortCtx() {
    const sf = (currentSortField?.value as any) || schema.defaultSortField
    const so = (currentSortOrder?.value as any) || schema.defaultSortOrder
    return { sf: sf as keyof TEntity & string, so: so as SortOrder }
  }

  function _isSnapshotFresh() {
    return lastSnapshot != null && Object.is(lastSnapshot, pageRows.value)
  }

  const applyInsert = async (newRow: TEntity, _pager?: IYZHPager) => {
    if (!enabled.value) return { index: -1 }
    if (!_isSnapshotFresh()) return { index: -1 }
    const { sf, so } = _sortCtx()
    const { rows: next, index } = insertByOrder(pageRows.value, newRow, sf, so)
    pageRows.value = next
    lastSnapshot = next
    return { index }
  }

  const applyReplace = async (updatedRow: TEntity) => {
    if (!enabled.value) return { index: -1 }
    if (!_isSnapshotFresh()) return { index: -1 }
    const { rows: next, index } = replaceByKey(pageRows.value, updatedRow, schema.keyField)
    if (index >= 0) {
      pageRows.value = next
      lastSnapshot = next
    }
    return { index }
  }

  /**
   * 删除：仅把选中的行从本页列表移除；
   * pager.total 同步减少 removed 数量；
   * （用户 §2 明确：删空不跳页、不补拉 N 条。保持 KISS）
   */
  const applyRemove = async (deletedKeys: TKey[], pager?: IYZHPager) => {
    if (!enabled.value) return { removed: 0 }
    if (!deletedKeys || !deletedKeys.length) return { removed: 0 }
    if (!_isSnapshotFresh()) return { removed: 0 }
    const { rows: next, removed } = removeByKeys(pageRows.value, deletedKeys, schema.keyField)
    if (removed > 0) {
      pageRows.value = next
      lastSnapshot = next
      if (pager) pager.total.value = Math.max(0, pager.total.value - removed)
    }
    return { removed }
  }

  return {
    enabled: computed(() => enabled.value),
    setRows,
    applyInsert,
    applyReplace,
    applyRemove
  }
}
