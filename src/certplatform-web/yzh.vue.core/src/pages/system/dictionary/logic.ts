/**
 * DictionaryPageLogic - 数据字典管理 Logic（左树右表，TreeTableCore 架构）
 *
 * 后端：DictionaryController (TreeTableControllerBase<Sys_Dictionary, Sys_DictionaryList>)
 * - 左树：Sys_Dictionary（字典/分类树，懒加载 + 增删改 + 启停，AllowDeleteWithChildren）
 * - 右表：Sys_DictionaryList（RelateField=DicCode；未选中树节点时 empty）
 *
 * 保留的 virtual 覆写（业务差异）：
 * - entityNameField / defaultValues / defaultTreeValues
 * - requireTreeSelectionMessage（请先在左侧选择字典）
 * - normalizeBeforeSubmit（OrderNo Decimal 字符串→数值）
 * - onPrepareAdd（注入 DicParentName 展示；DicCode 由 buildFilters 注入查询，
 *   提交时 DicCode 仍须带上 —— openRowDialog 的 onPrepareAdd 补）
 * - fallbackSearchFields（treepconfig 不映射 SearchFields 的历史场景）
 * - toolbarActions（本页 Toolbar：新增字典项/批量删除/刷新）
 * - openRootDictDialog（树底部「新增字典分类」绕开 requireTreeSelectionForAdd）
 * - deleteTreeNodeWithConfirm（级联软删除确认文案）
 *
 * TreeNode 一律 PascalCase（node.Code / node.Name / node.Extra）。
 */

import { TreeTableCore, type SearchField, type TreeNode, type YzhAction } from '@yzh-core'
import { ElMessage, ElMessageBox } from 'element-plus'

export class DictionaryPageLogic extends TreeTableCore<any> {
  controllerName = 'Dictionary'

  /** 删除确认显示真实姓名 */
  protected override get entityNameField(): string {
    return 'DicName'
  }

  protected override get defaultValues(): Record<string, any> {
    return { IsValid: 1, OrderNo: 0 }
  }

  protected override get defaultTreeValues(): Record<string, any> {
    return { IsValid: 1, OrderNo: 0 }
  }

  /** 未选中树节点提示 */
  protected override get requireTreeSelectionMessage(): string {
    return '请先在左侧选择字典'
  }

  /** 工具栏「新增字典项」与行新增同走 openRowDialog（含未选中校验） */
  override openAddDialog(): void {
    this.openRowDialog(null)
  }

  /**
   * 行「新增字典项」：必须已选中字典。
   * 树「新增下级」仍走内核 openTreeNodeDialog。
   */
  protected override onPrepareAdd(entity: Record<string, any>): void {
    entity.DicCode = this.selectedNode?.Code
    entity.ParentDicName = this.selectedNode?.Name ?? ''
  }

  /**
   * 编辑行补 ParentDicName（表只读展示字段，行数据未必带）。
   * openEditDialog → initFormData(row) 后再走本钩子路径不覆盖 row，
   * 故在 openRowDialog(row) 分支手工补。
   */
  override openRowDialog(row?: any | null): boolean {
    if (row && !row.ParentDicName) {
      row = { ...row, ParentDicName: this.selectedNode?.Name ?? '' }
    }
    return super.openRowDialog(row)
  }

  /** Decimal 字段（OrderNo）字符串→数值，规避 System.Text.Json 400 */
  protected override normalizeBeforeSubmit(payload: Record<string, any>): Record<string, any> {
    if ('OrderNo' in payload) {
      const v = payload.OrderNo
      if (v === '' || v === null || v === undefined) {
        payload.OrderNo = null
      } else if (typeof v === 'string' && !Number.isNaN(Number(v))) {
        payload.OrderNo = Number(v)
      }
    }
    return payload
  }

  /**
   * treepconfig 的 ConvertToDto 历史场景可能不带 SearchFields；
   * 声明唯一有意义的搜索项（显示文本 like）。
   */
  protected override get fallbackSearchFields(): SearchField[] {
    return [
      {
        prop: 'DicName',
        label: '显示文本',
        type: 'text',
        placeholder: '请输入显示文本',
      },
    ]
  }

  /** 树表单：过滤 ParentName 只读展示字段（改用 #prepend 上级节点） */
  override get treeFormFields() {
    return super.treeFormFields.filter((f) => f.prop !== 'ParentName')
  }

  /** 右表单：过滤 ParentDicName 只读展示字段（改用 #prepend 所属字典） */
  override get formFields() {
    return super.formFields.filter((f) => f.prop !== 'ParentDicName')
  }

  /** 工具栏：新增字典项 + 批量删除 + 刷新 */
  override get toolbarActions(): YzhAction[] {
    return [
      { key: 'add', text: '新增字典项', type: 'primary' },
      { key: 'delete', text: '批量删除', type: 'danger' },
      { key: 'refresh', text: '刷新', type: 'info' },
    ]
  }

  /**
   * 树底部「新增字典分类」：根级新增（绕开 requireTreeSelectionForAdd）。
   */
  openRootDictDialog(): boolean {
    this.treeDialogMode.value = 'add'
    this.treeEditingNode.value = null
    this.treeParentNode.value = null
    this.resetObject(this.treeFormData)
    const tmpl = (this.treeFormConfig?.NewEntity as any) || {}
    Object.assign(this.treeFormData, tmpl, this.defaultTreeValues, {
      DicName: '',
      ParentCode: (this.treeConfig?.RootParentCode as string) ?? null,
    })
    this.treeDialogVisible.value = true
    return true
  }

  /** 树删除：级联软删除确认文案（AllowDeleteWithChildren=true） */
  override async deleteTreeNodeWithConfirm(node: TreeNode): Promise<void> {
    const ok = await this.onBeforeDeleteTree(node)
    if (!ok) return
    await ElMessageBox.confirm(
      `确定删除字典/分类【${node.Name}】？\n（子节点将一并被软删除）`,
      '删除确认',
      {
        type: 'warning',
        confirmButtonText: '确定删除',
        cancelButtonText: '取消',
      },
    )
    await this.deleteTreeNode(node, true)
    this.onAfterDeleteTree(node)
    ElMessage.success('已删除')
  }

  constructor() {
    super()
    this.registerHandler('refresh', async () => {
      await this.refreshTable()
    })
  }
}

export default DictionaryPageLogic
