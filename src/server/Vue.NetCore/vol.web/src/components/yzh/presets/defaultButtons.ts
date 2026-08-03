// ============================================================
//  YZH 单表默认工具栏按钮 8 个（用户 §1 调整后）
//  · ✎编辑：只控制多选列显示/隐藏
//  · 🗑 删除：常显（与 onlyEditMode 解耦）；无选中时给出提示
// ============================================================
import type { IYZHButtons } from '@/types/yzh/YZHPageProps'

/** 顶部按钮 key 列表（顺序=显示顺序） */
export const DEFAULT_BUTTON_ORDER = [
  'add',
  'refresh',
  'import',
  'export',
  'column',
  'batchDelete',
  'sort'
] as const

export type IYZHButtonKey = (typeof DEFAULT_BUTTON_ORDER)[number]

export interface IYZHButton {
  key: IYZHButtonKey
  label: string
  /** 图标 class（Vol 内置） */
  icon: string
  /** 按钮类型 */
  type?: 'primary' | 'success' | 'warning' | 'danger' | 'default' | 'info'
  /** 按钮大小 */
  size?: 'large' | 'default' | 'small'
  /** 是否仅在编辑模式下可见（用户 §1 后仅剩内部预留，批量删除已解除） */
  onlyEditMode?: boolean
  /** Vol 内部原生指令名（如果是原生就写，自定义留空） */
  nativeCmd?: string
}

const add: IYZHButton = {
  key: 'add',
  label: '新增',
  icon: 'el-icon-plus',
  type: 'primary',
  size: 'small',
  nativeCmd: 'add'
}
const refresh: IYZHButton = {
  key: 'refresh',
  label: '刷新',
  icon: 'el-icon-refresh',
  type: 'default',
  size: 'small',
  nativeCmd: 'search'
}
const importBtn: IYZHButton = {
  key: 'import',
  label: '导入',
  icon: 'el-icon-upload2',
  type: 'default',
  size: 'small',
  nativeCmd: 'import'
}
const exportBtn: IYZHButton = {
  key: 'export',
  label: '导出',
  icon: 'el-icon-download',
  type: 'default',
  size: 'small',
  nativeCmd: 'export'
}
const column: IYZHButton = {
  key: 'column',
  label: '列设置',
  icon: 'el-icon-setting',
  type: 'default',
  size: 'small',
  nativeCmd: 'column'
}
// 用户 §1：批量删除「常显」，去除 onlyEditMode；在分发分支做「是否有选中」的运行时校验
const batchDelete: IYZHButton = {
  key: 'batchDelete',
  label: '删除',
  icon: 'el-icon-delete',
  type: 'danger',
  size: 'small'
}
const sort: IYZHButton = {
  key: 'sort',
  label: '排序',
  icon: 'el-icon-sort',
  type: 'default',
  size: 'small',
  nativeCmd: 'sort'
}

export const DEFAULT_BUTTONS: IYZHButton[] = [
  add,
  refresh,
  importBtn,
  exportBtn,
  column,
  batchDelete,
  sort
]

/**
 * 根据 enabled 布尔表 + 当前编辑模式，过滤出最终要显示的按钮列表
 * @param enabledMap 业务页传入的按钮开关表（不传=全显）
 * @param _editMode  已不再用于过滤 batchDelete（仅作为兼容参数保留）
 */
export function mergeDefaultButtons(
  enabledMap?: Partial<IYZHButtons>,
  _editMode: boolean = false
): IYZHButton[] {
  const map: IYZHButtons = Object.assign(
    {
      add: true,
      refresh: true,
      import: true,
      export: true,
      column: true,
      batchDelete: true,
      sort: true
    },
    enabledMap || {}
  )
  return DEFAULT_BUTTONS.filter((btn) => {
    if (!(map as any)[btn.key]) return false
    return true
  })
}
