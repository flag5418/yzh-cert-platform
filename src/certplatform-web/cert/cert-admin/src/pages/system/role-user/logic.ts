/**
 * RoleUserLogic - 角色-用户管理 Logic
 *
 * 继承 CheckTreeCore（AS-2 勾选授权内核，core check 端点约定与 Role 控制器对口）。
 * 角色树复用 /api/Role/tree/*（api 模块内收编，构造器注入 AssociationApi）。
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
} from '@/api/system/role-user'

export class RoleUserLogic extends CheckTreeCore {
  columns = [
    { prop: 'Name', label: '名称', minWidth: 200 },
    { prop: 'NodeType', label: '类型', width: 80 },
  ]

  protected override get selectableNodeTypes(): string[] {
    return ['user']
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
