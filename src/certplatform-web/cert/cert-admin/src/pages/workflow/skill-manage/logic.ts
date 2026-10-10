/**
 * SkillTreeTableLogic — 技能分类 → 技能 左树右表 Logic（TreeTable 架构）
 *
 * 布局：
 * - 左侧：分类树（两级：大类 → 子类；数据源 = 字典 skill_category；分类在当前页就地维护）
 * - 右侧：技能表格（选中分类后加载，分页、搜索、增删改 + 启用/禁用）
 *
 * 分类字典化（2026-09-26，方案A：字典为分类唯一数据源）：
 * - 树节点 Code = 字典项 DicValue（data_access 等语义值），Name = DicName
 * - wf_skill.CategoryCode 存 DicValue；表单下拉由 Skill.json 的 DictCode 驱动
 * - 分类就地维护（2026-10-09）：树的增删改在当前页完成，后端校验钩子保障数据卫生
 *
 * 启用/禁用（对齐机构-用户样板）：
 * - 行按钮由内核 rowActions 按 IsValid 二选一（disable/enable，后端 RegisterRowAction 注入）
 * - 分发走 @row-action → logic.onRowAction → POST action/{method}
 */

import { TreeTableLogic, type ApiResponse, type PagedData, type TreeNode, type YzhFormField } from '@yzh-core'

export class SkillTreeTableLogic extends TreeTableLogic<any> {
  // ⚠️ 必须含 Admin/ 端标记 —— 后端路由为 api/Admin/Workflow/SkillTreeTable（2026-10-03 端标记改造）。
  //    URL 由 SingleTableCore 拼成 `/api/${controllerName}${path}`（SingleTableCore.ts:878），
  //    漏 Admin/ ⇒ 全部端点 404，页面报「接口不存在」。
  controllerName = 'Admin/Workflow/SkillTreeTable'

  /**
   * 覆写树节点表单的"名称字段"：后端实体用 DicName（非默认 Name）
   * 使 submitTreeNodeForm 提交时能正确映射到 SkillCategoryDict.DicName
   */
  protected override get treeEntityNameField(): string {
    return 'DicName'
  }

  /**
   * 覆写 treeFormFields：为 ParentCode（上级分类）treeSelect 注入树形选项。
   *
   * 数据源 = 当前已加载的树根节点（排除虚拟"全部"节点 + 当前编辑节点自身），
   * 格式化为 el-tree-select 所需的 { label, value, children }。
   * 子级节点不允许修改父级（OnBeforeUpdateTree 已校验），编辑时 ParentCode 通过 disabled 实现只读。
   */
  override get treeFormFields(): YzhFormField[] {
    const fields = super.treeFormFields
    return fields.map((f) => {
      if (f.prop === 'ParentCode') {
        return {
          ...f,
          options: this.buildParentCodeOptions(),
          // 编辑子级分类时 ParentCode 只读（保两层结构）
          disabled: this.treeDialogMode.value === 'edit' && this.treeFormData.ParentCode,
        }
      }
      return f
    })
  }

  /** 为 ParentCode treeSelect 构建选项：根级分类列表（排除虚拟节点和编辑目标自身） */
  private buildParentCodeOptions(): Array<{ label: string; value: string; children?: any[] }> {
    const excludeCode = this.treeEditingNode?.value?.Code
    const nodes = this.treeData.filter((n) => n.Code !== '__all__' && n.Code !== excludeCode)
    return nodes.map((n) => this.nodeToOption(n))
  }

  /** TreeNode → el-tree-select 选项格式 */
  private nodeToOption(node: TreeNode): { label: string; value: string; children?: any[] } {
    return {
      label: node.Name,
      value: node.Code,
      children: node.Children?.map((c) => this.nodeToOption(c)),
    }
  }

  /**
   * 覆写 canAddUnderNode：两级分类树，只有根级节点（Extra.level=0）允许新增子级。
   * 子级节点（level=1）再点"新增下级"会产生第三级 → 超出 MaxLevel=2。
   */
  protected override canAddUnderNode(node: TreeNode): boolean {
    const level = (node.Extra?.level as number) ?? 0
    return level === 0
  }

  protected override canAddUnderNodeMessage(_node: TreeNode): string {
    return '子分类下不允许再新增下级（最多两级）'
  }

  // ──── 新增技能默认值（分类归入由 onPrepareAdd 按选中树节点注入） ────
  protected override get defaultValues(): Record<string, any> {
    return {
      SkillType: 'manual',
      SortOrder: 0,
      IsValid: 1,
    }
  }

  /** 新增前钩子：技能默认归入当前选中分类（"全部"虚拟节点由页面拦截，不会走到这） */
  protected override onPrepareAdd(data: Record<string, any>): void {
    if (this.selectedNode && this.selectedNode.Code !== '__all__') {
      data.CategoryCode = this.selectedNode.Code
    }
  }

  // ========================================================
  // 数据加载
  // ========================================================

  /**
   * YzhTable 数据加载器：选中分类时自动注入 CategoryCode 过滤
   */
  async dataLoader(params: {
    page: number
    rows: number
    sort?: string
    order?: string
    searchParams?: Record<string, any>
  }): Promise<{ rows: any[]; total: number }> {
    const filters = this.buildFilters()
    // "全部"节点（Code=__all__）不加分类过滤，加载全量技能
    if (this.selectedNode && this.selectedNode.Code !== '__all__') {
      filters.push({
        Field: this.relateField,
        Value: this.selectedNode.Code,
        Operator: 'eq',
      })
    }
    try {
      const res = await this.apiPost<ApiResponse<PagedData<any>>>('/filter', {
        Page: params.page,
        PageSize: params.rows,
        SortField: params.sort || '',
        SortOrder: params.order || '',
        Filters: filters,
      })
      if (res.data) {
        const rows = (res.data.Items ?? []).map((r: any) => ({
          ...r,
          // 分类编码翻译为分类名称显示（列 FieldName=CategoryCodeName）
          CategoryCodeName: this.findCategoryName(r.CategoryCode),
        }))
        return { rows, total: res.data.TotalCount ?? 0 }
      }
    } catch (_e: any) {
      // silently ignore
    }
    return { rows: [], total: 0 }
  }

  /** 根据分类编码（字典项 DicValue）获取分类名称（树节点来自 skill_category 字典） */
  findCategoryName(code: string): string {
    if (!code) return '-'
    const findInTree = (nodes: TreeNode[]): string => {
      for (const n of nodes) {
        if (n.Code === code) return n.Name
        if (n.Children?.length) {
          const found = findInTree(n.Children)
          if (found) return found
        }
      }
      return ''
    }
    return findInTree(this.treeData) || code
  }

  // ========================================================
  // 初始化
  // ========================================================

  async init(): Promise<void> {
    await this.loadConfig()
    await this.loadTreeRoot()
    // 注入"全部"虚拟根节点（showall = 加载全部技能，不加分类过滤）
    this.treeData.unshift({
      Code: '__all__',
      Name: '全部',
      ParentCode: null,
      NodeType: 'virtual',
      IsLeaf: true,
      Extra: { level: 0 },
    })
    // 默认选中"全部"节点，加载全量技能
    const allNode = this.treeData[0]
    if (allNode) await this.onNodeClick(allNode)
  }
}

export default SkillTreeTableLogic
