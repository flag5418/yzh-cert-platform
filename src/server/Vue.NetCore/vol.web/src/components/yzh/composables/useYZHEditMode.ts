// ============================================================
//  YZH 单表 —— 编辑模式 + 多选 状态机
//  UX 规则（用户 §1 调整后）：
//    · ✎编辑 按钮：只控制表格 showCheckbox（多选列显示/隐藏），退出时自动清空选中
//    · 🗑 删除 按钮：常显（与编辑模式解耦）；既支持编辑模式下的批量多选删除，
//      也支持全局 rowClick.selected 单选中的直接删除
// ============================================================
import type { IYZHEntitySchema } from '@/types/yzh/YZHEntitySchema'
import { ref, type Ref } from 'vue'

export interface IUseYZHEditModeOptions<TEntity = any> {
  /** 外部回调：编辑模式切换 */
  onEditModeChange?: (editing: boolean) => void
  /** 外部回调：选中行变化 */
  onSelectChange?: (rows: TEntity[]) => void
}

export function useYZHEditMode<TKey, TEntity extends object>(
  schema: IYZHEntitySchema<TKey, TEntity>,
  options: IUseYZHEditModeOptions<TEntity> = {}
) {
  const editMode: Ref<boolean> = ref(false)
  const selectedRows: Ref<TEntity[]> = ref([])
  const singleSelectedRow: Ref<TEntity | null> = ref(null)

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
    if (editMode.value) exitEditMode()
    else enterEditMode()
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

  /** 最真实的「当前选中」数据源（优先读 Vol 原生 getSelected()，避免我们自己维护两套 state 不同步：
   *     1. Vol Table 自己的 selection 勾选框（用户点全选/行勾选）
   *     2. 我们自己的 editMode.selectedRows / singleSelectedRow
   *   不一致时（用户勾了复选框但 setSelectedRows 没同步）就会出现「选了行但删不了」的 Bug。
   *   读取优先级：
   *     ① volSelected（不为空时，说明用户用复选框勾了）
   *     ② editMode 多选（用户显式点进入编辑模式 + 多选）
   *     ③ singleSelectedRow（用户点击行高亮选中的单选）
   */
  const _getterProvider: { getVolSelected?: () => any[] } = {}
  function _volSelected(): any[] {
    try {
      if (typeof _getterProvider.getVolSelected === 'function') {
        const r = _getterProvider.getVolSelected()
        if (Array.isArray(r) && r.length) return r
      }
    } catch (_) {}
    return []
  }
  /** 给外部（YzhBaseSingleTable）设置 Vol 原生选中行的读取函数；
   *  因 composables 早于 gridVM 创建，用「注册 getter」而非 gridVM 传参避免循环依赖 */
  function registerVolSelectionGetter(fn: () => any[]) {
    _getterProvider.getVolSelected = fn
  }

  /** 有效选中的主键数组：Vol 原生勾选 > 编辑模式多选 > 点击行单选（三者有任一即视为选中） */
  function selectedKeys(): TKey[] {
    const vs = _volSelected()
    const list = vs.length
      ? vs
      : editMode.value && selectedRows.value.length
        ? selectedRows.value
        : singleSelectedRow.value
          ? [singleSelectedRow.value]
          : []
    return list.map((r) => (r as any)[schema.keyField]).filter((v) => v !== undefined && v !== null)
  }
  /** 有效选中的行对象（同上优先级） */
  function selectedRowObjects(): TEntity[] {
    const vs = _volSelected()
    if (vs.length) return vs.slice()
    if (editMode.value && selectedRows.value.length) return selectedRows.value.slice()
    return singleSelectedRow.value ? [singleSelectedRow.value] : []
  }
  /** 是否存在可删除的选中（勾选/多选/单选任一有数据即可删，不再要求进入编辑模式） */
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
    registerVolSelectionGetter,
  }
}
