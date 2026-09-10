/**
 * OrgPageLogic - 组织机构-人员管理 Logic（左树右表，V1.1 - 对齐 YZH.Core.Stand）
 *
 * 数据访问规则：
 * - res.data：ApiResponse 顶层（camelCase）
 * - 业务行 r：PascalCase 字段（r.Code, r.OrgCode, r.Enable）
 * - TreeNode：el-tree 内部约定小写（node.code, node.name）
 * - formData：camelCase key（NewEntity 字典 key，反射 ToCamelCase）
 *
 * 架构：
 * - 左侧：机构树（Sys_Organization），懒加载 + 增删改 + 启用/禁用
 * - 右侧：人员表格（Sys_User），选中机构后加载 + 启用/禁用
 */

import { TreeTableLogic, type ApiResponse, type TreeNode } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'

// ========================================================
// Logic（前端不维护具体实体 interface，用 any 兜底）
// ========================================================

export class OrgPageLogic extends TreeTableLogic<any> {
  controllerName = 'Organization'

  // ──── ShowDisabled 开关 ────
  showDisabled = ref(false)

  // ──── 表格列扩展 ────
  get columnsWithActions(): any[] {
    const cols = this.columns as any[]
    return cols.map((c) => {
      if (c.prop === 'Enable') {
        return { ...c, slot: true }
      }
      return c
    })
  }

  // ──── 表单字段扩展 ────
  // OrgCode 不需要在表单中渲染为可编辑控件，但需要在弹窗顶部展示已选机构
  // 使用 custom 类型 + slot，让 YzhForm 渲染 #orgCode 插槽
  get formFieldsWithHidden() {
    const fields = [...this.formFields]
    if (this.dialogMode.value === 'add') {
      fields.unshift({
        prop: 'OrgCode',
        label: '所属机构',
        type: 'custom',
        slot: 'orgCode',
        required: false,
        disabled: true,
        span: 24,
        placeholder: '',
      } as any)
    }
    return fields
  }

  // ──── 弹窗状态 ────
  dialogVisible = ref(false)
  dialogMode = ref<'add' | 'edit'>('add')
  submitting = ref(false)
  /**
   * 人员表单数据：camelCase key（NewEntity 字典 key）
   * 例：{ code: "", userName: "", enable: 1, orgCode: "root" }
   */
  formData = reactive<Record<string, any>>({})

  // 机构弹窗状态
  orgDialogVisible = ref(false)
  orgDialogMode = ref<'add' | 'edit'>('add')
  orgSubmitting = ref(false)
  /**
   * 机构表单数据：camelCase key（NewEntity 字典 key）
   */
  orgFormData = reactive<Record<string, any>>({})
  orgParentNode = ref<TreeNode | null>(null)
  orgEditingNode = ref<TreeNode | null>(null)

  // ──── 表格行操作扩展（从后端 config 自动注入） ────
  get extendedRowButtons() {
    const buttons = [...this.rowButtons]
    // 读取后端自动注入的 CustomButtons
    const customButtons = this.rowCustomButtons
    for (const [key, text] of Object.entries(customButtons)) {
      const type = key === 'toggle-valid' ? 'warning' : 'primary'
      buttons.push({ key, text, type })
    }
    return buttons
  }

  // ──── 树节点操作（从 TreeConfig 配置驱动） ────
  get nodeActions(): Record<string, string> {
    const actions: Record<string, string> = {}
    const tc = this.treeConfig
    if (!tc) return actions
    // AllowEdit 控制新增下级 + 编辑按钮
    if (tc.AllowEdit) {
      actions['add-child'] = '新增下级'
      actions['edit'] = '编辑'
    }
    // AllowDelete 控制删除按钮
    if (tc.AllowDelete) {
      actions['delete'] = '删除'
    }
    // 合并后端自定义操作（禁用/启用等）
    if (tc.CustomActions) {
      Object.assign(actions, tc.CustomActions)
    }
    return actions
  }

