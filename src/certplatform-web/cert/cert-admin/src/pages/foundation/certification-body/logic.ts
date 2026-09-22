/**
 * CertificationBodyLogic — 认证机构管理 Logic（SingleTableCore 架构）
 *
 * 后端会同步 Sys_Organization 机构记录（合法差异，保留在 Controller）
 */

import { SingleTableCore } from '@yzh-core'

export class CertificationBodyLogic extends SingleTableCore<any> {
  controllerName = 'Foundation/CertificationBody'

  /** 新增默认值 */
  protected override get defaultValues(): Record<string, any> {
    return {
      IsValid: 1,
      Status: 'active',
      Sort: 0,
      MaxUsers: 100,
      MaxEnterprises: 1000,
    }
  }

  /** toggle-valid 确认弹窗显示机构名称 */
  protected override get entityNameField(): string {
    return 'OrgName'
  }
}

export default CertificationBodyLogic
