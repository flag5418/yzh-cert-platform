/**
 * PhaseDefinitionLogic — 阶段定义管理 Logic
 *
 * 基于 CrudPageLogic 实现标准 CRUD 页面
 */

import { CrudPageLogic } from '@yzh-core'

export class PhaseDefinitionLogic extends CrudPageLogic<any> {
  controllerName = 'Foundation/PhaseDefinition'

  /** 行操作按钮（基类已自动注入 toggle-valid） */

  /** 初始化 */
  async init(): Promise<void> {
    await this.loadConfig()
  }

  /** 新增弹窗：补默认值 */
  openAddDialog(): void {
    super.openAddDialog()
    if (this.formData.IsValid === undefined) this.formData.IsValid = 1
    if (this.formData.SequenceOrder === undefined) this.formData.SequenceOrder = 0
  }
}

export default PhaseDefinitionLogic
