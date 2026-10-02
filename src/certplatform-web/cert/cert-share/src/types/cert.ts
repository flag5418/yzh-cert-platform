/** 认证业务类型定义
 *
 * 字段命名约定（V4 强制）：PascalCase
 * - 与后端 C# 实体属性、CrudPageLogic/TreeTableLogic、EntityConfig 字段完全一致
 * - 前端不做 camelCase/PascalCase 互转，client.ts 原样透传
 *
 * §16 铁律：TS 属性名 == DB 列名 == C# 属性名
 */

export interface ISOStandard {
  Id?: number
  Code?: string
  CbCode?: string
  StandardCode: string
  StandardName: string
  VersionYear: number
  Category?: string
  CategoryName?: string
  Description?: string
  Remark?: string
  Status?: string
  StatusName?: string
  ParentCode?: string
  IsLeaf?: boolean
  Sort?: number
  IsValid?: number
  CreateBy?: string
  CreateTime?: string
  UpdateBy?: string
  UpdateTime?: string
  DeleteBy?: string
  DeleteTime?: string
  IsDeleted?: boolean
}

export interface CertificationBody {
  Id?: number
  Code?: string
  /** 数据权限隔离键 → Sys_Organization.Code（单 Code 策略下 == Code） */
  OrgCode?: string
  Name: string
  ShortName?: string
  CbCode?: string
  LegalPerson?: string
  ContactName?: string
  ContactPhone?: string
  ContactEmail?: string
  Address?: string
  LogoUrl?: string
  ScopeText?: string
  ThemeConfig?: string
  LoginConfig?: string
  MaxUsers?: number
  MaxEnterprises?: number
  ExpireDate?: string
  /** 运营状态：active / suspended / inactive */
  Status?: string
  Sort?: number
  /** 有效标志（1=启用，0=禁用）；同步 Sys_Organization.IsValid */
  IsValid?: number
  Remark?: string
  CreateBy?: string
  CreateTime?: string
  UpdateBy?: string
  UpdateTime?: string
  DeleteBy?: string
  DeleteTime?: string
  IsDeleted?: boolean
}

export interface ISOClause {
  Id?: number
  Code?: string
  StandardCode: string
  ParentCode?: string
  ClauseNumber: string
  Title: string
  Description?: string
  SortOrder?: number
  Sort?: number
  IsValid?: number
  Remark?: string
  CreateBy?: string
  CreateTime?: string
  UpdateBy?: string
  UpdateTime?: string
  DeleteBy?: string
  DeleteTime?: string
  IsDeleted?: boolean
}

export interface PhaseDefinition {
  Id?: number
  Code?: string
  PhaseCode: string
  PhaseName: string
  SequenceOrder: number
  Description?: string
  IsValid: number
  StatusName?: string
  CreateBy?: string
  CreateTime?: string
  UpdateBy?: string
  UpdateTime?: string
  DeleteBy?: string
  DeleteTime?: string
  IsDeleted?: boolean
}

export interface Enterprise {
  Id?: number
  Code?: string
  OrgCode?: string
  EnterpriseNo: string
  Name: string
  ShortName?: string
  CreditCode?: string
  LegalPerson?: string
  Province?: string
  City?: string
  Address?: string
  IndustryType?: string
  EmployeeCount?: number
  CertScope?: string
  ContactName?: string
  ContactPhone?: string
  ContactEmail?: string
  ArchiveDate?: string
  Sort?: number
  IsValid?: number
  Remark?: string
  CreateBy?: string
  CreateTime?: string
  UpdateBy?: string
  UpdateTime?: string
  DeleteBy?: string
  DeleteTime?: string
  IsDeleted?: boolean
}

export interface CertStage {
  Id?: number
  Code?: string
  StageCode: string
  StageName: string
  Category?: string
  SortOrder: number
  Description?: string
  IsValid: number
  Status?: string
  StatusName?: string
  Remark?: string
  CreateBy?: string
  CreateTime?: string
  UpdateBy?: string
  UpdateTime?: string
  DeleteBy?: string
  DeleteTime?: string
  IsDeleted?: boolean
}

