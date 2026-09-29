/**
 * 企业资料 API（审核员端 /resources）
 *
 * 后端：CertPlatform.Auditor/Controllers/EnterpriseFileController.cs
 * 路由前缀：/api/Auditor/EnterpriseFile
 * 设计：docs/40-实施/企业资料管理/{01,04,05}-V1.md
 *
 * 字段口径（AGENTS.md ③）：**DB 列名字段 PascalCase 逐字一致**（Code/FileName/StoragePath/Status…）；
 * 仅接口自己拼装的聚合字段用 camel（configured/standards/summary/level）。
 * 写错大小写 = 渲染空行且零报错。
 *
 * ⛔ 禁止 `window.open` / `iframe src` 直连下载与预览：本平台鉴权走 Authorization 头，
 *    裸链接必然 401。一律走 `yzhApi.getBlob` 取字节再交渲染器/触发保存。
 */
import { yzhApi } from '@yzh-core'
import type { ApiResponse } from '@yzh-core'
import { unwrap } from '@yzh-core'

const BASE = '/api/Auditor/EnterpriseFile'

/**
 * 预览/下载**不能**传裸 URL；统一本函数取 Blob。
 * @param storagePath 存储路径（`/enterprise-documents/...` 或归档/产物路径）
 */
export async function downloadBlob(storagePath: string): Promise<Blob> {
  return yzhApi.getBlob(`${BASE}/download`, { storagePath })
}

/** 取文件预览 PDF 产物字节（PDF/图片透传时后端回落源文件） */
export async function previewBlob(fileCode: string, enterpriseCode: string): Promise<Blob> {
  return yzhApi.getBlob(`${BASE}/file-preview/${fileCode}`, { enterpriseCode })
}

/** 浏览器触发保存（Blob → 临时 a 标签） */
export function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

// ═══════════════════════ 类型 ═══════════════════════

/** 左树阶段节点 */
export interface StageItem {
  StageCode: string
  StageName: string
  StageCodeBiz?: string | null
  SortOrder: number
  /** 该阶段关联的标准数（0 = 未关联，节点置灰） */
  StandardCount: number
}

export interface EnterpriseNode {
  Code: string
  Name: string
  EnterpriseNo?: string
  Stages: StageItem[]
}

export interface StageTreeResult {
  Configured: boolean
  Message?: string | null
  Enterprises: EnterpriseNode[]
}

/** 阶段汇总里的单个标准 */
export interface StageStandardSummary {
  StandardCode: string
  StandardName: string
  StandardNo?: string | null
  StandardVersionYear?: number | null
  Configured: boolean
  Message?: string | null
  EnterpriseConfigCode?: string | null
  TemplateConfigCode?: string | null
  Required: number
  Live: number
  Ready: number
  Converting: number
  Missing: number
}

export interface StageOverviewResult {
  EnterpriseCode: string
  StageCode: string
  StageName?: string | null
  Configured: boolean
  Message?: string | null
  Standards: StageStandardSummary[]
  Summary: {
    StandardCount: number
    TotalRequired: number
    TotalLive: number
    TotalConverting: number
    TotalMissing: number
  }
}

/** 模板文件夹（FullPath 相对配置根，03 §3.2） */
export interface DirectoryFolder {
  Code: string
  ParentCode?: string | null
  FolderName: string
  Depth: number
  SortOrder: number
  FullPath: string
}

/** 就位状态（01 §5.1，后端 StatusOf 为唯一权威） */
export type SlotStatus =
  | 'missing' | 'uploading' | 'uploaded' | 'converting'
  | 'ready' | 'convertFailed' | 'markdownFailed' | 'removed'

