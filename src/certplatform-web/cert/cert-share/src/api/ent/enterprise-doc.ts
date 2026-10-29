/**
 * 企业文档 API（专家端 `/resources`）—— **上传模块重写版**
 *
 * 后端：`CertPlatform.Auditor/Controllers/EnterpriseDocController.cs`
 * 路由前缀：`/api/Auditor/EnterpriseDoc`
 * 依据：`docs/70-V2设计/36-企业资料上传重写-V1.md`（§6.3 落地实现）
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★★ 与 `enterprise-original.ts` 是**两代实现**，⛔ 不是同一套
 *
 *   · `enterprise-original.ts`（老）= 企业 → 阶段 → 标准 → 槽位 四层，
 *     每个端点都要 `StageCode`，上传还要 `StandardCode`；
 *   · **本文件（新）= 只有「企业」一层**：任何端点都不收 `StageCode`，
 *     上传端点也不收 `StandardCode`（标准只在「提取」阶段出现）。
 *
 *   ⇒ 2026-10-29 用户裁定：一阶段与二阶段的资料是**同一批文件**，
 *     老实现让同一份文件在两个阶段下重复上传/重复转换/提取结果分叉。
 *   ⇒ 老文件保留到 P5 迁移完成后删除，⛔ 不要在新页面里混用两者。
 * ════════════════════════════════════════════════════════════════════════
 *
 * ★ 标准从哪来：**唯一入口 = 当前有效认证计划**（`cert_audit_plan`，一企业一有效计划）。
 *   前端只调 {@link entDocPlanStandards}，⛔ 不许自己查计划表再拆 `Standards` 字段。
 *
 * ★ 字段口径（AGENTS.md ③）：**DB 列名 PascalCase 逐字一致**。
 *   后端返回 `{ Configured, Nodes }` / `{ Success, Rows, Total }` / `{ TaskId, Items }`，
 *   这里必须写 `res.data.Nodes` / `res.data.Rows`。写成小写 ⇒ `undefined` ⇒
 *   页面全空且**零报错**（2026-10-03 实测踩到过）。
 *
 * ⛔ 禁止 `window.open` / `iframe src` 直连下载与预览：本平台鉴权走 Authorization 头，
 *    裸链接必然 401。一律走 `yzhApi.getBlob` 取字节再交渲染器 / 触发保存。
 */
import { yzhApi, unwrap } from '@yzh-core'
import type { ApiResponse } from '@yzh-core'

const BASE = '/api/Auditor/EnterpriseDoc'

/*
 * ⚠️ **命名前缀 `entDoc`（防 TS2308）**：本文件与 `enterprise-original.ts` /
 *    `enterprise-file.ts` / `enterprise-normalize.ts` 由 `index.ts` 同级 `export *` 导出，
 *    导出名**不得重复**（重名 ⇒ `TS2308 Module has already exported a member`）。
 *    老文件的通用动词是 `fetchFiles` / `removeFile` / `originalUploadInit`…，
 *    新文件一律 `entDoc*` 前缀，避免与任何一代撞名。
 */

// ═══════════════════════ 类型 ═══════════════════════

/**
 * 转换 / Markdown 状态取值。
 *
 * ⚠️ 与 `ConvertStatus`（C#）一一对应，⛔ 不要自造 `success` / `done`：
 * 后端写的是 `completed`，前端判 `success` ⇒ 永远显示「未开始」。
 */
export type EntDocConvertStatus =
  | 'none' | 'pending' | 'converting' | 'completed' | 'failed' | 'unsupported'

/**
 * 提取聚合状态（`cert_enterprise_doc.ExtractStatus`）。
 *
 * - `none` = 还没有任何提取产物
 * - `pending` = 已入队未开始
 * - `running` = 至少一个标准在跑
 * - `partial` = 部分标准完成（其余待跑 / 失败）
 * - `done` = 计划内标准全部完成
 * - `failed` = 全部失败
 * - `skipped` = 没有可提取内容（无计划 / 缺 Markdown），**不是错误**
 */
export type EntDocExtractStatus =
  | 'none' | 'pending' | 'running' | 'partial' | 'done' | 'failed' | 'skipped'

/** 计划里的标准（`cert_iso_standard` 的一行） */
export interface EntDocStandardRef {
  /** 标准业务键（GUID）—— 调提取 / 按条款反查时传的就是它 */
  Code: string
  /** 标准编号短码（`iso13485`），只用于展示与提示词作用域 */
  StandardCode: string
  StandardName: string
  VersionYear?: number
}

