/**
 * DictionaryPageLogic - 数据字典管理 Logic（左树右表）
 *
 * 数据访问规则（与 YZH.Core.Stand 严格一致）：
 * - res.data：ApiResponse 顶层（camelCase）
 * - 业务行 r：PascalCase 字段（r.Code / r.DicCode / r.IsValid）
 * - TreeNode：el-tree 内部约定小写（node.code / node.name / node.extra）
 * - formData：**PascalCase** key —— 必须与 YzhForm 的 field.prop、
 *   EntityConfig 的 Columns[].FieldName 保持同一套命名（后端 EntitySchemaHelper 也返回 PascalCase）
 *
 * 架构：
 * - 左侧：字典/分类树（Sys_Dictionary），懒加载 + 增删改 + 启用/禁用
 * - 右侧：字典项表格（Sys_DictionaryList），选中字典后加载
 * - 后端 TreeConfig 驱动：RelateField=DicCode，EnableField=IsValid
 *
 * 两条架构铁律：
 * ① 关联一律走 Code —— 所有入参都是记录 Code
 * ② Code 随机生成且不可修改 —— 前端不生成 Code，由后端框架自动填充
 */

import { TreeTableLogic, type TreeNode, type ApiResponse } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { ref } from 'vue'

// ========================================================
// Logic
// ========================================================

export class DictionaryPageLogic extends TreeTableLogic<any> {
  controllerName = 'Dictionary'

  /** 当前正在编辑的字典项行（用于提交后与后端返回值合并，避免表格行丢字段） */
  private editingRow = ref<any>(null)

  // ========================================================
  // 表格列 / 关联字段
  // ========================================================

  /** 关联字段名（DicCode），供页面构建 /filter 条件使用 */
  get relateFieldName(): string {
    return this.relateField
  }

  /**
   * 表格列：IsValid 列改为自定义插槽渲染状态标签
   * （其余列完全由后端 EntityConfig 驱动）
   */
  get columnsWithActions(): any[] {
    return (this.columns as any[]).map((c) =>
      c.prop === 'IsValid' ? { ...c, slot: true } : c,
    )
  }

  /**
   * 搜索栏字段（覆盖：框架 treepconfig 的 ConvertToDto 不映射 SearchFields，
   * 故 config 中永远取不到后端定义的 SearchFields，此处直接声明唯一有意义的搜索项）
   */
  get searchFields(): any[] {
    return [
      {
        prop: 'DicName',
        label: '显示文本',
        type: 'text',
        placeholder: '请输入显示文本',
      },
    ]
  }

  // ========================================================
  // 公共数据方法（供页面 dataLoader 调用）
  // ========================================================

  /**
   * 加载字典项分页数据
   * @param params YzhTable 传入的 {page, rows, sort, order, ...searchParams}
   */
  async fetchItems(params: Record<string, any>): Promise<{
    rows: any[]
    total: number
  }> {
    if (!this.selectedNode.value) {
      return { rows: [], total: 0 }
    }
    const filters: Array<{ Field: string; Operator: string; Value: any }> = []
    // 搜索条件（排除分页/排序/...内部字段）
    const reserved = new Set(['page', 'rows', 'sort', 'order'])
    for (const [k, v] of Object.entries(params)) {
      if (reserved.has(k)) continue
      if (v === '' || v === null || v === undefined) continue
      filters.push({ Field: k, Value: v, Operator: 'like' })
    }
    // 树节点过滤（DicCode = 选中节点 code）
    filters.push({
      Field: this.relateFieldName,
      Value: this.selectedNode.value.code,
      Operator: 'eq',
    })
    const res = await this.apiPost<ApiResponse<{
      Items?: any[]
      TotalCount?: number
    }>>('/filter', {
      Page: params.page,
      PageSize: params.rows,
      SortField: params.sort,
      SortOrder: params.order,
      Filters: filters,
    })
    return {
      rows: res.data?.Items ?? [],
      total: res.data?.TotalCount ?? 0,
    }
  }

