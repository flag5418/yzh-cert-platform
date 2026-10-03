/**
 * 企业原始资料 API（专家端 `/enterprise-original`）
 *
 * 后端：CertPlatform.Auditor/Controllers/EnterpriseOriginalController.cs
 * 路由前缀：/api/Auditor/EnterpriseOriginal
 * 规格：docs/20-体系认证/03-详细设计/05-企业资料规范化/36-企业原始资料管理设计-V1.md
 *
 * ⛔ **与 `enterprise-file.ts` 是两套东西**（36 号 §2.1）：
 *    · 本文件 = 企业**散乱原始资料**（标准无关，按阶段组织，落 `enterprise-original-source/`）
 *    · enterprise-file = 企业**已按标准备好**的材料（标准 × 槽位，落 `enterprise-documents/`）
 *    判别口诀：文件还"不像标准文档" → 本模块；已经"长得像" → `/resources`。
 *
 * 字段口径（AGENTS.md ③）：**DB 列名 PascalCase 逐字一致**
 * （Code/FileName/StoragePath/ConvertStatus/AnalyzePolicy/VersionNumber…），
 * 仅接口自己拼装的聚合字段用 camel（success/message/rows/summary/action…）。
 * 写错大小写 = 渲染空行且零报错。
 *
 * ⛔ 禁止 `window.open` / `iframe src` 直连下载与预览：本平台鉴权走 Authorization 头，
 *    裸链接必然 401。一律走 `yzhApi.getBlob` 取字节再交渲染器/触发保存。
 */
import { yzhApi, unwrap } from '@yzh-core'

const BASE = '/api/Auditor/EnterpriseOriginal'

/*
 * ⚠️ **函数命名约定（防 TS2308）**：本文件与同目录 `enterprise-file.ts` 由 `index.ts` 同级 `export *` 导出，
 *    两个文件的导出名**不得重复**（重名 ⇒ `TS2308 Module has already exported a member`）。
 *    故凡与通用动词同名的导出（upload/download/preview/…）一律加 `original` 前缀。
 */

// ═══════════════════════ 类型 ═══════════════════════

/** 转换状态取值 —— ⛔ 只能取这 6 个（后端已对齐 `convertStatus.ts`） */
export type ConvertStatusKey =
  | 'none' | 'pending' | 'converting' | 'completed' | 'failed' | 'unsupported'

/** 分析状态取值 */
export type AnalyzeStatusKey = 'pending' | 'analyzing' | 'analyzed' | 'failed' | 'skipped'

/** 分析策略取值（字典 ANALYZE_POLICY）—— L2「数据来源」层，不是文件删除 */
export type AnalyzePolicyKey = 'analyze' | 'skip' | 'ignore'

/** 策略原因（字典 POLICY_REASON） */
export type PolicyReasonKey = 'covered_by_params' | 'irrelevant' | 'duplicate' | 'manual'

/** 左树节点 */
export interface TreeNode {
  /** ★ 业务键（GUID）—— 阶段节点是 `cert_cert_stage.Code`，⛔ 不是 slug（jd01/03） */
  Code: string
  Label: string
  FileCount: number
  /** 仅阶段节点有：可读编码（slug），只用于展示，⛔ 不能当 StageCode 传回后端 */
  StageCodeSlug?: string
  /** ★ 所属企业 Code —— 阶段节点必须能拿到它（⛔ 不能拿 node.Code 当企业 Code） */
  EnterpriseCode?: string
  /** 阶段节点为 ''；企业节点为 true */
  StageCode?: string
  IsEnterpriseNode?: boolean
  Children?: TreeNode[]
  ConvertingCount?: number
  AnalyzingCount?: number
  FailedCount?: number
}

