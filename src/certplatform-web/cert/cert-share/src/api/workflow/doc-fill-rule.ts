/**
 * 标准文档填写规则 —— 管理端 API
 *
 * 后端：`CertPlatform.Admin/Controllers/Workflow/DocTemplateController.cs`
 *       `…/DocTemplateAnchorController.cs` · `…/DocFillPromptController.cs`
 * 路由前缀：`/api/Admin/Workflow/{控制器}`
 *
 * ⛔ 锚点 / 提示词两张表的**行级 CRUD** 不走本文件 —— 它们由
 *    `TreeTableLogic`（`controllerName`）经基类通用端点（`/filter` `/add` `/update`
 *    `/delete` `/toggle-valid`）驱动。本文件只放**跨实体编排**的三个端点。
 *
 * 契约（22 号）：业务失败恒 HTTP 200，`success` 是唯一判据；载荷 PascalCase。
 */

import { yzhApi } from '@yzh-core/api/client'
import type { ApiResponse } from '@yzh-core/types'

const BASE = '/api/Admin/Workflow'

// ==================== 类型（与后端匿名 DTO 逐字对齐） ====================

/** `GET /DocTemplate/candidates` 的单项 */
export interface DocTemplateCandidate {
  /** `cert_standard_directory_file.Code`（登记入参 `standardFileCode` 用这个） */
  Code: string
  /** 展示名。⚠️ 可能是 `.doc`/`.xls` —— 真实字节看 `EffectiveStoragePath` */
  FileName: string
  ConfigCode?: string
  StandardCode?: string
  StageCode?: string
  /** 原始扩展名（`doc` / `xls` / `docx` / `xlsx` / `txt`） */
  FileType?: string
  /** 归一状态（`completed` = 已产出可编辑副本） */
  EditableStatus?: string
  /** 后端权威推导的登记类型：`docx` / `xlsx` / `null`（不可登记） */
  RegisterKind?: string
  /** 归一产物优先的可用路径 */
  EffectiveStoragePath?: string
  /** 能否登记（`RegisterKind` 非空） */
  Ready: boolean
  /** 是否已登记为模板 */
  Registered: boolean
}

export interface DocTemplateCandidateResult {
  /** 过滤后条数 */
  Total: number
  /** 全量条数（不受 keyword 影响） */
  AllCount: number
  ReadyCount: number
  RegisteredCount: number
  Items: DocTemplateCandidate[]
}

export interface RegisterTemplatePayload {
  /** `cert_standard_directory_file.Code` */
  StandardFileCode: string
  FileName?: string
  FillPromptCode?: string
  Remark?: string
}

/** `GET /DocTemplate/tree` 的节点（已与内核 `TreeNode` 同形） */
export interface DocTemplateTreeNode {
  Code: string
  Name: string
  IsLeaf: boolean
  Children?: DocTemplateTreeNode[]
  Extra?: {
    /**
     * 节点类型 —— **两个端点的取值不同**：
     * - `tree`（旧，模板表驱动）：`org` / `scope` / `template`
     * - `directory-tree`（新，资料清单驱动）：`org` / `standard` / `stage` / `folder` / `file`
     */
    kind?: string
    orgCode?: string
    standardCode?: string
    stageCode?: string
    standardName?: string
    stageName?: string
    templateCount?: number
    standardFileCode?: string
    fileKind?: string
    storagePath?: string
    fillPromptCode?: string | null
    publishStatus?: string
    scanStatus?: string
    anchorCount?: number
    orphanCount?: number

    // ── 以下仅 `directory-tree` 的文件叶子有 ──
    /** `standard` 节点的标准编号（如 `iso9001-2015`） */
    standardNo?: string
    /** `stage` 节点的阶段 GUID */
    phaseCode?: string
    /** `stage` 节点的目录配置 Code（空 = 该「标准×阶段」还没建配置，不会有文件） */
    configCode?: string
    /** 文件扩展名（小写，无点）：`doc` / `docx` / `xls` / `xlsx` … */
    fileType?: string
    /** 资料清单里的**原始文档**路径（第 ② 步「下载标准文档」用） */
    standardStoragePath?: string
    /** 原始文档的 LibreOffice 归一产物（`.docx` / `.xlsx`）路径 */
    standardEditablePath?: string
    /**
     * ★ 是否已上传空白模板 —— 页面据此决定操作条分级。
     * 为 `false` 时只能「下载标准文档 + 上传空白模板」。
     */
    hasTemplate?: boolean
    /** 空白模板 `cert_doc_template.Code`（无模板时为空串） */
    templateCode?: string
    /** 空白模板在 MinIO 的路径（`…/_template/xxx.docx`） */
    templateStoragePath?: string
    templateFileName?: string
    /** ★ 权威分类列 `cert_standard_directory_file.DocCategory`：`editable` / `fixed` */
    docCategory?: string
    /** 文件夹深度（1 起） */
    depth?: number
  }
}