/** 槽位行（企业目录文件行 = cert_standard_directory_file，EnterpriseCode = 真实企业） */
export interface FileSlot {
  Code: string
  /** 标准 Code（冗余列）——局部刷新按它定位标准 Tab */
  StandardCode?: string | null
  StageCode?: string | null
  FolderCode: string
  FolderName: string
  /** 相对配置根的文件夹路径（上传/替换时后端据此算存储路径） */
  FolderPath: string
  FileName: string
  FileType?: string | null
  FullPath?: string | null
  IsRequired: boolean
  VersionNumber: number
  /** 1=在清单；0 且有对象 = 已移除（证据保留，可恢复） */
  IsValid: number
  StandardFileCode?: string | null
  ExtractionEnabled?: boolean
  FileSize?: number | null
  StoragePath?: string | null
  PreviewPdfPath?: string | null
  MarkdownPath?: string | null
  UploadStatus?: string | null
  ConvertStatus?: string | null
  ConvertMessage?: string | null
  MarkdownStatus?: string | null
  MarkdownMessage?: string | null
  ExtractStatus?: string | null
  Status: SlotStatus
  UpdateTime?: string | null
  CreateTime?: string | null
}

export interface StandardDirectoryResult {
  Configured: boolean
  Message?: string | null
  EnterpriseCode: string
  StageCode: string
  StandardCode: string
  StandardNo?: string | null
  StandardName?: string | null
  EnterpriseConfigCode: string
  TemplateConfigCode: string
  Folders: DirectoryFolder[]
  Files: FileSlot[]
  Summary: {
    TotalRequired: number
    Live: number
    Ready: number
    Converting: number
    Missing: number
  }
}

/** 槽位「有效已上传」——与后端 IsLive 同口径（在清单且有对象） */
export function isSlotLive(slot?: FileSlot | null): boolean {
  return !!slot && slot.IsValid !== 0 && !!slot.StoragePath
}

/** 槽位「已移除」：对象与版本都在，唯一出口是版本面板恢复 */
export function isSlotRemoved(slot?: FileSlot | null): boolean {
  return !!slot && slot.Status === 'removed'
}

/** 乐观锁时间戳：行无 UpdateTime 时后端回退 CreateTime（同口径） */
export function slotModifyTime(slot?: FileSlot | null): string | undefined {
  return slot?.UpdateTime ?? slot?.CreateTime ?? undefined
}

/** 就位状态 → 中文标签 + 颜色（01 §5.1） */
export const SLOT_STATUS_TEXT: Record<SlotStatus, { text: string; type: 'success' | 'warning' | 'danger' | 'info' | 'primary' }> = {
  missing: { text: '缺失', type: 'info' },
  uploading: { text: '上传中', type: 'primary' },
  uploaded: { text: '已上传', type: 'success' },
  converting: { text: '转换中', type: 'warning' },
  ready: { text: '已就绪', type: 'success' },
  convertFailed: { text: '转换失败', type: 'danger' },
  // PDF 成功但 Markdown 产物失败：提取/NC 拿不到正文，标黄提醒而不是报「已就绪」
  markdownFailed: { text: '缺 Markdown', type: 'warning' },
  removed: { text: '已移除', type: 'danger' }
}

// ═══════════════════════ 分发计划 ═══════════════════════

export interface DispatchRow {
  FileName: string
  RelativePath: string
  FileSize: number
  StandardCode: string
  StandardName: string
  SlotCode: string
  SlotFileName: string
  FolderCode: string
  FolderPath: string
  /** M0Path / M1Exact / M2Contains / M3FolderExt */
  Level: string
  /** M3 命中需人工确认 */
  NeedsConfirm: boolean
  /** 非空 = 该行不可执行（如槽位已就位需走替换） */
  BlockReason?: string | null
}

