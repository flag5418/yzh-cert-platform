/**
 * 角色树相关公共类型
 *
 * P4：定义已上移 yzh.vue.core/src/types/association.ts（单一来源），
 * 本文件保留为 re-export 垫片，既有 `@share/types` 消费者不受影响；
 * role-user/role-menu/role-api 下沉完成后本垫片可随 P6 清理删除。
 */

export type { CheckTreeNode, TreeNodeSelection, AssociationDto, RoleTreeItem } from '@yzh-core'
