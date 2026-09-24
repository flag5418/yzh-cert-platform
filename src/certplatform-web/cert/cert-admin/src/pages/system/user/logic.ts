/**
 * UserLogic — 用户管理 Logic（SingleTableCore 架构）
 *
 * 后端：UserController (YzhControllerBase<Sys_User>)
 * 差异只剩默认值；列/表单/搜索/按钮全部由后端 EntityConfig 驱动。
 */

import { SingleTableCore } from '@yzh-core'

export class UserLogic extends SingleTableCore<any> {
  controllerName = 'System/User'

  /** 新增默认值 */
  protected override get defaultValues(): Record<string, any> {
    return { IsValid: 1 }
  }
}

export default UserLogic
