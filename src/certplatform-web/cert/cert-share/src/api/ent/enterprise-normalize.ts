/**
 * 企业资料规范化 API（专家端 `/enterprise-normalize`）
 *
 * 后端：`CertPlatform.Auditor/Controllers/EnterpriseNormalizeController.cs`
 * 路由前缀：`/api/Auditor/EnterpriseNormalize`
 * 规格：`docs/20-体系认证/03-详细设计/05-企业资料规范化/{54,55,60}`
 *
 * ⚠️ **与 `enterprise-original.ts` 的分工（⛔ 不要合并）**：
 *   · `enterprise-original` = 企业**原始资料**（输入素材，标准无关，落 `enterprise-original-source/`）
 *   · 本文件 = 把原始资料**规范化成标准文档**（输出产物 + 取值账本，落 `enterprise-documents/`）
 *
 * 字段口径（AGENTS.md ③）：
 *   · 信封 **camelCase**（已登记例外 E1：`success` / `message` / `err` / `data` / `code`）
 *   · 业务字段 **PascalCase 逐字一致**（`StandardFileCode` / `FillCompletion` / `IsLocked` …）
 *   ⇒ 写成 `res.data.standardFileCode` ⇒ `undefined` ⇒ 页面全空且**零报错**。
 *
 * ⚠️ **函数命名约定（防 TS2308）**：本文件与同目录另两个文件由 `index.ts` 同级 `export *` 导出，
 *    导出名**不得重复**（重名 ⇒ `TS2308 Module has already exported a member`）。
 *
 * ⚠️ **信封判定唯一（铁律 F-1/F-2）**：一律走 `unwrapOk` —— 业务失败**抛 `BizError`**，
 *    ⛔ 不返回 null/false 给页面层。
 *
 * ★★ 2026-10-06 用户裁决（**推翻本文件此前的注释口径**）：
 *    「**明明是错误，又返回成功**，success flag 就是反映后端到底是否执行成功的」。
 *    此前把「全库没有已发布模板」当成 `success=true` 的「正常业务态」是**错的** ——
 *    那属于 `22-接口返回规范-V1.md` §B01「无条件包成功」/ §B02「错误文本进 data」，
 *    两者都是明令禁止的**假成功**。现在它由后端返回 `success=false` + `err`，
 *    前端 `unwrapOk` 抛 `BizError`，页面按「业务拒绝」处理（弹红 + 页面持久展示原因）。
 */
import { yzhApi, unwrapOk, type ApiResponse } from '@yzh-core'

const BASE = '/api/Auditor/EnterpriseNormalize'

// ═══════════════════════ 类型 ═══════════════════════

/**
 * 规范化范围行 —— 一行 = 一份「**配了填写规则且已发布**」的空白文档。
 *
 * ★ 范围口径（`60` §六之补三）：**以「配了填写规则的空白文档」为准**，⛔ 不是全部标准文件
 * （168 份标准原始文件里只有 2 份配了规则 ⇒ 范围 = 2）。
 */
export interface NormalizeScopeItem {
  /** ★ 标准域行 Code —— 编排器的目标标识（⛔ 不是企业侧实例行） */
  StandardFileCode: string
  FileName: string
  FolderPath: string
  TemplateCode: string
  /**
   * ★ 标准 Code（GUID）—— 调 `fill-one` 必传。
   * ⚠️ 前端**拼不出来**（一行范围里没有标准信息），必须由后端随行下发；
   *    漏传 ⇒ 编排器查画像过滤不到 ⇒ **静默零填充**（不报错，只是填不进去）。
   */
  StandardCode: string
  /** 阶段 Code（GUID）—— 模板自身声明的阶段（可能为空 = 不限） */
  StageCode: string
  /** `draft` / `scanned` / `ready` / `published` */
  TemplateStatus: string
  /** `none` / `pending` / `filling` / `filled` / `confirmed` / `archived` */
  InstanceState: string
  /** ★ 文件级锁（L2）—— 锁定后任何生成路径不得覆盖 */
  IsLocked: boolean
  /** 完成率 0~1；锚点为 0 时记 0（⛔ 不是 1） */
  FillCompletion: number
  /** 加权可信度 0~1（只对 filled 行求均值） */
  FillConfidence: number
  FillAnchorCount: number
  FillPendingCount: number
  OutputPath: string | null
  NormalizedTime: string | null
}

