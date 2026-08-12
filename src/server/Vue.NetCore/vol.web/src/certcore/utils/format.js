/**
 * certcore 格式化工具
 * 统一 formatFileSize / formatDate，消灭各页面重复实现
 */

/** 文件大小格式化：B/KB/MB/GB */
export function formatFileSize(bytes) {
  if (!bytes || bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

/** 日期格式化：YYYY-MM-DD（含容错） */
export function formatDate(dateStr) {
  if (!dateStr) return '--'
  const s = String(dateStr)
  return s.length >= 10 ? s.substring(0, 10) : s
}

/** 日期时间格式化：YYYY-MM-DD HH:mm */
export function formatDateTime(dateStr) {
  if (!dateStr) return '--'
  const s = String(dateStr)
  if (s.length >= 16) return s.substring(0, 16)
  if (s.length >= 10) return s.substring(0, 10)
  return s
}