export interface DispatchPlanResult {
  Configured: boolean
  Message?: string | null
  EnterpriseCode: string
  StageCode: string
  Standards: Array<{ StandardCode: string; StandardName: string; Rows: DispatchRow[] }>
  Unmatched: Array<{ FileName: string; RelativePath: string; FileSize: number; Reason: string }>
  Conflicts: Array<{ StandardCode: string; StandardName: string; SlotCode: string; SlotFileName: string; FileNames: string[] }>
  CrossStandard: Array<{ FileName: string; Standards: string[]; StandardCodes: string[] }>
  Messages: Array<{ StandardCode: string; Message: string }>
  Summary: {
    RowCount: number
    ExecutableCount: number
    BlockedCount: number
    UnmatchedCount: number
    ConflictCount: number
    CrossStandardCount: number
    StandardCount: number
  }
}

// ═══════════════════════ 四段式上传 ═══════════════════════

/** Step1 item：带 SlotCode = 槽位模式；仅带 FolderCode = 指派模式（D8 模板外文件） */
export interface UploadInitItem {
  SlotCode?: string
  FolderCode?: string
  FileName: string
  FileSize: number
}

export interface UploadInitItemResult {
  FileCode: string
  Mode: 'slot' | 'assign'
  FileName: string
  FolderCode: string
  FolderPath: string
  UploadFileName: string
  /** 建议路径（真实路径在 Step2 由后端按 DB 记录重算，防伪造） */
  StoragePath: string
}

export interface UploadInitResult {
  TaskId: string
  ConfigCode: string
  EnterpriseCode: string
  StageCode: string
  StandardCode: string
  TotalFiles: number
  TotalSize: number
  ExpireTime?: string | null
  Items: UploadInitItemResult[]
}

export interface UploadConfirmResult {
  TaskId: string
  ActivatedCount: number
  ConvertCount: number
  QueueCode?: string | null
  /** 队列创建失败不阻断激活（R5）：此处给原因，业务仍算成功 */
  QueueError?: string | null
}

export interface ActiveQueueResult {
  IsBusy: boolean
  QueueCode?: string | null
  QueueName?: string | null
  Status?: string | null
  TotalCount: number
  CompletedCount: number
  FailedCount: number
  PendingCount: number
  Progress: number
  QueueType?: string | null
  LockQueueCode?: string | null
  Message?: string | null
}

// ═══════════════════════ 接口 ═══════════════════════

/** 左树：本工作区企业 → 各企业已关联阶段（含标准数） */
export async function stageTree(): Promise<StageTreeResult> {
  const res = await yzhApi.post<ApiResponse<StageTreeResult>>(`${BASE}/stage-tree`, {})
  return unwrap(res, { Configured: false, Enterprises: [] } as StageTreeResult)
}

/** 阶段汇总：该企业该阶段下每个标准的应上传/已就位/转换中/缺失 */
export async function stageOverview(enterpriseCode: string, stageCode: string): Promise<StageOverviewResult> {
  const res = await yzhApi.post<ApiResponse<StageOverviewResult>>(`${BASE}/stage-overview`, {
    EnterpriseCode: enterpriseCode, StageCode: stageCode
  })
  return unwrap(res, { Configured: false, Standards: [] } as unknown as StageOverviewResult)
}

/** 标准卡片主数据：文件夹树 + 槽位全集（带就位状态） */
export async function standardDirectory(
  enterpriseCode: string, stageCode: string, standardCode: string
): Promise<StandardDirectoryResult> {
  const res = await yzhApi.post<ApiResponse<StandardDirectoryResult>>(`${BASE}/standard-directory`, {
    EnterpriseCode: enterpriseCode, StageCode: stageCode, StandardCode: standardCode
  })
  return unwrap(res, { Configured: false, Folders: [], Files: [] } as unknown as StandardDirectoryResult)
}

/** 分发预览（纯计算，不落库不写对象） */
export async function planDispatch(
  enterpriseCode: string, stageCode: string,
  files: Array<{ FileName: string; RelativePath: string; FileSize: number }>
): Promise<DispatchPlanResult> {
  const res = await yzhApi.post<ApiResponse<DispatchPlanResult>>(`${BASE}/upload/plan`, {
    EnterpriseCode: enterpriseCode, StageCode: stageCode, Files: files
  })
  return unwrap(res, { Configured: false, Standards: [] } as unknown as DispatchPlanResult)
}