/**
 * 一处「没填上」的待办（对齐后端 `OfficeFillPending`，`Shared/Office/OfficeFillModels.cs`）。
 *
 * ⚠️ `Reason` 是**后端归因后**的人话（`DocumentFillOrchestrator.DiagnoseNoValueAsync`），
 * ⛔ 不是写入器的笼统「未提供值」—— 它直接告诉你**下一步去哪儿修**。
 */
export interface NormalizePending {
  /** 原文 token，如 `{{文件控制程序}}` */
  Token: string
  /** 锚点键（不含花括号），如 `文件控制程序` */
  AnchorCode: string
  /**
   * 位置类别 —— ⚠️ **是字符串，不是数字**。
   *
   * 后端 `FillLocationKind` 虽是 C# enum（`BodyParagraph=0` / `TableCell=1` …），
   * 但全站 JSON 序列化器带 `JsonStringEnumConverter` ⇒ 实际输出 `"TableCell"`。
   * ⛔ 别照 C# 声明写成 `number`（写错不会报错，只会在取值时静默 `undefined`）。
   * 取值：`BodyParagraph` / `TableCell` / `Header` / `Footer` / `ExcelCell`。
   */
  LocationKind: string
  /** 可读位置，如「表格[0] 行 2 列 3」 */
  Location: string
  /** ★ 归因：模板没配数据源 / 参数未在后台定义 / 企业未填值 / 来源链未命中 */
  Reason: string
}

/** 单文件规范化结果（对齐后端 `FillOneResult`，`NormalizeModels.cs`） */
export interface NormalizeFillOneResult {
  /** `Status ∈ {filled, partial, skipped_*}` ⇒ true；`failed` ⇒ false */
  Success: boolean
  /** 六态：filled / partial / skipped_locked / skipped_no_template / skipped_no_anchor / failed */
  Status: string
  Message: string | null
  /** 产物路径（MinIO，`enterprise-documents/…`）；跳过/失败时为空 */
  OutputPath: string | null
  /** 文件级完成率 0~1；分母 0 ⇒ 0 */
  Completion: number
  /** 文件级加权可信度 0~1 */
  Confidence: number
  AnchorCount: number
  PendingCount: number
  /** 自验收：无残留锚点且无残留标记 */
  Verified: boolean
  /** 本次写入 `cert_doc_fill_value` 的行数 */
  LedgerRows: number
  FillLogCode: string | null
  /** 非致命问题清单（越界 / 丢弃 / 未写入）—— ⛔ 不静默 */
  Warnings: string[]
  /**
   * ★ 待办明细（含可操作归因）—— ⛔ 不能只看 `PendingCount`。
   *
   * 同一个「待办 1 个」背后有四种原因，指向四个**不同**的修复动作
   * （模板没配数据源 / 参数未在后台定义 / 企业没填值 / 企业档案字段为空）。
   * ⚠️ 同一 token 在文档里出现 N 次 ⇒ 本数组有 N 条（各带 `Location`），共享同一句 `Reason`。
   */
  Pendings: NormalizePending[]
}

/** 范围列表返回（`data` 载荷）—— ⚠️ 只表达「后端执行成功了」这一种情况 */
export interface NormalizeScopeList {
  items: NormalizeScopeItem[]
  total: number
}

// ═══════════════════════ 一、范围列表（只读） ═══════════════════════

/**
 * 列出规范化范围 —— 驱动源 = `cert_doc_template`（INNER JOIN 标准域行 + 企业侧实例行）。
 *
 * @param enterpriseCode 企业 Code（必填）
 * @param standardCode   标准 Code（GUID，可选；空 = 不限）
 * @param stageCode      阶段 Code（GUID，可选；空 = 不限）
 * @throws BizError 未选企业 / 工作区无法定位等业务失败
 */
export async function fetchNormalizeList(
  enterpriseCode: string,
  standardCode?: string,
  stageCode?: string,
): Promise<NormalizeScopeList> {
  const res = await yzhApi.get<ApiResponse<NormalizeScopeList>>(`${BASE}/list`, {
    enterpriseCode,
    standardCode: standardCode ?? '',
    stageCode: stageCode ?? '',
  })
  return unwrapOk<NormalizeScopeList>(res, '读取规范化范围失败')
}

