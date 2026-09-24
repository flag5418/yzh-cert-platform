/**
 * RoleApiLogic - 角色-接口权限管理 Logic
 *
 * 继承 CheckTreeCore（AS-2 勾选授权内核，core check 端点约定与 RoleApi 控制器对口）。
 * 特殊逻辑：分组节点跟随子级勾选状态（afterAssociationsLoaded 覆写）。
 */

import { CheckTreeCore } from '@yzh-core'
import type { AssociationApi, CheckTreeNode } from '@yzh-core'
import {
  getRoleTreeRoot,
  getRoleTreeChildren,
  getCheckTree,
  checkAdd,
  checkRemove,
  getAllAssociations,
} from '@/api/system/role-api'

export class RoleApiLogic extends CheckTreeCore {
  columns = [
    { prop: 'Name', label: '分组 / 接口名称', minWidth: 260 },
    { prop: 'Method', label: '方法', width: 90 },
    { prop: 'Path', label: '路径', minWidth: 280 },
  ]

  protected override get selectableNodeTypes(): string[] {
    return ['api']
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

  /** 特殊逻辑：分组节点跟随子级 */
  protected override afterAssociationsLoaded(data: CheckTreeNode[], cached: Set<string>): void {
    for (const node of data.filter((n) => n.NodeType === 'group')) {
      const children = data.filter((n) => n.ParentCode === node.Code)
      node.CheckFlag =
        children.length > 0 && children.every((child) => cached.has(child.Code))
    }
  }
}
