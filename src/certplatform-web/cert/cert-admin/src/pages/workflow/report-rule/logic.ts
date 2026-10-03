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
  type TreeNode,
  type EntityConfigDto,
  type TreeBehaviorConfig,
  type ApiResponse,
} from '@yzh-core'
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { getOrganizationTree } from '@share/composables/useDirectoryApi'
import { useFileTree, type TreeNode as FileTreeNode } from '@share/composables/useFileTree'
import { getISOClauseTree, type ISOClauseTreeNode } from '@share/api/workflow/nc-config'
import type { ReportSection } from '@share/types/cert'

export type { ReportSection }

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

/** 节点类型 → 图标（Element Plus 图标已在宿主 main.ts 全局注册） */
const NODE_ICON: Record<string, string> = {
  organization: 'OfficeBuilding',
  standard: 'Document',
  stage: 'Calendar',
}

/**
 * 目录域树（组织 → 标准 → 阶段 → 目录 → 文件）→ 内核 TreeNode。
 *
 * 只保留 组织/标准/阶段 三层：阶段以下的目录/文件属于标准文件管理域，
 * 与报告章节定义无关。
 *
 * TreeNode 字段一律 PascalCase（守卫 R2），阶段三编码进 Extra 供 buildFilters 读取。
 */
function toCoreNodes(nodes: FileTreeNode[]): TreeNode[] {
  const result: TreeNode[] = []
  for (const n of nodes) {
    if (!(n.Type in NODE_ICON)) continue
    const children = toCoreNodes(n.Children ?? [])
    result.push({
      Code: String(n.Code),
      Name: n.Name,
      NodeType: n.Type,
      IsLeaf: children.length === 0,
      Extra: {
        Icon: NODE_ICON[n.Type],
        OrgCode: n.OrgCode ?? '',
        StdCode: n.StdCode ?? '',
        PhaseCode: n.PhaseCode ?? '',
      },
      Children: children,
    })
  }
  return result
}

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

  /** 目录域组织树的纯转换器 */
  private readonly fileTree = useFileTree()

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
      const orgTree = await getOrganizationTree()
      this.treeSide.setNodes(toCoreNodes(this.fileTree.transformOrgTree(orgTree)))
    } finally {
      this.treeSide.treeLoading.value = false
    }
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
      .map((c: any) =>
        c.prop === 'IsValid'
          ? { ...c, tagMap: { 1: '启用', 0: '禁用' }, tagTypeMap: { 1: 'success', 0: 'info' } }
          : c,
      )
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
