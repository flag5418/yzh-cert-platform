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
  /**
   * 最近一次留痕时间。
   *
   * ★ 2026-10-09 已修口径：`cert_doc_fill_log.CreateTime` 是 **UTC**（`BaseEntity` 赋
   * `DateTime.UtcNow`），后端现已显式标 `DateTimeKind.Utc` ⇒ JSON 带 `Z` ⇒
   * `new Date()` 能正确换算成本地时间（此前无 `Z`，被当本地解析 ⇒ **少 8 小时**）。
   *
   * ⚠️ 对照：`NormalizedTime`（实例行）由 `DateTime.Now` 写入 = **本地时间**，JSON 无 `Z`，
   * 前端按本地解析**本来就是对的**。两者现在都能直接 `formatDateTime`。
   */
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
  /** 标准编号（如 `iso9001`，年份另见 `VersionYear`）—— ⚠️ 主数据缺失时为**空串** */
  StandardNo: string
  /** 标准名称（如 `9001标准`）—— ⚠️ 主数据缺失时为**空串**，⛔ 不是 Code */
  StandardName: string
  /**
   * ★ 本标准在 `cert_iso_standard` 里**是否登记在册**（2026-10-09 新增）。
   *
   * 为什么必须有：`StandardCode` 来自 `cert_enterprise_stage`（企业阶段关联）与
   * `cert_doc_template`（模板），两者只是**引用**，不保证主数据还在。
   * 主数据被删而关联没清时，后端此前 `StandardName = iso?.StandardName ?? code`
   * ⇒ **把裸 GUID 当标准名回传** ⇒ Tab 上直接显示 `475da4fe-8f50-4bf7-bf2b-b39869d5ddf7`
   * （2026-10-09 用户报障）。
   *
   * ⇒ `false` 时页面必须显示「未登记标准（短码）」+ 处置提示，⛔ 不能显示 GUID。
   */
  StandardRegistered: boolean
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
  /** ★ 必填缺失全局参数总数（`run` 会据此整批拒绝） */
  MissingRequired?: number
  /** ★ 本批是否含图片 / PDF（视觉模型不可达时 `run` 整批拒绝） */
  NeedsVision?: boolean
  // ──── 以下为「重写预检」追加段（仅 `rewrite` 端点填充）────
  /**
   * ★ **本次结果是否含重写预检**。
   * `false`（`plan` / `run`）⇒ 下面的 `*Total` **无意义**，⛔ 页面不得显示「将保留 0 个」。
   */
  RewritePrecheck?: boolean
  KeepManual?: boolean
  KeepPinned?: boolean
  /** 基准账本里「钉住」的锚点数（事实） */
  PinnedAnchorTotal?: number
  /** 基准账本里「人工改过值」的锚点数（事实） */
  ManualValueTotal?: number
  /** 模板示例数据锚点数（事实；规则④ 必清，⛔ 不受开关影响） */
  SampleAnchorTotal?: number
  /** ★ 按开关算出的**将保留**锚点数 */
  WillKeepAnchorTotal?: number
  /** ★ 按开关算出的**将重新取值**锚点数 */
  WillRecomputeAnchorTotal?: number
  RewriteFiles?: NormalizeRewriteFilePrecheck[]
}

/** 重写预检：单份文件（全是**事实计数**，⛔ 不是「已经保留」的承诺） */
export interface NormalizeRewriteFilePrecheck {
  StandardFileCode: string
  FileName: string
  Action: string
  IsLocked: boolean
  /** 基准 = 该文件**最近一次**填充；空 = 从未规范化过 */
  FillLogCode?: string | null
  FillLogTime?: string | null
  LedgerRowCount: number
  PinnedCount: number
  ManualCount: number
  SampleCount: number
  WillKeepCount: number
  WillRecomputeCount: number
}

