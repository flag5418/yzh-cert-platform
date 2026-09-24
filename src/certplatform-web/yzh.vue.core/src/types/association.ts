/**
 * 关联型（勾选授权）页面公共类型
 *
 * P4 上移：原 cert-share/src/types/role-tree.ts，随 role-user/role-menu/role-api
 * 三页下沉 yzh.vue.core 而迁入（share 改为 re-export，保持既有 @share/types 消费者兼容）。
 */

/** 混合树节点（后端 CheckTreeNodeDto） */
export interface CheckTreeNode {
  Code: string
  Name: string
  ParentCode?: string | null
  NodeType: string
  CheckFlag: boolean
  Extra?: Record<string, any>
}

/** 节点选择项（后端 TreeNodeSelection） */
export interface TreeNodeSelection {
  Code: string
  NodeType: string
}

/** 关联关系 DTO（后端 AssociationDto） */
export interface AssociationDto {
  ContextCode: string
  TargetCode: string
  NodeType: string
}

/** 树节点 DTO（后端 TreeItemDto，角色树） */
export interface RoleTreeItem {
  Code: string
  Name: string
  ParentCode?: string | null
  NodeType?: string
  IsLeaf?: boolean
  Level?: number
  Extra?: Record<string, any>
}