export interface AuditTask {
  Id?: number
  Code?: string
  OrgCode?: string
  PhaseCode: string
  TaskNumber: string
  AuditorCode: string
  PlannedDate?: string
  ActualStartDate?: string
  ActualCompleteDate?: string
  AuditScope?: string
  Status?: string
  IsValid?: number
  Sort?: number
  Remark?: string
  CreateBy?: string
  CreateTime?: string
  UpdateBy?: string
  UpdateTime?: string
  DeleteBy?: string
  DeleteTime?: string
  IsDeleted?: boolean
}

export interface AuditorProfile {
  Id?: number
  Code?: string
  OrgCode?: string
  UserCode?: string
  AuditorNo: string
  AuditorName: string
  Phone: string
  Email?: string
  Qualification?: string
  ExpertiseAreas?: string
  Status?: string
  Remark?: string
  CreateBy?: string
  CreateTime?: string
  UpdateBy?: string
  UpdateTime?: string
  DeleteBy?: string
  DeleteTime?: string
  IsDeleted?: boolean
  IsValid?: number
}

/** 机构-标准关联（右表行数据） */
export interface CertOrgStandardItem {
  Code: string
  StandardCode: string
  StandardName: string
  VersionYear: number
  Category: string
  /** 是否已关联当前选中机构 */
  Linked: boolean
}

/** 机构-阶段关联（右表行数据） */
export interface CertOrgStageItem {
  Code: string
  PhaseCode: string
  PhaseName: string
  SortOrder: number
  /** 是否已关联当前选中机构 */
  Linked: boolean
}

// ========================================================
// 标准目录管理
// ========================================================

/** 标准目录配置（cert_standard_directory_config；一行 = 一个「标准 × 阶段」，平台全局、无 OrgCode） */
export interface StandardDirectoryConfig {
  Id?: number
  /** 业务键 GUID（复合编码 DirectoryCode/SDC-… 已删除，2026-09-26 决策 ⑦） */
  Code: string
  /** 机构 Code → certification_body.Code（决策⑳修订 2026-09-27：按机构隔离，uk 三键含本列） */
  OrgCode: string
  /** 标准 Code → cert_iso_standard.Code */
  StandardCode: string
  /** 阶段 Code → cert_cert_stage.Code（原列名 PhaseCode，决策 ⑩） */
  StageCode: string
  RootFolderName?: string
  Status?: string
  StatusField?: string
  Sort?: number
  Remark?: string
  IsValid?: number
  Creator?: string
  CreateDate?: string
  CreateTime?: string
  Modifier?: string
  ModifyDate?: string
  Deleter?: string
  DeleteTime?: string
}

/** 标准目录文件夹（cert_standard_directory_folder；复合编码 FolderCode/FD-… 已删除，Code 即业务键） */
export interface StandardDirectoryFolder {
  Id?: number
  Code: string
  /** 配置 Code → cert_standard_directory_config.Code（原列名 DirectoryCode） */
  ConfigCode: string
  ParentCode?: string
  FolderName: string
  Depth?: number
  SortOrder?: number
  Status?: string
  IsValid?: number
  TaskId?: string
  FullPath?: string
  Creator?: string
  CreateDate?: string
  CreateTime?: string
  Modifier?: string
  ModifyDate?: string
  Remark?: string
  Children?: StandardDirectoryFolder[]
}

/** 标准目录文件（cert_standard_directory_file；复合编码 FileCode/FL-… 已删除，Code 即业务键） */
export interface StandardDirectoryFile {
  Id?: number
  Code: string
  /** 所属文件夹 Code → cert_standard_directory_folder.Code；根级文件恒为 "" */
  FolderCode: string
  /** 配置 Code → cert_standard_directory_config.Code（原列名 DirectoryCode） */
  ConfigCode: string
  FileName: string
  FileType?: string
  FilePattern?: string
  FileSize?: number
  IsRequired?: boolean
  MaxFileSizeMB?: number
  Description?: string
  SortOrder?: number
  ExtractionEnabled?: boolean
  ExtractionRules?: string
  PreCheckRequired?: boolean
  ComplianceRequired?: boolean
  Status?: string
  IsValid?: number
  TaskId?: string
  UploadStatus?: string
  StoragePath?: string
  FullPath?: string
  ConvertedStoragePath?: string
  ConvertStatus?: string
  ConvertMessage?: string
  ConvertDate?: string
  /** 预览 PDF 产物路径（PDF/图片透传时 == StoragePath）—— 2026-09-26 双产物链 */
  PreviewPdfPath?: string
  /** 提取用 Markdown 产物路径 */
  MarkdownPath?: string
  /** Markdown 转换状态：none/pending/converting/completed/failed/unsupported */
  MarkdownStatus?: string
  /** Markdown 失败原因 / OCR 能力边界提示 */
  MarkdownMessage?: string
  Creator?: string
  CreateDate?: string
  CreateTime?: string
  Modifier?: string
  ModifyDate?: string
  Remark?: string
}

