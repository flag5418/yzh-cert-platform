// ============================================================
//  YZH Framework V2.0 —— 默认工具栏按钮
//  基于 Element Plus el-button，不依赖 Vol
// ============================================================

import type { IYZHButtons } from '../types/YZHPageProps'

/** 按钮定义 */
export interface IYZHButton {
  key: string
  label: string
  /** Element Plus icon */
  icon: string
  type?: 'primary' | 'success' | 'warning' | 'danger' | 'default' | 'info'
  size?: 'large' | 'default' | 'small'
}

export const DEFAULT_BUTTONS: IYZHButton[] = [
  { key: 'add', label: '新增', icon: 'Plus', type: 'primary', size: 'small' },
  { key: 'refresh', label: '刷新', icon: 'Refresh', type: 'default', size: 'small' },
  { key: 'import', label: '导入', icon: 'Upload', type: 'default', size: 'small' },
  { key: 'export', label: '导出', icon: 'Download', type: 'default', size: 'small' },
  { key: 'batchDelete', label: '删除', icon: 'Delete', type: 'danger', size: 'small' },
]

/**
 * 根据开关表过滤显示的按钮
 */
export function mergeDefaultButtons(
  enabledMap?: Partial<IYZHButtons>
): IYZHButton[] {
  const map: IYZHButtons = Object.assign(
    {
      add: true,
      refresh: true,
      import: true,
      export: true,
      batchDelete: true,
    },
    enabledMap || {}
  )
  return DEFAULT_BUTTONS.filter((btn) => (map as any)[btn.key] !== false)
}
