/**
 * SkillTreeTableLogic — 技能分类 → 技能 左树右表 Logic（TreeTable 架构）
 *
 * 布局：
 * - 左侧：分类树（扁平，所有分类为根节点，含搜索、增删改）
 * - 右侧：技能表格（选中分类后加载，分页、搜索、增删改）
 *
 * 继承 TreeTableLogic 获得全套左树右表能力
 */

import { TreeTableLogic, type ApiResponse, type PagedData, type TreeNode } from '@yzh-core'
import { reactive, ref } from 'vue'

export class SkillTreeTableLogic extends TreeTableLogic<any> {
  controllerName = 'Workflow/SkillTreeTable'

  // ──── 技能弹窗状态（继承自 CrudPageLogic） ────
  // dialogVisible / dialogMode / submitting / formData 已在基类中定义

  // ──── 分类弹窗状态（基类没有，需自行扩展） ────
  categoryDialogVisible = ref(false)
  categoryDialogMode = ref<'add' | 'edit'>('add')
  categorySubmitting = ref(false)
  categoryFormData = reactive<Record<string, any>>({})
  categoryEditingNode = ref<TreeNode | null>(null)

  // ──── 行操作按钮 ────
  get rowActionButtons(): Record<string, string> {
    const buttons: Record<string, string> = {}
    const rb = (this.config.value as any)?.RowButtons
    if (rb?.Edit !== false) buttons['edit'] = '编辑'
    if (rb?.Delete !== false) buttons['delete'] = '删除'
    return buttons
  }

  // ──── 树节点操作（不含 add-child，分类扁平无层级） ────
  get nodeActions(): Record<string, string> {
    const actions: Record<string, string> = {}
    const tc = this.treeConfig
    if (!tc) return actions
    if (tc.AllowEdit) actions['edit'] = '编辑'
    if (tc.AllowDelete) actions['delete'] = '删除'
    return actions
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
    if (this.selectedNode.value && this.selectedNode.value.Code !== '__all__') {
      filters.push({
        Field: this.relateField,
        Value: this.selectedNode.value.Code,
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
          // 分类编码翻译为分类名称显示
          CategoryCodeName: this.findCategoryName(r.CategoryCode),
        }))
        return { rows, total: res.data.TotalCount ?? 0 }
      }
    } catch (_e: any) {
      // silently ignore
    }
    return { rows: [], total: 0 }
  }

  /** 根据分类编码获取分类名称 */
  findCategoryName(code: string): string {
    if (!code) return '-'
    const findInTree = (nodes: TreeNode[]): string => {
      for (const n of nodes) {
        if (n.code === code) return n.name
        if (n.children?.length) {
          const found = findInTree(n.children)
          if (found) return found
        }
      }
      return ''
    }
    return findInTree(this.treeData.value) || code
  }

  // ========================================================
  // 技能 CRUD
  // ========================================================

  /** 打开新增技能弹窗（\"全部\"节点不可新增，须选中具体分类） */
  openAddSkillDialog(): boolean {
    if (!this.selectedNode.value) return false
    if (this.selectedNode.value.Code === '__all__') return false
    this.dialogMode.value = 'add'
    this.formGroupIndex.value = '0'
    Object.keys(this.formData).forEach((k) => delete this.formData[k])
    const tmpl = (this.config.value as any)?.NewEntity || {}
    Object.assign(this.formData, {
      ...tmpl,
      CategoryCode: this.selectedNode.value.code,
      SkillType: 'manual',
      SortOrder: 0,
      IsValid: 1,
    })
    this.dialogVisible.value = true
    return true
  }

  /** 打开编辑技能弹窗 */
  openEditSkillDialog(row: any): void {
    this.dialogMode.value = 'edit'
    // 编辑模式传 '0' → GroupIndex="0"（默认）的字段可编辑，非 "0" 的字段只读
    this.formGroupIndex.value = '0'
    Object.keys(this.formData).forEach((k) => delete this.formData[k])
    Object.assign(this.formData, row)
    this.dialogVisible.value = true
  }

  /** 提交技能表单 */
  async submitSkillForm(): Promise<void> {
    this.submitting.value = true
    try {
      const submitData = { ...this.formData }
      if (this.dialogMode.value === 'add') {
        const saved = await this.add(submitData)
        this.insertRow(saved)
      } else {
        const saved = await this.update(submitData)
        this.replaceRowByCode(saved.Code, saved)
      }
      this.dialogVisible.value = false
    } finally {
      this.submitting.value = false
    }
  }

  /** 删除技能 */
  async deleteSkill(row: any): Promise<void> {
    await this.delete([row.Code])
    this.removeRowByCode(row.Code)
  }

  /** 批量删除技能 */
  async batchDeleteSkills(rows: any[]): Promise<void> {
    const codes = rows.map((r) => r.Code)
    await this.delete(codes)
    codes.forEach((code) => this.removeRowByCode(code))
  }

  // ========================================================
  // 分类（树节点）CRUD
  // ========================================================

  /** 打开新增分类弹窗 */
  openAddCategoryDialog(): void {
    this.categoryDialogMode.value = 'add'
    Object.keys(this.categoryFormData).forEach((k) => delete this.categoryFormData[k])
    const tmpl = (this.treeFormConfig as any)?.NewEntity || {}
    Object.assign(this.categoryFormData, {
      ...tmpl,
      Code: crypto.randomUUID?.() || `${Date.now()}`,
      Color: '#409EFF',
      SortOrder: 0,
      IsValid: 1,
    })
    this.categoryDialogVisible.value = true
  }

  /** 打开编辑分类弹窗 */
  openEditCategoryDialog(node: TreeNode): void {
    this.categoryDialogMode.value = 'edit'
    this.categoryEditingNode.value = node
    Object.keys(this.categoryFormData).forEach((k) => delete this.categoryFormData[k])
    const extra = (node.extra as any) || {}
    Object.assign(this.categoryFormData, {
      Code: node.code,
      Name: node.name,
      ...extra,
    })
    this.categoryDialogVisible.value = true
  }

  /** 提交分类表单 */
  async submitCategoryForm(): Promise<void> {
    this.categorySubmitting.value = true
    try {
      if (this.categoryDialogMode.value === 'add') {
        await this.addRootNode(this.categoryFormData)
      } else {
        await this.updateTreeNode(
          this.categoryEditingNode.value!,
          this.categoryFormData.Name ?? '',
          this.categoryFormData,
        )
      }
      this.categoryDialogVisible.value = false
    } finally {
      this.categorySubmitting.value = false
    }
  }

  /** 删除分类（skipConfirm=true，由页面确认弹窗后再调用） */
  async deleteCategory(node: TreeNode): Promise<void> {
    await this.deleteTreeNode(node, true)
    // 刷新右侧表格（基类没有 refreshTable，用 loadPageWithoutTree 清空）
    await this.loadPageWithoutTree()
  }

  // ========================================================
  // 初始化
  // ========================================================

  async init(): Promise<void> {
    await this.loadConfig()
    await this.loadTreeRoot()
    // 注入"全部"虚拟根节点（showall = 加载全部技能，不加分类过滤）
    this.treeData.value.unshift({
      Code: '__all__',
      Name: '全部',
      ParentCode: null,
      NodeType: 'virtual',
      IsLeaf: true,
      Extra: { level: 0 },
      Children: [],
    })
    // 默认选中"全部"节点，加载全量技能
    const allNode = this.treeData.value[0]
    this.selectedNode.value = allNode
    await (this as any)._tableRef?.refresh()
  }
}

export default SkillTreeTableLogic