/** 左树节点（**企业一层**，`Children` 恒为空数组） */
export interface EntDocNode {
  Code: string
  /** `YzhTree` 的 labelField（后端双写 Code/Label/Name，传哪个都能显示） */
  Label: string
  Name: string
  EnterpriseCode: string
  IsEnterpriseNode: true
  FileCount: number
  ConvertedCount: number
  ConvertingCount: number
  FailedCount: number
  ExtractDoneCount: number
  /** ★ 是否有**有效认证计划** —— 没有就提取不了（标准从计划来），前端必须显式提示 */
  HasPlan: boolean
  PlanCode?: string | null
  PlanName?: string | null
  Standards: EntDocStandardRef[]
  Children: EntDocNode[]
}

/** 某文档 × 某标准的提取产物行（**不含 JSON 正文**，正文在 {@link entDocExtractions}） */
export interface EntDocExtractionRow {
  Code: string
  StandardCode: string
  /** 逗号分隔的命中条款号（`1,4.1,4.2.4`） */
  ClauseNumbers: string
  ClauseCount: number
  ExtractStatus: EntDocExtractStatus
  ExtractMessage?: string | null
  ExtractedAt?: string | null
  /** ★ 源文件已变更（替换 / 回滚）⇒ 本行结果不可信，等重跑 */
  IsStale: boolean
  PromptCode?: string | null
  PromptVersion?: string | null
}

/** 文件行（`cert_enterprise_doc`） */
export interface EntDocRow {
  Code: string
  EnterpriseCode: string
  /** 相对文件夹路径（`1 体系文件/程序文件`）；空串 = 根目录 */
  RelFolderPath: string
  FileName: string
  FileType: string
  FileSize: number
  Sha256: string
  StoragePath: string
  VersionNumber: number
  ConvertStatus: EntDocConvertStatus
  ConvertMessage?: string | null
  MarkdownStatus: EntDocConvertStatus
  MarkdownMessage?: string | null
  PreviewPdfPath?: string | null
  MarkdownPath?: string | null
  ConvertDate?: string | null
  ExtractStatus: EntDocExtractStatus
  ExtractMessage?: string | null
  ExtractTime?: string | null
  /** ★ 每个标准一行：抽屉直接渲染，⛔ 不另开一次请求 */
  Extractions: EntDocExtractionRow[]
  /** 计划内标准中「还没有提取产物」的那几个 ⇒ 显示「待提取」 */
  MissingStandardCodes: string[]
  /** ★ 可提取 = Markdown 就绪（提取的唯一输入）；⛔ 不看 ConvertStatus */
  CanExtract: boolean
  CanPreview: boolean
}

/** 列表响应（含计划信息，前端不必再单独请求一次） */
export interface EntDocListResult {
  rows: EntDocRow[]
  /** 过滤前口径（用于「共 N 个文件，其中符合筛选 M 个」） */
  total: number
  plan: EntDocPlanInfo | null
}

export interface EntDocPlanInfo {
  Code: string
  Name: string
  /** 逗号分隔的标准 Code（GUID） */
  Standards: string
  StandardCodes: string[]
}

/** 状态条 */
export interface EntDocStatusBar {
  Total: number
  ConvertingCount: number
  FailedCount: number
  UnsupportedCount: number
  /** ★ Markdown 就绪 = 可提取 */
  ExtractableCount: number
  ExtractDoneCount: number
  ExtractFailedCount: number
  HasPlan: boolean
  PlanStandardCount: number
  QueueCode?: string | null
  QueueStatus?: string | null
  QueueType?: string | null
  QueueTotal: number
  QueueCompleted: number
  QueueProgress: number
  IsBusy: boolean
}

/** 队列明细 */
export interface EntDocQueueTask {
  Code: string
  TaskType: string
  Status: string
  ProcessTime?: string | null
  RetryCount: number
  MaxRetryCount: number
  NextRetryAt?: string | null
  ErrorType?: string | null
  /** 失败原因（后端截断 300 字） */
  Message?: string | null
  FileCode?: string | null
  FileName?: string | null
}

