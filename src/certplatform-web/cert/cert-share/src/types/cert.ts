/** 认证业务类型定义
 *
 * 字段命名约定（V4 强制）：PascalCase
 * - 与后端 C# 实体属性、CrudPageLogic/TreeTableLogic、EntityConfig 字段完全一致
 * - 前端不做 camelCase/PascalCase 互转，client.ts 原样透传
 */

export interface ISOStandard {
  Id?: number
  Code?: string
  StandardCode: string
  StandardName: string
  VersionYear: number
  Category?: string
  CategoryName?: string
  Description?: string
  Remark?: string
  Status?: string
  StatusName?: string
  CreateDate?: string
  ModifyDate?: string
}

export interface CertificationBody {
  Id: number
  OrgCode: string
  OrgName: string
  OrgShortName: string
  OrgStatus: number
  CreateDate: string
  UpdateDate: string
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
  CreateDate?: string
  ModifyDate?: string
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
  CreateTime?: string
}

export interface Enterprise {
  Id: number
  EntCode: string
  EntName: string
  EntStatus: number
  OrgId: number
  CreateDate: string
}

export interface AuditTask {
  Id: number
  TaskNo: string
  EntId: number
  EntName: string
  StandardId: number
  StandardName: string
  StageId: number
  StageName: string
  AuditorId: number
  AuditorName: string
  TaskStatus: number
  CreateDate: string
  AuditDate: string
}
