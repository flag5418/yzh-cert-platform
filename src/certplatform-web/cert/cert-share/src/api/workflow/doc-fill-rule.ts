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
    /** `org` / `scope` / `template` */
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
  return yzhApi.get<ApiResponse<{ Nodes: DocTemplateTreeNode[]; Total: number }>>(
    `${BASE}/DocTemplate/tree`,
  )
}

/**
 * 某 `promptCode` 的全部版本（**含已软删**，按 `Version` 降序）。
 *
 * ⚠️ `orgCode` 传空串 = 全局作用域；后端按「机构非空优先」选取。
 */
export function getDocFillPromptVersions(promptCode: string, orgCode = '') {
  return yzhApi.get<ApiResponse<{ PromptCode: string; OrgCode: string; Total: number; Items: DocFillPromptVersion[] }>>(
    `${BASE}/DocFillPrompt/versions`,
    { promptCode, orgCode },
  )
}

/**
 * 解析「某模板实际会用到哪一版提示词」——**唯一选取口径**：
 * ① 机构更具体优先 → ② `IsDefault` 优先 → ③ `Version` 新优先。
 *
 * 页面用它在「全文规则」标签页顶部显示「当前生效版本」，
 * 避免实施人员看到 3 个版本却不知道运行期会用哪一个。
 */
export function resolveDocFillPrompt(promptCode: string, orgCode = '') {
  return yzhApi.get<ApiResponse<any>>(`${BASE}/DocFillPrompt/resolve`, { promptCode, orgCode })
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