/** 文件行（表 cert_enterprise_original_file） */
export interface OriginalFile {
  Code: string
  EnterpriseCode: string
  StageCode: string
  RelFolderPath: string
  FileName: string
  FileType: string
  FileSize: number
  Sha256: string
  StoragePath: string
  /** ★ 版本号（36 号 D8；>1 说明发生过替换） */
  VersionNumber: number
  ConvertStatus: ConvertStatusKey
  ConvertMessage?: string | null
  PreviewPdfPath?: string | null
  MarkdownPath?: string | null
  MarkdownStatus: ConvertStatusKey
  MarkdownMessage?: string | null
  ConvertDate?: string | null
  AnalyzeStatus: AnalyzeStatusKey
  AnalyzeMessage?: string | null
  AnalyzeTime?: string | null
  AnalyzePolicy: AnalyzePolicyKey
  PolicyReason?: PolicyReasonKey | null
  PolicySource?: 'ai' | 'manual' | null
  PolicyDecidedBy?: string | null
  PolicyDecidedTime?: string | null
  CreateTime?: string
  CreateBy?: string | null
  UpdateTime?: string | null
  /** ★ 语义分组：这份文件命中的受控标签（值 ∈ cert_tag_dict.TagCode） */
  Tags?: string[]
  /** ★ 填写期可用（转换 completed ∧ Markdown completed ∧ 分析 analyzed） */
  IsUsableForFilling?: boolean
  /** ★ 半成品：转换成功但分析未成功（下游必须排除） */
  IsHalfProduct?: boolean
  /**
   * ★ **不建议提取**：策略为 skip/ignore（人工设的或 AI 建议的）。
   * ⇒ 前端**不显示**「加标签 / 填作用」入口 —— 营业执照、身份证、资质证书这类
   *   **特定证件**没有「体系文件作用」语义，硬打标签会污染召回词表。
   */
  IsNotSuggested?: boolean
  UnusableReason?: string | null
  DocPurpose?: string | null
  InfoItemsJson?: string | null
  ProfileVersion?: number | null
  HasProfile?: boolean
  ProfileStatus?: string | null
  DocCategory?: string | null
  Summary?: string | null
}

/** 历史版本行（表 cert_enterprise_original_file_version） */
export interface FileVersion {
  Code: string
  FileCode: string
  VersionNumber: number
  FileName: string
  FileType: string
  FileSize: number
  Sha256: string
  StoragePath: string
  Reason?: string | null
  CreateTime?: string
  CreateBy?: string | null
}

/** plan 结果行 */
export interface PlanRow {
  FileName: string
  RelFolderPath: string
  FileType: string
  FileSize: number
  Sha256: string
  /** ★ D7：create=新建 / replace=异 hash 替换（版本+1）/ skip=同 hash 幂等 */
  Action: 'create' | 'replace' | 'skip'
  ExistingCode?: string | null
  ExistingVersion?: number | null
  Blocked: boolean
  BlockReason?: string | null
}

/** plan 上传项 */
export interface PlanItem {
  FileName: string
  RelFolderPath?: string
  FileSize: number
  Sha256?: string
}

/** init 下发的项 */
export interface InitItem {
  FileCode: string
  FileName: string
  Action: 'create' | 'replace' | 'skip'
  StoragePath: string
}

/** 语义分组聚合项（每个标签命中多少份） */
export interface TagGroupItem {
  TagCode: string
  FileCount: number
}

/** 队列明细里的单个任务 */
export interface QueueTaskItem {
  Code: string
  TaskType: string
  Status: string
  ProcessTime?: string | null
  RetryCount?: number
  MaxRetryCount?: number
  ErrorType?: string | null
  Message?: string | null
  FileCode?: string | null
  FileName?: string | null
}

/** 队列明细 */
export interface QueueDetail {
  RunningCount: number
  PendingCount: number
  FailedCount: number
  Rows: Array<{
    Code: string
    QueueName: string
    QueueType: string
    Status: string
    CreateTime?: string | null
    StartTime?: string | null
    EndTime?: string | null
    TotalCount?: number
    PendingCount?: number
    ProcessingCount?: number
    CompletedCount?: number
    FailedCount?: number
    Progress?: number
    QueuePosition?: number
    Tasks: QueueTaskItem[]
  }>
}

