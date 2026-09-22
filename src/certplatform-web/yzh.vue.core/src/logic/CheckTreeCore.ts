/**
 * CheckTreeCore - 勾选授权内核（AS-2，类型A）
 *
 * 端点约定（checkTree / check/add / check/remove / check/all）：
 * - 左树（如角色）选择节点 → 右侧加载 checkTree（含 CheckFlag）
 * - 勾选/取消 → check/add / check/remove 增量提交
 * - check/all 用于本地缓存初始化
 *
 * 在 AssociationTreeCore 之上补充：
 * - selectableNodeTypes 强约束（如 ['user'] / ['menu'] / ['api']）
 * - 祖先补全钩子（勾选子节点后由服务端补全祖先并回传 Applied）
 */

import { AssociationTreeCore } from './AssociationTreeCore'

export type { AssociationApi, AssociationSelection } from './AssociationTreeCore'

export abstract class CheckTreeCore extends AssociationTreeCore {
  /** 勾选树的列配置（页面绑定 :columns） */
  abstract columns: Array<{ prop: string; label: string; minWidth?: number; width?: number }>

  /** 可勾选的节点类型（如 ['menu']）—— 子类必须声明 */
  protected override get selectableNodeTypes(): string[] {
    return []
  }
}

export default CheckTreeCore
