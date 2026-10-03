/**
 * NC 规则管理 — 工作流规则 Logic（YZH TreeTableCore 架构）
 *
 * 布局：
 * - 左树：组织 → 标准 → 阶段（只读，数据源 = StandardDirectory 组织树）
 * - 右表：NC 检查规则（YzhTable + 分页 + 搜索）
 *
 * 架构（样板页面指南-V1 §四）：
 * - 继承 TreeTableLogic（= TreeTableCore = SingleTableCore + 树能力），
 *   由 useTreeTable(NCConfigLogic) 注入 tableRef/treeTableRef 与初始化流程
 * - 选中阶段节点后，内核自动注入 OrgCode / StandardCode / PhaseCode 三字段联动过滤
 * - 数据加载走内核 dataLoader（未选中阶段 → NoSelectionBehavior=empty → 空表）
 * - 行按钮全部由后端 Cert/ValidationRule.json 的 RowButtons 配置驱动
 *   （Edit/Delete + CustomButtons: disable/enable/Copy，EnableField=IsActive →
 *   内核按行状态二选一：启用行只显橙「禁用」、停用行只显绿「启用」），
 *   走标准 POST /action/{method} 约定，前端零硬编码按钮
 * - JudgeMode（判定方式）: 列/表单的**选项**在本页注入（见 JUDGE_MODE_* 常量）
 *
 * ⚠️ 两个覆盖点（本页后端是 YzhControllerBase 单表控制器，不是 TreeTableControllerBase）：
 *   loadConfig()   → 后端只有 GET /config，无 /treepconfig → 前端构造 TreeConfig
 *   loadTreeRoot() → 树不是本实体树，取 StandardDirectory 的组织树
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
import { ElMessage, ElMessageBox } from 'element-plus'
import { getOrganizationTree } from '@share/composables/useDirectoryApi'
import { useFileTree, type TreeNode as FileTreeNode } from '@share/composables/useFileTree'
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

// ──── 左树：树行为配置 ────
//
// 后端 ValidationRuleController 继承 YzhControllerBase（单表），没有 /treepconfig，
// 故 TreeConfig 由本页构造（覆盖点 loadConfig）。字段语义对齐 YZH.Core.Stand/TreeBehaviorConfig。
const TREE_BEHAVIOR: TreeBehaviorConfig = {
  // 树只读：组织/标准/阶段的维护在各自业务页，本页只作筛选维度
  Lazy: false,
  AllowEdit: false,
  AllowDelete: false,
  AllowRename: false,
  // ★ 必须显式 false：否则 resolveTreeActions 会回落到 config.EnableField(IsValid)
  //   在树节点上吐出「启用/禁用」按钮 —— 本页树节点没有启停语义
  AllowToggle: false,
  NameField: 'Name',
  CodeField: 'Code',
  ParentCodeField: 'ParentCode',
  // 右表 ValidationRule.PhaseCode ↔ 阶段节点关联（OrgCode/StandardCode 由 buildFilters 追加）
  RelateField: 'PhaseCode',
  // 未选中阶段 → 右表空（不发请求）
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
 * 与 NC 检查规则无关（原 CertBizTree 用 nodeTypes 过滤，此处等价）。
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
        PhaseDefinitionCode: n.PhaseDefinitionCode ?? '',
      },
      Children: children,
    })
  }
  return result
}

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

export class NCConfigLogic extends TreeTableLogic<any> {
  // ──── 控制器名称（对应后端 ValidationRuleController 路由） ────
  controllerName = 'Admin/Workflow/ValidationRule'

  /** 目录域组织树的纯转换器（不走 useFileTree.loadTree —— 它会预加载阶段目录，本页用不到） */
  private readonly fileTree = useFileTree()

  // ──── 条款树数据（编辑弹窗 tree-select 使用） ────
  clauseTreeData = ref<ISOClauseTreeNode[]>([])
  clauseLoading = ref(false)

  /** 是否已选中「阶段」节点（右表过滤 / 新增检查项的前置条件） */
  get anySelected(): boolean {
    const node = this.selectedNode
    return !!node && node.NodeType === 'stage' && !!node.Extra?.PhaseCode
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
  // 覆盖点①：配置加载（后端无 /treepconfig）
  // ========================================================

  /** YzhControllerBase 只有 GET /config → 单表配置 + 前端构造的 TreeConfig 合成 TreeTableConfig */
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

  /** 左树 = 组织 → 标准 → 阶段（StandardDirectory 组织树），非 /tree/root */
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
   * 这里追加 OrgCode + StandardCode —— 阶段编码（如 jd01）跨机构/标准会重复，
   * 三字段才能唯一定位一个阶段。
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

  /** 新增准备钩子：注入树关联编码 */
  protected override onPrepareAdd(formData: Record<string, any>) {
    const ex = this.selectedNode?.Extra
    formData.OrgCode = ex?.OrgCode ?? ''
    formData.StandardCode = ex?.StdCode ?? ''
    formData.PhaseCode = ex?.PhaseCode ?? ''
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
    // 新增：StandardCode 由 onPrepareAdd 注入；编辑：来自行数据
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