// ═══════════════════════ 二、单文件同步执行 ═══════════════════════

/**
 * ★ **同步规范化单个标准文件** —— 试跑 / 单文件测试入口。
 *
 * ⚠️ 一个文件一个请求，⛔ 不做范围展开、⛔ 不入队（范围展开与队列化见 `planNormalize` / `runNormalize`）。
 * ⚠️ 后端串行调 LLM + Office 写入，**耗时可能到分钟级** ⇒ 调用方须给 loading 态。
 *
 * @throws BizError 请求参数缺失 / 工作区无法定位
 */
export async function fillOneNormalize(payload: {
  EnterpriseCode: string
  StandardCode: string
  StageCode: string
  /** ★ 标准域行 Code（`cert_doc_template.StandardFileCode` 指向的那一行） */
  StandardFileCode: string
}): Promise<NormalizeFillOneResult> {
  const res = await yzhApi.post<ApiResponse<NormalizeFillOneResult>>(`${BASE}/fill-one`, payload)
  return unwrapOk<NormalizeFillOneResult>(res, '规范化执行失败')
}

// ═══════════════════════ 三、范围树（标准 › 文件夹 › 文件） ═══════════════════════

/**
 * 范围树 · 文件节点 —— 一行 = 一份**可规范化**的标准文件。
 *
 * ★ 口径（用户 2026-10-07 裁决）：**未配填写规则的标准文件不显示** ——
 *   所以本节点只会出现在「已配规则且已发布」的文件上，⛔ 不会出现「灰显的不可用文件」。
 */
export interface NormalizeFileNode {
  /** ★ 标准域行 Code —— 入队载荷与编排器的目标标识（⛔ 不是企业侧实例行） */
  StandardFileCode: string
  FileName: string
  FolderCode: string
  FolderPath: string
  TemplateCode: string
  StandardCode: string
  StageCode: string
  /** `draft` / `scanned` / `ready` / `published`（本树内恒为 `published`） */
  TemplateStatus: string
  /** `none` / `pending` / `filling` / `filled` / `confirmed` / `archived` */
  InstanceState: string
  /** ★ 文件级锁（L2）—— 锁定后任何生成路径不得覆盖 */
  IsLocked: boolean
  FillCompletion: number
  FillConfidence: number
  FillAnchorCount: number
  FillPendingCount: number
  OutputPath: string | null
  NormalizedTime: string | null
  /** 最近一次留痕状态：`success` / `partial` / `failed`；无留痕为 null */
  LastFillStatus: string | null
  /** 最近一次留痕时间（⚠️ 后端该列是 UTC，与 `NormalizedTime`（本地）有 8h 时差，⛔ 不要并排显示） */
  LastFillTime: string | null
  /**
   * ★ 最近一次填充的**待办明细（含可操作归因）** —— ⛔ 别只显示 `FillPendingCount` 那个数字。
   *
   * 同一个「待办 1 个」背后有四种原因，指向四个**不同**的修复动作
   * （模板没配数据源 / 参数未在后台定义 / 参数曾被裁决移除 / 企业没填值）。
   *
   * 来源 = 最近一条 `cert_doc_fill_log.PendingsJson`（后端**零额外查询**带出）。
   * ⚠️ 同一 token 在文档里出现 N 次 ⇒ 本数组有 N 条（各带 `Location`）。
   * ⚠️ 后端解析失败会退化为**空数组**（⛔ 不会让整棵树打不开）⇒ 页面必须容忍 `length === 0`。
   */
  LastFillPendings: NormalizePending[]
}

/** 范围树 · 文件夹节点 */
export interface NormalizeFolderNode {
  FolderCode: string
  FolderName: string
  ParentCode: string
  Depth: number
  SortOrder: number
  FullPath: string
  /** ★ 该文件夹（含子文件夹）下标准域文件总数 —— ⛔ 不是可规范化数，用于解释「为什么只有这几个」 */
  TotalFileCount: number
  /** ★ 该文件夹（含子文件夹）下**可规范化**文件数（0 ⇒ 整枝不可勾选） */
  FillableCount: number
  /** 直属本文件夹的可规范化文件 */
  Files: NormalizeFileNode[]
  Children: NormalizeFolderNode[]
}

