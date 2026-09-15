import { yzhApi } from '@yzh-core/api/client'

const API_PREFIX = '/api/file-storage'

/** 上传文件 */
export function uploadFile(
  file: File,
  options?: { objectName?: string; subPath?: string }
): Promise<{ path: string; fileName: string; size: number; contentType: string }> {
  const formData = new FormData()
  formData.append('file', file)
  return yzhApi.post(`${API_PREFIX}/upload`, formData, {
    params: options,
    headers: { 'Content-Type': 'multipart/form-data' },
  })
}

/** 批量上传文件 */
export function uploadFileBatch(
  files: File[],
  options?: { subPath?: string }
): Promise<Array<{ path: string; fileName: string; size: number; success: boolean; error?: string }>> {
  const formData = new FormData()
  files.forEach(f => formData.append('files', f))
  return yzhApi.post(`${API_PREFIX}/upload-batch`, formData, {
    params: options,
    headers: { 'Content-Type': 'multipart/form-data' },
  })
}

/** 下载文件（返回 Blob URL） */
export function getFileUrl(path: string): string {
  const base = yzhApi.baseURL || ''
  const token = localStorage.getItem('token') || ''
  return `${base}${API_PREFIX}/download?path=${encodeURIComponent(path)}&token=${token}`
}

/** 删除文件 */
export function deleteFile(path: string): Promise<void> {
  return yzhApi.post(`${API_PREFIX}/delete`, null, { params: { path } })
}

/** 检查文件是否存在 */
export function fileExists(path: string): Promise<boolean> {
  return yzhApi.get(`${API_PREFIX}/exists`, { path })
}

/** 列出指定前缀下的文件 */
export function listFiles(prefix: string): Promise<string[]> {
  return yzhApi.get(`${API_PREFIX}/list`, { prefix })
}
