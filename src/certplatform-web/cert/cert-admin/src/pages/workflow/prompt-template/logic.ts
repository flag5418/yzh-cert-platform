/**
 * PromptTemplateLogic — 提示词模板管理 Logic（SingleTableCore 架构）
 *
 * 后端：PromptTemplateController (YzhControllerBase<PromptTemplate>)
 */

import { SingleTableCore } from '@yzh-core'

export class PromptTemplateLogic extends SingleTableCore<any> {
  controllerName = 'PromptTemplate'

  /** 新增默认值 */
  protected override get defaultValues(): Record<string, any> {
    return {
      PromptType: 'document_analysis',
      Version: 1,
      IsActive: true,
      IsValid: 1,
    }
  }
}

export default PromptTemplateLogic