/** `GET /DocFillPrompt/versions` 的单项 */
export interface DocFillPromptVersion {
  Code: string
  OrgCode: string
  PromptCode: string
  PromptName: string
  Temperature?: number
  MaxTokens?: number
  Version: number
  IsDefault: boolean
  Status: string
  SystemPrompt?: string
  UserTemplate?: string
  OutputSchema?: string
  Sort?: number
  IsValid: number
  IsDeleted?: boolean
  UpdateTime?: string
  Remark?: string
}

// ==================== 端点 ====================

/**
 * 可登记的空白模板候选（来源 = 标准目录里 `EnterpriseCode='YZH-STD-ENT'` 的模板行）。
 *
 * ⚠️ 实测：168 个模板行里 143 个 `.doc` + 11 个 `.xls`，而填写引擎只认
 * `.docx`/`.xlsx` ⇒ **登记的是「归一产物」**（`EditableStoragePath`），
 * 不是原始文件。`RegisterKind` 就是后端据此推导出来的。
 */
export function getDocTemplateCandidates(keyword?: string) {
  return yzhApi.get<ApiResponse<DocTemplateCandidateResult>>(
    `${BASE}/DocTemplate/candidates`,
    keyword ? { keyword } : undefined,
  )
}

/** 登记一个空白模板（已登记且未删 → 报错；已软删 → 沿用同 Code 复活） */
export function registerDocTemplate(payload: RegisterTemplatePayload) {
  return yzhApi.post<ApiResponse<any>>(`${BASE}/DocTemplate/register`, payload)
}

/** 模板三级树：机构 → 「标准 · 阶段」→ 模板 */
export function getDocTemplateTree() {
  return yzhApi.get<
    ApiResponse<{ Nodes: DocTemplateTreeNode[]; Total: number }>
  >(`${BASE}/DocTemplate/tree`)
}

/**
 * ★★ **资料清单树**（页面左树首选）—— 机构 → 标准 → 阶段 → 文件夹 → 文件。
 *
 * <p>与「标准资料清单」页（`directory-manager`）**同源同形**（后端复用同一个
 * `GetOrganizationTreeAsync` + 同一份保留段过滤口径），差别只有两点：</p>
 * <ol>
 *   <li>载荷是 <b>PascalCase</b>（对齐 `YzhTreeTableLayout` 契约），
 *       而资料清单页内部用的是 `CertBizTree` 的 `{id,label,type}` camelCase 私有格式；</li>
 *   <li>每个<b>文件叶子</b>额外带空白模板状态（`hasTemplate` / `templateCode` /
 *       `scanStatus` / `publishStatus` / `anchorCount` / `docCategory`）。</li>
 * </ol>
 *
 * <p>⛔ 不要退回 `tree` 端点 —— 那个按 `cert_doc_template` 构树，
 * 只显示**已上传过模板的文件**（实测 1 个 vs 资料清单 168 个），
 * 用户会看到一棵几乎是空的树，且无从下手（没模板的文件根本不在树上）。</p>
 */
export function getDirectoryTree() {
  return yzhApi.get<
    ApiResponse<{
      Nodes: DocTemplateTreeNode[]
      Total: number
      WithTemplate: number
    }>
  >(`${BASE}/DocTemplate/directory-tree`)
}

