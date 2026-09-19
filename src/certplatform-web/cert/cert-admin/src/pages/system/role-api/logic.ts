/**
 * RoleApiLogic - 角色-接口权限管理 Logic
 *
 * 继承 BaseRoleTreeLogic，覆写差异化部分
 * 特殊逻辑：分组节点跟随子级勾选状态
 */

import {
  getRoleTreeRoot,
  getRoleTreeChildren,
  getCheckTree,
  checkAdd,
  checkRemove,
  getAllAssociations,
} from '@/api/system/role-api'
import { BaseRoleTreeLogic, type CheckTreeNode } from '../_shared/BaseRoleTreeLogic'

export class RoleApiLogic extends BaseRoleTreeLogic {
  columns = [
    { prop: 'Name', label: '分组 / 接口名称', minWidth: 260 },
    { prop: 'Method', label: '方法', width: 90 },
    { prop: 'Path', label: '路径', minWidth: 280 },
  ]

  protected readonly selectableNodeTypes = ['api']

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

  /** 特殊逻辑：分组节点跟随子级 */
  protected handleRoleSelectPostProcess(data: CheckTreeNode[], cached: Set<string>): void {
    for (const node of data.filter((n) => n.NodeType === 'group')) {
      const children = data.filter((n) => n.ParentCode === node.Code)
      node.CheckFlag =
        children.length > 0 && children.every((child) => cached.has(child.Code))
    }
  }
}
