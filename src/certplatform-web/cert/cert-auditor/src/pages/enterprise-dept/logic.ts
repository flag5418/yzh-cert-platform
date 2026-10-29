/**
 * EnterpriseDeptLogic — 企业机构管理 Logic（左树右表，TreeTableCore 架构）
 *
 * 后端：CertPlatform.Auditor/Controllers/EnterpriseDeptController.cs
 *   TreeTableControllerBase<CertEnterpriseDept, CertEnterpriseDeptMember>
 * 路由：/api/Auditor/EnterpriseDept
 *
 * - 左树：本工作区企业（根节点，只读）+ 企业部门（cert_enterprise_dept，可增删改启停）
 * - 右表：选中部门下的人员（cert_enterprise_dept_member，RelateField=DeptCode）
 *
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

  /** 右表（人员）新增默认值 */
  protected override get defaultValues(): Record<string, any> {
    return { IsValid: 1 }
  }

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

  /** 工具栏「新增人员」与行新增同走 openRowDialog（含选中校验） */
  override openAddDialog(): void {
    this.openRowDialog(null)
  }

  /** 行「新增人员」：必须选中部门（企业节点下不能直接挂人员） */
  override openRowDialog(row?: any | null): boolean {
    if (!row) {
      const node = this.selectedNode
      if (!node) {
        ElMessage.warning(this.requireTreeSelectionMessage)
        return false
      }
      if (this.isEnterpriseNode(node)) {
        ElMessage.warning('请先在企业下选择部门')
        return false
      }
    }
    return super.openRowDialog(row)
  }

  /** 新增人员注入所属部门 Code（EnterpriseCode 由后端按部门归属回填） */
  protected override onPrepareAdd(entity: Record<string, any>): void {
    entity.DeptCode = this.selectedNode?.Code
  }

  /** 树自定义动作确认文案 */
  protected override confirmTreeActionMessage(method: string, node: TreeNode): string | null {
    if (method === 'disable') return `确定禁用部门【${node.Name}】？`
    if (method === 'enable') return `确定启用部门【${node.Name}】？`
    return null
  }

  /** 行按钮：编辑 / 删除 + 按 IsValid 二选一 禁用/启用 */
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

  /** 工具栏：新增人员 + 批量删除 + 刷新 */
  override get toolbarActions(): YzhAction[] {
    return [
      { key: 'add', text: '新增人员', type: 'primary' },
      { key: 'delete', text: '批量删除', type: 'danger' },
      { key: 'refresh', text: '刷新', type: 'default' },
    ]
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
