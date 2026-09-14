import { yzhApi, tokenStore } from '@yzh-core/api/client'
import type { ApiResponse } from '@yzh-core/types'
import type {
  StandardDirectoryConfig,
  StandardDirectoryFolder,
  StandardDirectoryFile,
  OrgTreeNode,
  FileUploadProgress,
  QueueStatus,
  DirectoryTemplate,
} from '@share/types'

// ========================================================
// StandardDirectoryController (/api/Workflow/StandardDirectory/*)
// ========================================================

/** 组织树 */
export async function getOrganizationTree(): Promise<OrgTreeNode[]> {
  const res = await yzhApi.get<ApiResponse<OrgTreeNode[]>>('/api/Workflow/StandardDirectory/organization-tree')
  return res.data!
}

/** 获取所有目录配置 */
export async function getDirectoryConfigs(): Promise<StandardDirectoryConfig[]> {
  const res = await yzhApi.get<ApiResponse<StandardDirectoryConfig[]>>('/api/Workflow/StandardDirectory/configs')
  return res.data!
}

/** 获取单个目录配置 */
export async function getDirectoryConfig(directoryCode: string): Promise<StandardDirectoryConfig | null> {
  const res = await yzhApi.get<ApiResponse<StandardDirectoryConfig>>(`/api/Workflow/StandardDirectory/configs/${encodeURIComponent(directoryCode)}`)
  return res.data ?? null
}

/** 创建目录配置 */
export async function createDirectoryConfig(config: Partial<StandardDirectoryConfig>): Promise<StandardDirectoryConfig> {
  const res = await yzhApi.post<ApiResponse<StandardDirectoryConfig>>('/api/Workflow/StandardDirectory/configs/create', config)
  return res.data!
}

/** 更新目录配置 */
export async function updateDirectoryConfig(directoryCode: string, config: Partial<StandardDirectoryConfig>): Promise<void> {
  await yzhApi.post<ApiResponse<void>>(`/api/Workflow/StandardDirectory/configs/${encodeURIComponent(directoryCode)}`, config)
}

/** 删除目录配置 */
export async function deleteDirectoryConfig(directoryCode: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>(`/api/Workflow/StandardDirectory/configs/${encodeURIComponent(directoryCode)}/delete`)
}

/** 获取文件夹列表 */
export async function getFolders(directoryCode: string): Promise<StandardDirectoryFolder[]> {
  const res = await yzhApi.get<ApiResponse<StandardDirectoryFolder[]>>(`/api/Workflow/StandardDirectory/configs/${encodeURIComponent(directoryCode)}/folders`)
  return res.data!
}

/** 创建文件夹 */
export async function createFolder(directoryCode: string, folder: Partial<StandardDirectoryFolder>): Promise<StandardDirectoryFolder> {
  const res = await yzhApi.post<ApiResponse<StandardDirectoryFolder>>(`/api/Workflow/StandardDirectory/configs/${encodeURIComponent(directoryCode)}/folders/create`, folder)
  return res.data!
}

/** 更新文件夹 */
export async function updateFolder(folderCode: string, folder: Partial<StandardDirectoryFolder>): Promise<void> {
  await yzhApi.post<ApiResponse<void>>(`/api/Workflow/StandardDirectory/folders/${encodeURIComponent(folderCode)}`, folder)
}

/** 删除文件夹 */
export async function deleteFolder(folderCode: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>(`/api/Workflow/StandardDirectory/folders/${encodeURIComponent(folderCode)}/delete`)
}

/** 获取文件列表 */
export async function getFiles(folderCode: string): Promise<StandardDirectoryFile[]> {
  const res = await yzhApi.get<ApiResponse<StandardDirectoryFile[]>>(`/api/Workflow/StandardDirectory/folders/${encodeURIComponent(folderCode)}/files`)
  return res.data!
}

/** 获取目录根级文件（无文件夹的文件） */
export async function getRootFiles(directoryCode: string): Promise<StandardDirectoryFile[]> {
  const res = await yzhApi.get<ApiResponse<StandardDirectoryFile[]>>(`/api/Workflow/StandardDirectory/directories/${encodeURIComponent(directoryCode)}/root-files`)
  return res.data!
}

/** 更新文件 */
export async function updateFile(fileCode: string, file: Partial<StandardDirectoryFile>): Promise<void> {
  await yzhApi.post<ApiResponse<void>>(`/api/Workflow/StandardDirectory/files/${encodeURIComponent(fileCode)}`, file)
}

/** 删除文件 */
export async function deleteFile(fileCode: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>(`/api/Workflow/StandardDirectory/files/${encodeURIComponent(fileCode)}/delete`)
}

