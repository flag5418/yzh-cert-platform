/**
 * UserLogic — 用户管理 Logic
 *
 * 基于 CrudPageLogic 实现标准 CRUD 页面
 * 后端：UserController (YzhControllerBase<Sys_User>)
 */

import { CrudPageLogic } from '@yzh-core'

export class UserLogic extends CrudPageLogic<any> {
  controllerName = 'System/User'

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
    if (this.formData.Enable === undefined) this.formData.Enable = 1
  }
}

export default UserLogic
