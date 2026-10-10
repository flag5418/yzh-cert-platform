/**
 * ★ 报告章节定义 — 左树右表 Logic（★2026-09-29 去主表化）
 *
 * 【★ 与 NC 规则定义（nc-config）的对称性】
 *   本页与 `nc-config` 使用**完全相同的架构**：
 *   - 继承 TreeTableLogic，由 useTreeTable 注入 tableRef/treeTableRef
 *   - 左树：组织 → 标准 → 阶段（StandardDirectory 组织树，只读）
 *   - 右表：按 (OrgCode + StandardCode + PhaseCode) 三元组过滤
 *   - 行动作/工具栏由后端 EntityConfig（反射生成）+ 基类端点驱动
 *
 * 【★ 去主表化（D34）带来的差异】
 *   改造前：左树选中阶段 → 先查「报告主表」拿 ReportCode → 再按 ReportCode 查章节
 *   改造后：左树选中阶段 → ★ 直接按三元组查章节（章节自带 OrgCode/StandardCode/PhaseCode）
 *           → 与 NC 规则完全一样，**不再有两层**
 *
 * 【★ 本页不需要的能力（D34 删除主表后一并去除）】
 *   - 报告名称（TemplateName）
 *   - 报表模板上传（TemplateFilePath / template/upload）
 *   - 多套模板选默认（IsDefault）
 *   - 章节顺序整体编排（SectionConfig）
 */
import {
  TreeTableLogic,
  expectOk,
  type YzhTableColumn,
  type YzhFormField,
  type FilterItem,
  type EntityConfigDto,
  type TreeBehaviorConfig,
  type ApiResponse,
} from '@yzh-core'
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { loadOrgStageTree } from '@share/composables/useOrgStageTree'
import { getISOClauseTree, type ISOClauseTreeNode } from '@share/api/workflow/nc-config'
import { getReportSectionPage } from '@share/api/workflow/report-rule'
import type { ReportSection } from '@share/types/cert'

export type { ReportSection }

// ──── 判定方式（JudgeMode）字典 ────
//
// 值域：auto=AI 自动判定 / manual=人工判定 / semi=半自动
// 背景：部分报告章节必须由人工判断（如现场作业一致性、员工访谈），
//       AI 无法从企业上传资料自动分析 → 执行引擎据此跳过不可自动化的章节。
//
// 与 `nc-config/logic.ts` 的 JUDGE_MODE_* 完全一致，本页复用同一套选项。

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

// ──── 左树：树行为配置 ────
//
// 与 nc-config 完全一致：树只读，阶段节点 Code 是目录树 id，右表关联键是 PhaseCode。
const TREE_BEHAVIOR: TreeBehaviorConfig = {
  Lazy: false,
  AllowEdit: false,
  AllowAddChild: false,
  AllowDelete: false,
  AllowRename: false,
  // ★ 必须显式 false：否则 resolveTreeActions 会回落到 config.EnableField(IsValid)
  //   在树节点上吐出「启用/禁用」按钮 —— 本页树节点没有启停语义
  AllowToggle: false,
  NameField: 'Name',
  CodeField: 'Code',
  ParentCodeField: 'ParentCode',
  // 右表 cert_report_section.PhaseCode ↔ 阶段节点关联（OrgCode/StandardCode 由 buildFilters 追加）
  RelateField: 'PhaseCode',
  NoSelectionBehavior: 'empty',
  MaxLevel: 3,
  AllowDeleteWithChildren: false,
}

// ──── 左树：目录域树节点 → 内核 TreeNode ────


// ──── 工具函数：扁平条款列表 → 树形（与 nc-config 同款）───
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

export class ReportRuleLogic extends TreeTableLogic<any> {
  // ──── 控制器名称（对应后端 ReportDefinitionController 路由）───
  controllerName = 'Admin/Workflow/ReportDefinition'


  // ──── 条款树数据（编辑弹窗 tree-select 使用）───
  clauseTreeData = ref<ISOClauseTreeNode[]>([])
  clauseLoading = ref(false)