  /**
   * 获取树节点操作按钮文本（上下文感知：已启用→禁用，已禁用→启用）
   */
  getNodeActionLabel(action: string, node: TreeNode): string {
    if (action === 'toggle-valid') {
      const extra = (node.extra as any) || {}
      const isValid = extra.IsValid ?? 1
      return isValid === 1 ? '禁用' : '启用'
    }
    // 其他操作使用 nodeActions 静态文本
    return this.nodeActions[action] || action
  }

  // ========================================================
  // 公共 API（供模板调用）
  // ========================================================

  async apiPostPublic<T = any>(
    path: string,
    body?: any,
  ): Promise<ApiResponse<T>> {
    return this.apiPost<ApiResponse<T>>(path, body)
  }

  // ========================================================
  // 覆盖：构建过滤器（添加 ShowDisabled）
  // ========================================================

  /** 带树条件的表格加载 */
  async loadPageWithTree(treeCode: string): Promise<void> {
    this.loading.value = true
    try {
      const filters = this.buildFilters()
      filters.push({
        Field: this.relateField,
        Value: treeCode,
        Operator: 'eq',
      })
      if (this.showDisabled.value) {
        filters.push({ Field: 'ShowDisabled', Value: 'true', Operator: 'eq' })
      }
      const request = {
        Page: this.pagination.page,
        PageSize: this.pagination.pageSize,
        SortField: this.sortField.value,
        SortOrder: this.sortOrder.value,
        Filters: filters,
      }
      const res = await this.apiPost(`/filter`, request)
      const page = res.data
      if (page) {
        this.rows.value = page.Items ?? []
        this.pagination.total = page.TotalCount ?? 0
      }
    } catch (e: any) {
      this.rows.value = []
      this.pagination.total = 0
    } finally {
      this.loading.value = false
    }
  }

  /** 未选中树节点时的表格加载 */
  async loadPageWithoutTree(): Promise<void> {
    if (this.noSelectionBehavior === 'empty') {
      this.rows.value = []
      this.pagination.total = 0
    } else {
      await this.loadPageWithTree('')
    }
  }

  // ========================================================
  // 行操作：启用/禁用
  // ========================================================

  /** 切换用户有效标志 */
  async toggleUserIsValid(row: any): Promise<void> {
    const result = await this.toggleIsValid(row.Code)
    if (result) {
      this.replaceRowByCode(row.Code, {
        ...row,
        IsValid: result.IsValid,
      })
    }
  }

  // ========================================================
  // 树节点操作：启用/禁用
  // ========================================================

  /** 切换机构有效标志 */
  async toggleOrgIsValid(treeNode: TreeNode): Promise<void> {
    const result = await this.toggleTreeNodeIsValid(treeNode)
    if (result) {
      // 更新节点 extra 中的 IsValid 值
      const extra = (treeNode.extra as any) || {}
      extra.IsValid = result.IsValid
      treeNode.extra = { ...extra }
    }
    await this.refreshTable()
  }

  // ========================================================
  // 人员 CRUD
  // ========================================================

  /** 打开新增人员弹窗（仅叶子节点允许） */
  openAddUserDialog(): boolean {
    if (!this.selectedNode.value) {
      return false
    }
    // 必须选择末端机构（叶子节点）才能增加人员
    if (!this.selectedNode.value.isLeaf) {
      ElMessage.warning('请选择末端机构（不含子机构的节点）')
      return false
    }
    this.dialogMode.value = 'add'
    // 先清空旧数据，再从 NewEntity 模板初始化（避免上次编辑的残留）
    Object.keys(this.formData).forEach((k) => delete this.formData[k])
    // NewEntity 和 formFields 都是 PascalCase key，直接使用
    const tmpl = (this.config.value?.NewEntity as any) || {}
    Object.assign(this.formData, {
      ...tmpl,
      Code: crypto.randomUUID?.() || `${Date.now()}`,
      IsValid: 1,
      OrgCode: this.selectedNode.value.code,
    })
    this.dialogVisible.value = true
    return true
  }