/** 范围树 · 标准节点（= 页面上的一个 Tab） */
export interface NormalizeStandardNode {
  StandardCode: string
  /** 标准编号（如 `iso9001-2015`） */
  StandardNo: string
  /** 标准名称（如 `9001标准`） */
  StandardName: string
  VersionYear: number
  Sort: number
  /** 该企业该阶段是否挂了本标准（`cert_enterprise_stage`）—— false = 只是配了模板但企业没勾选 */
  Mounted: boolean
  FillableCount: number
  TotalFileCount: number
  Folders: NormalizeFolderNode[]
  /** 不在任何文件夹下的可规范化文件 */
  RootFiles: NormalizeFileNode[]
}

/** 范围树返回（`data` 载荷） */
export interface NormalizeTreeResult {
  standards: NormalizeStandardNode[]
  totalStandards: number
  totalFillable: number
  totalFiles: number
}

/**
 * ★ **范围树** —— 「标准 › 文件夹 › 文件」三级展开。
 *
 * ★ 为什么必须有它（⛔ 不能用 `fetchNormalizeList` 代替）：
 *   扁平清单回答不了用户真正的问题 ——「**这个阶段下，哪些标准的哪些文件夹里，有可以规范化的文件**」。
 *   范围驱动源是模板（全库 168 份标准文件里只有 2 份配了规则）⇒ 扁平清单会让用户误判成
 *   「这个阶段只有 2 个文件」。
 *
 * @throws BizError 未选企业 / 全库还没有已发布的空白模板（业务拒绝）
 */
export async function fetchNormalizeTree(
  enterpriseCode: string,
  stageCode: string,
): Promise<NormalizeTreeResult> {
  const res = await yzhApi.get<ApiResponse<NormalizeTreeResult>>(`${BASE}/tree`, {
    enterpriseCode,
    stageCode: stageCode ?? '',
  })
  return unwrapOk<NormalizeTreeResult>(res, '读取规范化范围失败')
}

// ═══════════════════════ 四、干跑 / 整批入队 ═══════════════════════

/** `plan` 与 `run` 的**同一份**入参（★ 同参是「干跑即承诺」的前提） */
export interface NormalizeScopeRequest {
  EnterpriseCode: string
  StageCode: string
  /** ★ 选中的标准域行 Code 清单（`cert_standard_directory_file.Code`） */
  StandardFileCodes: string[]
}

/**
 * 干跑明细一行。
 *
 * `Action` 五态：`fill`（首次规范化）｜`regenerate`（重新生成）｜
 * `skip_locked`｜`skip_no_template`｜`skip_no_anchor`
 */
export interface NormalizePlanItem {
  StandardFileCode: string
  FileName: string
  Action: string
  /** 人话说明（⛔ 不是编码 —— 直接显示） */
  Reason: string
  AnchorCount: number
  IsLocked: boolean
  HasOutput: boolean
}

/** 干跑结果 —— 纯读、零副作用 */
export interface NormalizePlanResult {
  WillFill: number
  WillRegenerate: number
  SkipLocked: number
  SkipNoTemplate: number
  SkipNoAnchor: number
  Total: number
  /** ★ 真正会入队的数量 = WillFill + WillRegenerate */
  Queued: number
  Items: NormalizePlanItem[]
}

/** 入队结果 —— 只回批次号，⛔ 不阻塞等待执行 */
export interface NormalizeRunResult {
  QueueCode: string
  Queued: number
  Skipped: number
  Items: NormalizePlanItem[]
}

/**
 * ★ **干跑（dry-run）** —— 回答「选中这批文件，真跑会发生什么」。
 *
 * ★ 为什么它必须是「点执行前的一步」：规范化是**覆盖性**操作（重新生成会覆盖企业侧已有产物）。
 *   没有干跑，用户点「一键规范化」就是盲签 —— 尤其「已锁定 / 未配规则 / 无锚点」这三类会被
 *   静默跳过，跑完只看到数字对不上。
 *
 * ⛔ 零副作用：不写库 / 不产文件 / 不调 LLM / 不入队。
 *
 * @throws BizError 未选企业 / 全库还没有已发布的空白模板
 */
