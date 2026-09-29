/**
 * 转换状态字典（★ 2026-09-26 随双产物链扩充）
 *
 * 后端状态值来源：
 *  - ConvertStatus（预览 PDF 链）：none/pending/converting/completed/failed
 *  - MarkdownStatus（提取链）：none/pending/converting/completed/failed/unsupported
 *
 * ⚠️ 后端新增状态值必须同步本字典，否则页面会把英文原样显示给用户（静默失败）。
 * `unsupported` = 图片/扫描件等**能力边界**（不是故障）：提示用户手工定义字段、人工填写。
 */
export const CONVERT_STATUS_MAP = {
  none: '未转换',
  pending: '待转换',
  converting: '转换中',
  completed: '已完成',
  failed: '失败',
  unsupported: '需人工填写'
} as const

export type ConvertStatusKey = keyof typeof CONVERT_STATUS_MAP

export function convertStatusInfo(status: string) {
  return CONVERT_STATUS_MAP[status as ConvertStatusKey] || status
}

export function convertStatusBadgeType(status: string) {
  const map = {
    none: 'info',
    pending: 'default',
    converting: 'warning',
    completed: 'success',
    failed: 'error',
    unsupported: 'warning'
  }
  return map[status as keyof typeof map] || 'default'
}

export function convertStatusLabel(status: string) {
  return convertStatusInfo(status)
}

/** 双链状态优先级：失败 > 需人工填写 > 转换中 > 待转换 > 已完成 > 未转换 */
const STATUS_RANK: Record<string, number> = {
  failed: 6,
  unsupported: 5,
  converting: 4,
  pending: 3,
  completed: 2,
  none: 0
}

/**
 * 合并「预览链 + 提取链」两个状态，取更需要用户关注的那个。
 * 用于目录树/列表的单一状态徽标（后端返回的是两个独立状态）。
 */
export function mergeConvertStatus(convertStatus?: string, markdownStatus?: string): string {
  const a = String(convertStatus || '').toLowerCase()
  const b = String(markdownStatus || '').toLowerCase()
  const ra = STATUS_RANK[a] ?? -1
  const rb = STATUS_RANK[b] ?? -1
  return rb > ra ? b : a
}
