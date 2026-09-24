/**
 * MenuPageLogic - 菜单管理 Logic（左树右表，TreeTableCore 架构）
 *
 * 架构：
 * - 左树：Sys_Menu 菜单树（懒加载 + 增删改 + 启停），TreeConfig 由后端驱动
 * - 右表：选中节点的子级菜单（RelateField=ParentCode）；未选中时展示根级
 * - 写操作全部走树（右表仅查看/编辑/删除行）；根级新增走 openRootMenuDialog
 *
 * 菜单变更后 notifyMenuChanged → 宿主侧栏（YzhAppLayout/AuditorLayout）强刷。
 */

import { TreeTableCore, notifyMenuChanged, type YzhFormField } from '@yzh-core'

export class MenuPageLogic extends TreeTableCore<any> {
  controllerName = 'System/MenuManagement'

  /** 右表始终按 ParentCode 过滤（未选中时 = RootParentCode，展示根级） */
  protected override shouldApplyTreeFilter(): boolean {
    return true
  }

  protected override relatedValue(): string | null {
    return this.selectedNode?.Code ?? (this.treeConfig?.RootParentCode as string) ?? '0'
  }

  /** 无选中时也要加载（不能走 NoSelectionBehavior=empty 清空） */
  override async loadPageWithoutTree(): Promise<void> {
    await this.loadPage()
  }

  protected override get defaultValues(): Record<string, any> {
    return { IsValid: 1, OrderNo: 0 }
  }

  /** 右表新增：注入当前选中节点 Code 作 ParentCode（未选中 = 根） */
  protected override onPrepareAdd(entity: Record<string, any>): void {
    entity.ParentCode =
      this.selectedNode?.Code ?? (this.treeConfig?.RootParentCode as string) ?? '0'
  }

  /** 树表单：Icon 字段改为 custom slot（IconPicker 穿透 YzhFormDialog） */
  override get treeFormFields(): YzhFormField[] {
    return super.treeFormFields.map((f) =>
      f.prop === 'Icon' ? { ...f, type: 'custom' as const, slot: 'Icon' } : f
    )
  }

  /** 右表单：排除 ParentCode（由逻辑注入），Icon 走 slot */
  override get formFields(): YzhFormField[] {
    return super.formFields
      .filter((f) => f.prop !== 'ParentCode')
      .map((f) =>
        f.prop === 'Icon' ? { ...f, type: 'custom' as const, slot: 'Icon' } : f
      )
  }

  /**
   * 新增根菜单（绕开 openTreeNodeDialog 的 requireTreeSelectionForAdd）。
   * 供树底部「新增根菜单」调用。
   */
  openRootMenuDialog(): boolean {
    this.treeDialogMode.value = 'add'
    this.treeEditingNode.value = null
    this.treeParentNode.value = null
    this.resetObject(this.treeFormData)
    const tmpl = (this.treeFormConfig?.NewEntity as any) || {}
    Object.assign(this.treeFormData, tmpl, this.defaultTreeValues, {
      MenuName: '',
      ParentCode: (this.treeConfig?.RootParentCode as string) ?? '0',
    })
    this.treeDialogVisible.value = true
    return true
  }

  // ──── 菜单变更广播（侧栏刷新） ────

  protected override onAfterAddTree(): void {
    notifyMenuChanged()
    void this.refreshTable()
  }

  protected override onAfterUpdateTree(): void {
    notifyMenuChanged()
    void this.refreshTable()
  }

  protected override onAfterDeleteTree(): void {
    notifyMenuChanged()
    void this.refreshTable()
  }

  protected override onAfterAdd(): void {
    notifyMenuChanged()
  }

  protected override onAfterUpdate(): void {
    notifyMenuChanged()
  }

  protected override onAfterDelete(): void {
    notifyMenuChanged()
  }

  override async toggleTreeNodeIsValid(
    node: Parameters<TreeTableCore<any>['toggleTreeNodeIsValid']>[0],
  ): Promise<{ Code: string; IsValid: number } | null> {
    const result = await super.toggleTreeNodeIsValid(node)
    if (result) notifyMenuChanged()
    return result
  }
}

export default MenuPageLogic
