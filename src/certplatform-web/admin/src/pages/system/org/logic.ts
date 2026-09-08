/**
 * OrgPageLogic - 组织机构-人员管理 Logic（左树右表）
 *
 * 架构：
 * - 左侧：机构树（Sys_Organization），懒加载 + 增删改 + 启用/禁用
 * - 右侧：人员表格（Sys_User），选中机构后加载 + 启用/禁用
 *
 * 配置驱动：
 * - 表格列从 /config 自动获取（xsFlag=true）
 * - 表单字段从 /config 自动获取（bcFlag=true）
 * - 树行为从 treeConfig 获取
 *
 * 特殊功能：
 * - ShowDisabled 开关：控制是否显示已禁用的记录
 * - 禁用机构：递归禁用子机构及用户
 * - 启用人员：需检查所属机构是否已启用
 */

import { ref, reactive } from 'vue'
import { TreeTableLogic } from '@share/logic'
import type { TreeNode } from '@share/types/tree'
import type { ColumnConfig } from '@share/types/contracts'

// ========================================================
// 类型
// ========================================================

export interface OrgUserRow {
  code: string
  userName: string
  userTrueName: string
  roleId: number
  roleName: string
  enable: number
  enableDesc: string
  phoneNo: string
  email: string
  createTime: string
  orgCode: string
  [key: string]: any
}

// ========================================================
// Logic
// ========================================================

export class OrgPageLogic extends TreeTableLogic<OrgUserRow> {
  controllerName = 'Organization'

  // ──── ShowDisabled 开关 ────
  /** 是否显示已禁用的记录 */
  showDisabled = ref(false)

  // ──── 表格列扩展（config 列 + 操作列）────
  get columnsWithActions(): ColumnConfig[] {
    const cols = this.config.value?.columns?.filter(c => c.xsFlag) ?? []
    return [
      ...cols,
      {
        fieldName: 'actions',
        desName: '操作',
        type: 'CustomSlot',
        xsFlag: true,
        bcFlag: false,
        enable: false,
        yxk: true,
        sortable: false,
        width: 180,
      } as ColumnConfig,
    ]
  }

  // ──── 表单字段扩展（config 字段 + 隐藏字段）────
  get formFieldsWithHidden() {
    const fields = this.formFields
    // 新增时添加 OrgCode 隐藏字段
    if (this.dialogMode.value === 'add') {
      fields.push({
        prop: 'orgCode',
        label: '所属机构',
        type: 'hidden',
        required: false,
        disabled: false,
        span: 0,
        placeholder: '',
      })
    }
    return fields
  }

  // ──── 弹窗状态 ────
  dialogVisible = ref(false)
  dialogMode = ref<'add' | 'edit'>('add')
  submitting = ref(false)
  formData = reactive<Partial<OrgUserRow>>({})

  // 树节点弹窗状态
  orgDialogVisible = ref(false)
  orgDialogMode = ref<'add' | 'edit'>('add')
  orgSubmitting = ref(false)
  orgFormData = reactive<Record<string, any>>({})
  orgParentNode = ref<TreeNode | null>(null)
  orgEditingNode = ref<TreeNode | null>(null)

  // ──── 机构表单字段（从后端 TreeTableConfig.TreeFormConfig 自动派生）────
  // 父类 TreeTableLogic.treeFormFields 已自动从 sys_organization_form.json 派生

  // ──── 表格行操作扩展（添加禁用/启用按钮）────
  get extendedRowButtons() {
    const buttons = [...this.rowButtons]
    // 动态添加启用/禁用按钮（根据当前行状态）
    if (this.config.value) {
      // 已在后端注册 action，前端通过 row.enable 判断显示哪个按钮
      buttons.push({ key: 'disable', text: '禁用', type: 'warning' })
      buttons.push({ key: 'enable', text: '启用', type: 'success' })
    }
    return buttons
  }

  // ──── 树节点操作扩展 ────
  get extendedTreeActions() {
    return [
      { key: 'disable', text: '禁用', type: 'warning' },
      { key: 'enable', text: '启用', type: 'success' },
    ]
  }

  // ========================================================
  // 公共 API（供模板调用）
  // ========================================================

  /** 通用 POST 请求（供模板直接调用） */
  async apiPostPublic<T = any>(path: string, body?: any): Promise<ApiResponse<T>> {
    return this.apiPost<T>(path, body)
  }

  // ========================================================
  // 覆盖：构建过滤器（添加 ShowDisabled）
  // ========================================================

