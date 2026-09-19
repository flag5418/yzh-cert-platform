/**
 * CertificationBodyLogic — 认证机构管理 Logic
 *
 * 基于 CrudPageLogic 实现标准 CRUD 页面
 * 后端会同步 Sys_Organization 机构记录（合法差异，保留在 Controller）
 */

import { CrudPageLogic } from '@yzh-core'

export class CertificationBodyLogic extends CrudPageLogic<any> {
  controllerName = 'Foundation/CertificationBody'

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
    if (this.formData.Status === undefined) this.formData.Status = 'active'
    if (this.formData.Sort === undefined) this.formData.Sort = 0
    if (this.formData.MaxUsers === undefined) this.formData.MaxUsers = 100
    if (this.formData.MaxEnterprises === undefined) this.formData.MaxEnterprises = 1000
  }
}

export default CertificationBodyLogic