/** 下载文件 */
export async function downloadFile(storagePath: string): Promise<Blob> {
  const res = await yzhApi.get<Blob>('/api/Workflow/StandardDirectory/download', {
    params: { storagePath },
    responseType: 'blob',
  } as any)
  return res as any
}

/** 上传初始化（创建任务） */
const IgnoredFileExtensions = new Set(['.ds_store', '.thumbs.db', 'desktop.ini'])

export function filterIgnoredFiles(files: File[]): File[] {
  return files.filter(f => {
    const name = f.name.toLowerCase()
    if (IgnoredFileExtensions.has(name)) return false
    const ext = '.' + name.split('.').pop()
    return !IgnoredFileExtensions.has(ext)
  })
}

export async function uploadInit(
  directoryCode: string,
  files: File[],
): Promise<{ taskId: string; fileMap: Record<string, { fileCode: string; storagePath: string }> }> {
  const filtered = filterIgnoredFiles(files)

  if (filtered.length === 0) {
    return { taskId: '', fileMap: {} }
  }

  // 从文件列表提取唯一文件夹路径
  const folderSet = new Set<string>()
  const fileItems: Array<{ RelativePath: string; FileName: string; FileSize: number; MimeType: string }> = []

  for (const file of filtered) {
    const relPath = (file as any).webkitRelativePath || file.name
    const lastSlash = relPath.lastIndexOf('/')
    if (lastSlash > 0) {
      folderSet.add(relPath.substring(0, lastSlash))
    }
    fileItems.push({
      RelativePath: relPath,
      FileName: file.name,
      FileSize: file.size,
      MimeType: file.type || 'application/octet-stream',
    })
  }

  const folderItems = Array.from(folderSet).map(p => ({ Path: p }))

  const res = await yzhApi.post<ApiResponse<any>>('/api/Workflow/StandardDirectory/upload-init', {
    DirectoryCode: directoryCode,
    Folders: folderItems,
    Files: fileItems,
  })
  const data = res.data
  const taskId = data?.TaskId || data?.taskId || ''

  // 构建 relativePath → { fileCode, storagePath } 映射
  const fileMap: Record<string, { fileCode: string; storagePath: string }> = {}
  const enhancedFiles = data?.Files || data?.files || []
  for (const ef of enhancedFiles) {
    const relPath = ef.RelativePath || ef.relativePath || ef.FileName || ef.fileName
    fileMap[relPath] = {
      fileCode: ef.FileCode || ef.fileCode,
      storagePath: ef.StoragePath || ef.storagePath,
    }
  }

  return { taskId, fileMap }
}

/** 上传单个文件（只发 fileCode + taskId，StoragePath 由后端从 DB 读取） */
export async function uploadFile(taskId: string, fileCode: string, file: File): Promise<void> {
  const formData = new FormData()
  formData.append('File', file)
  formData.append('TaskId', taskId)
  formData.append('FileCode', fileCode)
  const token = tokenStore.get()
  const res = await fetch('/api/Workflow/StandardDirectory/upload-file-v2', {
    method: 'POST',
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: formData,
  })
  const json = await res.json()
  if (!res.ok || json.code !== 200) throw new Error(json.msg || json.message || '上传失败')
}

/** 上传确认（激活文件 + 创建转换队列） */
export async function uploadConfirm(taskId: string): Promise<{ queueCode: string }> {
  const res = await yzhApi.post<ApiResponse<any>>('/api/Workflow/StandardDirectory/upload-confirm', { TaskId: taskId })
  const data = res.data
  return { queueCode: data?.convertQueueCode || data?.ConvertQueueCode || '' }
}

/** 上传取消（回滚） */
export async function uploadCancel(taskId: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>('/api/Workflow/StandardDirectory/upload-cancel', { TaskId: taskId })
}

/** 获取上传状态 */
export async function getUploadStatus(taskId: string): Promise<FileUploadProgress[]> {
  const res = await yzhApi.get<ApiResponse<FileUploadProgress[]>>('/api/Workflow/StandardDirectory/upload-status', { params: { taskId } })
  return res.data!
}

/** 获取活跃队列 */
export async function getActiveQueue(directoryCode: string): Promise<any[]> {
  const res = await yzhApi.get<ApiResponse<any[]>>('/api/Workflow/StandardDirectory/active-queue', { params: { directoryCode } })
  return res.data!
}

