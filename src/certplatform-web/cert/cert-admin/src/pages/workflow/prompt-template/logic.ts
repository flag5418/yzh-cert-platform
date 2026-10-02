/**
 * PromptTemplateLogic — 提示词模板管理 Logic（SingleTableCore 架构）
 *
 * ⚠️ 2026-10-02：本页已改造为「提示词工作台」（三栏：列表 / 编辑器 / 上传试跑），
 *    不再走 SingleTableCore，**本文件当前无引用**（保留原因：单表 CRUD 路径
 *    `POST /api/PromptTemplate/{filter,add,update,delete}` 与 EntityConfig
 *    `Workflow/PromptTemplate.json` 仍然可用，需要时可直接复用）。
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