  /** 带树条件的表格加载 */
  async loadPageWithTree(treeCode: string): Promise<void> {
    this.loading.value = true
    try {
      const filters = this.buildFilters()
      filters.push({ field: this.relateField, value: treeCode, operator: 'eq' })
      if (this.showDisabled.value) {
        filters.push({ field: 'ShowDisabled', value: 'true', operator: 'eq' })
      }
      const request = {
        page: this.pagination.page,
        pageSize: this.pagination.pageSize,
        sortField: this.sortField.value,
        sortOrder: this.sortOrder.value,
        filters,
      }
      const res = await this.apiPost(`/filter`, request)
      this.rows.value = res.data.items
      this.pagination.total = res.data.total
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

  /** 禁用用户 */
  async disableUser(row: OrgUserRow): Promise<void> {
    await this.executeAction('disable', row)
    // Split 更新：修改行 enable 字段
    this.replaceRowByCode(row.code, { ...row, enable: 0, enableDesc: '已禁用' })
  }

  /** 启用用户 */
  async enableUser(row: OrgUserRow): Promise<void> {
    await this.executeAction('enable', row)
    // Split 更新：修改行 enable 字段
    this.replaceRowByCode(row.code, { ...row, enable: 1, enableDesc: '启用' })
  }

  // ========================================================
  // 树节点操作：启用/禁用
  // ========================================================

  /** 禁用机构（递归子机构及用户） */
  async disableOrg(treeNode: TreeNode): Promise<void> {
    await this.executeTreeAction('disable', treeNode)
    // 禁用后刷新整棵树（因为递归影响多个节点）
    await this.loadTreeRoot()
    // 刷新表格
    await this.refreshTable()
  }

  /** 启用机构 */
  async enableOrg(treeNode: TreeNode): Promise<void> {
    await this.executeTreeAction('enable', treeNode)
    await this.loadTreeRoot()
    await this.refreshTable()
  }

  // ========================================================
  // 人员 CRUD
  // ========================================================

  /** 打开新增人员弹窗 */
  openAddUserDialog(): boolean {
    if (!this.selectedNode.value) {
      return false
    }
    this.dialogMode.value = 'add'
    Object.assign(this.formData, {
      userName: '',
      userTrueName: '',
      roleId: null,
      enable: 1,
      phoneNo: '',
      email: '',
      orgCode: this.selectedNode.value.code,
      userPwd: '',
    } as Partial<OrgUserRow>)
    this.dialogVisible.value = true
    return true
  }

  /** 打开编辑人员弹窗 */
  openEditUserDialog(row: OrgUserRow): void {
    this.dialogMode.value = 'edit'
    Object.assign(this.formData, { ...row })
    this.dialogVisible.value = true
  }

  /** 提交人员表单 */
  async submitUserForm(): Promise<void> {
    this.submitting.value = true
    try {
      if (this.dialogMode.value === 'add') {
        const saved = await this.add(this.formData as Partial<OrgUserRow>)
        this.insertRow(saved as OrgUserRow)
      } else {
        const saved = await this.update(this.formData as Partial<OrgUserRow>)
        this.replaceRowByCode((saved as OrgUserRow).code, saved as OrgUserRow)
      }
      this.dialogVisible.value = false
    } finally {
      this.submitting.value = false
    }
  }

  /** 删除人员 */
  async deleteUser(row: OrgUserRow): Promise<void> {
    await this.delete([row.code])
    this.removeRowByCode(row.code)
  }

  /** 批量删除人员 */
  async batchDeleteUsers(rows: OrgUserRow[]): Promise<void> {
    const codes = rows.map(r => r.code)
    await this.delete(codes)
    codes.forEach(code => this.removeRowByCode(code))
  }

  // ========================================================
  // 机构 CRUD
  // ========================================================

  /** 打开新增机构弹窗 */
  openAddOrgDialog(parentNode: TreeNode | null = null): void {
    this.orgDialogMode.value = 'add'
    this.orgParentNode.value = parentNode
    Object.assign(this.orgFormData, {
      orgName: '',
      orgCode: '',
      orgType: 'Dept',
      enable: 1,
      leaderName: '',
      leaderPhone: '',
      sort: 0,
      remark: '',
    })
    this.orgDialogVisible.value = true
  }

  /** 打开编辑机构弹窗 */
  openEditOrgDialog(node: TreeNode): void {
    this.orgDialogMode.value = 'edit'
    this.orgEditingNode.value = node
    Object.assign(this.orgFormData, {
      code: node.code,
      orgName: node.name,
      orgCode: (node.raw as any)?.orgCode ?? '',
      orgType: node.nodeType ?? 'Dept',
      enable: node.extra?.enable ?? 1,
      leaderName: node.extra?.leaderName ?? '',
      leaderPhone: node.extra?.leaderPhone ?? '',
      sort: node.extra?.sort ?? 0,
      remark: node.extra?.remark ?? '',
      parentCode: node.parentCode,
    })
    this.orgDialogVisible.value = true
  }

  /** 提交机构表单 */
  async submitOrgForm(): Promise<void> {
    this.orgSubmitting.value = true
    try {
      if (this.orgDialogMode.value === 'add') {
        await this.addTreeNode(this.orgParentNode.value, this.orgFormData)
      } else {
        await this.updateTreeNode(this.orgEditingNode.value!, this.orgFormData.orgName, this.orgFormData)
      }
      this.orgDialogVisible.value = false
      await this.loadTreeRoot()
    } finally {
      this.orgSubmitting.value = false
    }
  }

  /** 删除机构（含子节点检查） */
  async deleteOrg(node: TreeNode): Promise<void> {
    await this.deleteTreeNode(node)
    await this.loadTreeRoot()
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