/** Step1：建上传任务 + 预建/重置目标行 */
export async function uploadInit(
  enterpriseCode: string, stageCode: string, standardCode: string, items: UploadInitItem[]
): Promise<UploadInitResult> {
  const res = await yzhApi.post<ApiResponse<UploadInitResult>>(`${BASE}/upload/init`, {
    EnterpriseCode: enterpriseCode, StageCode: stageCode, StandardCode: standardCode, Items: items
  })
  return unwrap(res, null as unknown as UploadInitResult)
}

/** Step2：逐文件传字节（multipart；单文件失败不整体中断） */
export async function uploadFileStep(file: File, fileCode: string, taskId: string): Promise<void> {
  const fd = new FormData()
  fd.append('File', file)
  fd.append('FileCode', fileCode)
  fd.append('TaskId', taskId)
  const res = await yzhApi.post<ApiResponse<unknown>>(`${BASE}/upload/file`, fd)
  unwrap(res, null)
}

/** Step3：激活 + 入 file_convert 队列 */
export async function uploadConfirm(taskId: string, enterpriseCode: string): Promise<UploadConfirmResult> {
  const res = await yzhApi.post<ApiResponse<UploadConfirmResult>>(`${BASE}/upload/confirm`, {
    TaskId: taskId, EnterpriseCode: enterpriseCode
  })
  return unwrap(res, null as unknown as UploadConfirmResult)
}

/** Step4：回滚（取消队列 + 删对象 + 撤草稿行） */
export async function uploadCancel(taskId: string, enterpriseCode: string): Promise<void> {
  const res = await yzhApi.post<ApiResponse<unknown>>(`${BASE}/upload/cancel`, {
    TaskId: taskId, EnterpriseCode: enterpriseCode
  })
  unwrap(res, null)
}

/** 四段式一键执行（单标准）：init → 逐文件 file → confirm */
export async function uploadFiles(
  enterpriseCode: string, stageCode: string, standardCode: string,
  items: Array<{ File: File; SlotCode?: string; FolderCode?: string }>,
  onProgress?: (done: number, total: number, fileName: string) => void
): Promise<UploadConfirmResult> {
  const init = await uploadInit(enterpriseCode, stageCode, standardCode, items.map(i => ({
    SlotCode: i.SlotCode,
    FolderCode: i.FolderCode,
    // ★ 槽位模式：manifest 的 FileName 只作展示，行 FileName 保持标准定义名
    FileName: i.SlotCode ? i.File.name : i.File.name,
    FileSize: i.File.size
  })))

  try {
    for (let i = 0; i < items.length; i++) {
      const target = init.Items[i]
      if (!target) continue
      await uploadFileStep(items[i].File, target.FileCode, init.TaskId)
      onProgress?.(i + 1, items.length, items[i].File.name)
    }
    return await uploadConfirm(init.TaskId, enterpriseCode)
  } catch (e) {
    // 任一步失败 ⇒ 回滚（清对象 + 撤草稿行），避免半成品行留在清单里
    try { await uploadCancel(init.TaskId, enterpriseCode) } catch { /* 回滚失败不覆盖原始错误 */ }
    throw e
  }
}

// ═══════════════════════ 队列 ═══════════════════════

/** 该标准目录是否有运行中队列（前端 5s 轮询） */
export async function activeQueue(configCode: string, enterpriseCode: string): Promise<ActiveQueueResult> {
  const res = await yzhApi.post<ApiResponse<ActiveQueueResult>>(`${BASE}/active-queue`, {
    ConfigCode: configCode, EnterpriseCode: enterpriseCode
  })
  return unwrap(res, { IsBusy: false } as unknown as ActiveQueueResult)
}

