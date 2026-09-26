/**
 * NC 规则管理 — 工作流规则 Logic（YZH SingleTableCore 架构）
 *
 * 布局：
 * - 左树：组织 → 标准 → 阶段（通过 useFileTree composable 加载）
 * - 右表：NC 检查规则（YzhTable + 分页 + 搜索）
 *
 * 架构：
 * - 继承 SingleTableCore 获得标准 CRUD 能力（filter/add/update/delete/search）
 * - 选中树节点后，自动注入 OrgCode/StandardCode/PhaseCode 三字段联动过滤
 * - 数据加载统一通过 dataLoader（YzhTable 驱动）
 * - 行按钮全部由后端 Cert/ValidationRule.json 的 RowButtons 配置驱动
 *   （Edit/Delete + CustomButtons: disable/enable/Copy，EnableField=IsActive →
 *   内核按行状态二选一：启用行只显橙「禁用」、停用行只显绿「启用」），
 *   走标准 POST /action/{method} 约定，前端零硬编码按钮
 * - JudgeMode（判定方式）: 列/表单的**选项**在本页注入（见 JUDGE_MODE_* 常量）
 */
import {
  SingleTableCore,
  type YzhTableColumn,
  type YzhFormField,
  type FilterItem,
  type PageParams,
  type Page,
} from '@yzh-core'
import { ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getISOClauseTree } from '@share/api/workflow/nc-config'
import type { NCRule, ISOClauseTreeNode } from '@share/api/workflow/nc-config'

export type { NCRule, ISOClauseTreeNode }

// ──── 判定方式（JudgeMode）字典 ────
//
// 值域：auto=AI 自动判定 / manual=人工判定 / semi=半自动
// 背景：部分检查项是**人为调研**发现的（如「某个该有的设备是否存在」），
//       AI 不可能知道 → 必须由人工判定。术语是「判定方式」，不是「复核」。
//
// ⚠️ 为什么不走 EntityConfig 的 DictCode：
//   ① `entityAdapters.toFormFields` 明确 `options: undefined`，且未把 DictCode
//      映射成 loadOptions → 表单里的 select 会渲染成**没有选项的空下拉**；
//   ② `GET /api/System/Dictionary/items/{code}` 的 Value 取的是字典项 **Code**
//      （随机唯一值），不是 DicValue 字面量 —— 存不进 `auto/manual/semi`。
//   故本页在 Logic 里显式给 options（与 ClauseCode 的 treeSelect 同一手法）。

/** 判定方式：值 → 显示文字 */
const JUDGE_MODE_LABEL: Record<string, string> = {
  auto: 'AI 自动',
  manual: '人工',
  semi: '半自动',
}

/** 判定方式：值 → 标签颜色 */
const JUDGE_MODE_TAG: Record<string, 'success' | 'warning' | 'primary'> = {
  auto: 'success',
  manual: 'warning',
  semi: 'primary',
}

/** 判定方式下拉选项 */
const JUDGE_MODE_OPTIONS = [
  { label: 'AI 自动判定', value: 'auto' },
  { label: '人工判定', value: 'manual' },
  { label: '半自动（AI 初判 + 人工确认）', value: 'semi' },
]