export interface EntDocQueueRow {
  /** `Q-2026...` 展示号（`yzh_queue_task.QueueCode` 关联的是这个） */
  Code: string
  BizCode: string
  QueueName: string
  QueueType: string
  Status: string
  CreateTime?: string | null
  StartTime?: string | null
  EndTime?: string | null
  TotalCount: number
  PendingCount: number
  ProcessingCount: number
  CompletedCount: number
  FailedCount: number
  CancelledCount: number
  Progress: number
  QueuePosition: number
  Tasks: EntDocQueueTask[]
}

export interface EntDocQueueDetail {
  RunningCount: number
  PendingCount: number
  FailedCount: number
  Rows: EntDocQueueRow[]
}

/** 上传预检行 */
export interface EntDocPlanRow {
  FileName: string
  RelFolderPath: string
  FileType: string
  FileSize: number
  Sha256: string
  /** `create` 新增 / `replace` 替换（异 hash）/ `skip` 幂等跳过（同 hash） */
  Action: 'create' | 'replace' | 'skip'
  ExistingCode?: string | null
  ExistingVersion?: number | null
  Blocked: boolean
  BlockReason?: string | null
}

export interface EntDocPlanSummary {
  Total: number
  CreateCount: number
  ReplaceCount: number
  SkipCount: number
  BlockedCount: number
  /** 系统噪声（`.DS_Store` 等）静默剔除的条数 */
  FilteredCount: number
  FilteredNames?: string[] | null
}

/** 上传项（前端 → 后端；⛔ 无 StageCode、⛔ 无 StandardCode） */
export interface EntDocPlanItem {
  FileName: string
  RelFolderPath: string
  FileSize: number
  Sha256?: string
}

/** init 返回项 */
export interface EntDocInitItem {
  FileCode: string
  FileName: string
  Action: 'create' | 'replace' | 'skip'
  StoragePath: string
}

/** 版本行 */
export interface EntDocVersionRow {
  Code: string
  VersionNumber: number
  FileName: string
  FileType: string
  FileSize: number
  Sha256: string
  StoragePath: string
  Reason?: string | null
  CreateTime?: string | null
  IsCurrent: boolean
}

/** 提取产物（含 JSON 正文，`/extraction/{fileCode}` 专用） */
export interface EntDocExtractionFull extends EntDocExtractionRow {
  SourceSha256?: string | null
  /** 原始 JSON 字符串（PascalCase：`{ DocMeta, Clauses }`） */
  ContentJson?: string | null
}

/** 提取 JSON 里的单条条款（契约见 `ClauseExtractExecutor`） */
export interface EntDocClauseHit {
  ClauseNumber: string
  Title?: string
  /** ★ 原文逐字连续片段（10–40 字）—— 下游「原文定位」必须**先归一标点再比** */
  Anchor?: string
  Excerpts?: string[]
  /** 0–1，模型自评 */
  Confidence?: number
}

/** 提取 JSON 根（`ContentJson` 解析结果） */
export interface EntDocExtractContent {
  DocMeta?: {
    FileName?: string
    StandardCode?: string
    StandardName?: string
    Model?: string
    ExtractedAt?: string
    Chunks?: number
  }
  Clauses?: EntDocClauseHit[]
}

/** 按条款反查结果（检查表阶段的取数入口） */
export interface EntDocClauseEvidence {
  FileCode: string
  EnterpriseCode: string
  StandardCode: string
  ClauseNumbers: string
  ClauseCount: number
  ExtractStatus: EntDocExtractStatus
  ExtractMessage?: string | null
  ExtractedAt?: string | null
  IsStale: boolean
  PromptCode?: string | null
  PromptVersion?: string | null
}

/** 计划标准解析结果 */
export interface EntDocPlanStandardsResult {
  plan: EntDocPlanInfo | null
  standardCodes: string[]
  /** ★ 空列表时的**人话原因**（「还没有有效认证计划」等）—— ⛔ 不静默当「无标准」 */
  reason: string | null
}

/** 提取入队结果 */
export interface EntDocExtractResult {
  Queued: number
  Skipped: number
  Message: string
}

// ═══════════════════════ 一、左树 / 列表 / 状态条 ═══════════════════════

/**
 * 统一信封（`YZH.Core.Stand/Models/Result/ApiResponse.cs`）。
 *
 * ★ 直接用内核契约类型，⛔ 不在这里另抄一份 `Payload` ——
 *   抄一份就会漏 `code` / `timestamp`，导致 `unwrap()` 参数类型不兼容
 *   （`Payload` 少两个必填字段 ⇒ TS2345）。
 */
type Payload = ApiResponse<any>