  // ========================================================
  // 树节点操作按钮（由 TreeConfig 配置驱动）
  // ========================================================

  get nodeActions(): Record<string, string> {
    const actions: Record<string, string> = {}
    const tc = this.treeConfig
    if (!tc) return actions
    if (tc.AllowEdit) {
      actions['add-child'] = '新增下级'
      actions['edit'] = '编辑'
    }
    if (tc.AllowDelete) {
      actions['delete'] = '删除'
    }
    if (this.enableField) {
      actions['toggle-valid'] = '禁用/启用'
    }
    return actions
  }

  /** 上下文感知的按钮文案（已启用→禁用，已禁用→启用） */
  getNodeActionLabel(action: string, node: TreeNode): string {
    if (action === 'toggle-valid') {
      const field = this.enableField ?? 'IsValid'
      const extra = (node.extra as any) || {}
      return (extra[field] ?? 1) === 1 ? '禁用' : '启用'
    }
    return this.nodeActions[action] || action
  }

  // ========================================================
  // 工具：数值字段归一化
  // --------------------------------------------------------
  // EntityConfig 的 Decimal 控件在前端落到 text 输入，用户键入的是字符串；
  // 后端 System.Text.Json 默认不允许「字符串 → 数值」，直接提交会 400。
  // 故提交前统一把数值字段转成 number（空值转 null）。
  // ========================================================

  private static readonly NUMERIC_FIELDS = ['OrderNo']

  private normalizeNumbers(payload: Record<string, any>): Record<string, any> {
    for (const key of DictionaryPageLogic.NUMERIC_FIELDS) {
      if (!(key in payload)) continue
      const v = payload[key]
      if (v === '' || v === null || v === undefined) {
        payload[key] = null
      } else if (typeof v === 'string' && !Number.isNaN(Number(v))) {
        payload[key] = Number(v)
      }
    }
    return payload
  }

  /** 按声明的表单字段白名单从 extra 取回填值（规避 Extra 里 camelCase/PascalCase 混用） */
  private pickFormValues(
    fields: Array<{ prop: string }>,
    extra: Record<string, any>,
  ): Record<string, any> {
    const out: Record<string, any> = {}
    for (const f of fields) {
      if (f.prop in extra) out[f.prop] = extra[f.prop]
    }
    return out
  }

  // ========================================================
  // 字典/分类（树节点）弹窗
  // ========================================================

  /**
   * 打开字典/分类弹窗
   * @param mode 'add' | 'edit'
   * @param node 编辑时的目标节点
   * @param parent 新增时的父节点（缺省取当前选中节点）
   */
  openTreeDialog(
    mode: 'add' | 'edit',
    node: TreeNode | null = null,
    parent: TreeNode | null = null,
  ): void {
    this.treeDialogMode.value = mode
    this.treeEditingNode.value = mode === 'edit' ? node : null
    this.treeParentNode.value = mode === 'add' ? (parent ?? this.selectedNode.value) : null

    Object.keys(this.treeFormData).forEach((k) => delete this.treeFormData[k])

    // NewEntity 是 PascalCase（EntitySchemaHelper V4 约定），与 treeFormFields[].prop 一致
    const tmpl = (this.treeFormConfig?.NewEntity as any) || {}
    const base: Record<string, any> = { ...tmpl, IsValid: 1 }

    if (mode === 'edit' && node) {
      const extra = (node.extra as any) || {}
      Object.assign(base, this.pickFormValues(this.treeFormFields, extra), {
        Code: node.code,
        ParentCode: node.parentCode,
        DicName: node.name,
        IsValid: extra.IsValid ?? 1,
      })
    }

    Object.assign(this.treeFormData, base)
    this.treeDialogVisible.value = true
  }

