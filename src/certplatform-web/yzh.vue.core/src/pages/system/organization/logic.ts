/**
 * OrgPageLogic - 组织机构-人员管理 Logic（左树右表，TreeTableCore 架构）
 *
 * 后端：OrganizationController (TreeTableControllerBase<Sys_Organization, Sys_User>)
 * - 左树：Sys_Organization（懒加载 + 增删改 + 自定义级联 disable/enable，AllowToggle=false）
 * - 右表：Sys_User（RelateField=OrgCode；行动作走 /action/{method}）
 *
 * 保留的 virtual 覆写（业务差异）：
 * - entityNameField / defaultValues / defaultTreeValues
 * - requireTreeSelectionMessage（请先选择机构）
 * - openRowDialog：行新增仅限末端机构（树 add-child 不受限）
 * - onPrepareAdd（注入 OrgCode，不生成 Code）
 * - confirmTreeActionMessage（级联禁用文案）
 * - rowActions 函数（CustomButtons 为 {method:label}，按行状态二选一）
 * - openOrgAddFromFooter（树底：有选中加子级，无选中加根）
 *
 * TreeNode 一律 PascalCase（node.Code / node.Name / node.IsLeaf / node.Extra）。
 */

import { ElMessage } from 'element-plus'
import { TreeTableCore, type TreeNode, type YzhAction } from '@yzh-core'

export class OrgPageLogic extends TreeTableCore<any> {
  controllerName = 'Organization'

  /** 删除确认显示真实姓名 */
  protected override get entityNameField(): string {
    return 'UserTrueName'
  }

  protected override get defaultValues(): Record<string, any> {
    return { IsValid: 1 }
  }

  protected override get defaultTreeValues(): Record<string, any> {
    return { IsValid: 1 }
  }

  /** 未选机构提示（内核默认「请先在左侧选择节点」，本页业务文案） */
  protected override get requireTreeSelectionMessage(): string {
    return '请先选择机构'
  }

  /** 工具栏「新增人员」与行新增同走 openRowDialog（含末端/未选中校验） */
  override openAddDialog(): void {
    this.openRowDialog(null)
  }

  /**
   * 行「新增人员」：必须已选机构且为末端机构。
   * 树「新增下级机构」仍走内核 openTreeNodeDialog（canAddUnderNode 默认 true 不动）。
   */
  override openRowDialog(row?: any | null): boolean {
    if (!row) {
      const node = this.selectedNode
      if (!node) {
        ElMessage.warning(this.requireTreeSelectionMessage)
        return false
      }
      if (node.IsLeaf !== true) {
        ElMessage.warning(this.canAddUnderNodeMessage(node))
        return false
      }
    }
    return super.openRowDialog(row)
  }

  protected override canAddUnderNodeMessage(_node: TreeNode): string {
    return '请选择末端机构（不含子机构的节点）'
  }

  /** 新增人员注入所属机构 Code（Code 由后端生成） */
  protected override onPrepareAdd(entity: Record<string, any>): void {
    entity.OrgCode = this.selectedNode?.Code
  }

  /** 树自定义动作确认文案（级联禁用业务规则） */
  protected override confirmTreeActionMessage(method: string, node: TreeNode): string | null {
    if (method === 'disable') {
      return `确定禁用机构【${node.Name}】？（将级联禁用子机构和人员）`
    }
    if (method === 'enable') {
      return `确定启用机构【${node.Name}】？`
    }
    return null
  }

  /**
   * 行按钮：edit + delete + 按 row.IsValid 二选一 disable/enable。
   * CustomButtons 形状为 { method: label }（与 toRowActions 一致）；
   * 本页保留显式覆写以维持确认文案与按钮文案的独立控制。
   */
  override get rowActions(): YzhAction[] | ((row: any) => YzhAction[]) {
    return (row: any) => {
      const rb = this.config.value?.RowButtons
      const actions: YzhAction[] = []
      if (rb?.Edit !== false) {
        actions.push({ key: 'edit', text: '编辑', type: 'primary' })
      }
      if (rb?.Delete !== false) {
        actions.push({ key: 'delete', text: '删除', type: 'danger' })
      }
      const cb = rb?.CustomButtons ?? {}
      if (row?.IsValid === 1 && cb['disable']) {
        actions.push({ key: 'custom:disable', text: cb['disable'], type: 'warning' })
      } else if (row?.IsValid === 0 && cb['enable']) {
        // 配色约定：停用行 → success 绿「启用」（与全项目二选一按钮一致）
        actions.push({ key: 'custom:enable', text: cb['enable'], type: 'success' })
      }
      return actions
    }
  }

  /** 工具栏：新增人员 + 批量删除 + 刷新 */
  override get toolbarActions(): YzhAction[] {
    return [
      { key: 'add', text: '新增人员', type: 'primary' },
      { key: 'delete', text: '批量删除', type: 'danger' },
      { key: 'refresh', text: '刷新', type: 'info' },
    ]
  }

  /**
   * 树底部「新增机构」：有选中加子级；无选中加根（旧语义）。
   * 绕开 requireTreeSelectionForAdd（本页树允许根新增）。
   */
  openOrgAddFromFooter(): boolean {
    if (this.selectedNode) {
      return this.openTreeNodeDialog(null, this.selectedNode)
    }
    this.treeDialogMode.value = 'add'
    this.treeEditingNode.value = null
    this.treeParentNode.value = null
    this.resetObject(this.treeFormData)
    const tmpl = (this.treeFormConfig?.NewEntity as any) || {}
    Object.assign(this.treeFormData, tmpl, this.defaultTreeValues, {
      [this.treeEntityNameField]: '',
      ParentCode: (this.treeConfig?.RootParentCode as string) ?? null,
    })
    this.treeDialogVisible.value = true
    return true
  }

  constructor() {
    super()
    this.registerHandler('refresh', async () => {
      await this.refreshTable()
    })
  }
}

export default OrgPageLogic
