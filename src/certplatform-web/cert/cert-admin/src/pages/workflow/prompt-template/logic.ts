/**
 * PromptTemplateLogic — 提示词模板管理 Logic
 *
 * 基于 CrudPageLogic 实现标准 CRUD 页面
 * 后端：PromptTemplateController (YzhControllerBase<PromptTemplate>)
 */

import {CrudPageLogic} from '@yzh-core'

export class PromptTemplateLogic extends CrudPageLogic<any> {
  controllerName = 'PromptTemplate'

  /** 行操作按钮 */
  get rowActionButtons(): Record<string, string> {
    return {
      edit: '编辑',
      delete: '删除'
    }
  }

  /** 初始化 */
  async init(): Promise<void> {
    await this.loadConfig()
  }

  /** 新增弹窗：补默认值 */
  openAddDialog(): void {
    super.openAddDialog()
    if (this.formData.PromptType === undefined) this.formData.PromptType = 'document_analysis'
    if (this.formData.Version === undefined) this.formData.Version = 1
    if (this.formData.IsActive === undefined) this.formData.IsActive = true
    if (this.formData.IsValid === undefined) this.formData.IsValid = 1
  }
}

export default PromptTemplateLogic