/** 状态条 */
export interface StatusBar {
  Total: number
  ConvertingCount: number
  AnalyzingCount: number
  FailedCount: number
  UnsupportedCount: number
  PendingCount: number
  AnalyzedCount: number
  QueueCode?: string | null
  QueueStatus?: string | null
  /** ★ 填写期可用份数（转换 ∧ 分析） */
  UsableCount?: number
  /** ★ 半成品份数（转成功但分析失败） */
  HalfProductCount?: number
  /** ★ 队列总任务数（进度条分母） */
  QueueTotal?: number
  /** ★ 队列已完成数（进度条分子） */
  QueueCompleted?: number
  /** ★ 队列进度百分比 0-100 */
  QueueProgress?: number
  /**
   * ★ **忙碌态**：该阶段有队列排队中/执行中 ⇒ 前端必须**禁用上传**。
   * 2026-10-03 用户要求：「卡住该阶段不允许继续上传，只有队列完成后才能继续上传，
   * 可以显示进度的详情，否则会造成误判」。
   */
  IsBusy?: boolean
  /** 队列类型（内部字面量，UI 请用 `queueLabel` 转成人话） */
  QueueType?: string | null
}

/** L3 画像（表 cert_enterprise_doc_profile） */
export interface DocProfile {
  Code: string
  OriginalFileCode: string
  ProfileVersion: number
  IsLatest: boolean
  StandardCode: string
  FileName: string
  TagsJson?: string | null
  TagsSource?: string | null
  TagsReason?: string | null
  TagsConfidence?: number | null
  DocPurpose?: string | null
  DocPurposeSource?: string | null
  DocPurposeConfidence?: number | null
  InfoItemsJson?: string | null
  FieldsJson?: string | null
  TablesJson?: string | null
  DocCategory?: string | null
  Summary?: string | null
  Keywords?: string | null
  Confidence?: number | null
  TypeGuess?: string | null
  ProfileStatus: string
  ModelName?: string | null
  PromptCode?: string | null
  AnalyzeTime?: string | null
  IsManualCorrected?: boolean
  CorrectedBy?: string | null
  CorrectedTime?: string | null
}

/** 受控标签（cert_tag_dict） */
export interface TagItem {
  Code?: string
  TagCode: string
  TagName: string
  TagGroup?: string | null
  ApplicableSide?: string
  TagPurposeHint?: string | null
  MatchFeature?: string | null
}

/**
 * 一个带响应包裹的结果。
 *
 * ⚠️ **字段一律 PascalCase**（AGENTS.md ③ 铁律：DB 列名 = 接口字段名）：
 * 后端返回 `{ Configured, Message, Nodes }` / `{ Success, Message, Rows }`，
 * 这里<b>必须</b>写 `res.data.Nodes` / `res.data.Rows`。
 * 写成小写 `res.data.nodes` ⇒ `undefined` ⇒ 页面全空且**零报错**（2026-10-03 实测踩到）。
 */
interface Payload {
  success: boolean
  message: string | null
  err?: string
  data?: any
}

// ═══════════════════════ 一、左树 / 列表 / 状态条 ═══════════════════════

/** 左树：企业 → 认证阶段 */
export async function fetchStageTree(): Promise<TreeNode[]> {
  const res = await yzhApi.post<Payload>(`${BASE}/stage-tree`)
  return (res?.data?.Nodes as TreeNode[]) ?? []
}

/** 文件列表 */
export async function fetchFiles(enterpriseCode: string, stageCode: string): Promise<OriginalFile[]> {
  const res = await yzhApi.post<Payload>(`${BASE}/list`, { EnterpriseCode: enterpriseCode, StageCode: stageCode })
  if (!res?.success) return []
  return (res.data?.Rows as OriginalFile[]) ?? []
}

/**
 * ★ 语义过滤版列表（老板问题 1 的落点）。
 *
 * @param tagCodes 受控标签码并集；空 = 不按标签过滤
 * @param onlyUsable true = 只返回「转换 ∧ 分析」双条件都成功的行（**填写期取资料必须用这个**）
 * @param groupByTag true = 额外返回按标签的分组聚合（语义分组视图）
 */
