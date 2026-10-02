/**
 * FillParamDefLogic — 体系认证全局参数定义（后台管理 · ★ 左树右表）
 *
 * 后端：`FillParamDefController`（`YzhControllerBase<FillParamDef>`）@ `api/Cert/FillParamDef`
 * 表：`cert_fill_param_def`｜菜单 `MENU_00217`｜路由 `/business/fill-param-def`
 *
 * ★ 页面形态（26 号 §3.1 方案 A）：
 *   左树 = **机构 → 标准 → 阶段**（只读，数据源 = StandardDirectory 组织树）
 *   右表 = **生效参数集** —— 选中一个「机构 × 标准 × 阶段」后，引擎实际会用到的那批参数
 *
 * ★ 为什么右表不用基类的 `/filter`：
 *   参数作用域可以「不限标准 / 不限阶段」，生效条件是
 *   `OrgCode = X AND (StandardCode = '' OR S) AND (StageCode = '' OR P)` —— 这是 **OR 组合**，
 *   `FilterItem` 的 AND 语义表达不了；且同一 `ParamCode` 可能有多条（通用 + 标准专属），
 *   必须按「更具体优先」去重取一条。
 *   ⇒ 覆写 `dataLoader` 走 `POST /effective`，**与填充引擎共用
 *     `ParamValueResolver.PickMostSpecific`**；⛔ 不在前端再实现一份合并逻辑 ——
 *     两套口径的后果是「后台看到 A 条生效、文档里填的却是 B 条的值」且两边都不报错。
 *
 * ★ 本页比样板多做的三件事：
 *   ① 三个作用域下拉的选项来自 `GET scopes`（**运行期数据**，EntityConfig 写不下，
 *      写进 JSON 会立刻过期 —— 机构/标准/阶段都是可增删的业务数据）；
 *   ② `SourceExpr`（取值表达式）渲染成**下拉**，选项 = 企业全部属性目录
 *      （`GET enterprise-attrs`），标签里带**真实示例值** —— 这正是需求里那句
 *      「自动形成带企业所有属性的列表，让用户选择」。
 *      ⛔ 刻意用下拉而非自由输入：当前引擎只支持 `enterprise.*`，
 *        自由输入只会让人写错字段名（写错后表现为「文档里一直是空」且不报错）；
 *   ③ 覆写 `dataLoader` 走 `effective`，并**本地分页** —— 生效集规模小（实测 < 100 条），
 *      后端不分页，前端切片响应更快。
 */
import { ref } from 'vue'
import {
  TreeTableLogic,
  expectOk,
  type ApiResponse,
  type EntityConfigDto,
  type TreeBehaviorConfig,
  type TreeNode,
  type YzhFormField,
  type YzhTableColumn,
} from '@yzh-core'
import { getOrganizationTree } from '@share/composables/useDirectoryApi'
import { useFileTree, type TreeNode as FileTreeNode } from '@share/composables/useFileTree'
import {
  getEffectiveParams,
  getEnterpriseAttrs,
  getScopes,
} from '@share/api/cert/fill-param-def'
import type {
  EffectiveItem,
  EffectiveStats,
  EnterpriseAttr,
  ScopeOption,
  Scopes,
} from '@share/api/cert/fill-param-def'

/** 维护方式 → 界面标签（与后端 ParamValueResolver 的三个取值一致） */
export const MAINTAIN_MODE_LABEL: Record<string, string> = {
  auto: '自动映射',
  manual: '企业填写',
  both: '自动带出可覆盖',
}

/** 维护方式 → 标签色 */
export const MAINTAIN_MODE_TAG: Record<string, 'success' | 'warning' | 'info'> = {
  auto: 'success',
  manual: 'warning',
  both: 'info',
}

/** 取值来源类别 → 界面标签（与填充引擎的 4 项能力一一对应） */
export const SOURCE_KIND_LABEL: Record<string, string> = {
  global: '全局参数',
  replace: '替换',
  headerFooter: '页眉页脚',
  ai: 'AI 生成',
  manual: '人工填写',
  compute: '计算派生',
}

// ──── 左树：树行为配置 ────
//
// 与 nc-config / report-rule 完全一致：树**只读**，阶段节点的 Code 是目录树 id，
// 右表用节点 Extra 里的三元组（OrgCode + StdCode + PhaseCode）定位。
const TREE_BEHAVIOR: TreeBehaviorConfig = {
  Lazy: false,
  AllowEdit: false,
  AllowAddChild: false,
  AllowDelete: false,
  AllowRename: false,
  // ★ 必须显式 false：否则 resolveTreeActions 会回落到 config.EnableField(IsValid)，
  //   在树节点上吐出「启用/禁用」按钮 —— 本页树是只读的组织树，没有启停语义
  AllowToggle: false,
  NameField: 'Name',
  CodeField: 'Code',
  ParentCodeField: 'ParentCode',
  // 本页覆写了 dataLoader，不走基类的 RelateField 过滤；此处填 OrgCode 只为语义自洽
  RelateField: 'OrgCode',
  NoSelectionBehavior: 'empty',
  MaxLevel: 3,
  AllowDeleteWithChildren: false,
}

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
 * 与全局参数定义无关。
 *
 * ⚠️ 后端节点字典的键是 `phaseCode`（**已确认为 GUID = `cert_cert_stage.Code`**，
 * 见 `StandardDirectoryService.GetOrganizationTreeAsync` 的注释），
 * `useFileTree.transformOrgTree` 把它映射为 `PhaseCode`，与 `cert_fill_param_def.StageCode` 同口径。
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

