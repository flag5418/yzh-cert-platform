/**
 * ISOStandardTreeTableLogic — ISO 标准 → 条款 左树右表 Logic（TreeTable 架构）
 *
 * 数据访问规则：
 * - res.data：ApiResponse 顶层（camelCase）
 * - 业务行 r：PascalCase 字段（r.Code, r.StandardCode, r.Enable）
 * - TreeNode：el-tree 内部约定小写（node.code, node.name）
 * - formData：PascalCase key（YzhForm 双向绑定）
 *
 * 架构：
 * - 左侧：标准树（扁平，所有标准为根节点，不允许增加下级）
 * - 右侧：条款表格（选中标准后加载）
 * - 继承 TreeTableLogic 获得全套左树右表能力
 */

import { TreeTableLogic, type ApiResponse, type PagedData, type TreeNode } from '@yzh-core'
import { reactive, ref } from 'vue'

export class ISOStandardTreeTableLogic extends TreeTableLogic<any> {
  controllerName = 'Foundation/ISOStandardTreeTable'

  // ──── 条款弹窗状态 ────
  dialogVisible = ref(false)
  dialogMode = ref<'add' | 'edit'>('add')
  submitting = ref(false)
  /**
   * 条款表单数据：PascalCase key（与 formFields prop 一致）
   */
  formData = reactive<Record<string, any>>({})

  // ──── 标准弹窗状态 ────
  stdDialogVisible = ref(false)
  stdDialogMode = ref<'add' | 'edit'>('add')
  stdSubmitting = ref(false)
  stdFormData = reactive<Record<string, any>>({})
  stdEditingNode = ref<TreeNode | null>(null)

  // ──── 行操作按钮（基类 TreeTableLogic.rowActionButtons 已自动注入 toggle-valid） ────

  // ──── 树节点操作（从 TreeConfig 配置驱动，取消 add-child） ────
  get nodeActions(): Record<string, string> {
    const actions: Record<string, string> = {}
    const tc = this.treeConfig
    if (!tc) return actions
    // AllowEdit 控制编辑按钮（不含 add-child，标准无层级）
    if (tc.AllowEdit) {
      actions['edit'] = '编辑'
    }
    if (tc.AllowDelete) {
      actions['delete'] = '删除'
    }
    return actions
  }

  // ========================================================
  // 数据加载（供 YzhTable data-loader 使用）
  // ========================================================

  /**
   * YzhTable 数据加载器：返回 { rows, total } 供表格渲染。
   * 选中树节点时自动注入 StandardCode 过滤。
   */
  async dataLoader(params: {
    page: number
    rows: number
    sort?: string
    order?: string
    searchParams?: Record<string, any>
  }): Promise<{ rows: any[]; total: number }> {
    const filters = this.buildFilters()
    if (this.selectedNode.value) {
      filters.push({
        Field: this.relateField,
        Value: this.selectedNode.value.code,
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
        return { rows: res.data.Items ?? [], total: res.data.TotalCount ?? 0 }
      }
    } catch (_e: any) {
      // silently ignore
    }
    return { rows: [], total: 0 }
  }

  // ========================================================
  // 条款 CRUD
  // ========================================================

  /** 打开新增条款弹窗 */
  openAddClauseDialog(): boolean {
    if (!this.selectedNode.value) {
      return false
    }
    this.dialogMode.value = 'add'
    this.formGroupIndex.value = '0'
    // 先清空旧数据，再从 NewEntity 模板初始化
    Object.keys(this.formData).forEach((k) => delete this.formData[k])
    const tmpl = (this.config.value as any)?.NewEntity || {}
    Object.assign(this.formData, {
      ...tmpl,
      StandardCode: this.selectedNode.value.code,
      SortOrder: 0,
    })
    this.dialogVisible.value = true
    return true
  }

  /** 打开编辑条款弹窗 */
  openEditClauseDialog(row: any): void {
    this.dialogMode.value = 'edit'
    // 编辑模式传 '0' → GroupIndex="0"（默认）的字段可编辑，非 "0" 的字段只读
    this.formGroupIndex.value = '0'
    Object.keys(this.formData).forEach((k) => delete this.formData[k])
    Object.assign(this.formData, row)
    this.dialogVisible.value = true
  }

  /** 提交条款表单 */
  async submitClauseForm(): Promise<void> {
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

  /** 删除条款 */
  async deleteClause(row: any): Promise<void> {
    await this.delete([row.Code])
    this.removeRowByCode(row.Code)
  }

  /** 批量删除条款 */
  async batchDeleteClauses(rows: any[]): Promise<void> {
    const codes = rows.map((r) => r.Code)
    await this.delete(codes)
    codes.forEach((code) => this.removeRowByCode(code))
  }

  // ========================================================
  // 标准（树节点）CRUD
  // ========================================================

  /** 打开新增标准弹窗 */
  openAddStdDialog(): void {
    this.stdDialogMode.value = 'add'
    Object.keys(this.stdFormData).forEach((k) => delete this.stdFormData[k])
    const tmpl = (this.treeFormConfig as any)?.NewEntity || {}
    Object.assign(this.stdFormData, {
      ...tmpl,
      Code: crypto.randomUUID?.() || `${Date.now()}`,
    })
    this.stdDialogVisible.value = true
  }

  /** 打开编辑标准弹窗 */
  openEditStdDialog(node: TreeNode): void {
    this.stdDialogMode.value = 'edit'
    this.stdEditingNode.value = node
    Object.keys(this.stdFormData).forEach((k) => delete this.stdFormData[k])
    const extra = (node.extra as any) || {}
    Object.assign(this.stdFormData, {
      Code: node.code,
      StandardName: node.name,
      ...extra,
    })
    this.stdDialogVisible.value = true
  }

  /** 提交标准表单 */
  async submitStdForm(): Promise<void> {
    this.stdSubmitting.value = true
    try {
      if (this.stdDialogMode.value === 'add') {
        await this.addRootNode(this.stdFormData)
      } else {
        await this.updateTreeNode(
          this.stdEditingNode.value!,
          this.stdFormData.StandardName ?? '',
          this.stdFormData,
        )
      }
      this.stdDialogVisible.value = false
    } finally {
      this.stdSubmitting.value = false
    }
  }

  /** 删除标准（skipConfirm=true，由页面确认弹窗后再调用） */
  async deleteStd(node: TreeNode): Promise<void> {
    await this.deleteTreeNode(node, true)
    // 刷新右侧表格（被删标准下的条款需要重新加载）
    await this.refreshTable()
  }

  // ========================================================
  // 初始化
  // ========================================================

  async init(): Promise<void> {
    await this.loadConfig()
    await this.loadTreeRoot()
    // 表格通过 YzhTable 的 dataLoader 自动加载数据，无需在此调用
  }
}

export default ISOStandardTreeTableLogic