/**
 * 左树：本工作区企业（**一层，无阶段**）。
 *
 * ⚠️ `Configured=false` 是**正常状态**（工作区还没配企业），不是错误；
 * 调用方要区分「没配」与「配了但没企业」两种空态文案。
 */
export async function entDocTree(): Promise<{ configured: boolean; message: string | null; nodes: EntDocNode[] }> {
  const res = await yzhApi.post<Payload>(`${BASE}/tree`)
  const d = res?.data ?? {}
  return {
    configured: d.Configured !== false,
    message: d.Message ?? null,
    nodes: (d.Nodes as EntDocNode[]) ?? [],
  }
}

/**
 * 该企业的文档列表。
 *
 * @param standardCode 非空 = **只返回该标准下有提取产物**的文件（「按标准看覆盖」视图）。
 *   ⚠️ 这与「文件属于哪个标准」不同 —— 文件本身不属任何标准。
 */
export async function entDocList(params: {
  enterpriseCode: string
  standardCode?: string
  keyword?: string
  onlyFailed?: boolean
}): Promise<EntDocListResult> {
  const res = await yzhApi.post<Payload>(`${BASE}/list`, {
    EnterpriseCode: params.enterpriseCode,
    StandardCode: params.standardCode || '',
    Keyword: params.keyword || '',
    OnlyFailed: params.onlyFailed ?? false,
  })
  const d = res?.data ?? {}
  return {
    rows: (d.Rows as EntDocRow[]) ?? [],
    total: d.Total ?? 0,
    plan: (d.Plan as EntDocPlanInfo) ?? null,
  }
}

/** 状态条：转换 / 提取计数 + 是否忙碌 */
export async function entDocStatusBar(enterpriseCode: string): Promise<EntDocStatusBar | null> {
  const res = await yzhApi.post<Payload>(`${BASE}/status-bar`, { EnterpriseCode: enterpriseCode })
  return res?.success ? ((res.data as EntDocStatusBar) ?? null) : null
}

/** 队列明细（转换 + 提取两个队列类型都在里面） */
export async function entDocQueueDetail(enterpriseCode: string): Promise<EntDocQueueDetail | null> {
  const res = await yzhApi.post<Payload>(`${BASE}/queue-detail`, { EnterpriseCode: enterpriseCode })
  return res?.success ? ((res.data as EntDocQueueDetail) ?? null) : null
}

/** 已生成的 Markdown **文本**（页面内核对原文用） */
export async function entDocMarkdown(fileCode: string, enterpriseCode: string): Promise<{
  success: boolean
  message: string | null
  markdown: string
  length: number
}> {
  const res = await yzhApi.get<Payload>(`${BASE}/markdown/${fileCode}`, { enterpriseCode })
  const d = res?.data ?? {}
  return {
    success: res?.success === true,
    message: d.Message ?? res?.err ?? null,
    markdown: (d.Markdown as string) ?? '',
    length: d.Length ?? 0,
  }
}

// ═══════════════════════ 二、五段式上传（无阶段、无标准） ═══════════════════════

/** Step0：预检（纯计算不落库） */
export async function entDocPlanUpload(
  enterpriseCode: string, items: EntDocPlanItem[],
): Promise<{ rows: EntDocPlanRow[]; summary: EntDocPlanSummary | null }> {
  const res = await yzhApi.post<Payload>(`${BASE}/upload/plan`, { EnterpriseCode: enterpriseCode, Items: items })
  const d = res?.data ?? {}
  return {
    rows: (d.Rows as EntDocPlanRow[]) ?? [],
    summary: (d.Summary as EntDocPlanSummary) ?? null,
  }
}

/** Step1：建批次 + 建/命中文件行，下发 `TaskId` */
export async function entDocUploadInit(
  enterpriseCode: string, items: EntDocPlanItem[],
): Promise<{ TaskId: string; Items: EntDocInitItem[]; SkipCount: number }> {
  const res = await yzhApi.post<Payload>(`${BASE}/upload/init`, { EnterpriseCode: enterpriseCode, Items: items })
  const d = unwrap(res, null as any)
  return { TaskId: d?.TaskId ?? '', Items: d?.Items ?? [], SkipCount: d?.SkipCount ?? 0 }
}

/**
 * Step2：逐文件传字节。**前端不传路径**（后端按 DB 行重算，防伪造）。
 *
 * ⚠️ `sha256` 可留空 —— 服务端收到字节会自行计算并以那个为准。
 */
