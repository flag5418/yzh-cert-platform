/**
 * AuditMemberLogic — 专家组设置 Logic（SingleTableCore 架构）
 *
 * 后端：CertPlatform.Auditor/Controllers/AuditMemberController.cs（YzhControllerBase<CertAuditMember>）
 * 路由：/api/Auditor/AuditMember
 *
 * 审核人员基本档案管理（姓名/职称/专业/资质/联系方式）。
 * 入场计划指派审核员时从本池子挑选，不关联登录账号。
 * 工作区隔离由后端 OnBuildingFilter 强制。
 */

import { SingleTableCore } from '@yzh-core'

export class AuditMemberLogic extends SingleTableCore<any> {
  controllerName = 'Auditor/AuditMember'

  protected override get defaultValues(): Record<string, any> {
    return { IsValid: 1, Gender: 0, Sort: 0 }
  }
}

export default AuditMemberLogic
