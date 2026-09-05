/** 认证业务类型定义 */

export interface ISOStandard {
  id: number
  standardCode: string
  standardName: string
  standardDesc: string
  standardStatus: number
  orgId: number
  orgName: string
  createDate: string
  updateDate: string
}

export interface CertificationBody {
  id: number
  orgCode: string
  orgName: string
  orgShortName: string
  orgStatus: number
  createDate: string
  updateDate: string
}

export interface ISOClause {
  id: number
  standardId: number
  clauseNo: string
  clauseName: string
  createDate: string
}

export interface CertStage {
  id: number
  standardId: number
  stageNo: number
  stageName: string
  createDate: string
}

export interface Enterprise {
  id: number
  entCode: string
  entName: string
  entStatus: number
  orgId: number
  createDate: string
}

export interface AuditTask {
  id: number
  taskNo: string
  entId: number
  entName: string
  standardId: number
  standardName: string
  stageId: number
  stageName: string
  auditorId: number
  auditorName: string
  taskStatus: number
  createDate: string
  auditDate: string
}