  /** 是否已选中「阶段」节点（右表过滤 / 新增章节的前置条件） */
  get anySelected(): boolean {
    const node = this.selectedNode
    return !!node && node.NodeType === 'stage' && !!node.Extra?.PhaseCode
  }

  constructor() {
    super()
    // 新增前校验树选中（业务差异钩子）
    this.registerHandler('add', () => {
      if (!this.anySelected) {
        ElMessage.warning('请先选择阶段')
        return
      }
      this.openAddDialog()
    })
  }

  /** 提交前剥离 SectionNameEn 换行（章节英文名称为单行字段，禁止保留 \n） */
  override normalizeBeforeSubmit(payload: Record<string, any>): Record<string, any> {
    if (payload.SectionNameEn != null) {
      payload.SectionNameEn = String(payload.SectionNameEn).replace(/[\r\n]+/g, ' ')
    }
    return super.normalizeBeforeSubmit(payload)
  }

  // ========================================================
  // 覆盖点①：配置加载（后端无 /treepconfig）
  // ========================================================

  override async loadConfig(): Promise<void> {
    const res = await this.apiGet<ApiResponse<EntityConfigDto>>('/config')
    expectOk(res, '加载页面配置失败')
    this.config.value = res.data
    this.treeTableConfig.value = {
      TableConfig: res.data!,
      TreeConfig: TREE_BEHAVIOR,
    }
  }

  // ========================================================
  // 覆盖点②：树加载（树不是本实体树）
  // ========================================================

  override async loadTreeRoot(): Promise<void> {
    this.treeSide.treeLoading.value = true
    try {
      this.treeSide.setNodes(await loadOrgStageTree())
      // 加载各阶段章节数量徽标
      this.loadSectionCounts()
    } finally {
      this.treeSide.treeLoading.value = false
    }
  }

  /**
   * 加载各阶段报告章节数量，附加到树节点徽标（countField = 'SectionCount'）。
   * 后端暂缺批量聚合接口，暂走 N+1（PageSize=1 仅取 TotalCount）。
   */
  async loadSectionCounts(): Promise<void> {
    const nodes = this.treeSide.treeData.value
    if (!nodes?.length) return

    const stages: { code: string; phaseCode: string }[] = []
    const walk = (list: any[]) => {
      for (const n of list) {
        if (n.NodeType === 'stage' && n.Extra?.PhaseCode) {
          stages.push({ code: String(n.Code), phaseCode: n.Extra.PhaseCode })
        }
        if (n.Children?.length) walk(n.Children)
      }
    }
    walk(nodes)

    if (!stages.length) return

    await Promise.all(
      stages.map(async (s) => {
        try {
          const res = await getReportSectionPage({
            Page: 1,
            PageSize: 1,
            Filters: [{ Field: 'PhaseCode', Value: s.phaseCode, Operator: 'eq' }],
          })
          const count = res?.data?.TotalCount ?? 0
          const node = this.findNodeByCode(s.code)
          if (node) (node as any).SectionCount = count
        } catch {
          // 静默失败：数量显示异常不影响主功能
        }
      }),
    )
  }

  /** 按 Code 在树里定位节点（供 loadSectionCounts 使用） */
  private findNodeByCode(code: string): any | null {
    const walk = (nodes: any[]): any | null => {
      for (const n of nodes) {
        if (String(n.Code) === code) return n
        const hit = n.Children?.length ? walk(n.Children) : null
        if (hit) return hit
      }
      return null
    }
    return walk(this.treeSide.treeData.value)
  }

  // ========================================================
  // 树→表格联动过滤
  // ========================================================

  /** 只有「阶段」节点参与右表过滤；组织/标准节点 → 右表清空 */
  protected override shouldApplyTreeFilter(): boolean {
    return this.anySelected
  }

  /** 关联值：阶段节点 Code 是目录树 id，右表存的关联键是 PhaseCode */
  protected override relatedValue(): string | null {
    return this.selectedNode?.Extra?.PhaseCode ?? null
  }