export async function entDocUploadFile(
  taskId: string, fileCode: string, file: File, sha256?: string,
): Promise<{ FileCode: string; FileName: string; Action: string; VersionNumber: number; Sha256: string }> {
  const fd = new FormData()
  fd.append('TaskId', taskId)
  fd.append('FileCode', fileCode)
  if (sha256) fd.append('Sha256', sha256)
  // ⚠️ 文件名取 `webkitRelativePath` 尾段：文件夹上传时 `file.name` 在某些浏览器下带路径
  fd.append('File', file, (file as any).webkitRelativePath?.split('/').pop() || file.name)

  const res = await yzhApi.post<Payload>(`${BASE}/upload/file`, fd)
  return unwrap(res, null as any)
}

/** Step3：激活 + 入 `enterprise_doc_convert` 队列 */
export async function entDocUploadConfirm(taskId: string, enterpriseCode: string): Promise<{
  TaskId: string
  ActivatedCount: number
  ConvertCount: number
  SkipCount: number
  ReplaceCount: number
  QueueCode?: string | null
  QueueError?: string | null
}> {
  const res = await yzhApi.post<Payload>(`${BASE}/upload/confirm`, { TaskId: taskId, EnterpriseCode: enterpriseCode })
  return unwrap(res, null as any)
}

/** Step4：回滚（取消队列 + 删对象 + 撤本批次行） */
export async function entDocUploadCancel(taskId: string, enterpriseCode: string): Promise<any> {
  const res = await yzhApi.post<Payload>(`${BASE}/upload/cancel`, { TaskId: taskId, EnterpriseCode: enterpriseCode })
  return unwrap(res, null as any)
}

// ═══════════════════════ 三、删除 / 版本 / 重新转换 ═══════════════════════

/** 删除（软删 + 删对象 + **级联软删提取产物**） */
export async function entDocDelete(fileCode: string, enterpriseCode: string, reason?: string): Promise<void> {
  const res = await yzhApi.post<Payload>(`${BASE}/delete`, { FileCode: fileCode, EnterpriseCode: enterpriseCode, Reason: reason })
  unwrap(res, undefined as any)
}

/**
 * 批量删除 —— 页面上「删除整个文件夹」的落点。
 *
 * ⚠️ 只收 `FileCodes`：**「哪些文件属于这个文件夹」是前端按 `RelFolderPath` 算出的展示口径**，
 * 后端不持有「文件夹」这个概念（⛔ 不要试图传文件夹路径）。
 */
export async function entDocBatchDelete(
  fileCodes: string[], enterpriseCode: string, reason?: string,
): Promise<{ DeletedCount: number; Failed: Array<{ FileCode: string; Message: string }> }> {
  const res = await yzhApi.post<Payload>(`${BASE}/delete/batch`, {
    FileCodes: fileCodes, EnterpriseCode: enterpriseCode, Reason: reason,
  })
  return unwrap(res, { DeletedCount: 0, Failed: [] })
}

/**
 * 版本时间线。
 *
 * ⚠️ 失败**抛 `BizError`**（同 {@link entDocMarkdown}：R13 禁裸 `if (!res.success)`）。
 */
export async function entDocVersions(fileCode: string, enterpriseCode: string): Promise<{
  fileName: string
  currentVersion: number
  rows: EntDocVersionRow[]
}> {
  const res = await yzhApi.get<Payload>(`${BASE}/versions/${fileCode}`, { enterpriseCode })
  const d = unwrap(res, { FileName: '', CurrentVersion: 0, Rows: [] } as any)
  return {
    fileName: d?.FileName ?? '',
    currentVersion: d?.CurrentVersion ?? 0,
    rows: (d?.Rows as EntDocVersionRow[]) ?? [],
  }
}

/**
 * 回滚到历史版本。
 *
 * ⚠️ **版本号不回退**：回滚是「又改了一次」，版本继续 +1（与 git revert 同语义）。
 */
export async function entDocRestore(
  fileCode: string, enterpriseCode: string, versionNumber: number, reason?: string,
): Promise<any> {
  const res = await yzhApi.post<Payload>(`${BASE}/restore`, {
    FileCode: fileCode, EnterpriseCode: enterpriseCode, VersionNumber: versionNumber, Reason: reason,
  })
  return unwrap(res, null as any)
}

