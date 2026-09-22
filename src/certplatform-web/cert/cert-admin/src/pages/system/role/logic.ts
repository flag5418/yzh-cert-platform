/**
 * RolePageLogic - 角色管理 Logic（树形结构，TreeTableCore 架构）
 *
 * 架构：
 * - 树节点增删改/启停/按钮全部由后端 /treepconfig 的 TreeConfig 配置驱动
 * - 表单字段由 TreeFormConfig 配置驱动
 * - 本类零业务覆写：页面的全部差异都在后端配置里
 */

import { TreeTableCore } from '@yzh-core'

export class RolePageLogic extends TreeTableCore<any> {
  controllerName = 'Role'
}

export default RolePageLogic