/**
 * 某 `promptCode` 的全部版本（**含已软删**，按 `Version` 降序）。
 *
 * ⚠️ 可见范围与 `resolve` 一致（`OrgCode = ''` 或命中 `orgCode`）——
 * 「编辑生效版本」= resolve 拿 `Picked.Code` → 本端点取**整行** → `update` 整行提交，
 * ⛔ 不能只发 `{Code, UserTemplate}`（`updateFields` 是全部 `BcFlag` 列，缺的会被清空）。
 *
 * ⚠️ `orgCode` 传空串 = 只看全局作用域；后端按「机构非空优先」选取。
 */
export function getDocFillPromptVersions(promptCode: string, orgCode = '') {
  return yzhApi.get<
    ApiResponse<{
      PromptCode: string
      OrgCode: string
      Total: number
      Items: DocFillPromptVersion[]
    }>
  >(`${BASE}/DocFillPrompt/versions`, { promptCode, orgCode })
}

/**
 * 解析「某模板实际会用到哪一版提示词」——**唯一选取口径**：
 * ① 机构更具体优先 → ② `IsDefault` 优先 → ③ `Version` 新优先。
 *
 * 页面用它在「全文规则」标签页顶部显示「当前生效版本」，
 * 避免实施人员看到 3 个版本却不知道运行期会用哪一个。
 */
export function resolveDocFillPrompt(promptCode: string, orgCode = '') {
  return yzhApi.get<ApiResponse<any>>(`${BASE}/DocFillPrompt/resolve`, {
    promptCode,
    orgCode,
  })
}

export function updateDocFillPrompt(version: DocFillPromptVersion) {
  return yzhApi.post<ApiResponse<any>>(`${BASE}/DocFillPrompt/update`, version)
}

/**
 * 把某一版设为该 `(OrgCode, PromptCode)` 下唯一默认（后端做排他）。
 *
 * ⚠️ **必须提交整行**，不能只发 `{Code, IsDefault}`：
 * 后端 `DocFillPromptController.UpdateCore` 会走 `Validate(entity)`，
 * 而 `PromptName` / `PromptCode` 是必填列 —— 只发两个字段会被拒
 * 「PromptName 不能为空」。（`updateFields` 又是全部 `BcFlag` 列，缺的字段会被清空。）
 */
export function setDocFillPromptDefault(version: DocFillPromptVersion) {
  return yzhApi.post<ApiResponse<any>>(`${BASE}/DocFillPrompt/update`, {
    ...version,
    IsDefault: true,
  })
}

/**
 * **可选提示词清单**（按 `PromptCode` 聚合，不含已软删）—— 「挂接提示词」下拉用。
 *
 * 【为什么需要它】
 *   「全文填写规则」页签原来 `:disabled="!promptCode"`，而新模板的 `FillPromptCode` 必然是空的
 *   ⇒ 页签点不开；即便点开，面板里只有「版本表 + 设为默认」，**没有任何新建/挂接入口**。
 *   这个端点就是打破死循环的第一步：先让用户看得见「现有提示词有哪些」。
 */
export function getDocFillPromptCodes(keyword?: string) {
  return yzhApi.get<
    ApiResponse<{
      Total: number
      Items: {
        PromptCode: string
        PromptName?: string
        VersionCount: number
        MaxVersion: number
        Orgs: string[]
        ActiveVersion: number | null
        UpdateTime?: string
      }[]
    }>
  >(`${BASE}/DocFillPrompt/codes`, keyword ? { keyword } : undefined)
}

/**
 * 新增一版提示词（`Version <= 0` 时后端自动取 `max + 1`）。
 *
 * ⚠️ `OrgCode` 空串 = 全局；本页新建的提示词一律落**全局**（机构级差异化留给提示词工作台）。
 */
export function addDocFillPrompt(payload: {
  PromptCode: string
  PromptName: string
  SystemPrompt?: string
  UserTemplate?: string
  Model?: string
  Temperature?: number
  MaxTokens?: number
  Status?: string
  IsDefault?: boolean
}) {
  return yzhApi.post<ApiResponse<any>>(`${BASE}/DocFillPrompt/add`, {
    OrgCode: '',
    // 0 = 未指定 ⇒ 后端取 max(Version) + 1
    Version: 0,
    Status: 'draft',
    IsDefault: false,
    MaxTokens: 4000,
    Temperature: 0.2,
    ...payload,
  })
}