/** 上传任务（cert_upload_task；原列名 DirectoryCode → ConfigCode） */
export interface UploadTask {
  Id?: number
  TaskId: string
  ConfigCode: string
  TotalFiles?: number
  TotalSize?: number
  SuccessCount?: number
  Status?: string
  Creator?: string
  CreateDate?: string
  ModifyDate?: string
  ExpireTime?: string
}

/** 目录模板文件夹 */
export interface DirectoryTemplate {
  Id?: number
  ConfigCode: string
  ParentCode?: string
  FolderName: string
  SortOrder?: number
}

/** 文件要求/模板文件 */
export interface FileRequirement {
  Id?: number
  Code?: string
  FolderCode: string
  FileNameTemplate: string
  FileType: string
  IsRequired?: boolean
  MaxSizeMB?: number
  Description?: string
  SortOrder?: number
  TemplateStoragePath?: string
  TemplateFileName?: string
  StandardCode?: string
}

/** 组织树节点 */
export interface OrgTreeNode {
  Code: string
  Name: string
  NodeType?: string
  IsLeaf?: boolean
  Level?: number
  Children?: OrgTreeNode[]
}

/** 文件上传进度 */
export interface FileUploadProgress {
  FileCode: string
  FileName: string
  Status: 'pending' | 'uploading' | 'uploaded' | 'failed'
  Progress: number
  Error?: string
}

/** 队列状态 */
export interface QueueStatus {
  runningQueues: number
  pendingQueues: number
  todayCompleted: number
  todayFailed: number
  todayCancelled: number
  maxConcurrent: number
  runningWorkers: number
}

// ========================================================
// 报告定义
// ========================================================

/**
 * ★【已废弃 2026-09-29】报告模板
 * <p>报告主表已废弃（D34：不做报表系统，章节定义改为扁平结构）。
 * 保留此 interface 仅供历史代码编译期提示，新代码不应引用。</p>
 * @deprecated 表已改名为 z_deprecated_cert_report_template，0 行，不再写入
 */
export interface ReportTemplate {
  Id: number
  Code: string
  OrgCode: string
  StandardCode: string
  PhaseCode: string
  TemplateName: string
  TemplateFilePath: string
  Remark: string
  IsDefault: boolean
  IsValid: number
  CreateBy: string
  CreateDate: string
  ModifyDate: string
}

/**
 * ★ 报告章节（★2026-09-29 去主表化，扁平结构）
 * <p>★ 与 NC 检查规则（ValidationRule）完全对称：都是配置层、都按
 * (OrgCode + StandardCode + PhaseCode) 三元组定位、都用 IsValid 启用。</p>
 * <p>★ 相比旧版：删 ReportCode（语义错位列），增 StandardCode/PhaseCode，
 * IsActive 统一为 IsValid（铁律九）。</p>
 */
export interface ReportSection {
  Id: number
  Code: string
  /** ★ 认证机构编码（配置层归属，不是专家工作区） */
  OrgCode: string
  /** ★ 标准编码 */
  StandardCode: string
  /** ★ 阶段编码（cert_cert_stage.Code，GUID 不是业务码） */
  PhaseCode: string
  SectionName: string
  SectionNameEn: string
  /** 章节内容（模板示例正文，纯文本） */
  Content: string
  /** 章节排序（唯一键组成：OrgCode+StandardCode+PhaseCode+SortOrder） */
  SortOrder: number
  /** ★ 有效标志（1=启用，0=禁用）—— 铁律九唯一启用字段 */
  IsValid: number
  /** 对应条款编码（★可空：概述/结论类章节不映射条款） */
  ClauseCode: string
  WorkflowCode: string
  /** ★ 工作流 DAG（★注意：规则的 DAG 在 RuleJson，本表是 WorkflowConfig，字段名不同） */
  WorkflowConfig: string
  LayoutJson: string
  SectionJson: string
  Remark: string
  Status: string
  CreateBy: string
  CreateTime: string
  UpdateBy: string
  UpdateTime: string
}