/** 重新转换（重跑 PDF + Markdown 两链），不动源文件、不改版本号 */
export async function entDocRegenerate(
  fileCode: string, enterpriseCode: string, reExtract = true,
): Promise<any> {
  const res = await yzhApi.post<Payload>(`${BASE}/regenerate`, {
    FileCode: fileCode, EnterpriseCode: enterpriseCode, ReExtract: reExtract,
  })
  return unwrap(res, null as any)
}

// ═══════════════════════ 三之二、按标准提取条款（P2） ═══════════════════════

/**
 * 入队「按标准提取条款」。
 *
 * @param fileCodes 给 = 只跑这几份；不给 = 整家企业。
 *
 * ★ **可以反复点**：后端只入队「真需要跑的」（Markdown 就绪 + 该标准未完成 / 已过期 / hash 变了），
 *   已完成的进 `Skipped` 计数。
 * ⚠️ 没有有效计划 ⇒ `success=true` 但 `Queued=0` 且 `Message` 说清原因
 *   （⛔ 不是失败：这是「还没建计划」，不该弹红字）。
 */
export async function entDocExtract(
  enterpriseCode: string, fileCodes?: string[],
): Promise<EntDocExtractResult> {
  const res = await yzhApi.post<Payload>(`${BASE}/extract`, {
    EnterpriseCode: enterpriseCode,
    FileCodes: fileCodes ?? null,
  })
  const d = res?.data ?? {}
  return {
    Queued: d.Queued ?? 0,
    Skipped: d.Skipped ?? 0,
    Message: d.Message ?? res?.err ?? res?.message ?? '',
  }
}

/**
 * ★ **「这个条款有哪些文档能作证」** —— 按条款号反查提取产物。
 *
 * @param standardCode 标准 **Code（GUID）**，不是短码
 * @param clauseNumber 条款号（如 `4.2.4`）；后端走 `FIND_IN_SET` 精确匹配
 *   （⛔ 不是 `LIKE`：`4.2.4` 不会误中 `4.2.45`）
 */
export async function entDocExtractionsByClause(
  standardCode: string, clauseNumber: string, onlyDone = true,
): Promise<EntDocClauseEvidence[]> {
  const res = await yzhApi.get<Payload>(`${BASE}/extraction/by-clause`, {
    standardCode, clauseNumber, onlyDone,
  })
  return (res?.data as EntDocClauseEvidence[]) ?? []
}

/** 某文档的提取产物行（**含 JSON 正文**，抽屉渲染用） */
export async function entDocExtractions(fileCode: string): Promise<EntDocExtractionFull[]> {
  const res = await yzhApi.get<Payload>(`${BASE}/extraction/${fileCode}`)
  return (res?.data as EntDocExtractionFull[]) ?? []
}

// ═══════════════════════ 四、标准来源（唯一入口） ═══════════════════════

/**
 * 取当前有效计划 + 标准 Code 列表。
 *
 * ★ **这是「标准从哪来」的唯一答案** —— 前端与提取编排都必须走这里。
 */
export async function entDocPlanStandards(enterpriseCode: string): Promise<EntDocPlanStandardsResult> {
  const res = await yzhApi.get<Payload>(`${BASE}/plan-standards`, { enterpriseCode })
  const d = res?.data ?? {}
  return {
    plan: (d.Plan as EntDocPlanInfo) ?? null,
    standardCodes: (d.StandardCodes as string[]) ?? [],
    reason: d.Reason ?? null,
  }
}

// ═══════════════════════ 五、下载 / 预览（必须走 Blob） ═══════════════════════

/** 下载原件（按存储路径取流；路径由后端校验必须位于已知文档库之下） */
export async function entDocDownload(
  storagePath: string, enterpriseCode: string,
): Promise<Blob> {
  return yzhApi.getBlob(`${BASE}/download`, { storagePath, enterpriseCode })
}

/** PDF 预览字节（`PreviewPdfPath` 就绪才有；未就绪后端返回 JSON 信封 ⇒ getBlob 会抛） */
export async function entDocPreviewBlob(fileCode: string, enterpriseCode: string): Promise<Blob> {
  return yzhApi.getBlob(`${BASE}/file-preview/${fileCode}`, { enterpriseCode })
}

/** Markdown 产物字节（走文件流，适合大文件；小文件核对可用 {@link entDocMarkdown}） */
export async function entDocMarkdownBlob(fileCode: string, enterpriseCode: string): Promise<Blob> {
  return yzhApi.getBlob(`${BASE}/file-markdown/${fileCode}`, { enterpriseCode })
}
