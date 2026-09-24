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
export async function createDirectoryConfig(config: Partial<StandardDirectoryConfig>): Promise<BizResult> {
  return await yzhApi.post<BizResult>('/api/Workflow/StandardDirectory/configs/create', config)
}

/** 更新目录配置 */
export async function updateDirectoryConfig(directoryCode: string, config: Partial<StandardDirectoryConfig>): Promise<BizResult> {
  return await yzhApi.post<BizResult>(`/api/Workflow/StandardDirectory/configs/${encodeURIComponent(directoryCode)}`, config)
}

/** 删除目录配置 */
export async function deleteDirectoryConfig(directoryCode: string): Promise<BizResult> {
  return await yzhApi.post<BizResult>(`/api/Workflow/StandardDirectory/configs/${encodeURIComponent(directoryCode)}/delete`)
}

/** 获取文件夹列表 */
export async function getFolders(directoryCode: string): Promise<StandardDirectoryFolder[]> {
  const res = await yzhApi.get<ApiResponse<StandardDirectoryFolder[]>>(`/api/Workflow/StandardDirectory/configs/${encodeURIComponent(directoryCode)}/folders`)
  return res.data!
}

/** 获取文件夹扁平列表（按 ParentCode 过滤用） */
export async function getFoldersFlat(directoryCode: string): Promise<StandardDirectoryFolder[]> {
  const res = await yzhApi.get<ApiResponse<StandardDirectoryFolder[]>>(`/api/Workflow/StandardDirectory/configs/${encodeURIComponent(directoryCode)}/folders-flat`)
  return res.data!
}

/** 创建文件夹 */
export async function createFolder(directoryCode: string, folder: Partial<StandardDirectoryFolder>): Promise<BizResult> {
  return await yzhApi.post<BizResult>(`/api/Workflow/StandardDirectory/configs/${encodeURIComponent(directoryCode)}/folders/create`, folder)
}

/** 业务响应体：这些端点始终 HTTP 200，业务成败看 code（200/400） */
export interface BizResult {
  code: number
  msg?: string
  message?: string
  data?: any
}

/** 更新文件夹 */
export async function updateFolder(folderCode: string, folder: Partial<StandardDirectoryFolder>): Promise<BizResult> {
  return await yzhApi.post<BizResult>(`/api/Workflow/StandardDirectory/folders/${encodeURIComponent(folderCode)}`, folder)
}

