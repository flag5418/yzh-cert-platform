/**
 * RoleUserLogic - 角色-用户管理 Logic
 *
 * 继承 CheckTreeCrudCore（AS-2 勾选授权内核 + 树节点增删改）：
 * - 左侧角色树：懒加载 + 角色增删改（节点动作与弹窗字段全部由 /api/Role/treepconfig 驱动），
 *   树底「新增角色」按钮走 openRoleAddFromFooter（与 organization 同构）
 * - 右侧：机构+用户混合勾选树（checkTree / check/add / check/remove）
 * - 角色树复用 /api/Role/tree/*（api 模块内收编，构造器注入 CheckTreeCrudApi）
 */

import { CheckTreeCrudCore } from '@yzh-core'
import type { CheckTreeCrudApi } from '@yzh-core'
import {
  getRoleTreeRoot,
  getRoleTreeChildren,
  getRoleTreePageConfig,
  addRoleTreeNode,
  updateRoleTreeNode,
  deleteRoleTreeNodes,
  getCheckTree,
  checkAdd,
  checkRemove,
  getAllAssociations,
} from '../../../api/system/role-user'

export class RoleUserLogic extends CheckTreeCrudCore {
  columns = [
    { prop: 'Name', label: '名称', minWidth: 200 },
    { prop: 'NodeType', label: '类型', width: 80 },
  ]

  protected override get selectableNodeTypes(): string[] {
    return ['user']
  }

  /**
   * 角色可多根（Sys_Role 存量 14 行中 12 行 ParentId=0 / ParentCode 为空）
   * → 根级新增不要求先选中节点：树底「新增角色」未选中时建根角色，
   *   已选中时建在其下（弹窗「上级角色」会显示目标父级）。
   */
  protected override get requireTreeSelectionForAdd(): boolean {
    return false
  }

  /**
   * 树底「新增角色」（`#treeFooter` 插槽，绑定零参 handler）：
   * 有选中加子级、无选中建根 —— 与 `organization.openOrgAddFromFooter` 同构。
   */
  openRoleAddFromFooter(): boolean {
    return this.openTreeNodeDialog(null, this.selectedNode.value)
  }

  constructor() {
    const api: CheckTreeCrudApi = {
      getTreeRoot: getRoleTreeRoot,
      getTreeChildren: getRoleTreeChildren,
      getAssociations: getCheckTree,
      add: checkAdd,
      remove: checkRemove,
      getAll: getAllAssociations,
      getTreePageConfig: getRoleTreePageConfig,
      addTreeNode: addRoleTreeNode,
      updateTreeNode: updateRoleTreeNode,
      deleteTreeNodes: deleteRoleTreeNodes,
    }
    super(api)
  }
}

export default RoleUserLogic
