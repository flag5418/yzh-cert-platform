/**
 * RoleMenuLogic - 角色-菜单管理 Logic
 *
 * 继承 BaseRoleTreeLogic，覆写差异化部分
 */

import {
  getRoleTreeRoot,
  getRoleTreeChildren,
  getCheckTree,
  checkAdd,
  checkRemove,
  getAllAssociations,
} from '@/api/system/role-menu'
import { BaseRoleTreeLogic } from '../_shared/BaseRoleTreeLogic'

export class RoleMenuLogic extends BaseRoleTreeLogic {
  columns = [
    { prop: 'Name', label: '菜单名称', minWidth: 260 },
    { prop: 'Url', label: '路由', minWidth: 220 },
    { prop: 'NodeType', label: '类型', width: 90 },
  ]

  protected readonly selectableNodeTypes = ['menu']

  constructor() {
    super({
      getRoleTreeRoot,
      getRoleTreeChildren,
      getCheckTree,
      checkAdd,
      checkRemove,
      getAllAssociations,
    })
  }
}
