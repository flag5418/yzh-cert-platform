/**
 * certcore 转换状态映射
 * 后端 ConvertStatus 取值：pending / converting / converted / failed
 * 统一图标/颜色/文案，消灭 FileTree 与 DirectoryManager 两套实现
 */

export const CONVERT_STATUS_MAP = {
  pending: { label: '等待转换', badge: 'info' },
  converting: { label: '转换中', badge: 'warning' },
  converted: { label: '已转换', badge: 'success' },
  failed: { label: '转换失败', badge: 'danger' }
}

/** 取转换状态信息（未匹配返回 null） */
export function convertStatusInfo(status) {
  return CONVERT_STATUS_MAP[status] || null
}

/** 转换状态 → YzhStatusBadge 的 type */
export function convertStatusBadgeType(status) {
  return CONVERT_STATUS_MAP[status]?.badge || 'info'
}

/** 转换状态 → 文案 */
export function convertStatusLabel(status) {
  return CONVERT_STATUS_MAP[status]?.label || '—'
}
