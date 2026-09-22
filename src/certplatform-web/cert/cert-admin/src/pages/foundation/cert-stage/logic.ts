/**
 * CertStageLogic — 认证阶段管理 Logic（SingleTableCore 架构）
 *
 * 后端 Controller：CertStageController (YzhControllerBase<CertStage>)
 * 差异只剩默认值；列/表单/搜索/按钮全部由后端 EntityConfig 驱动。
 */

import { SingleTableCore } from '@yzh-core'

export class CertStageLogic extends SingleTableCore<any> {
  controllerName = 'Foundation/CertStage'

  /** 新增默认值 */
  protected override get defaultValues(): Record<string, any> {
    return {
      IsValid: 1,
      SortOrder: 0,
      Category: 'process',
    }
  }
}

export default CertStageLogic