  /**
   * 覆盖 buildFilters：基类注入 RelateField(PhaseCode)，
   * 这里追加 OrgCode + StandardCode —— ★ 章节表自带三元组（去主表化后），
   * 三字段才能唯一定位一个阶段的章节集合。
   */
  protected override buildFilters(extra?: Record<string, any>): FilterItem[] {
    const base = super.buildFilters(extra)
    if (this.anySelected) {
      const ex = this.selectedNode!.Extra!
      base.push({ Field: 'OrgCode', Value: ex.OrgCode, Operator: 'eq' })
      base.push({ Field: 'StandardCode', Value: ex.StdCode, Operator: 'eq' })
    }
    return base
  }

  /** 新增准备钩子：注入树关联的三元组（★ 去主表化后章节自带归属） */
  protected override onPrepareAdd(formData: Record<string, any>) {
    const ex = this.selectedNode?.Extra
    formData.OrgCode = ex?.OrgCode ?? ''
    formData.StandardCode = ex?.StdCode ?? ''
    formData.PhaseCode = ex?.PhaseCode ?? ''
  }

  // ========================================================
  // 表格列配置（覆盖：隐藏归属三列，隐藏 ClauseCode，IsValid 转标签）
  // ========================================================

  override get columns(): YzhTableColumn<any>[] {
    return super.columns
      // 归属三列由左树决定，右表不展示（与 nc-config 隐藏 ClauseCode 同理）
      .filter((c: any) => !['OrgCode', 'StandardCode', 'PhaseCode', 'ClauseCode', 'Id'].includes(c.prop))
      .map((c: any) => {
        // ★ JudgeMode → 带颜色的标签（与 nc-config 同款）
        if (c.prop === 'JudgeMode') {
          return { ...c, tagMap: JUDGE_MODE_LABEL, tagTypeMap: JUDGE_MODE_TAG }
        }
        if (c.prop === 'IsValid') {
          return { ...c, tagMap: { 1: '启用', 0: '禁用' }, tagTypeMap: { 1: 'success', 0: 'info' } }
        }
        return c
      })
  }

  // ========================================================
  // 表单字段（覆盖：ClauseCode 用 tree-select；IsValid 用 switch）
  // ========================================================

  override get formFields(): YzhFormField[] {
    const base = super.formFields
    return base
      // 归属三列不进表单（由树注入）
      .filter((f) => !['OrgCode', 'StandardCode', 'PhaseCode'].includes(f.prop))
      .map((f) => {
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
        // ★ JudgeMode → select 下拉（选项由本页注入，与 nc-config 同款）
        if (f.prop === 'JudgeMode') {
          return {
            ...f,
            type: 'select' as any,
            options: JUDGE_MODE_OPTIONS,
            placeholder: '选择判定方式',
          }
        }
        // ★ IsValid（int 0/1）→ switch
        if (f.prop === 'IsValid') {
          return {
            ...f,
            type: 'switch' as any,
            fieldProps: { 'active-value': 1, 'inactive-value': 0 },
          }
        }
        // ★ 章节内容：JSON 里 Type='Memo' → 内核 mapControlType 自动映射 textarea
        //   （与 organization 一致：控件类型由 EntityConfig 决定，前端不覆写）
        return f
      })
  }

  // ========================================================
  // 条款树加载（与 nc-config 同款）
  // ========================================================

  async loadClauseTree(): Promise<void> {
    const stdCode =
      (this.formData.StandardCode as string) || this.selectedNode?.Extra?.StdCode || ''
    if (!stdCode) {
      this.clauseTreeData.value = []
      return
    }
    this.clauseLoading.value = true
    try {
      const flat = await getISOClauseTree(stdCode)
      this.clauseTreeData.value = buildClauseTree(flat)
    } catch {
      ElMessage.error('加载条款失败')
      this.clauseTreeData.value = []
    } finally {
      this.clauseLoading.value = false
    }
  }

  override openAddDialog() {
    super.openAddDialog()
    this.loadClauseTree()
  }

  override openEditDialog(row: any) {
    super.openEditDialog(row)
    this.loadClauseTree()
  }
}