/**
 * **挂接 / 解绑**本模板的全文填写提示词（只改 `cert_doc_template.FillPromptCode` 一列）。
 *
 * ⛔ 不走通用 `update`：那会把全部 `BcFlag` 列提交一遍，漏传的业务键被清空。
 * `promptCode` 传空串 = 解绑。
 */
export function setDocTemplatePrompt(templateCode: string, promptCode: string) {
  return yzhApi.post<
    ApiResponse<{
      TemplateCode: string
      FillPromptCode?: string
      Bound: boolean
    }>
  >(`${BASE}/DocTemplate/set-prompt`, {
    TemplateCode: templateCode,
    PromptCode: promptCode,
  })
}

// ====================================================================
// ★ 自动生成全文填写提示词（2026-10-07 用户裁决：「自动生成 = 后端调 LLM」）
// ====================================================================

/** `POST /DocFillPrompt/generate` 的请求体（PascalCase，与后端 `GenerateRequest` 逐字一致） */
export interface GenerateFillPromptPayload {
  /** 文档作用（全局规则 Tab「文档作用」文本框内容）；与 Anchors 至少给一项 */
  DocPurpose?: string
  /** 文档角色（可空） */
  DocRole?: string
  /** 现有提示词正文；**非空 = 优化模式**（保留结构，不全量重写） */
  CurrentPrompt?: string
  /** 锚点清单（前端从当前模板锚点行组装） */
  Anchors?: { AnchorRef: string; ValueType?: string; Source?: string }[]
}

/** 生成结果 —— ⛔ **不落库**，由调用方写进文本框、点保存才持久化 */
export interface GenerateFillPromptResult {
  /** 提示词正文（后端已 `StripFence` 去围栏） */
  Prompt: string
  Model?: string
  DurationMs?: number
  /** `true` = 本次是优化模式（带了 `CurrentPrompt`） */
  Optimize: boolean
}

/**
 * ★ **按「锚点清单 + 文档作用」LLM 生成提示词正文**（全局填写规则卡片「自动生成」）。
 *
 * ⚠️ 生成约 5~15s ⇒ 调用方需给按钮加载态；失败时业务文案已在 `err` 里。
 * ⚠️ 生成≠保存：未挂接提示词时调用方在生成成功后自行走「新建+挂接」落库。
 */
export function generateFillPrompt(payload: GenerateFillPromptPayload) {
  return yzhApi.post<ApiResponse<GenerateFillPromptResult>>(
    `${BASE}/DocFillPrompt/generate`,
    payload,
  )
}

// ====================================================================
// 七步闭环（37 号 §7.1）—— 2026-10-03 新增的跨实体编排端点
//
// 命名与后端逐字对齐：
//   `POST /DocTemplate/upload-template`        ← ④ 上传空白模板
//   `GET  /StandardDocContract/detail`         ← Tab3 读契约
//   `POST /StandardDocContract/save`           ← Tab3 写契约（含 DocCategory 分流）
//   `POST /StandardDocContract/analyze`        ← 「自动分析」按钮（聚合 C 字段提取 + A 扫描）
//   `POST /DocTemplateAnchor/scan`             ← ⑤ 重新扫描（零 LLM）
//   `POST /DocTemplateAnchor/validate`         ← ⑥ 校验（三色）
//   `POST /DocTemplateAnchor/publish`          ← ⑦ 发布
//
// ⚠️ 后端这几个端点的**业务失败恒 HTTP 200**，`success` 是唯一判据 ⇒ 调用方
//    必须用 `unwrapOk` / `expectOk`（守卫 R13，⛔ 禁裸 `if (!res.success)`）。
// ====================================================================