/** `rewrite`（全部重写预告）入参 */
export interface NormalizeRewriteRequest {
  EnterpriseCode: string
  StageCode?: string
  /** `enterprise` / `stage` / `standard` / `folder` / `file`（默认 `file`） */
  ScopeType?: string
  ScopeCode?: string
  /** ★ 显式文件清单（优先级最高；与 `plan` / `run` / `lock` 同口径，前端已展开） */
  StandardFileCodes?: string[]
  /** 保留「人工改过的值」（规则③，默认 `true`） */
  KeepManual?: boolean
  /** 保留「钉住的锚点」（规则②，默认 `true`） */
  KeepPinned?: boolean
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

// ═══════════════════════ 四之二、锁定 / 账本 / 重写 / 候选 ═══════════════════════
//
// ⚠️ 这些端点全部是 2026-10-09 补齐的（`54` §5.2 端点 7~13 / 17）。
//    ⛔ 未在此封装的端点：固定文档（`fixed/*`，契约表当前 0 行）、
//    导出与预览（`package` / `download` / `export-values` / `preview`）。

/** 锁定 / 解锁入参 —— ⚠️ `EnterpriseCode` **必需**（⛔ `41-03` 的表里漏了） */
export interface NormalizeLockRequest {
  EnterpriseCode: string
  StageCode?: string
  /** ★ **标准域行** Code 清单（文件夹级由前端展开后传入） */
  StandardFileCodes: string[]
}

/** 解锁入参 —— ⛔ `Reason` **必填**（解锁 = 放开一道保护，必须留痕为什么） */
export interface NormalizeUnlockRequest extends NormalizeLockRequest {
  Reason: string
}

export interface NormalizeLockResult {
  LockedCount: number
  /** ★ 其中「实例行原本不存在、本次新建」的数量（有意行为，但必须可见） */
  CreatedCount: number
  SkippedCount: number
  /** ⚠️ 非空 = 动作已生效，但审计留痕写失败（页面必须显式提示，⛔ 不吞） */
  AuditWarning?: string | null
}

export interface NormalizeUnlockResult {
  UnlockedCount: number
  SkippedCount: number
  AuditWarning?: string | null
}

/**
 * ★ **锁定**（L2 文件锁）—— 锁定后任何生成路径不得覆盖该文件产物。
 *
 * ⚠️ 锁落在**企业侧实例行**（同一标准文件，企业 A 锁了不影响企业 B）
 *   ⇒ `EnterpriseCode` 不是可选项，缺了无从定位要锁哪一行。
 * ⚠️ 幂等：已锁定 ⇒ 计入 `LockedCount` 但**不重复写库、不重复留痕**。
 *
 * @throws BizError 企业不存在 / 未选中文件 / 写入失败
 */
export async function lockNormalize(payload: NormalizeLockRequest): Promise<NormalizeLockResult> {
  const res = await yzhApi.post<ApiResponse<NormalizeLockResult>>(`${BASE}/lock`, payload)
  return unwrapOk<NormalizeLockResult>(res, '锁定失败')
}

/**
 * ★ **解锁** —— 只改 3 列（`IsLocked` / `LockedBy` / `LockedTime`），放开覆盖保护。
 *
 * ⛔ `Reason` 必填：解锁后产物会被下次生成覆盖，事后必须答得出「为什么放开」。
 *
 * @throws BizError 未填理由 / 企业不存在 / 没有一个处于锁定态
 */
export async function unlockNormalize(payload: NormalizeUnlockRequest): Promise<NormalizeUnlockResult> {
  const res = await yzhApi.post<ApiResponse<NormalizeUnlockResult>>(`${BASE}/unlock`, payload)
  return unwrapOk<NormalizeUnlockResult>(res, '解锁失败')
}

/** 取值账本一行（账本 + 锚点 JOIN） */
export interface FillValueDto {
  Code: string
  AnchorCode: string
  /** 锚点原文（含花括号，如 `{{文件控制程序}}`）—— 规则已删时仍会带出（`AnchorExists=false`） */
  AnchorRef: string
  AnchorType: string
  AnchorKind: string
  FieldCode: string
  /** `body` / `table_cell` / `header` / `excel_cell` / …（⚠️ 后端**序列化成字符串**，⛔ 不按数字比） */
  LocationKind: string
  LocationDesc: string
  ValueType: string
  ValueDisplay: string
  ValueText: string
  /** `global` / `self` / `profile` / `compute` / `ai` / `manual` */
  SourceKind: string
  SourceLabel: string
  Confidence: number
  ConfidenceReason: string
  /** `filled` / `pending` / `kept_as_is` / `removed`（★ 只有 `filled` 计入完成率分子） */
  FillStatus: string
  WriteMode: string
  OriginalText?: string | null
  IsOverridden: boolean
  OverrideKind: string
  OverrideReason?: string | null
  OverriddenBy?: string | null
  OverriddenTime?: string | null
  /** ★ L3 锚点钉住 —— 重写时跳过本行 */
  IsPinned: boolean
  SampleData: boolean
  Required: boolean
  IsOrphan: boolean
  /** ★ 规则是否还在（锚点被软删 / 停用 ⇒ false） */
  AnchorExists: boolean
  Sort: number
}

/** 账本读结果 —— 多包一层**日志上下文**（不传 FillLogCode 时后端替用户挑了哪一条） */
export interface NormalizeLedgerResult {
  /** ★ 空 = 该企业**从未规范化过**这份文件（⛔ 不是错误，显示「尚未规范化」即可） */
  FillLogCode: string
  FillLogStatus: string
  FillLogMessage: string
  /** ★ UTC，JSON 带 `Z` */
  FillLogTime?: string | null
  FillLogTotalAnchors: number
  FillLogPendingCount: number
  OutputPath?: string | null
  Total: number
  Items: FillValueDto[]
}

export interface SourceCandidateDto {
  Code: string
  SourceKind: string
  SourceLabel: string
  Value: string
  ValueKind: string
  Confidence?: number | null
  Evidence?: string | null
  Location?: string | null
  /** ★ 这条候选来自哪一份企业原始资料 → 写回 `SourceDetailJson.originalFileCode` 用 */
  SourceDocCode?: string | null
  Reason?: string | null
  IsPicked: boolean
  Index: number
}

/** 动作留痕一行（只追加表，⛔ 永不修改） */
export interface NormalizeActionDto {
  Code: string
  /** `lock` / `unlock` / `rewrite` / `pin` / `unpin` / `batch_run` / `batch_cancel` */
  ActionType: string
  ScopeType: string
  ScopeCode: string
  ScopeName: string
  TargetCode: string
  AnchorCode: string
  BeforeJson?: string | null
  AfterJson?: string | null
  Reason?: string | null
  CreateBy: string
  /** ★ 操作人姓名（人话；查不到时后端回退为 Code，⛔ 不会留空） */
  OperatorName: string
  /** UTC，JSON 带 `Z` */
  CreateTime?: string | null
  QueueCode: string
}

export interface NormalizeActionListResult {
  Limit: number
  /** ★ 还有更早的留痕未返回（页面必须显式提示，⛔ 不静默） */
  Truncated: boolean
  Total: number
  Items: NormalizeActionDto[]
}

/** 单锚点详情 = 账本行 + 证据链 + 候选 + 该锚点的动作时间线 */
export interface FillValueDetailDto extends FillValueDto {
  EvidenceText?: string | null
  EvidencePageHint?: string | null
  /** ★ 证据链 5 级 JSON（原样回传，⛔ 后端不解析） */
  SourceDetailJson?: string | null
  Candidates: SourceCandidateDto[]
  Actions: NormalizeActionDto[]
}

/**
 * ★ **读取某份文件的取值账本**（审计抽屉 Tab1）。
 *
 * ⚠️ 不传 `FillLogCode` 时后端取**最近一次**填充，并把「取的是哪一条」回传
 *   ⇒ 页面必须用回传的 `FillLogCode`，⛔ 不要假设是「刚刚那次」。
 * ⚠️ `FillLogCode` 为空 ⇒ 该文件**从未规范化过**（⛔ 不是错误）。
 *
 * @throws BizError 未选企业/文件；指定了 `fillLogCode` 但查不到
 */
export async function fetchNormalizeValues(params: {
  EnterpriseCode: string
  StandardFileCode: string
  FillLogCode?: string
}): Promise<NormalizeLedgerResult> {
  const res = await yzhApi.get<ApiResponse<NormalizeLedgerResult>>(`${BASE}/values`, params)
  return unwrapOk<NormalizeLedgerResult>(res, '读取取值账本失败')
}

/**
 * ★ **单个锚点的取值详情**（审计抽屉 Tab2「数据来源」）。
 *
 * ⚠️ `anchorCode` 走**路由**（`value/{anchorCode}`），⛔ 不要塞进查询串 ——
 *   与查询参数同名，但后端以路由为准。
 *
 * @throws BizError 缺锚点 / 该文件尚未规范化 / 该锚点在这份产物里没有取值记录
 */
export async function fetchNormalizeValueDetail(params: {
  AnchorCode: string
  EnterpriseCode: string
  StandardFileCode: string
  FillLogCode?: string
}): Promise<FillValueDetailDto> {
  const { AnchorCode, ...query } = params
  const res = await yzhApi.get<ApiResponse<FillValueDetailDto>>(
    `${BASE}/value/${encodeURIComponent(AnchorCode)}`,
    query,
  )
  return unwrapOk<FillValueDetailDto>(res, '读取取值详情失败')
}

/**
 * ★ **动作留痕时间线**（审计抽屉 Tab3）—— 两种查法，取**并集**去重。
 *
 * @param targetCode 文件级（= 文件 Code）
 * @param scopeCode  文件夹 / 阶段级
 * ⚠️ 有上限：返回的 `Truncated=true` 表示还有更早的留痕未返回（页面必须提示）。
 *
 * @throws BizError 两者都没给
 */
export async function fetchNormalizeActions(params: {
  TargetCode?: string
  ScopeCode?: string
  Limit?: number
}): Promise<NormalizeActionListResult> {
  const res = await yzhApi.get<ApiResponse<NormalizeActionListResult>>(`${BASE}/actions`, params)
  return unwrapOk<NormalizeActionListResult>(res, '读取动作时间线失败')
}

/** 账本写入参 —— 改值 / 改来源 / 钉住（`Kind` 分流） */
export interface NormalizeOverrideRequest {
  FillLogCode: string
  AnchorCode: string
  /** `value` 只改值 · `source` 只改来源（值不变）· `both` 两者都改；空串 = 只做钉住 */
  Kind?: string
  /** ⚠️ **改前的旧值**（可选）：传了即做乐观并发校验 */
  SourceKind?: string
  /** ★ 目标来源类别（`Kind` 含 `source` 时必填） */
  NewSourceKind?: string
  SourceLabel?: string
  /** ★ 换原始件时带新的证据链（`originalFileCode` 就在这里面） */
  SourceDetailJson?: string
  /**
   * ★ 人工值（`Kind` 含 `value` 时必填）。
   * ⚠️ **空串 = 显式清空**（落 `FillStatus='removed'`，⛔ 不计完成率分子）；`undefined` = 没给 ⇒ 拒绝。
   */
  ValueText?: string
  /** ★ 必填：改值 / 换来源都要留「为什么」 */
  Reason: string
  /**
   * ★ 钉住开关（L3）。
   * ⚠️ **`undefined` = 本次不改钉住状态**；⛔ 不要用 `false` 表达「不改」—— 那会静默解开。
   */
  IsPin?: boolean
}

export interface NormalizeOverrideResult {
  Code: string
  Kind: string
  ValueDisplay: string
  SourceKind: string
  SourceLabel: string
  /** 落库后的钉住状态（事实） */
  IsPinned: boolean
  AuditWarning?: string | null
}

/**
 * ★ **账本写** —— 改值 / 改来源 / 钉住（`54` §5.2 端点 11）。
 *
 * ⛔ **只改账本，⛔ 不重跑、⛔ 不动产物文件**：这是「编辑三缓冲」的**落笔**那一半
 *   —— 账本改了、产物还是旧的，必须再走一次生成才会体现在文档里。
 *
 * ★ 留痕粒度 = **一动作一行**：改值/改来源写 `rewrite`，钉住写 `pin`/`unpin`
 *   ⇒ 一次调用两者都做会写**两行**。
 *
 * @throws BizError 缺 FillLogCode/AnchorCode · 没填理由 · Kind 非法 ·
 *   来源类别非法 · 乐观并发校验失败 · 该锚点没有取值记录
 */
export async function overrideNormalizeValue(
  payload: NormalizeOverrideRequest,
): Promise<NormalizeOverrideResult> {
  const res = await yzhApi.post<ApiResponse<NormalizeOverrideResult>>(`${BASE}/value/override`, payload)
  return unwrapOk<NormalizeOverrideResult>(res, '改写取值失败')
}

/**
 * ★ **「全部重写」预告**（`54` §5.2 端点 12）—— 按**五条不覆盖规则**回答
 * 「会保留什么、会覆盖什么」。
 *
 * ⛔ **本端点只预告、不入队**（出参是 `NormalizePlan`，没有 `QueueCode`）。
 *   真正执行仍走 {@link runNormalize}。
 *
 * ⚠️ 返回的 `*Total` 里：`PinnedAnchorTotal` / `ManualValueTotal` / `SampleAnchorTotal`
 *   是**事实**（库里现在有多少行）；`WillKeepAnchorTotal` 是**按开关算出来会保留多少**。
 *   ⛔ 别把 `WillKeepAnchorTotal` 当成「已经保留了」的承诺。
 * ⚠️ `RewritePrecheck=false` ⇒ 下面的计数无意义（⛔ 不要显示「将保留 0 个」）。
 *
 * @throws BizError 未选企业 / 范围内没有可重写文件 / `ScopeType` 非法
 */
export async function rewriteNormalize(
  payload: NormalizeRewriteRequest,
): Promise<NormalizePlanResult> {
  const res = await yzhApi.post<ApiResponse<NormalizePlanResult>>(`${BASE}/rewrite`, payload)
  return unwrapOk<NormalizePlanResult>(res, '重写预览失败')
}

/**
 * ★ **候选来源**（`54` §5.2 端点 13）—— 改来源时「有哪些可选」。
 *
 * ★ `ProfileCode` 有两层作用：① 只返回**来自这份原始资料**的建议（换原始件场景）；
 *   ② 顺便解析出企业与目标文件 ⇒ 只给 `ProfileCode` 也能定位。
 * ⚠️ 但候选池按「企业 × 文件 × 锚点」三键存放 ⇒ 至少要能确定企业与文件：
 *   给了 `ProfileCode`，或同时给 `EnterpriseCode` + `StandardFileCode`。
 * ⛔ 查不到返回空数组（不是错误）—— 确定性来源的锚点本来就没有 AI 候选。
 *
 * @throws BizError 缺 AnchorCode · 画像不存在 · 无法确定候选池范围
 */
export async function fetchNormalizeCandidates(params: {
  AnchorCode: string
  ProfileCode?: string
  EnterpriseCode?: string
  StandardFileCode?: string
}): Promise<SourceCandidateDto[]> {
  const res = await yzhApi.get<ApiResponse<SourceCandidateDto[]>>(`${BASE}/candidates`, params)
  return unwrapOk<SourceCandidateDto[]>(res, '读取候选来源失败')
}

/** 动作类型 → 人话（`normalize_action` 字典七值） */
export const ACTION_TYPE_TEXT: Record<string, string> = {
  lock: '锁定',
  unlock: '解锁',
  rewrite: '改写',
  pin: '钉住锚点',
  unpin: '取消钉住',
  batch_run: '整批执行',
  batch_cancel: '取消批次',
}

/** 动作类型 → 徽标语义色（`YzhStatusBadge` 的 type） */
export const ACTION_TYPE_TYPE: Record<string, string> = {
  lock: 'warning',
  unlock: 'info',
  rewrite: 'success',
  pin: 'success',
  unpin: 'info',
  batch_run: 'success',
  batch_cancel: 'danger',
}

/** 取值来源 → 人话 */
export const SOURCE_KIND_TEXT: Record<string, string> = {
  global: '全局参数',
  self: '文档自身',
  profile: '企业资料画像',
  compute: '计算得出',
  ai: 'AI 建议',
  manual: '人工填写',
}

/** 锚点取值状态 → 人话 */
export const VALUE_STATUS_TEXT: Record<string, string> = {
  filled: '已写入',
  pending: '待办（无值）',
  kept_as_is: '未命中，保留原文',
  removed: '已清空',
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

/** 最近一次填充留痕状态（`cert_doc_fill_log.Status`）→ 人话 */
export const LOG_STATUS_TEXT: Record<string, string> = {
  success: '上次成功',
  partial: '上次部分完成',
  failed: '上次失败',
}

/** 最近一次填充留痕状态 → 徽标语义色 */
export const LOG_STATUS_TYPE: Record<string, string> = {
  success: 'success',
  partial: 'warning',
  failed: 'danger',
}

/**
 * ★ Code 短码（前 8 位）—— 与后端 `EnterpriseNormalizeController.ShortCode` **同口径**。
 *
 * 用途：主数据缺失时的人话占位。⛔ 不要改成完整 GUID（版面撑爆 + 把人劝退）。
 */
export function shortCode(code?: string | null): string {
  const c = (code ?? '').trim()
  if (!c) return '?'
  return c.length > 8 ? c.slice(0, 8) : c
}

/**
 * ★★ **标准显示名** —— ⛔ 绝不把裸 GUID 当名字显示（2026-10-09 用户报障）。
 *
 * 取值顺序：`StandardName` → `StandardNo`（业务编号，如 `iso9001`）→「未登记标准（短码）」。
 *
 * 背景：`cert_iso_standard` 里 `475da4fe-8f50-4bf7-bf2b-b39869d5ddf7`（食品标准）被删，
 * 但 `cert_enterprise_stage` 的关联行还在 ⇒ 后端此前 `StandardName = iso?.StandardName ?? code`
 * 回退 ⇒ 页面 Tab 直接渲染出一串 GUID。用户看到的是「系统坏了」，而不是「数据缺了」。
 *
 * ⚠️ 配套：后端已改为**不回退**（`StandardName`/`StandardNo` 缺就是空串），
 * 并用 `NormalizeStandardNode.StandardRegistered` 说明「为什么缺」。
 */
export function standardLabel(s: {
  StandardName?: string | null
  StandardNo?: string | null
  StandardCode?: string | null
}): string {
  const name = (s.StandardName ?? '').trim()
  if (name) return name
  const no = (s.StandardNo ?? '').trim()
  if (no) return no
  return `未登记标准（${shortCode(s.StandardCode)}）`
}

/** 0~1 小数 → 百分比整数（⛔ 后端可能给 decimal 字符串，必须先 Number()） */
export function toPercent(v: unknown): number {
  const n = Number(v)
  if (!Number.isFinite(n) || n <= 0) return 0
  if (n >= 1) return 100
  return Math.round(n * 100)
}