export async function fetchFilesFiltered(
  enterpriseCode: string, stageCode: string,
  tagCodes?: string[], onlyUsable = false, groupByTag = false,
): Promise<{ rows: OriginalFile[]; total: number; usableCount: number; halfProductCount: number; groups: TagGroupItem[] }> {
  const res = await yzhApi.post<Payload>(`${BASE}/list`, {
    EnterpriseCode: enterpriseCode, StageCode: stageCode,
    TagCodes: tagCodes ?? [], OnlyUsable: onlyUsable, GroupByTag: groupByTag,
  })
  const d = res?.data ?? {}
  return {
    rows: (d.Rows as OriginalFile[]) ?? [],
    total: d.Total ?? 0,
    usableCount: d.UsableCount ?? 0,
    halfProductCount: d.HalfProductCount ?? 0,
    groups: (d.Groups as TagGroupItem[]) ?? [],
  }
}

/** ★ 队列明细（排队位置 + 进度 + 每份文件的任务状态） */
export async function fetchQueueDetail(enterpriseCode: string, stageCode: string): Promise<QueueDetail | null> {
  const res = await yzhApi.post<Payload>(`${BASE}/queue-detail`, { EnterpriseCode: enterpriseCode, StageCode: stageCode })
  return res?.success ? ((res.data as QueueDetail) ?? null) : null
}

/** 状态条 */
export async function fetchStatusBar(enterpriseCode: string, stageCode: string): Promise<StatusBar | null> {
  const res = await yzhApi.post<Payload>(`${BASE}/status-bar`, { EnterpriseCode: enterpriseCode, StageCode: stageCode })
  return res?.success ? ((res.data as StatusBar) ?? null) : null
}

// ═══════════════════════ 二、五段式上传 ═══════════════════════

/** Step0：预检（纯计算，不落库） */
export async function originalPlanUpload(
  enterpriseCode: string, stageCode: string, items: PlanItem[],
): Promise<{ rows: PlanRow[]; summary: any }> {
  const res = await yzhApi.post<Payload>(`${BASE}/upload/plan`, {
    EnterpriseCode: enterpriseCode, StageCode: stageCode, Items: items,
  })
  return {
    rows: (res?.data?.Rows as PlanRow[]) ?? [],
    summary: res?.data?.Summary ?? {},
  }
}

/** Step1：建批次 + 建/命中文件行 */
export async function originalUploadInit(
  enterpriseCode: string, stageCode: string, items: PlanItem[],
): Promise<{ TaskId: string; Items: InitItem[]; SkipCount: number }> {
  const res = await yzhApi.post<any>(`${BASE}/upload/init`, {
    EnterpriseCode: enterpriseCode, StageCode: stageCode, Items: items,
  })
  const d = unwrap(res, null as any)
  return { TaskId: d?.TaskId ?? '', Items: d?.Items ?? [], SkipCount: d?.SkipCount ?? 0 }
}

/**
 * Step2：逐文件传字节。
 * ⛔ 前端**不传存储路径**，后端按 DB 行重算（防伪造）。
 * ⛔ `sha256` 可留空 —— 服务端收到字节会自行计算并以那个为准（D7 判定的权威依据是实际字节）。
 */
export async function originalUploadFile(
  taskId: string, fileCode: string, file: File, sha256?: string,
): Promise<{ FileCode: string; Action: string; VersionNumber: number }> {
  const fd = new FormData()
  fd.append('TaskId', taskId)
  fd.append('FileCode', fileCode)
  if (sha256) fd.append('Sha256', sha256)
  fd.append('File', file, (file as any).webkitRelativePath?.split('/').pop() || file.name)

  const res = await yzhApi.post<any>(`${BASE}/upload/file`, fd)
  return unwrap(res, null as any)
}

/** Step3：激活 + 入队 */
export async function originalUploadConfirm(taskId: string, enterpriseCode: string): Promise<any> {
  const res = await yzhApi.post<any>(`${BASE}/upload/confirm`, { TaskId: taskId, EnterpriseCode: enterpriseCode })
  return unwrap(res, null as any)
}