/** `GET /StandardDocContract/detail` 的返回（契约不存在时 `Exists=false` 的空壳） */
export interface DocContractDetail {
  /** 契约行是否已存在。false 时其余业务字段回落到文件行默认值，仍可直接编辑后保存 */
  Exists: boolean
  Code?: string
  StandardFileCode: string
  FileName: string
  /** ★ 分类权威列 = `cert_standard_directory_file.DocCategory`（不是契约行） */
  DocCategory: string
  DocName: string
  /** required / optional / reference / attachment */
  DocRole: string
  DocPurpose?: string
  /** 标签数组 JSON 字符串 */
  TagsJson?: string
  InfoItemsJson?: string
  /**
   * ★ `fixed` 文档专用：**可替换性**（人工判断，49-V3 §2.2 已裁 D-AA1，⛔ 程序不推导）。
   *
   * - `standard_provided` = 标准自带（不向企业索取）
   * - `enterprise_provided` = 企业提供（必须给匹配依据）
   *
   * ⚠️ 非 `fixed` 行忽略此列（后端不写）。默认值 `enterprise_provided`。
   * ⛔ DDL 注释里曾写 `platform_generated` —— **那是错的**，别照着抄。
   */
  FixedDocSubtype?: string
  /** `fixed` 文档专用：指纹规则集 JSON */
  FingerprintJson?: string

  AnalyzeStatus: string
  AnalyzeMessage?: string
  ModelName?: string
  AnalyzeTime?: string
  /** `ai` / `manual` —— 人工改过的批量重跑不得覆盖 */
  TagsSource?: string
  DocPurposeSource?: string
  TagsConfidence?: number
  DocPurposeConfidence?: number
  IsManualCorrected: boolean

  // ── 标准原始文档的存储信息（供「下载标准文档」与中栏预览）──
  StandardFileType?: string
  StandardStoragePath?: string
  StandardEditablePath?: string
  StandardConvertedPath?: string
  StandardEditableStatus?: string
  EnterpriseCode?: string
}

/** `POST /StandardDocContract/save` 的请求体（字段名 PascalCase，与后端逐字一致） */
export interface DocContractSavePayload {
  StandardFileCode: string
  DocName?: string
  /** ★ 「这个文档是否不需要编辑」：`fixed`（固定格式免填）/ `editable`（要配填写规则） */
  DocCategory?: string
  DocRole?: string
  /** ⚠️ 传 `null`（或不传）= 本次不动该字段；传空串 = 清空 */
  DocPurpose?: string | null
  TagsJson?: string | null
  InfoItemsJson?: string | null
  /** ★ 仅 `fixed` 文档用：`standard_provided` / `enterprise_provided` */
  FixedDocSubtype?: string | null
  FingerprintJson?: string | null
}

/**
 * 上传**空白模板**（37 号 §7.1 第 ④ 步）。
 *
 * ⚠️ 必须用 `FormData`；**⛔ 不要手工设 `Content-Type: multipart/form-data`** ——
 * 手写不带 boundary，后端解析不出任何字段，请求发得出去、后端只见空 form、前端零报错。
 * `yzhApi` 检测到 `FormData` 会自动删掉该头，让浏览器补 boundary。
 *
 * ★ 覆盖语义：同一 `standardFileCode` 重新上传 = **换版**。模板行沿用同 `Code`，
 * 但 `PublishStatus` 重置为 `draft`、`ScanStatus` 重置为 `pending` ⇒ 必须重扫重校验。
 */
export function uploadDocTemplate(
  file: File,
  standardFileCode: string,
  remark?: string,
) {
  const fd = new FormData()
  fd.append('File', file)
  fd.append('StandardFileCode', standardFileCode)
  if (remark) fd.append('Remark', remark)
  return yzhApi.post<ApiResponse<any>>(
    `${BASE}/DocTemplate/upload-template`,
    fd,
  )
}

/** 读文档契约（不存在返回空壳，不报错） */
export function getDocContract(fileCode: string) {
  return yzhApi.get<ApiResponse<DocContractDetail>>(
    `${BASE}/StandardDocContract/detail`,
    { fileCode },
  )
}

/** 保存文档契约（人工编辑 ⇒ 标记 `manual`，后端同批同步标准目录行的 `DocCategory`） */
export function saveDocContract(payload: DocContractSavePayload) {
  return yzhApi.post<ApiResponse<any>>(
    `${BASE}/StandardDocContract/save`,
    payload,
  )
}