  /** 提交字典/分类表单 */
  async submitTreeForm(): Promise<void> {
    this.treeSubmitting.value = true
    try {
      const payload = this.normalizeNumbers({ ...this.treeFormData })
      if (this.treeDialogMode.value === 'add') {
        await this.addTreeNode(this.treeParentNode.value, payload)
      } else {
        await this.updateTreeNode(
          this.treeEditingNode.value!,
          payload.DicName ?? '',
          payload,
        )
      }
      this.treeDialogVisible.value = false
    } finally {
      this.treeSubmitting.value = false
    }
  }

  /** 删除字典/分类（软删除；确认弹窗由页面负责） */
  async deleteTree(node: TreeNode): Promise<void> {
    await this.deleteTreeNode(node, true)
    await this.refreshTable()
  }

  /** 启用/禁用字典/分类（基类统一处理：确认弹窗 → API → 更新 node.extra） */
  async toggleTreeValid(node: TreeNode): Promise<void> {
    await this.toggleTreeNodeWithConfirm(node)
  }

  // ========================================================
  // 字典项弹窗
  // ========================================================

  /**
   * 打开字典项弹窗
   * @param row 传入则为编辑，否则为新增（需已选中左侧字典）
   */
  openItemDialog(row?: any): boolean {
    if (!row && !this.selectedNode.value) {
      ElMessage.warning('请先在左侧选择字典')
      return false
    }

    this.dialogMode.value = row ? 'edit' : 'add'
    this.editingRow.value = row ?? null

    Object.keys(this.formData).forEach((k) => delete this.formData[k])

    // NewEntity 是 PascalCase（含 DicCode / DicName / DicValue / Color / OrderNo / IsValid / Remark）
    const tmpl = (this.config.value?.NewEntity as any) || {}
    if (row) {
      Object.assign(this.formData, tmpl, row)
    } else {
      Object.assign(this.formData, tmpl, {
        IsValid: 1,
        OrderNo: 0,
        DicCode: this.selectedNode.value!.code,
      })
    }

    this.dialogVisible.value = true
    return true
  }

  /** 提交字典项表单 */
  async submitItemForm(): Promise<void> {
    if (!this.selectedNode.value) throw new Error('请先在左侧选择字典')

    this.submitting.value = true
    try {
      const payload = this.normalizeNumbers({ ...this.formData })
      if (this.dialogMode.value === 'add') {
        const saved = await this.add(payload)
        this.insertRow(saved)
      } else {
        const saved = await this.update(payload)
        // 与后端返回值合并，避免表格行丢失未提交的字段（如 CreateTime）
        this.replaceRowByCode(saved.Code, { ...(this.editingRow.value || {}), ...saved })
      }
      ElMessage.success(this.dialogMode.value === 'add' ? '新增成功' : '修改成功')
      this.dialogVisible.value = false
    } finally {
      this.submitting.value = false
    }
  }

  /** 删除单个字典项 */
  async deleteItem(row: any): Promise<void> {
    await this.delete([row.Code])
    this.removeRowByCode(row.Code)
  }

  /** 批量删除字典项 */
  async batchDeleteItems(rows: any[]): Promise<void> {
    const codes = rows.map((r) => r.Code)
    await this.delete(codes)
    codes.forEach((code) => this.removeRowByCode(code))
  }

  /** 启用/禁用字典项（基类统一处理：确认弹窗 → API → 更新行） */
  async toggleItemValid(row: any): Promise<void> {
    await this.toggleRowIsValidWithConfirm(row, {
      entityName: row.DicName,
      field: this.config.value?.EnableField ?? 'IsValid',
    })
  }

  // ========================================================
  // 刷新
  // ========================================================

  /** 刷新右侧表格（保持当前选中节点） */
  async refreshTable(): Promise<void> {
    if (this.selectedNode.value) {
      await this.loadPageWithTree(this.selectedNode.value.code)
    } else {
      await this.loadPageWithoutTree()
    }
  }
}

export default DictionaryPageLogic