/** Step4：回滚 */
export async function originalUploadCancel(taskId: string, enterpriseCode: string): Promise<any> {
  const res = await yzhApi.post<any>(`${BASE}/upload/cancel`, { TaskId: taskId, EnterpriseCode: enterpriseCode })
  return unwrap(res, null as any)
}

// ═══════════════════════ 三、删除 / 版本 / 回滚 ═══════════════════════

/** 删除（★ 只传业务键 Code，⛔ 不用 Id） */
export async function removeFile(fileCode: string, enterpriseCode: string, reason?: string): Promise<void> {
  const res = await yzhApi.post<any>(`${BASE}/delete`, { FileCode: fileCode, EnterpriseCode: enterpriseCode, Reason: reason })
  unwrap(res, undefined as any)
}

/** 历史版本列表 */
export async function fetchVersions(fileCode: string, enterpriseCode: string): Promise<{
  CurrentVersion: number; Rows: FileVersion[]
}> {
  const res = await yzhApi.get<any>(`${BASE}/versions/${fileCode}`, { enterpriseCode })
  const d = unwrap(res, null as any)
  return { CurrentVersion: d?.CurrentVersion ?? 1, Rows: d?.Rows ?? [] }
}

/** 回滚到指定版本（★ 反向替换：当前版被归档，版本号继续递增，不倒退） */
export async function restoreVersion(
  fileCode: string, enterpriseCode: string, versionNumber: number, reason?: string,
): Promise<any> {
  const res = await yzhApi.post<any>(`${BASE}/restore`, {
    FileCode: fileCode, EnterpriseCode: enterpriseCode, VersionNumber: versionNumber, Reason: reason,
  })
  return unwrap(res, null as any)
}

// ═══════════════════════ 四、分析策略 ═══════════════════════

/**
 * 改分析策略。
 * @param reanalyze true = 保存后立即入队重算（策略立刻生效）；false = 只保存，等下次重跑
 */
export async function setPolicy(
  fileCode: string, enterpriseCode: string, policy: AnalyzePolicyKey,
  reason?: PolicyReasonKey | null, reanalyze = false,
): Promise<any> {
  const res = await yzhApi.post<any>(`${BASE}/policy/set`, {
    FileCode: fileCode, EnterpriseCode: enterpriseCode,
    AnalyzePolicy: policy, PolicyReason: reason, Reanalyze: reanalyze,
  })
  return unwrap(res, null as any)
}

/** 批量设置策略 */
export async function batchSetPolicy(
  fileCodes: string[], enterpriseCode: string, policy: AnalyzePolicyKey,
  reason?: PolicyReasonKey | null, reanalyze = false,
): Promise<{ Total: number; SuccessCount: number; FailedCount: number; Failed: string[] }> {
  const res = await yzhApi.post<any>(`${BASE}/policy/batch`, {
    FileCodes: fileCodes, EnterpriseCode: enterpriseCode,
    AnalyzePolicy: policy, PolicyReason: reason, Reanalyze: reanalyze,
  })
  return unwrap(res, { Total: 0, SuccessCount: 0, FailedCount: 0, Failed: [] })
}

// ═══════════════════════ 五、画像与人工修正（D6）═══════════════════════

/** 取最新画像 */
export async function fetchProfile(fileCode: string, enterpriseCode: string): Promise<DocProfile | null> {
  const res = await yzhApi.get<any>(`${BASE}/profile/${fileCode}`, { enterpriseCode })
  const d = unwrap(res, null as any)
  return (d?.Profile as DocProfile) ?? null
}

/**
 * 人工修正画像（D6 全量编辑）。
 * ⛔ 标签值必须 ∈ `cert_tag_dict.TagCode`，否则后端拒绝（标签漂移 → 召回退化）。
 * 未提交的字段沿用上一版；提交后写 `ProfileVersion+1`（旧版保留可审计）。
 */
export async function correctProfile(payload: {
  FileCode: string
  EnterpriseCode: string
  TagsJson?: string | null
  TagsReason?: string | null
  TagsConfidence?: number | null
  DocPurpose?: string | null
  DocPurposeConfidence?: number | null
  InfoItemsJson?: string | null
  AnalyzePolicy?: AnalyzePolicyKey | null
  PolicyReason?: PolicyReasonKey | null
}): Promise<{ ProfileVersion: number }> {
  const res = await yzhApi.post<any>(`${BASE}/profile/correct`, payload)
  return unwrap(res, { ProfileVersion: 0 })
}