/**
 * 「自动分析」（用户要求：每个文件一个分析按钮）。
 *
 * 一次调用聚合三项，**互相独立、互不阻断**（任一项失败只影响它自己的 `Status` 段）：
 *   - **C 字段提取**（LLM）⇒ `Field.Status` ∈ `ok` / `empty` / `failed`
 *   - **B 语义分析**（LLM 两跳 `doc_group` + `doc_content`，结论落契约表）
 *     ⇒ `Semantic.Status` ∈ `completed` / `partial` / `blocked` / `skipped` / `failed`
 *   - **A 锚点扫描**（零 LLM，但**需要空白模板** ⇒ 未上传时 `Scan.Status='blocked'`）
 *
 * ⚠️ **B 会真调 LLM 两次，约 15~25 秒** ⇒ 本请求超时必须 ≥120s，否则前端先超时而后端仍在跑。
 * ⚠️ `Semantic.SuggestedCategory` 只是 **AI 建议**，⛔ 未落库 —— 需人工在 Tab3 确认后走
 * `saveDocContract`（后端才同批写契约行 + 标准目录行的 `DocCategory`）。
 */
export function analyzeDocForFill(fileCode: string) {
  return yzhApi.post<ApiResponse<any>>(
    `${BASE}/StandardDocContract/analyze`,
    null,
    {
      params: { fileCode },
    },
  )
}

/**
 * 重新扫描空白模板 → 锚点清单（**零 LLM**）。
 *
 * ⚠️ `force=false`（默认）时，若已扫过且 `PublishStatus !== 'draft'`，后端**直接跳过**
 * 并返回 `Skipped=true` —— 想强制重扫必须传 `force=true`。
 */
export function scanTemplateAnchors(templateCode: string, force = false) {
  return yzhApi.post<ApiResponse<any>>(`${BASE}/DocTemplateAnchor/scan`, null, {
    params: { templateCode, force },
  })
}

/** 校验（三色）：返回 `ErrorCount` / `WarningCount` / `Violations` / `CanPublish` */
export function validateTemplate(templateCode: string) {
  return yzhApi.post<ApiResponse<any>>(
    `${BASE}/DocTemplateAnchor/validate`,
    null,
    {
      params: { templateCode },
    },
  )
}

/** 发布（硬前置：无红牌 + 有锚点；后端会重新校验一遍，⛔ 不信缓存） */
export function publishTemplate(templateCode: string) {
  return yzhApi.post<ApiResponse<any>>(
    `${BASE}/DocTemplateAnchor/publish`,
    null,
    {
      params: { templateCode },
    },
  )
}

/**
 * 批量保存锚点（按 `uk_tpl_anchor` upsert，单事务）。
 *
 * ★ 侧边栏「保存」走这个端点而不是通用 `/update`，原因有两条：
 *   ① 语义 = **整行 upsert**（`SaveAnchorBatchRequest` 的注释已声明）：每条 `Items[i]`
 *      是该锚点的**完整状态**，未出现的字段会被写成 CLR 默认值 ⇒ 调用方必须提交完整行；
 *   ② 它**按唯一键定位**（`TemplateCode+AnchorType+AnchorKind+SheetName+SectionIndex+HeaderKind+AnchorRef`），
 *      所以即便 `Code` 缺失也不会插重复行，比 `/update` 更稳。
 *
 * ⚠️ 同一批次内**唯一键不得重复**，否则后端直接拒绝整批。
 */
export function saveAnchorBatch(templateCode: string, items: any[]) {
  return yzhApi.post<ApiResponse<any>>(`${BASE}/DocTemplateAnchor/save-batch`, {
    TemplateCode: templateCode,
    Items: items,
  })
}

/** `POST /DocTemplateAnchor/lock` 的返回 */
export interface LockAnchorResult {
  TemplateCode: string
  /** 本次请求的目标状态 */
  Locked: boolean
  /** 真正写入成功的条数 */
  Affected: number
  /** 本来就已是目标状态、未变更的条数 */
  Unchanged: number
  Failed: number
  /** ★ 操作范围内「操作后」仍处于锁定态的条数（前端据此刷新，⛔ 不自己算） */
  LockedTotal: number
  /** ★ 如实推导、⛔ 不阻断：如「锁定后仍不能用于自动填充」 */
  Warnings: string[]
}

