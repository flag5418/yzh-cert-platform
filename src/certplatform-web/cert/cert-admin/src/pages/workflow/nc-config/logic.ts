/**
 * NC 规则设计 — 左树右表 Logic
 *
 * 布局：
 * - 左树：组织 → 标准 → 阶段（useFileTree composable）
 * - 右表：NC 检查规则（YzhTable + 分页）
 *
 * 联动：选中阶段节点后，自动注入 OrgCode/StandardCode/PhaseCode 过滤
 */
import { ref, reactive } from 'vue'
import { ElMessage } from 'element-plus'
import {
  getNCRulePage,
  saveNCRule,
  updateNCRule,
  deleteNCRule,
  toggleNCRuleActive,
  copyNCRule,
  getISOClauseTree,
  type NCRule,
  type ISOClauseTreeNode,
} from '@share/api/workflow/nc-config'
import type { TreeNode } from '@share/composables/useFileTree'

export type { NCRule, ISOClauseTreeNode }

// ──── 表格列定义 ────
export const tableColumns = [
  { prop: 'RuleCode', label: '规则编号', width: 150 },
  { prop: 'RuleName', label: '规则名称', minWidth: 200 },
  { prop: 'RuleNameEn', label: '英文名称', width: 150, showOverflowTooltip: true },
  { prop: 'ClauseNumber', label: '条款编号', width: 120 },
  { prop: 'IsActive', label: '启用', width: 80, align: 'center' },
  { prop: 'Remark', label: '备注', minWidth: 150, showOverflowTooltip: true },
  { prop: 'actions', label: '操作', width: 120, fixed: 'right' },
]

// ──── 编辑表单字段 ────
export const editFormFields = [
  { prop: 'RuleName', label: '规则名称', required: true, placeholder: '如：资源提供检查' },
  { prop: 'RuleNameEn', label: '英文名称', placeholder: '如：Resource Provision' },
  { prop: 'ClauseCode', label: '关联条款', required: true, type: 'tree-select' },
  { prop: 'IsActive', label: '是否启用', type: 'switch' },
  { prop: 'Remark', label: '备注', type: 'textarea', rows: 2 },
]

export class NCConfigLogic {
  // ──── 树状态 ────
  selectedPhase = ref<TreeNode | null>(null)

  // ──── 表格状态 ────
  tableData = ref<NCRule[]>([])
  total = ref(0)
  loading = ref(false)
  page = ref(1)
  pageSize = ref(20)

  // ──── 筛选 ────
  keyword = ref('')

  // ──── 编辑弹窗 ────
  dialogVisible = ref(false)
  dialogMode = ref<'add' | 'edit'>('add')
  submitting = ref(false)
  editingRule = ref<NCRule | null>(null)
  formData = reactive<Partial<NCRule>>({
    RuleName: '',
    RuleNameEn: '',
    ClauseCode: '',
    IsActive: true,
    Remark: '',
  })

  // ──── 条款树 ────
  clauseTreeData = ref<ISOClauseTreeNode[]>([])
  clauseLoading = ref(false)

  /** 按.ParentCode 将扁平条款列表组树（按条款编号自然排序），并注入展示标签 */
  static buildClauseTree(flat: ISOClauseTreeNode[]): ISOClauseTreeNode[] {
    const byNumber = (a: ISOClauseTreeNode, b: ISOClauseTreeNode) =>
      (a.ClauseNumber || '').localeCompare(b.ClauseNumber || '', undefined, { numeric: true })
    const map = new Map<string, ISOClauseTreeNode>()
    flat.forEach(c => map.set(c.Code!, { ...c, Label: `${c.ClauseNumber} ${c.Title}`, Children: [] }))
    const roots: ISOClauseTreeNode[] = []
    flat.forEach(c => {
      const node = map.get(c.Code!)
      const parent = c.ParentCode ? map.get(c.ParentCode) : undefined
      if (parent) parent.Children!.push(node!)
      else roots.push(node!)
    })
    roots.sort(byNumber)
    roots.forEach(r => r.Children?.sort(byNumber))
    return roots
  }

  // ========================================================
  // 树→表格联动
  // ========================================================

  /** 选中阶段节点 */
  async handleNodeClick(node: TreeNode) {
    if (node.type !== 'stage') {
      this.selectedPhase.value = null
      this.tableData.value = []
      this.total.value = 0
      return
    }
    this.selectedPhase.value = node
    this.page.value = 1
    await this.loadTable()
  }

  // ========================================================
  // 表格数据加载
  // ========================================================

