/**
 * CertStageLogic — 认证阶段管理 Logic
 *
 * 基于 CrudPageLogic 实现标准 CRUD 页面
 * 后端 Controller：CertStageController (YzhControllerBase<CertStage>)
 */

import { CrudPageLogic } from '@yzh-core'

export class CertStageLogic extends CrudPageLogic<any> {
  controllerName = 'Foundation/CertStage'

  /** 行操作按钮 */
  get rowActionButtons(): Record<string, string> {
    return {
      edit: '编辑',
      toggleValid: '启用/禁用',
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
    if (this.formData.IsValid === undefined) this.formData.IsValid = 1
    if (this.formData.SortOrder === undefined) this.formData.SortOrder = 0
    if (this.formData.Category === undefined) this.formData.Category = 'process'
  }
}

export default CertStageLogic
