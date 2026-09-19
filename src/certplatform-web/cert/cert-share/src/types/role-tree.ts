/**
 * 角色树相关公共类型
 *
 * 来源：role-user.ts / role-menu.ts / role-api.ts 重复定义
 * 统一抽取到此处，三处 import 替换
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