export async function planNormalize(payload: NormalizeScopeRequest): Promise<NormalizePlanResult> {
  const res = await yzhApi.post<ApiResponse<NormalizePlanResult>>(`${BASE}/plan`, payload)
  return unwrapOk<NormalizePlanResult>(res, '干跑预览失败')
}

/**
 * ★ **整批入队** —— 把选中的文件投成一条 `enterprise_normalize` 队列。
 *
 * ⚠️ 服务端会**按当前事实重算一遍范围**（⛔ 不信任本清单）：页面可能已停留很久，
 *   期间模板可能被取消发布、文件可能被锁定 ⇒ 若重算后无可执行项，后端返回**业务拒绝**。
 * ⚠️ 同企业同阶段同时只允许一条运行中的规范化队列，重复调用会被拒。
 *
 * @throws BizError 未选阶段 / 无可规范化文件 / 已有运行中队列 / 队列创建失败
 */
export async function runNormalize(payload: NormalizeScopeRequest): Promise<NormalizeRunResult> {
  const res = await yzhApi.post<ApiResponse<NormalizeRunResult>>(`${BASE}/run`, payload)
  return unwrapOk<NormalizeRunResult>(res, '入队失败')
}

/** 干跑动作 → 人话 */
export const PLAN_ACTION_TEXT: Record<string, string> = {
  fill: '将规范化',
  regenerate: '将重新生成',
  skip_locked: '跳过（已锁定）',
  skip_no_template: '跳过（未配规则）',
  skip_no_anchor: '跳过（模板无锚点）',
}

/** 干跑动作 → 徽标语义色（`YzhStatusBadge` 的 type） */
export const PLAN_ACTION_TYPE: Record<string, string> = {
  fill: 'success',
  regenerate: 'warning',
  skip_locked: 'info',
  skip_no_template: 'info',
  skip_no_anchor: 'info',
}

// ═══════════════════════ 五、展示辅助（⛔ 不做状态判断，只做文案映射） ═══════════════════════

/**
 * 六态 → 人话（`54` §3.4：失败原因要人话）。
 * ⛔ 刻意**不用** `{ text, type }` 单表：`type: 'info'|'success'|'warning'` 会命中样式法条 S03。
 */
export const FILL_STATUS_TEXT: Record<string, string> = {
  filled: '已规范化',
  partial: '部分完成（有待办）',
  skipped_locked: '已锁定，跳过',
  skipped_no_template: '无已发布模板，跳过',
  skipped_no_anchor: '无锚点，跳过',
  failed: '执行失败',
}

/** 六态 → 徽标语义色（`YzhStatusBadge` 的 type） */
export const FILL_STATUS_TYPE: Record<string, string> = {
  filled: 'success',
  partial: 'warning',
  skipped_locked: 'info',
  skipped_no_template: 'info',
  skipped_no_anchor: 'info',
  failed: 'danger',
}

/** 模板发布态 → 人话 */
export const TEMPLATE_STATUS_TEXT: Record<string, string> = {
  draft: '草稿',
  scanned: '已扫描（未发布）',
  ready: '待发布',
  published: '已发布',
}

/** 模板发布态 → 徽标语义色 */
export const TEMPLATE_STATUS_TYPE: Record<string, string> = {
  draft: 'info',
  scanned: 'info',
  ready: 'warning',
  published: 'success',
}

/** 企业侧实例态 → 人话 */
export const INSTANCE_STATE_TEXT: Record<string, string> = {
  none: '尚未规范化',
  pending: '待规范化',
  filling: '规范化中',
  filled: '已生成',
  confirmed: '已确认',
  archived: '已归档',
}

/** 企业侧实例态 → 徽标语义色 */
export const INSTANCE_STATE_TYPE: Record<string, string> = {
  none: 'info',
  pending: 'info',
  filling: 'warning',
  filled: 'success',
  confirmed: 'success',
  archived: 'info',
}

/** 0~1 小数 → 百分比整数（⛔ 后端可能给 decimal 字符串，必须先 Number()） */
export function toPercent(v: unknown): number {
  const n = Number(v)
  if (!Number.isFinite(n) || n <= 0) return 0
  if (n >= 1) return 100
  return Math.round(n * 100)
}