/**
 * ★ 锁定 / 解锁锚点（用户第 20 轮第 2 条 + 2026-10-05 口径修订）。
 *
 * 【★ 语义】`IsLocked = 1` = 实施人员已认可该锚点的设置规则，**配置就此冻结**：
 *   后端 `save-batch` / `UpdateCore` 命中锁定行且**配置列有实质变化**时**整批拒绝**，
 *   `clear` **跳过**锁定行。⇒ 前端在锁定后应把「配置规则」置为只读，⛔ 别让用户白改一遍。
 *
 * 【★ 与保存配置正交】本端点**只改 `IsLocked`**；`save-batch` 的列清单里**刻意不含**
 *   `IsLocked` ⇒ 保存配置**不会**顺手解除锁定。两者是**两个独立动作**，⛔ 不要合并成一次提交。
 *
 * 【★ 锁定动作本身不设前置】锁定一个「还没配取值来源」的锚点是**允许**的 ——
 *   后端只在 `Warnings` 里如实提示。是否配齐由前端（C8 闸）与人工决定。
 *
 * @param codes 目标锚点 Code 清单（单行切换传 1 个）；传空数组且 `all=true` 时作用于整个模板
 * @param locked `true` = 锁定，`false` = 解锁
 */
export function lockAnchor(
  templateCode: string,
  codes: string[],
  locked: boolean,
  all = false,
) {
  return yzhApi.post<ApiResponse<LockAnchorResult>>(
    `${BASE}/DocTemplateAnchor/lock`,
    { TemplateCode: templateCode, Codes: codes, All: all, Locked: locked },
  )
}

/** 全局参数定义（`global` 类来源的下拉候选）。⛔ 只读，仅用于提示可选值 */
export function listFillParamDefs(keyword?: string) {
  const filters = keyword
    ? [{ Field: 'ParamCode', Operator: 'like' as const, Value: keyword }]
    : []
  return yzhApi.post<ApiResponse<{ Items: any[]; TotalCount: number }>>(
    '/api/Admin/Cert/FillParamDef/filter',
    { Page: 1, PageSize: 500, Filters: filters },
  )
}

// ====================================================================
// ★ 试填 / 预览（`52` B7 + D1，2026-10-06）
//
// ⚠️ **本段的 BASE 与文件其余部分不同** —— 上面全是 `/api/Admin/Workflow/*`，
//    本段是 `/api/Auditor/DocFillPreview/*`。这不是笔误：
//    试填的唯一实现是 `DocumentFillOrchestrator`（**Auditor 工程**），
//    而依赖方向是 `Auditor → Admin`（单向），`Admin` ⛔ 不能引 `Auditor`
//    ⇒ 控制器只能落在专家端。`52` 写的 `api/Admin/Workflow/DocFillPreview` **落不了地**。
//    （用户 2026-10-05 原话只要求「新建一个 controller」，`api/Admin/…` 是文档作者补的路径。）
//
// 【链路（`52` §12.2，用户口径「先按 office 填写，再调后台方法形成 pdf」）】
//   ① NPOI 填空白模板（**只读**：不写账本、不更新宿主行、不产企业产物）
//   ② IFileConvertCore 转 PDF（纯内核，不上传产物）
//   ③ 落 MinIO `…/_preview/{模板名}.docx.pdf`（**固定 key，重复试填覆盖**）
//   ④ 回写 `cert_doc_template.PreviewPdfPath` + `PreviewTime`（列级）
//
// 【★ 门槛低于「发布」】试填**不要求** `PublishStatus='published'` ——
//   试填正是「发布前验证规则」的工具，要求已发布 = 发布后才能验证 = 死锁。
// ====================================================================

const AUDITOR_BASE = '/api/Auditor/DocFillPreview'