/** 删除文件夹 */
export async function deleteFolder(folderCode: string): Promise<BizResult> {
  return await yzhApi.post<BizResult>(`/api/Workflow/StandardDirectory/folders/${encodeURIComponent(folderCode)}/delete`)
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
export async function updateFile(fileCode: string, file: Partial<StandardDirectoryFile>): Promise<BizResult> {
  return await yzhApi.post<BizResult>(`/api/Workflow/StandardDirectory/files/${encodeURIComponent(fileCode)}`, file)
}

/** 删除文件 */
export async function deleteFile(fileCode: string): Promise<BizResult> {
  return await yzhApi.post<BizResult>(`/api/Workflow/StandardDirectory/files/${encodeURIComponent(fileCode)}/delete`)
}

/** 下载文件（带鉴权取回 Blob；预览与下载共用） */
export async function downloadFile(storagePath: string): Promise<Blob> {
  return yzhApi.getBlob('/api/Workflow/StandardDirectory/download', { storagePath })
}

/** 重试转换失败的文件（重新入队） */
export async function retryFailedConversions(): Promise<{
  ok: boolean
  message: string
  enqueued: number
  queueCount: number
}> {
  // 该端点始终 HTTP 200，业务结果在 code 字段（code=400 表示失败/无文件）
  const res = await yzhApi.post<any>('/api/Workflow/StandardDirectory/retry-failed-conversions')
  return {
    ok: res?.code === 200,
    message: res?.msg || res?.message || '',
    enqueued: res?.enqueued ?? 0,
    queueCount: res?.queueCount ?? 0,
  }
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

  console.log('[uploadInit] Sending:', { DirectoryCode: directoryCode, Folders: folderItems, Files: fileItems })

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

  console.log('[uploadInit] Response:', { taskId, fileMapKeys: Object.keys(fileMap), enhancedFilesCount: enhancedFiles.length })

  if (!taskId) {
    console.error('[uploadInit] Failed: TaskId is empty. Response data:', data)
  }

  return { taskId, fileMap }
}

/** 上传单个文件（只发 fileCode + taskId，StoragePath 由后端从 DB 读取） */
export async function uploadFile(taskId: string, fileCode: string, file: File): Promise<void> {
  if (!taskId) throw new Error('上传失败：TaskId 为空')
  if (!fileCode) throw new Error('上传失败：FileCode 为空')

  console.log('[uploadFile] Sending:', { taskId, fileCode, fileName: file.name, fileSize: file.size })

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
  console.log('[uploadFile] Response:', { status: res.status, code: json.code, msg: json.msg || json.message })
  if (!res.ok || json.code !== 200) throw new Error(json.msg || json.message || '上传失败')
}

/** 单文件替换（一步完成：覆盖上传 + 回填大小 + doc/xls 自动进转换队列） */
export async function replaceFile(fileCode: string, file: File): Promise<{ queueCode: string }> {
  if (!fileCode) throw new Error('替换失败：FileCode 为空')

  const formData = new FormData()
  formData.append('File', file)
  const token = tokenStore.get()
  const res = await fetch(`/api/Workflow/StandardDirectory/files/${encodeURIComponent(fileCode)}/replace`, {
    method: 'POST',
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: formData,
  })
  const json = await res.json()
  if (!res.ok || json.code !== 200) throw new Error(json.msg || json.message || '替换失败')
  return { queueCode: json.convertQueueCode || '' }
}

/** 上传确认（激活文件 + 创建转换队列）；业务失败（code≠200）必须抛错，否则页面误报「上传成功」 */
export async function uploadConfirm(taskId: string): Promise<{ queueCode: string }> {
  const res = await yzhApi.post<any>('/api/Workflow/StandardDirectory/upload-confirm', { TaskId: taskId })
  if (res?.code !== 200) {
    throw new Error(res?.msg || res?.message || '上传确认失败')
  }
  const data = res.data
  return { queueCode: data?.convertQueueCode || data?.ConvertQueueCode || '' }
}

/** 上传取消（回滚） */
export async function uploadCancel(taskId: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>('/api/Workflow/StandardDirectory/upload-cancel', { TaskId: taskId })
}

/** 获取上传状态 */
export async function getUploadStatus(taskId: string): Promise<FileUploadProgress[]> {
  // 注意：get 的第二个参数就是查询对象，不能再包一层 { params: {...} }
  // （否则会序列化成 ?params=[object Object]，后端 400）
  const res = await yzhApi.get<ApiResponse<FileUploadProgress[]>>('/api/Workflow/StandardDirectory/upload-status', { taskId })
  return res.data!
}

/**
 * 获取阶段完整文件树（单请求返回文件夹+文件+规则状态+根目录孤儿文件）。
 * 对齐历史老项目 stage-files 接口；后端：GET stage-files/{directoryCode}。
 */
export interface StageFileNode {
  FileCode: string
  FileName: string
  FolderCode?: string
  StoragePath?: string
  ConvertedStoragePath?: string
  ConvertStatus?: string
  ConvertMessage?: string
  UploadStatus?: string
  FileSize?: number | null
  MimeType?: string
  RuleStatus?: 'none' | 'configured' | 'failed'
}

export interface StageFolderNode {
  Code: string
  Name: string
  ParentCode?: string
  Depth?: number
  SortOrder?: number
  Children?: StageFolderNode[]
  Files?: StageFileNode[]
}

export async function getStageFileTree(directoryCode: string): Promise<{ folders: StageFolderNode[]; statistics?: { TotalFolders: number; TotalFiles: number; ConfiguredFiles: number } }> {
  const res = await yzhApi.get<ApiResponse<{ Folders: StageFolderNode[]; Statistics?: any }>>(
    `/api/Workflow/StandardDirectory/stage-files/${encodeURIComponent(directoryCode)}`,
  )
  return { folders: res.data?.Folders ?? [], statistics: res.data?.Statistics }
}

/** 获取活跃队列 */
export async function getActiveQueue(directoryCode: string): Promise<any[]> {
  const res = await yzhApi.get<ApiResponse<any[]>>('/api/Workflow/StandardDirectory/active-queue', { directoryCode })
  return res.data!
}

/**
 * 查询转换进度
 * ⚠️ 后端签名为 `[FromQuery] string taskId`（非空必填），参数必须走查询串：
 *    放在 body 会被 [ApiController] 模型校验拦下，直接返回 400 validation error。
 */
export async function getConvertProgress(taskId: string): Promise<any> {
  const res = await yzhApi.post<ApiResponse<any>>(
    '/api/Workflow/StandardDirectory/convert/progress',
    undefined,
    { params: { taskId } }
  )
  return res.data!
}

/**
 * 取消转换
 * ⚠️ 同 getConvertProgress：后端 `[FromQuery] string queueCode` 必填，body 传参会 400。
 * 返回业务结果（HTTP 恒 200，成败在 body.code），调用方需判断 code 而不是直接提示成功。
 */
export async function cancelConvert(queueCode: string): Promise<BizResult> {
  return await yzhApi.post<BizResult>(
    '/api/Workflow/StandardDirectory/convert/cancel',
    undefined,
    { params: { queueCode } }
  )
}

// ========================================================
// DirectoryTemplateController (/api/Foundation/DirectoryTemplate/*)
// ========================================================

/** 获取目录模板树 */
export async function getTemplateTree(configCode: string): Promise<DirectoryTemplate[]> {
  const res = await yzhApi.get<ApiResponse<DirectoryTemplate[]>>('/api/Foundation/DirectoryTemplate/tree', { configCode })
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

/** 下载模板文件（带鉴权取回 Blob） */
export async function downloadTemplateFile(storagePath: string): Promise<Blob> {
  return yzhApi.getBlob('/api/Foundation/DirectoryTemplate/downloadTemplateFile', { storagePath })
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

/** 重试任务（准则 A：业务键 TaskCode） */
export async function retryTask(taskCode: string): Promise<void> {
  await yzhApi.post<ApiResponse<void>>('/api/System/QueueMonitor/task/retry', { TaskCode: taskCode })
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