  async loadTable() {
    const phase = this.selectedPhase.value
    if (!phase) {
      this.tableData.value = []
      this.total.value = 0
      return
    }

    this.loading.value = true
    try {
      const filters: Array<{ Field: string; Value: string; Operator: string }> = [
        { Field: 'OrgCode', Value: phase.orgCode || '', Operator: 'eq' },
        { Field: 'StandardCode', Value: phase.stdCode || '', Operator: 'eq' },
        // PhaseCode 存 cert_cert_stage.StageCode（如 AP/S1），与树节点一致
        { Field: 'PhaseCode', Value: phase.phaseCode || '', Operator: 'eq' },
      ]
      if (this.keyword.value) {
        filters.push({ Field: 'RuleName', Value: this.keyword.value, Operator: 'contains' })
      }

      const res = await getNCRulePage({
        Page: this.page.value,
        PageSize: this.pageSize.value,
        Filters: filters,
      })
      this.tableData.value = res?.data?.Items ?? []
      this.total.value = res?.data?.TotalCount ?? 0
    } catch (e: any) {
      ElMessage.error(e?.message || '加载失败')
    } finally {
      this.loading.value = false
    }
  }

  /** 分页切换 */
  handlePageChange(newPage: number) {
    this.page.value = newPage
    this.loadTable()
  }

  /** 搜索 */
  handleSearch() {
    this.page.value = 1
    this.loadTable()
  }

  /** 重置 */
  handleReset() {
    this.keyword.value = ''
    this.page.value = 1
    this.loadTable()
  }

  // ========================================================
  // 条款树
  // ========================================================

  async loadClauseTree() {
    const phase = this.selectedPhase.value
    if (!phase?.stdCode) {
      this.clauseTreeData.value = []
      return
    }
    this.clauseLoading.value = true
    try {
      const flat = await getISOClauseTree(phase.stdCode)
      this.clauseTreeData.value = NCConfigLogic.buildClauseTree(flat)
    } catch {
      ElMessage.error('加载条款失败')
    } finally {
      this.clauseLoading.value = false
    }
  }

  // ========================================================
  // 规则 CRUD
  // ========================================================

  /** 打开新增弹窗 */
  openAddDialog() {
    if (!this.selectedPhase.value) {
      ElMessage.warning('请先选择阶段')
      return
    }
    this.dialogMode.value = 'add'
    this.editingRule.value = null
    Object.assign(this.formData, {
      RuleName: '',
      RuleNameEn: '',
      ClauseCode: '',
      IsActive: true,
      Remark: '',
    })
    this.dialogVisible.value = true
    this.loadClauseTree()
  }

  /** 打开编辑弹窗 */
  openEditDialog(row: NCRule) {
    this.dialogMode.value = 'edit'
    this.editingRule.value = row
    Object.assign(this.formData, {
      RuleName: row.RuleName,
      RuleNameEn: row.RuleNameEn || '',
      ClauseCode: row.ClauseCode,
      IsActive: row.IsActive,
      Remark: row.Remark || '',
    })
    this.dialogVisible.value = true
    this.loadClauseTree()
  }

  /** 提交表单 */
  async handleSubmit() {
    if (!this.formData.RuleName) {
      ElMessage.warning('请输入规则名称')
      return
    }
    if (!this.formData.ClauseCode) {
      ElMessage.warning('请选择关联条款')
      return
    }

    const phase = this.selectedPhase.value
    if (!phase) return

    this.submitting.value = true
    try {
      if (this.dialogMode.value === 'add') {
        const payload: Partial<NCRule> = {
          ...this.formData,
          OrgCode: phase.orgCode,
          StandardCode: phase.stdCode || '',
          // PhaseCode 存 cert_cert_stage.StageCode（如 AP/S1），与树节点/列表过滤一致
          PhaseCode: phase.phaseCode || '',
        }
        await saveNCRule(payload)
        ElMessage.success('新增成功')
      } else {
        await updateNCRule({
          Code: this.editingRule.value?.Code,
          ...this.formData,
        } as Partial<NCRule>)
        ElMessage.success('修改成功')
      }
      this.dialogVisible.value = false
      await this.loadTable()
    } catch (e: any) {
      ElMessage.error(e?.message || '保存失败')
    } finally {
      this.submitting.value = false
    }
  }

  /** 删除规则 */
  async handleDelete(row: NCRule) {
    const { ElMessageBox } = await import('element-plus')
    try {
      await ElMessageBox.confirm(`确定删除规则「${row.RuleName}」？`, '确认删除', { type: 'warning' })
    } catch { return }

    try {
      await deleteNCRule([row.Code || ''])
      ElMessage.success('删除成功')
      await this.loadTable()
    } catch (e: any) {
      ElMessage.error(e?.message || '删除失败')
    }
  }

  /** 切换启用 */
  async handleToggleActive(row: NCRule) {
    try {
      await toggleNCRuleActive(row.Code || '')
      ElMessage.success('操作成功')
      await this.loadTable()
    } catch (e: any) {
      ElMessage.error(e?.message || '操作失败')
    }
  }

  /** 复制规则 */
  async handleCopy(row: NCRule) {
    const { ElMessageBox } = await import('element-plus')
    try {
      await ElMessageBox.confirm(`确定复制规则「${row.RuleName}」？`, '确认复制', { type: 'info' })
    } catch { return }

    try {
      await copyNCRule(row.Code || '')
      ElMessage.success('复制成功')
      await this.loadTable()
    } catch (e: any) {
      ElMessage.error(e?.message || '复制失败')
    }
  }
}