export class FillParamDefLogic extends TreeTableLogic<EffectiveItem> {
  controllerName = 'Cert/FillParamDef'

  /** 目录域组织树的纯转换器（不参与状态，只借它的 transformOrgTree） */
  private readonly fileTree = useFileTree()

  /** 机构 / 标准 / 阶段下拉数据（onAfterInit 拉取） */
  readonly scopes = ref<Scopes>({ orgs: [], standards: [], stages: [] })

  /** 企业属性目录（含真实示例值） */
  readonly attrs = ref<EnterpriseAttr[]>([])
  readonly attrSampleEnterprise = ref('')

  /** 下拉数据是否就绪（未就绪时不要把 SourceExpr 渲染成空下拉） */
  readonly optionsReady = ref(false)

  /** ★ 当前作用域的生效集统计（右表上方展示「生效 N 条 / 通用 x / 标准 y / …」） */
  readonly stats = ref<EffectiveStats | null>(null)

  /** ★ 关键字（后端按 ParamCode / ParamName 模糊匹配；改动后调 tableRef.refresh()） */
  readonly keyword = ref('')

  /** 是否已选中「阶段」节点 —— 右表数据的前置条件 */
  get anySelected(): boolean {
    const node = this.selectedNode
    return !!node && node.NodeType === 'stage' && !!node.Extra?.PhaseCode
  }

  /** 当前选中作用域的可读描述（顶栏展示用） */
  get scopeText(): string {
    const node = this.selectedNode
    if (!node) return '未选择'
    if (node.NodeType === 'organization') return `${node.Name}（请继续选择标准 / 阶段）`
    if (node.NodeType === 'standard') return `${node.Name}（请继续选择阶段）`
    return node.Name
  }

