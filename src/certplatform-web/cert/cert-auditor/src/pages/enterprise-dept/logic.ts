/**
 * EnterpriseDeptLogic — 企业机构管理 Logic（左树右表，TreeTableCore 架构）
 *
 * 后端：CertPlatform.Auditor/Controllers/EnterpriseDeptController.cs
 *   TreeTableControllerBase<CertEnterpriseDept, CertEnterpriseDept>
 * 路由：/api/Auditor/EnterpriseDept
 *
 * - 左树：本工作区企业（根节点，只读）+ 企业部门（cert_enterprise_dept，可增删改启停）
 * - 右表：选中节点的「下级部门」（RelateField=ParentCode），只读 + 编辑（用于完善部门职能）
 *
 * ⚠️ 本页不管理企业人员（人员不出现在审核报告中）。
 * 企业节点只读（编辑/删除/启停不开放），仅保留「新增下级」用于建顶层部门；
 * 部门节点支持 编辑 / 删除 / 新增下级 / 禁用 / 启用。
 *
 * TreeNode 一律 PascalCase（node.Code / node.Name / node.IsLeaf / node.Extra）。
 */
import { ElMessage } from 'element-plus'
import { TreeTableCore, type TreeNode, type YzhAction } from '@yzh-core'

/** 企业节点类型标记（后端 GetRootNodes / MapToTreeItem 写入 NodeType="enterprise"） */
const NODE_TYPE_ENTERPRISE = 'enterprise'

export class EnterpriseDeptLogic extends TreeTableCore<any> {
  controllerName = 'Auditor/EnterpriseDept'

  /** 左树（部门）新增默认值 */
  protected override get defaultTreeValues(): Record<string, any> {
    return { IsValid: 1, Sort: 0 }
  }

  protected override get requireTreeSelectionMessage(): string {
    return '请先选择部门'
  }

  /** 企业节点判定 */
  private isEnterpriseNode(node: TreeNode): boolean {
    return node.NodeType === NODE_TYPE_ENTERPRISE
  }

  /**
   * 企业节点只读：仅保留「新增下级」（在企业下建顶层部门）。
   * 部门节点保留 编辑 / 删除 / 新增下级 / 禁用 / 启用。
   */
  protected override resolveTreeActions(node: TreeNode): YzhAction[] {
    const actions = super.resolveTreeActions(node)
    if (!this.isEnterpriseNode(node)) return actions
    return actions.filter(
      a =>
        a.key !== 'edit' &&
        a.key !== 'delete' &&
        a.key !== 'custom:disable' &&
        a.key !== 'custom:enable',
    )
  }

  /** 树底部「新增部门」置灰条件：未选中节点（后端要求部门必须归属企业） */
  get treeFooterAddDisabled(): boolean {
    return !this.selectedNode
  }

  /** 树自定义动作确认文案 */
  protected override confirmTreeActionMessage(method: string, node: TreeNode): string | null {
    if (method === 'disable') return `确定禁用部门【${node.Name}】？`
    if (method === 'enable') return `确定启用部门【${node.Name}】？`
    return null
  }

  /**
   * 行按钮：仅「编辑」（右表 = 下级部门，用于完善部门职能）。
   * 由后端 EntityConfig.RowButtons 驱动（Delete=false / 无 CustomButtons）；
   * 新增 / 删除 / 批量删除一律在左树操作。
   */
  override get rowActions(): YzhAction[] | ((row: any) => YzhAction[]) {
    return (row: any) => {
      const rb = this.config.value?.RowButtons
      const actions: YzhAction[] = []
      if (rb?.Edit !== false) actions.push({ key: 'edit', text: '编辑', type: 'primary' })
      if (rb?.Delete !== false) actions.push({ key: 'delete', text: '删除', type: 'danger' })
      const cb = rb?.CustomButtons ?? {}
      if (row?.IsValid === 1 && cb['disable']) {
        actions.push({ key: 'custom:disable', text: cb['disable'], type: 'default' })
      } else if (row?.IsValid === 0 && cb['enable']) {
        actions.push({ key: 'custom:enable', text: cb['enable'], type: 'default' })
      }
      return actions
    }
  }

  /** 工具栏：仅「刷新」（新增/删除走左树） */
  override get toolbarActions(): YzhAction[] {
    return [{ key: 'refresh', text: '刷新', type: 'default' }]
  }

  /** 树底部「新增部门」：企业节点下建顶层部门，部门节点下建子部门 */
  openDeptAddFromFooter(): boolean {
    const node = this.selectedNode
    if (!node) {
      ElMessage.warning('请先在左侧选择企业或部门')
      return false
    }
    return this.openTreeNodeDialog(null, node)
  }

  constructor() {
    super()
    this.registerHandler('refresh', async () => {
      await this.refreshTable()
    })
  }
}

export default EnterpriseDeptLogic