/** 查询转换进度 */
export async function getConvertProgress(taskId: string): Promise<any> {
  const res = await yzhApi.post<ApiResponse<any>>('/api/Workflow/StandardDirectory/convert/progress', { TaskId: taskId })
  return res.data!
}

/** 取消转换 */
export async function cancelConvert(queueCode: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>('/api/Workflow/StandardDirectory/convert/cancel', { QueueCode: queueCode })
}

// ========================================================
// DirectoryTemplateController (/api/Foundation/DirectoryTemplate/*)
// ========================================================

/** 获取目录模板树 */
export async function getTemplateTree(configCode: string): Promise<DirectoryTemplate[]> {
  const res = await yzhApi.get<ApiResponse<DirectoryTemplate[]>>('/api/Foundation/DirectoryTemplate/tree', { params: { configCode } })
  return res.data!
}

/** 添加模板文件夹 */
export async function addTemplateFolder(folder: Partial<DirectoryTemplate>): Promise<DirectoryTemplate> {
  const res = await yzhApi.post<ApiResponse<DirectoryTemplate>>('/api/Foundation/DirectoryTemplate/addFolder', folder)
  return res.data!
}

/** 更新模板文件夹 */
export async function updateTemplateFolder(folder: Partial<DirectoryTemplate>): Promise<void> {
  await yzhApi.post<ApiResponse<void>>('/api/Foundation/DirectoryTemplate/updateFolder', folder)
}

/** 删除模板文件夹 */
export async function deleteTemplateFolder(code: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>(`/api/Foundation/DirectoryTemplate/deleteFolder?code=${encodeURIComponent(code)}`)
}

/** 上传模板文件 */
export async function uploadTemplateFile(file: File, configCode: string): Promise<string> {
  const formData = new FormData()
  formData.append('file', file)
  const res = await yzhApi.post<ApiResponse<string>>(
    `/api/Foundation/DirectoryTemplate/uploadTemplateFile?configCode=${encodeURIComponent(configCode)}`,
    formData,
    { headers: { 'Content-Type': 'multipart/form-data' } } as any,
  )
  return res.data!
}

/** 下载模板文件 */
export async function downloadTemplateFile(storagePath: string): Promise<Blob> {
  const res = await yzhApi.get<Blob>('/api/Foundation/DirectoryTemplate/downloadTemplateFile', {
    params: { storagePath },
    responseType: 'blob',
  } as any)
  return res as any
}

/** 删除模板文件 */
export async function deleteTemplateFile(storagePath: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>(`/api/Foundation/DirectoryTemplate/deleteTemplateFile?storagePath=${encodeURIComponent(storagePath)}`)
}

/** 重命名模板文件 */
export async function renameTemplateFile(oldPath: string, newPath: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>(`/api/Foundation/DirectoryTemplate/renameTemplateFile?oldPath=${encodeURIComponent(oldPath)}&newPath=${encodeURIComponent(newPath)}`)
}

// ========================================================
// QueueMonitorController (/api/System/QueueMonitor/*)
// ========================================================

/** 获取队列列表 */
export async function getQueueList(params: { Type?: string; Status?: string; StartTime?: string; EndTime?: string; Page?: number; Rows?: number }): Promise<any> {
  const res = await yzhApi.post<ApiResponse<any>>('/api/System/QueueMonitor/list', params)
  return res.data!
}

/** 获取队列详情 */
export async function getQueueDetail(queueCode: string): Promise<any> {
  const res = await yzhApi.post<ApiResponse<any>>('/api/System/QueueMonitor/detail', { QueueCode: queueCode })
  return res.data!
}

/** 取消队列 */
export async function cancelQueue(queueCode: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>('/api/System/QueueMonitor/cancel', { QueueCode: queueCode })
}

/** 重试队列 */
export async function retryQueue(queueCode: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>('/api/System/QueueMonitor/retry', { QueueCode: queueCode })
}

/** 重试任务 */
export async function retryTask(taskId: number): Promise<void> {
  await yzhApi.post<ApiResponse<void>>('/api/System/QueueMonitor/task/retry', { TaskId: taskId })
}

/** 获取队列状态统计 */
export async function getQueueStatus(): Promise<QueueStatus> {
  const res = await yzhApi.post<ApiResponse<QueueStatus>>('/api/System/QueueMonitor/status')
  return res.data!
}

/** 查找资源锁 */
export async function findResourceLock(resourceTable: string, resourceCodes: string[]): Promise<any[]> {
  const res = await yzhApi.post<ApiResponse<any[]>>('/api/System/QueueMonitor/resource/locked', {
    ResourceTable: resourceTable,
    ResourceCodes: resourceCodes,
  })
  return res.data!
}