  protected override get defaultValues(): Record<string, any> {
    return {
      IsValid: 1,
      ValueType: 'text',
      SourceKind: 'global',
      MaintainMode: 'auto',
      GroupName: '基础信息',
      IsRequired: false,
      IsBuiltin: false,
      SortOrder: 0,
    }
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

  /**
   * 树加载完成后自动定位到**第一个阶段节点**。
   *
   * <p>不这么做的话，页面打开时右表是空的（选中根节点 = 机构，`anySelected=false`），
   * 用户得自己一路点开才知道哪里能看参数 —— 空右表容易被误读成「功能没做完」。</p>
   */
  protected override async afterTreeLoaded(): Promise<void> {
    const first = this.firstStageNode(this.treeData)
    if (first) await this.onNodeClick(first)
  }

  private firstStageNode(nodes: TreeNode[]): TreeNode | null {
    for (const n of nodes) {
      if (n.NodeType === 'stage' && n.Extra?.PhaseCode) return n
      const hit = this.firstStageNode(n.Children ?? [])
      if (hit) return hit
    }
    return null
  }

  // ========================================================
  // 覆盖点③：右表数据 = 生效参数集（★ 本页的核心差异）
  // ========================================================

  /**
   * 覆写 `dataLoader`：走 `POST /effective` 而不是基类的 `/filter`。
   *
   * <p>返回契约与基类一致（`{ rows, total }`），`YzhTable` 无需改动；
   * 分页在前端做（后端不分页 —— 生效集规模小）。</p>
   */
  override async dataLoader(params: any): Promise<{ rows: EffectiveItem[]; total: number }> {
    if (!this.anySelected) {
      this.pagination.total = 0
      this.rows.value = []
      this.stats.value = null
      return { rows: [], total: 0 }
    }

    const ex = this.selectedNode!.Extra!
    const page = Number(params?.page ?? 1) || 1
    const pageSize = Number(params?.rows ?? this.pagination.pageSize) || this.pagination.pageSize

    this.loading.value = true
    try {
      const res = await getEffectiveParams({
        OrgCode: String(ex.OrgCode ?? ''),
        StandardCode: String(ex.StdCode ?? ''),
        StageCode: String(ex.PhaseCode ?? ''),
        Keyword: this.keyword.value.trim(),
        // ★ 复用内核的「显示已禁用」开关：语义从「显示禁用行」升级为
        //   「显示未生效的定义」（被更具体覆写 + 已禁用）—— 管理员更关心「我配的为什么没生效」
        IncludeShadowed: this.showDisabled.value,
      })

      this.stats.value = res.Stats
      const all = res.Items ?? []
      const start = (page - 1) * pageSize
      const slice = all.slice(start, start + pageSize)

      this.pagination.page = page
      this.pagination.pageSize = pageSize
      this.pagination.total = all.length
      this.rows.value = slice

      return { rows: slice, total: all.length }
    } catch (e) {
      this.reportError(e, '查询生效参数失败')
      return { rows: [], total: 0 }
    } finally {
      this.loading.value = false
    }
  }

  /** 关键字变化 → 回到第 1 页重查 */
  async applyKeyword(): Promise<void> {
    this.pagination.page = 1
    await this.refreshTable()
  }

  // ========================================================
  // 覆盖点④：表格列（隐藏归属列，复用「所属标准」列位渲染作用域计算列）
  // ========================================================

  override get columns(): YzhTableColumn<EffectiveItem>[] {
    return super.columns
      // 机构 / 阶段由左树决定，右表不展示；Id 永不展示（双关键字准则 A）
      .filter((c: any) => !['OrgCode', 'StageCode', 'Id'].includes(c.prop))
      .map((c: any) => {
        // ★ 复用 EntityConfig 里「所属标准」的列位，渲染**作用域计算列**
        //   （`ScopeKind` / `ScopeText` 是后端 effective 端点算出的，EntityConfig 里没有）
        if (c.prop === 'StandardCode') {
          return { ...c, label: '作用域', width: 230 }
        }
        if (c.prop === 'IsValid') {
          return { ...c, tagMap: { 1: '启用', 0: '禁用' }, tagTypeMap: { 1: 'success', 0: 'info' } }
        }
        return c
      })
  }

  // ========================================================
  // 覆盖点⑤：表单字段（注入作用域下拉 + 企业属性下拉）
  // ========================================================

  override get formFields(): YzhFormField[] {
    // 机构由左树注入，不进表单 —— 避免用户改出「挂在别的机构下」的参数
    const base = super.formFields.filter((f) => f.prop !== 'OrgCode')

    // 未就绪时原样返回：宁可暂时显示为普通输入框，也不要渲染「空下拉」
    // （空下拉会让用户以为「没有可选项」，而实际只是数据还没到）
    if (!this.optionsReady.value) return base

    const withBlank = (list: ScopeOption[], blankLabel: string) => [
      { label: blankLabel, value: '' },
      ...list.map((o) => ({ label: o.no ? `${o.label}（${o.no}）` : o.label, value: o.value })),
    ]

    // ★ 企业属性选项：标签带真实示例值，用户能直接看到「选了这个会填进去什么」
    const attrOptions = this.attrs.value.map((a) => ({
      label: a.sample ? `${a.label}　→　${a.sample}` : `${a.label}　（该企业此项为空）`,
      value: a.expr,
    }))

    return base.map((f) => {
      switch (f.prop) {
        case 'StandardCode':
          return { ...f, options: withBlank(this.scopes.value.standards, '不限标准（对所有标准生效）') }

        case 'StageCode':
          return { ...f, options: withBlank(this.scopes.value.stages, '不限阶段（对所有阶段生效）') }

        case 'SourceExpr':
          return {
            ...f,
            type: 'select' as const,
            options: [{ label: '（不使用自动取值）', value: '' }, ...attrOptions],
            placeholder: attrOptions.length
              ? '选择要自动带入的企业属性'
              : '暂无企业属性可用（请先在企业端建档）',
          }

        // ★ IsValid 是 int（0/1），switch 必须显式声明 active-value / inactive-value，
        //   否则会写入布尔 true/false（DB 能容下 1/0，但语义上不该依赖隐式转换）
        case 'IsValid':
          return { ...f, type: 'switch' as any, fieldProps: { 'active-value': 1, 'inactive-value': 0 } }

        default:
          return f
      }
    })
  }

  /** 新增准备钩子：注入树选中的三元组（★ 默认作用域 = 当前节点，可在表单里改为「不限」） */
  protected override onPrepareAdd(formData: Record<string, any>) {
    const ex = this.selectedNode?.Extra
    formData.OrgCode = ex?.OrgCode ?? ''
    formData.StandardCode = ex?.StdCode ?? ''
    formData.StageCode = ex?.PhaseCode ?? ''
  }

  // ========================================================
  // 初始化：拉两个运行期下拉数据源
  // ========================================================

  protected override async onAfterInit(): Promise<void> {
    // 两个下拉数据源并行拉取；任一失败不阻断页面（表格仍可用，只是下拉为空）
    const [scopes, attrs] = await Promise.all([
      getScopes().catch(() => ({ orgs: [], standards: [], stages: [] }) as Scopes),
      getEnterpriseAttrs().catch(() => ({ items: [], enterpriseCode: '', enterpriseName: '' })),
    ])
    this.scopes.value = scopes
    this.attrs.value = attrs.items
    this.attrSampleEnterprise.value = attrs.enterpriseName ?? ''
    this.optionsReady.value = true
  }
}

export default FillParamDefLogic
