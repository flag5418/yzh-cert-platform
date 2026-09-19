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
  /** 有效标志（1=启用，0=禁用）；同步 Sys_Organization.Enable */
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
  Status?: string
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

/** 标准目录配置 */
export interface StandardDirectoryConfig {
  Id?: number
  Code: string
  DirectoryCode: string
  OrgCode?: string
  StandardCode: string
  StandardName?: string
  PhaseCode: string
  PhaseName?: string
  RootFolderName?: string
  Status?: string
  Enable?: boolean
  Creator?: string
  CreateDate?: string
  Modifier?: string
  ModifyDate?: string
  Deleter?: string
  DeleteTime?: string
  Status_field?: string
  Enable_field?: boolean
  Sort?: number
  Remark?: string
}

/** 标准目录文件夹 */
export interface StandardDirectoryFolder {
  Id?: number
  Code: string
  FolderCode: string
  DirectoryCode: string
  ParentCode?: string
  FolderName: string
  Depth?: number
  SortOrder?: number
  Status?: string
  Enable?: boolean
  IsValid?: boolean
  TaskId?: string
  FullPath?: string
  Creator?: string
  CreateDate?: string
  Modifier?: string
  ModifyDate?: string
  Remark?: string
  Children?: StandardDirectoryFolder[]
}

/** 标准目录文件 */
export interface StandardDirectoryFile {
  Id?: number
  Code: string
  FileCode: string
  FolderCode: string
  DirectoryCode: string
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
  Enable?: boolean
  IsValid?: boolean
  TaskId?: string
  UploadStatus?: string
  StoragePath?: string
  FullPath?: string
  ConvertedStoragePath?: string
  ConvertStatus?: string
  ConvertMessage?: string
  ConvertDate?: string
  Creator?: string
  CreateDate?: string
  Modifier?: string
  ModifyDate?: string
  Remark?: string
}

/** 上传任务 */
export interface UploadTask {
  Id?: number
  TaskId: string
  DirectoryCode: string
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

/** 报告模板 */
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

/** 报告章节 */
export interface ReportSection {
  Id: number
  Code: string
  ReportCode: string
  ClauseCode: string
  SectionName: string
  SectionNameEn: string
  Content: string
  SortOrder: number
  IsActive: number
  WorkflowCode: string
  WorkflowConfig: string
  LayoutJson: string
  SectionJson: string
  OrgCode: string
  Remark: string
  IsValid: number
  CreateBy: string
  CreateDate: string
  ModifyDate: string
}