  /** 打开编辑人员弹窗 */
  openEditUserDialog(row: any): void {
    this.dialogMode.value = 'edit'
    // formData 使用 PascalCase key（与 formFields prop 一致）
    Object.keys(this.formData).forEach((k) => delete this.formData[k])
    Object.assign(this.formData, row)
    this.dialogVisible.value = true
  }

  /** 提交人员表单 */
  async submitUserForm(): Promise<void> {
    this.submitting.value = true
    try {
      // formData 已经是 PascalCase key，直接提交
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

  /** 删除人员 */
  async deleteUser(row: any): Promise<void> {
    await this.delete([row.Code])
    this.removeRowByCode(row.Code)
  }

  /** 批量删除人员 */
  async batchDeleteUsers(rows: any[]): Promise<void> {
    const codes = rows.map((r) => r.Code)
    await this.delete(codes)
    codes.forEach((code) => this.removeRowByCode(code))
  }

  // ========================================================
  // 机构 CRUD
  // ========================================================

  /** 打开新增机构弹窗 */
  openAddOrgDialog(parentNode: TreeNode | null = null): void {
    this.orgDialogMode.value = 'add'
    this.orgParentNode.value = parentNode
    // 先清空旧数据，再从 NewEntity 模板初始化
    Object.keys(this.orgFormData).forEach((k) => delete this.orgFormData[k])
    const tmpl = (this.treeFormConfig?.NewEntity as any) || {}
    Object.assign(this.orgFormData, {
      ...tmpl,
      Code: crypto.randomUUID?.() || `${Date.now()}`,
      IsValid: 1,
    })
    this.orgDialogVisible.value = true
  }

  /** 打开编辑机构弹窗 */
  openEditOrgDialog(node: TreeNode): void {
    this.orgDialogMode.value = 'edit'
    this.orgEditingNode.value = node
    // 先清空旧数据，再用 PascalCase key 还原
    Object.keys(this.orgFormData).forEach((k) => delete this.orgFormData[k])
    const extra = (node.extra as any) || {}
    Object.assign(this.orgFormData, {
      Code: node.code,
      OrgName: node.name,
      ParentCode: node.parentCode,
      ...extra,
    })
    this.orgDialogVisible.value = true
  }

  /** 提交机构表单（基类 addTreeNode/updateTreeNode 已做局部更新，无需 reload 树） */
  async submitOrgForm(): Promise<void> {
    this.orgSubmitting.value = true
    try {
      if (this.orgDialogMode.value === 'add') {
        await this.addTreeNode(this.orgParentNode.value, this.orgFormData)
      } else {
        await this.updateTreeNode(
          this.orgEditingNode.value!,
          this.orgFormData.OrgName ?? this.orgFormData.Name ?? '',
          this.orgFormData,
        )
      }
      this.orgDialogVisible.value = false
    } finally {
      this.orgSubmitting.value = false
    }
  }

  /** 删除机构（skipConfirm=true，由页面 handleDeleteOrg 处理确认弹窗） */
  async deleteOrg(node: TreeNode): Promise<void> {
    await this.deleteTreeNode(node, true)
    // 刷新右侧表格（被删机构下的人员需要重新加载）
    await this.refreshTable()
  }

  // ========================================================
  // ShowDisabled 开关
  // ========================================================

  /** 切换 ShowDisabled 并刷新 */
  async toggleShowDisabled(): Promise<void> {
    this.showDisabled.value = !this.showDisabled.value
    await this.refreshTable()
  }

  /** 刷新当前表格（保持选中节点） */
  async refreshTable(): Promise<void> {
    if (this.selectedNode.value) {
      await this.loadPageWithTree(this.selectedNode.value.code)
    } else {
      await this.loadPageWithoutTree()
    }
  }
}

export default OrgPageLogic