/** `POST /DocFillPreview/preview` 的返回（字段 PascalCase，与后端逐字一致） */
export interface DocFillPreviewResult {
  TemplateCode: string
  /** 空白模板在 MinIO 的路径（`…/_template/x.docx`） */
  TemplateStoragePath: string
  /**
   * ★ 试填预览 PDF 的路径（`…/_preview/x.docx.pdf`）。
   *
   * 中栏「填充后预览」视图直接把它交给 `DocPreview` ——
   * 后者只有 `storagePath`（无 `fileCode`）时走 `preview-by-path`，
   * 而该端点对 `.pdf` 是**原样透传**（读字节即返回，不二次转换）。
   */
  PreviewPdfPath: string
  PreviewTime?: string
  /** 路径是否已成功回写 DB（`false` = 字节已落 MinIO、前端照样能看，只是「试填过」没记住） */
  PathSaved: boolean
  /** `docx` / `xlsx` */
  FileKind: string
  FileName: string
  /** `filled` / `partial` / `skipped_no_anchor` / `failed` */
  Status: string
  Message?: string
  AnchorCount: number
  PendingCount: number
  Completion: number
  Verified: boolean
  /** ⛔ 非致命问题如实回报（越界 / 丢弃 / 自验收残留），⛔ 不静默 */
  Warnings: string[]
  Pendings: { AnchorRef: string; Key: string; Reason: string }[]
  /**
   * 取值明细。
   *
   * ⚠️ `Value` 是**未截断**的完整值（预览 Tab 编辑框的初值、回传覆盖的原文）；
   *    `Display` 是给表格看的 120 字符截断版 —— ⛔ 别拿 `Display` 回传，
   *    那会把长值静默截断后写进产物。
   */
  Values: {
    AnchorRef: string
    Key: string
    Source: string
    Value: string
    Display: string
  }[]
}

/** 试填人工覆盖一行（`POST /DocFillPreview/preview` 的 `Overrides[]`，PascalCase 与后端逐字一致） */
export interface FillPreviewOverride {
  /** 定位锚点的业务键（后端按它建索引，⛔ 不用 `Key` 定位） */
  AnchorRef: string
  /** 冗余传入便于日志对账 */
  Key?: string
  /** 覆盖后的文本；空串 = 显式清空（仍算已填，⛔ 不进 Pendings） */
  Value: string
}

/** `GET /DocFillPreview/preview-info` 的返回 */
export interface DocFillPreviewInfo {
  TemplateCode: string
  /** 空 = 从未试填过 ⇒ 中栏「填充后预览」视图禁用 */
  HasPreview: boolean
  PreviewPdfPath: string
  PreviewTime?: string
}

/**
 * ★ **试填一份空白模板并生成预览 PDF**（`52` B7）。
 *
 * 前置（前端闸 = C8）：锚点**配齐**才让点 —— `logic.anchorReadiness.ready`
 * （已上传模板 ∧ 扫描完成 ∧ 至少 1 个锚点 ∧ 无未配来源 ∧ 无孤儿）。
 * ⛔ 后端**不设**这道闸（`52` §六 P2'：程序不阻断业务组合，只如实推导）——
 * 闸门在前端，是为了「不让用户白等一次必然全空的试填」。
 *
 * ⚠️ 首次调用要等 LibreOffice 转 PDF（秒级，冷启动更久）⇒ 调用方需给加载态。
 *
 * @param overrides 人工覆盖（预览 Tab「自动填写 → 用户改 → 再预览」）。
 *   命中 `AnchorRef` 的锚点不走占位工厂、直接落笔；空串也算已填。
 *   ⛔ 只传**被用户改过**的项 —— 未传的锚点每次都会重新推导（结果稳定），
 *   全量回传只是把「用户没动的值」也冻结成人工值，白白丢掉规则更新的效果。
 */
export function runDocFillPreview(
  templateCode: string,
  overrides?: FillPreviewOverride[],
) {
  return yzhApi.post<ApiResponse<DocFillPreviewResult>>(`${AUDITOR_BASE}/preview`, {
    TemplateCode: templateCode,
    Overrides: overrides ?? [],
  })
}

/**
 * 读「这个模板试填过没有」—— 切文件时驱动中栏「填充后预览」视图的可用性。
 *
 * 【为什么不在左树节点上带】
 *   `directory-tree` 的文件叶子已带模板元信息（锚点数 / 发布状态）。
 *   再把 `PreviewPdfPath` 塞进去，每次**试填**都要让整棵树失效重取，
 *   而试填是高频动作 ⇒ 单文件粒度查询把刷新限制在**被试填的那一个文件**上。
 */
export function getDocFillPreviewInfo(templateCode: string) {
  return yzhApi.get<ApiResponse<DocFillPreviewInfo>>(
    `${AUDITOR_BASE}/preview-info`,
    { templateCode },
  )
}
