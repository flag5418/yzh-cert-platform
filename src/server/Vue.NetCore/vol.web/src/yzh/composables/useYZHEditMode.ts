// ============================================================
//  YZH Framework V2.0 —— 编辑模式 + 多选 状态机
//  纯 Vue 3 composable，不依赖 Vol
// ============================================================

import type { IYZHEntitySchema } from '../types/YZHEntitySchema'
import { ref, type Ref } from 'vue'

export interface IUseYZHEditModeOptions<TEntity = any> {
  onEditModeChange?: (editing: boolean) => void
  onSelectChange?: (rows: TEntity[]) => void
}

export function useYZHEditMode<TKey, TEntity extends object>(
  schema: IYZHEntitySchema<TKey, TEntity>,
  options: IUseYZHEditModeOptions<TEntity> = {}
) {
  const editMode = ref(false)
  const selectedRows = ref<TEntity[]>([])
  const singleSelectedRow = ref<TEntity | null>(null)

  function enterEditMode() {
    editMode.value = true
    options.onEditModeChange?.(true)
  }
  function exitEditMode() {
    editMode.value = false
    selectedRows.value = []
    options.onEditModeChange?.(false)
  }
  function toggleEditMode() {
    editMode.value ? exitEditMode() : enterEditMode()
  }
  function setSelectedRows(rows: TEntity[]) {
    selectedRows.value = rows || []
    options.onSelectChange?.(selectedRows.value)
  }
  function setSingleSelected(row: TEntity | null) {
    singleSelectedRow.value = row
  }
  function clearSelected() {
    selectedRows.value = []
    singleSelectedRow.value = null
    options.onSelectChange?.([])
  }

  /** 有效选中的主键数组 */
  function selectedKeys(): TKey[] {
    const list = selectedRows.value.length
      ? selectedRows.value
      : singleSelectedRow.value
        ? [singleSelectedRow.value]
        : []
    return list.map((r) => (r as any)[schema.keyField]).filter((v) => v !== undefined && v !== null)
  }

  /** 有效选中的行对象 */
  function selectedRowObjects(): TEntity[] {
    if (selectedRows.value.length) return selectedRows.value.slice()
    return singleSelectedRow.value ? [singleSelectedRow.value] : []
  }

  function hasSelection(): boolean {
    return selectedKeys().length > 0
  }

  return {
    editMode,
    selectedRows,
    singleSelectedRow,
    enterEditMode,
    exitEditMode,
    toggleEditMode,
    setSelectedRows,
    setSingleSelected,
    clearSelected,
    selectedKeys,
    selectedRowObjects,
    hasSelection,
  }
}