/** 取消队列 */
export async function cancelQueue(queueCode: string, enterpriseCode: string): Promise<void> {
  const res = await yzhApi.post<ApiResponse<unknown>>(`${BASE}/queue/cancel`, {
    QueueCode: queueCode, EnterpriseCode: enterpriseCode
  })
  unwrap(res, null)
}

// ═══════════════════════ 替换 / 移除 / 恢复 / 版本 / 历史 ═══════════════════════

/** 替换文件（Reason 必填；ExpectedModifyTime = 乐观锁原文回传） */
export async function replaceFile(
  file: File, fileCode: string, enterpriseCode: string, reason: string, expectedModifyTime?: string
): Promise<void> {
  const fd = new FormData()
  fd.append('file', file)
  fd.append('FileCode', fileCode)
  fd.append('EnterpriseCode', enterpriseCode)
  fd.append('Reason', reason)
  if (expectedModifyTime) fd.append('ExpectedModifyTime', expectedModifyTime)
  const res = await yzhApi.post<ApiResponse<null>>(`${BASE}/replace`, fd)
  unwrap(res, null)
}

/** 移除文件（软删 + 保留存储对象，Reason 必填） */
export async function deleteFile(
  fileCode: string, enterpriseCode: string, reason: string, expectedModifyTime?: string
): Promise<void> {
  const res = await yzhApi.post<ApiResponse<null>>(`${BASE}/delete`, {
    FileCode: fileCode, EnterpriseCode: enterpriseCode, Reason: reason, ExpectedModifyTime: expectedModifyTime
  })
  unwrap(res, null)
}

/** 从归档版本恢复（单调新版本，不做版本回拨） */
export async function restoreFile(
  fileCode: string, enterpriseCode: string, versionNumber: number, reason?: string
): Promise<void> {
  const res = await yzhApi.post<ApiResponse<null>>(`${BASE}/restore`, {
    FileCode: fileCode, EnterpriseCode: enterpriseCode, VersionNumber: versionNumber, Reason: reason
  })
  unwrap(res, null)
}

/** 归档版本行（cert_enterprise_file_version） */
export interface FileVersionRow {
  Code: string
  FileCode: string
  VersionNumber: number
  FileName: string
  StoragePath: string
  FileSize: number
  Reason?: string | null
  CreateBy?: string | null
  CreateTime?: string | null
}

/** 时间线条目（op_log + 版本归档合并） */
export interface HistoryItem {
  Source: 'op_log' | 'version'
  OpType: string
  VersionNumber?: number | null
  Detail?: string | null
  CreateBy?: string | null
  Time?: string | null
}

export async function getVersions(fileCode: string, enterpriseCode: string): Promise<FileVersionRow[]> {
  const res = await yzhApi.get<ApiResponse<FileVersionRow[]>>(`${BASE}/versions/${fileCode}`, { enterpriseCode })
  return unwrap(res, [])
}

export async function getHistory(
  fileCode: string, enterpriseCode: string
): Promise<{ Code: string; FileName: string; VersionNumber: number; Status: string; Timeline: HistoryItem[] } | null> {
  const res = await yzhApi.get<ApiResponse<any>>(`${BASE}/history/${fileCode}`, { enterpriseCode })
  return unwrap(res, null)
}

// ═══════════════════════ 提取（真链，偏差 D10 保留） ═══════════════════════

/** 手动触发定向提取（真入队 doc_extract，非假成功） */
export async function triggerExtract(fileCode: string, enterpriseCode: string): Promise<void> {
  const res = await yzhApi.post<ApiResponse<null>>(`${BASE}/trigger-extract`, {
    FileCode: fileCode, EnterpriseCode: enterpriseCode
  })
  unwrap(res, null)
}

export async function getExtractionResult(fileCode: string, enterpriseCode: string): Promise<any> {
  const res = await yzhApi.get<ApiResponse<any>>(`${BASE}/extraction-result/${fileCode}`, { enterpriseCode })
  return unwrap(res, null)
}
