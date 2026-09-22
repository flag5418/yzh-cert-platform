/**
 * PhaseDefinitionLogic — 阶段定义管理 Logic（SingleTableCore 架构）
 */

import { SingleTableCore } from '@yzh-core'

export class PhaseDefinitionLogic extends SingleTableCore<any> {
  controllerName = 'Foundation/PhaseDefinition'

  /** 新增默认值 */
  protected override get defaultValues(): Record<string, any> {
    return {
      IsValid: 1,
      SequenceOrder: 0,
    }
  }
}

export default PhaseDefinitionLogic
