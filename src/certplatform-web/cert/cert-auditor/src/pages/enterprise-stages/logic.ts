/**
 * EnterpriseStageLogic - 企业-阶段-标准关联 Logic（core 关联型范式 · AS-2 勾选授权内核）
 *
 * 语义（`AssociationTreeCore` 文档原文）：
 * 「左树选择 + 右侧关联态维护（非 CRUD）」—— 勾选即建关联，取消即解除。
 *
 * 参照实现：`yzh.vue.core/src/pages/system/role-api/`（角色-接口授权），结构 1:1 对应。
 *
 * ★ 本文件**只声明差异**：列、可勾选类型、阶段行跟随子级。
 *   全选 / 取消全选 / 展开折叠 / 搜索 / 差集勾选 / 乐观更新 / 回滚 / 左树 badge
 *   **全部由内核提供** —— ⛔ 不要手写。
 *
 * ★ 后端契约：`EnterpriseStageController` 的
 *   `tree/root` / `tree/children` / `checkTree` / `check/add` / `check/remove` / `check/all`。
 */
import { CheckTreeCore } from '@yzh-core'
import type { AssociationApi, CheckTreeNode } from '@yzh-core'
import {
  checkAdd,
  checkRemove,
  getAllAssociations,
  getEnterpriseTreeChildren,
  getEnterpriseTreeRoot,
  getStageStandardTree,
} from '@share/api/cert/enterprise-stage'

/**
 * 右侧勾选树表行。
 *
 * `CheckTreeNode` 是内核契约（`Code / Name / ParentCode / NodeType / CheckFlag / Extra`）；
 * 其余字段是后端放在 `Extra` 里的展示字段 ——
 * `AssociationTreeCore.handleNodeSelect` 会把 `Extra` **平铺**到节点上，列可直接绑。
 */
export interface StageStandardRow extends CheckTreeNode {
  StageCode?: string
  StageName?: string
  /** 关联键 = `cert_iso_standard.Code`（GUID）；⛔ 不要用于展示 */
  StandardCode?: string
  /** 展示用标准编号（如 `iso9001-2015`）= `cert_iso_standard.StandardCode` */
  StandardNo?: string
  StandardName?: string
  VersionYear?: number
  Category?: string
  /** 仅阶段行有意义：该阶段下的标准数 */
  ChildCount?: number
}

export class EnterpriseStageLogic extends CheckTreeCore {
  /** 右侧勾选树表的列（`YzhTreeTableCheckSelector` 的 `ColumnConfig`） */
  columns = [
    { prop: 'Name', label: '阶段 / 标准', minWidth: 280 },
    { prop: 'StandardNo', label: '标准编号', minWidth: 180 },
    { prop: 'VersionYear', label: '版本年', width: 90 },
    { prop: 'Category', label: '类别', width: 110 },
  ]

  /**
   * 只有「标准」（叶子）可勾选 —— 阶段行只是分组。
   *
   * 这同时让 `buildSelections` 过滤掉阶段行：即使 `cascade` 把阶段行也带进变更集合，
   * 也不会向后端提交非叶子节点。
   */
  protected override get selectableNodeTypes(): string[] {
    return ['standard']
  }

  constructor() {
    const api: AssociationApi = {
      getTreeRoot: getEnterpriseTreeRoot,
      getTreeChildren: getEnterpriseTreeChildren,
      getAssociations: getStageStandardTree,
      add: checkAdd,
      remove: checkRemove,
      getAll: getAllAssociations,
    }
    super(api)
  }

  /**
   * 阶段行勾选态跟随子级。
   *
   * 与 `role-api/logic.ts` 的分组跟随同构 —— 内核不做这件事，因为它不知道
   * 「哪种 NodeType 是分组」。判定用**本地缓存**（`cached`）而不是 `node.CheckFlag`，
   * 因为此刻 `CheckFlag` 刚被缓存覆盖过，语义等价但更直接。
   */
  protected override afterAssociationsLoaded(
    data: CheckTreeNode[],
    cached: Set<string>,
  ): void {
    for (const node of data.filter((n) => n.NodeType === 'stage')) {
      const children = data.filter((n) => n.ParentCode === node.Code)
      node.CheckFlag = children.length > 0 && children.every((child) => cached.has(child.Code))
    }
  }
}

export default EnterpriseStageLogic
