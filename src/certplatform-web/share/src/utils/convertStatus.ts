export const CONVERT_STATUS_MAP = {
  pending: '待转换',
  converting: '转换中',
  completed: '已完成',
  failed: '失败'
} as const

export function convertStatusInfo(status: string) {
  return CONVERT_STATUS_MAP[status as keyof typeof CONVERT_STATUS_MAP] || status
}

export function convertStatusBadgeType(status: string) {
  const map = { pending: 'default', converting: 'warning', completed: 'success', failed: 'error' }
  return map[status as keyof typeof map] || 'default'
}

export function convertStatusLabel(status: string) {
  return convertStatusInfo(status)
}