// ──── 工具函数：扁平条款列表 → 树形 ────
function buildClauseTree(flat: ISOClauseTreeNode[]): ISOClauseTreeNode[] {
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

export class NCConfigLogic extends SingleTableCore<any> {
  // ──── 控制器名称（对应后端 ValidationRuleController 路由） ────
  controllerName = 'ValidationRule'

  // ──── 树联动过滤状态 ────
  selectedOrgCode = ref('')
  selectedStandardCode = ref('')
  selectedPhaseCode = ref('')

  // ──── 条款树数据（编辑弹窗 tree-select 使用） ────
  clauseTreeData = ref<ISOClauseTreeNode[]>([])
  clauseLoading = ref(false)

  /** 是否有选中的阶段节点 */
  get anySelected(): boolean {
    return !!this.selectedPhaseCode.value
  }

  constructor() {
    super()
    // 「复制」动作加二次确认（按钮本身来自后端 RowButtons.CustomButtons，
    // 前端仅覆写行为钩子，不改按钮声明）
    this.registerHandler('custom:Copy', async (row) => {
      if (!row) return
      try {
        await ElMessageBox.confirm(`确定复制规则「${row.RuleName}」？`, '确认复制', { type: 'info' })
      } catch {
        return
      }
      await this.executeAction('Copy', row)
    })
    // 新增前校验树选中（业务差异钩子）
    this.registerHandler('add', () => {
      if (!this.anySelected) {
        ElMessage.warning('请先选择阶段')
        return
      }
      this.openAddDialog()
    })
  }

  // ========================================================
  // 树→表格联动过滤
  // ========================================================

  /** 设置树过滤条件 → 重置分页 */
  setTreeFilter(orgCode: string, standardCode: string, phaseCode: string) {
    this.selectedOrgCode.value = orgCode
    this.selectedStandardCode.value = standardCode
    this.selectedPhaseCode.value = phaseCode
    this.pagination.page = 1
  }

  /**
   * 覆盖 buildFilters：注入树联动条件（OrgCode + StandardCode + PhaseCode）
   */
  protected override buildFilters(extra?: Record<string, any>): FilterItem[] {
    const base = super.buildFilters(extra)
    if (this.selectedOrgCode.value) {
      base.push({ Field: 'OrgCode', Value: this.selectedOrgCode.value, Operator: 'eq' })
    }
    if (this.selectedStandardCode.value) {
      base.push({ Field: 'StandardCode', Value: this.selectedStandardCode.value, Operator: 'eq' })
    }
    if (this.selectedPhaseCode.value) {
      base.push({ Field: 'PhaseCode', Value: this.selectedPhaseCode.value, Operator: 'eq' })
    }
    return base
  }

  /** 新增准备钩子：注入树关联编码 */
  protected override onPrepareAdd(formData: Record<string, any>) {
    formData.OrgCode = this.selectedOrgCode.value
    formData.StandardCode = this.selectedStandardCode.value
    formData.PhaseCode = this.selectedPhaseCode.value
  }

  /** 无树选中时返回空数据；有树选中时走基类逻辑 */
  override async dataLoader(params: PageParams): Promise<Page<any>> {
    if (!this.anySelected) {
      return { rows: [], total: 0 }
    }
    return super.dataLoader(params)
  }

  // ========================================================
  // 表格列配置（覆盖：隐藏 ClauseCode 列，表格展示 ClauseNumber）
  // ========================================================

  override get columns(): YzhTableColumn<any>[] {
    const cols = super.columns
    return cols
      .filter((c: any) => c.prop !== 'ClauseCode')
      .map((c: any) =>
        c.prop === 'JudgeMode'
          ? { ...c, tagMap: JUDGE_MODE_LABEL, tagTypeMap: JUDGE_MODE_TAG }
          : c,
      )
  }

  // ========================================================
  // 行操作按钮：不覆写 —— 全部由后端 RowButtons 配置驱动（架构铁律）
  // ========================================================

  // ========================================================
  // 表单字段（覆盖：ClauseCode 使用 tree-select）
  // ========================================================

  override get formFields(): YzhFormField[] {
    const base = super.formFields
    return base.map((f) => {
      if (f.prop === 'ClauseCode') {
        return {
          ...f,
          type: 'treeSelect' as any,
          options: this.clauseTreeData.value as any[],
          fieldProps: {
            nodeKey: 'Code',
            props: { label: 'Label', children: 'Children' },
            checkStrictly: true,
            filterable: true,
          },
        }
      }
      // JudgeMode: 判定方式下拉（EntityConfig 只给类型，选项由本页注入，见文件顶部说明）
      if (f.prop === 'JudgeMode') {
        return {
          ...f,
          type: 'select' as any,
          options: JUDGE_MODE_OPTIONS,
          placeholder: '选择判定方式',
        }
      }
      // IsActive: boolean switch（后端 NewEntity 是 boolean，覆盖默认 1/0 值避免类型不匹配）
      if (f.prop === 'IsActive') {
        return {
          ...f,
          type: 'switch' as any,
          fieldProps: {
            'active-value': true,
            'inactive-value': false,
          },
        }
      }
      return f
    })
  }

  // ========================================================
  // 条款树加载
  // ========================================================

  /** 加载条款树（编辑弹窗内 tree-select 使用） */
  async loadClauseTree(): Promise<void> {
    if (!this.selectedStandardCode.value) {
      this.clauseTreeData.value = []
      return
    }
    this.clauseLoading.value = true
    try {
      const flat = await getISOClauseTree(this.selectedStandardCode.value)
      this.clauseTreeData.value = buildClauseTree(flat)
    } catch {
      ElMessage.error('加载条款失败')
      this.clauseTreeData.value = []
    } finally {
      this.clauseLoading.value = false
    }
  }

  /** 打开新增/编辑弹窗时同步加载条款树 */
  override openAddDialog() {
    super.openAddDialog()
    this.loadClauseTree()
  }

  override openEditDialog(row: any) {
    super.openEditDialog(row)
    this.loadClauseTree()
  }
}
