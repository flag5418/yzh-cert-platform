/**
 * certcore 下载工具
 * 统一 blob 下载（带 JWT 鉴权，走 http.js），消灭各页面手写 fetch/blob
 */
import http from '@/api/http'

/** 从 Content-Disposition 解析文件名（含 URL 解码） */
export function parseFileNameFromDisposition(disposition, fallback = 'download') {
  if (!disposition) return fallback
  const match = disposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/)
  if (!match) return fallback
  try {
    return decodeURIComponent(match[1].replace(/['"]/g, '')) || fallback
  } catch {
    return match[1].replace(/['"]/g, '') || fallback
  }
}

/** 解析响应中可能携带的下载文件名（兼容 http.js 不返回 headers 的场景） */
export function fileNameOf(file, fallback) {
  return file?.name || file?.FileName || file?.fileName || fallback || 'download'
}

/**
 * 通过 http.js 下载（带 JWT/语言头）
 * @param {string} url 下载地址（如 /api/standard-directory/download?path=xxx）
 * @param {string} fileName 下载文件名
 */
export async function downloadBlob(url, fileName) {
  if (!url) throw new Error('无可用的下载地址')
  const blob = await http.get(url, null, false, { responseType: 'blob' })
  const effective = blob instanceof Blob ? blob : new Blob([blob])
  const a = document.createElement('a')
  a.href = URL.createObjectURL(effective)
  a.download = fileName || 'download'
  document.body.appendChild(a)
  a.click()
  setTimeout(() => {
    document.body.removeChild(a)
    URL.revokeObjectURL(a.href)
  }, 1200)
}

/**
 * 通过 http.js POST 下载（导出类接口，body 为 JSON）
 * @param {string} url 下载地址
 * @param {object} params POST body（JSON）
 * @param {string} fileName 下载文件名
 */
export async function downloadBlobPost(url, params, fileName) {
  if (!url) throw new Error('无可用的下载地址')
  const blob = await http.post(url, params, false, { responseType: 'blob' })
  const effective = blob instanceof Blob ? blob : new Blob([blob])
  const a = document.createElement('a')
  a.href = URL.createObjectURL(effective)
  a.download = fileName || 'download'
  document.body.appendChild(a)
  a.click()
  setTimeout(() => {
    document.body.removeChild(a)
    URL.revokeObjectURL(a.href)
  }, 1200)
}