/** 受控标签清单（`cert_tag_dict` 全量，供标签下拉/筛选/分组视图） */
export async function fetchTags(applicableSide = 'enterprise'): Promise<TagItem[]> {
  const res = await yzhApi.post<any>('/api/Admin/Workflow/TagDict/list', { ApplicableSide: applicableSide })
  return (res?.data?.Rows as TagItem[]) ?? []
}

/** 受控标签清单（按分组聚合，供「语义分组」侧栏渲染） */
export async function fetchTagGroups(applicableSide = 'enterprise'): Promise<Array<{ TagGroup: string; Count: number }>> {
  const res = await yzhApi.post<any>('/api/Admin/Workflow/TagDict/list', { ApplicableSide: applicableSide })
  return (res?.data?.Groups as Array<{ TagGroup: string; Count: number }>) ?? []
}

// ═══════════════════════ 六、下载 / 预览 ═══════════════════════

/** 下载（带鉴权取 Blob）—— ⛔ 禁 window.open / iframe src */
export async function originalDownloadBlob(storagePath: string, enterpriseCode?: string): Promise<Blob> {
  return yzhApi.getBlob(`${BASE}/download`, enterpriseCode ? { storagePath, enterpriseCode } : { storagePath })
}

/** 预览 PDF 产物（无产物时后端回落源文件：PDF/图片透传场景） */
export async function originalPreviewBlob(fileCode: string, enterpriseCode: string): Promise<Blob> {
  return yzhApi.getBlob(`${BASE}/file-preview/${fileCode}`, { enterpriseCode })
}

/** 预览 Markdown 产物 */
export async function originalMarkdownBlob(fileCode: string, enterpriseCode: string): Promise<Blob> {
  return yzhApi.getBlob(`${BASE}/file-markdown/${fileCode}`, { enterpriseCode })
}

// ═══════════════════════ 七、展示辅助 ═══════════════════════

export const CONVERT_STATUS_TEXT: Record<ConvertStatusKey, string> = {
  none: '未转换',
  pending: '待转换',
  converting: '转换中',
  completed: '已完成',
  failed: '失败',
  unsupported: '需人工填写',
}

/** 转换状态 → el-tag 类型（⚠️ YzhStatusBadge 只是语义色板，映射表必须页面自建） */
export const CONVERT_STATUS_TAG: Record<ConvertStatusKey, string> = {
  none: 'info',
  pending: 'info',
  converting: 'warning',
  completed: 'success',
  failed: 'danger',
  unsupported: 'warning',
}

export const ANALYZE_STATUS_TEXT: Record<AnalyzeStatusKey, string> = {
  pending: '待分析',
  analyzing: '分析中',
  analyzed: '已分析',
  failed: '分析失败',
  skipped: '已跳过',
}

export const ANALYZE_STATUS_TAG: Record<AnalyzeStatusKey, string> = {
  pending: 'info',
  analyzing: 'warning',
  analyzed: 'success',
  failed: 'danger',
  skipped: 'info',
}

export const POLICY_TEXT: Record<AnalyzePolicyKey, string> = {
  analyze: '分析',
  skip: '跳过分析',
  ignore: '忽略',
}

export const POLICY_REASON_TEXT: Record<PolicyReasonKey, string> = {
  covered_by_params: '值已在全局参数定义',
  irrelevant: '与企业体系无关',
  duplicate: '与其他文件重复',
  manual: '人工判定',
}

/** 字节数格式化 */
export function formatSize(bytes?: number | null): string {
  if (!bytes || bytes <= 0) return '—'
  const units = ['B', 'KB', 'MB', 'GB']
  let v = bytes
  let i = 0
  while (v >= 1024 && i < units.length - 1) { v /= 1024; i++ }
  return `${v.toFixed(i === 0 ? 0 : 1)} ${units[i]}`
}