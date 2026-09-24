/**
 * RoleMenuLogic - 角色-菜单管理 Logic
 *
 * 继承 CheckTreeCore（AS-2 勾选授权内核，core check 端点约定与 RoleMenu 控制器对口）。
 * 勾选子菜单时后端自动补全祖先菜单（Applied 回传，内核局部更新缓存）。
 */

import { CheckTreeCore } from '@yzh-core'
import type { AssociationApi } from '@yzh-core'
import {
  getRoleTreeRoot,
  getRoleTreeChildren,
  getCheckTree,
  checkAdd,
  checkRemove,
  getAllAssociations,
} from '@/api/system/role-menu'

export class RoleMenuLogic extends CheckTreeCore {
  columns = [
    { prop: 'Name', label: '菜单名称', minWidth: 260 },
    { prop: 'Url', label: '路由', minWidth: 220 },
    { prop: 'NodeType', label: '类型', width: 90 },
  ]

  protected override get selectableNodeTypes(): string[] {
    return ['menu']
  }

  constructor() {
    const api: AssociationApi = {
      getTreeRoot: getRoleTreeRoot,
      getTreeChildren: getRoleTreeChildren,
      getAssociations: getCheckTree,
      add: checkAdd,
      remove: checkRemove,
      getAll: getAllAssociations,
    }
    super(api)
  }
}
