/**
 * ★ 标准文档填写规则 — 左树右表 Logic
 *
 * 【页面定位】
 *   给「已登记的空白模板」标注**填写锚点**（哪一处 ← 填什么值），并挂上**全文填写提示词**。
 *   产出物 = `cert_doc_template_anchor` 规则行 —— 它是 `DocumentFillEngine` 的**唯一输入**。
 *
 * 【架构】
 *   - 左树：机构 → 「标准 · 阶段」→ 模板（数据源 = `GET /DocTemplate/tree`，只读）
 *   - 右表：按 `TemplateCode` 过滤的锚点规则（`YzhControllerBase<DocTemplateAnchor>` 通用端点驱动）
 *   - 列 / 表单 / 搜索全部由后端 EntityConfig 反射生成，页面**零手写 CRUD handler**
 *
 * 【为什么树只读】
 *   模板 = 标准目录里已存在的空白文件（`EnterpriseCode='YZH-STD-ENT'`），
 *   登记动作走顶部「登记模板」弹窗（`/DocTemplate/candidates` + `/register`）。
 *   在树上「新建/改名/删除」会让 `StandardFileCode` 唯一键失去意义。
 *
 * 【⛔ 与「文档提取规则」页的本质差异】
 *   提取规则描述「从上传件里**读出**什么」（`cert_doc_extraction_rule`，自带 org/std/stage 三元组）；
 *   填写规则描述「往空白模板里**写入**什么」（锚点表只挂 `TemplateCode`，归属由模板决定）。
 *   两者**不是同一张表的两种视图**，⛔ 不要合并。
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
import { ElMessage } from 'element-plus'
import { getDocTemplateTree } from '@share/api/workflow/doc-fill-rule'

// ──── 左树：树行为配置 ────
const TREE_BEHAVIOR: TreeBehaviorConfig = {
  Lazy: false,
  AllowEdit: false,
  AllowAddChild: false,
  AllowDelete: false,
  AllowRename: false,
  // ★ 必须显式 false：否则 resolveTreeActions 会回落到 config.EnableField(IsValid)，
  //   在树节点上吐出「启用/禁用」按钮 —— 本页树节点是目录/模板的**只读投影**，无启停语义
  AllowToggle: false,
  NameField: 'Name',
  CodeField: 'Code',
  ParentCodeField: 'ParentCode',
  // 右表 `cert_doc_template_anchor.TemplateCode` ↔ 模板节点 `Code` 关联
  RelateField: 'TemplateCode',
  NoSelectionBehavior: 'empty',
  MaxLevel: 3,
  AllowDeleteWithChildren: false,
}

/** 节点类型 → 图标（Element Plus 图标已在宿主 main.ts 全局注册） */
const NODE_ICON: Record<string, string> = {
  org: 'OfficeBuilding',
  scope: 'Document',
  template: 'Files',
}

// ──── L3 受控值（与后端 `DocTemplateAnchorController` 的静态集合逐字一致）────
//
// ⛔ 改这里必须同步改后端：后端才是权威校验（前端下拉只是防手滑）。
const ANCHOR_TYPES = ['scalar', 'block', 'table', 'table_total', 'domain']
const ANCHOR_KINDS = ['token', 'bookmark', 'range']
const WRITE_MODES = ['replace', 'overwrite', 'append', 'remove']
const VALUE_TYPES = ['text', 'number', 'date', 'bool', 'enum']
const HEADER_KINDS = ['default', 'first', 'even']

/** 受控值 → `{ label, value }`（空选项由调用方决定是否加） */
const toOptions = (values: string[]) => values.map((v) => ({ label: v, value: v }))

export class DocFillRuleLogic extends TreeTableLogic<any> {
  /** 后端锚点控制器路由（★ 与目录同名同路径） */
  controllerName = 'Admin/Workflow/DocTemplateAnchor'

  /** 当前选中的模板节点（`Extra.kind === 'template'` 才有值） */
  get templateNode(): TreeNode | null {
    const node = this.selectedNode
    return node && node.Extra?.kind === 'template' ? node : null
  }

  /** 右表可操作的前置条件：选中了「模板」叶子 */
  get anySelected(): boolean {
    return !!this.templateNode
  }

  /** 模板 Code（= `cert_doc_template.Code`） */
  get templateCode(): string {
    return this.templateNode?.Code ?? ''
  }

  /** 模板上挂的全文提示词编码（可能为空 —— 表示该模板只走锚点填充） */
  get promptCode(): string {
    return (this.templateNode?.Extra?.fillPromptCode as string) ?? ''
  }

  /** 模板所属机构（用于提示词「更具体优先」的选取） */
  get templateOrgCode(): string {
    return (this.templateNode?.Extra?.orgCode as string) ?? ''
  }

  constructor() {
    super()
    // 新增锚点前必须选中模板 —— 否则 `TemplateCode` 无处可注入
    this.registerHandler('add', () => {
      if (!this.anySelected) {
        ElMessage.warning('请先在左侧选择一个模板')
        return
      }
      this.openAddDialog()
    })
  }

  // ========================================================
  // 覆盖点①：配置加载（后端无 /treepconfig，手工装配 TreeTableConfig）
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
  // 覆盖点②：树加载（树不是本实体树 —— 数据源是模板表）
  // ========================================================

  override async loadTreeRoot(): Promise<void> {
    this.treeSide.treeLoading.value = true
    try {
      const res = await getDocTemplateTree()
      expectOk(res, '加载模板树失败')
      const nodes = res.data?.Nodes ?? []
      this.treeSide.setNodes(nodes.map((n) => this.toCoreNode(n)))
    } finally {
      this.treeSide.treeLoading.value = false
    }
  }

  /**
   * 后端 `DocTemplateTreeNode` → 内核 `TreeNode`。
   *
   * 字段一律 **PascalCase**（守卫 R2）。后端已按同形返回，这里只补 `Icon`
   * 并把 `Extra` 原样透传（`kind` 是判据，其余给页面用）。
   */
  private toCoreNode(n: any): TreeNode {
    const children = (n.Children ?? []).map((c: any) => this.toCoreNode(c))
    return {
      Code: String(n.Code),
      Name: n.Name,
      NodeType: n.Extra?.kind ?? '',
      IsLeaf: children.length === 0,
      Extra: { ...(n.Extra ?? {}), Icon: NODE_ICON[n.Extra?.kind] },
      Children: children,
    }
  }

  // ========================================================
  // 树 → 表格联动过滤
  // ========================================================

  /** 只有「模板」叶子参与右表过滤；机构/作用域节点 → 右表清空 */
  protected override shouldApplyTreeFilter(): boolean {
    return this.anySelected
  }

  /** 关联值 = 模板 Code */
  protected override relatedValue(): string | null {
    return this.templateCode || null
  }

  /**
   * 覆盖 buildFilters：基类注入 `RelateField(TemplateCode)`；
   * 这里**不再追加** org/std/stage —— 锚点表只有 `TemplateCode` 一个归属键
   * （归属由模板行决定，重复存一份必然漂移）。
   */
  protected override buildFilters(extra?: Record<string, any>): FilterItem[] {
    return super.buildFilters(extra)
  }

  /** 新增准备钩子：注入所属模板（⛔ 表单里不出现，避免改出「挂到别的模板」的孤儿锚点） */
  protected override onPrepareAdd(formData: Record<string, any>) {
    formData.TemplateCode = this.templateCode
    // 三个定位列的「不适用」语义 = 空串 / 0（它们参与唯一键 uk_tpl_anchor，
    // MySQL 唯一索引里 NULL 不互相冲突 ⇒ 必须落空串）
    if (formData.SheetName == null) formData.SheetName = ''
    if (formData.HeaderKind == null) formData.HeaderKind = ''
    if (formData.SectionIndex == null) formData.SectionIndex = 0
  }

  // ========================================================
  // 覆盖点③：表格列（隐藏归属列与两个原始 JSON 列）
  // ========================================================

  override get columns(): YzhTableColumn<any>[] {
    return super.columns
      .filter(
        (c: any) =>
          // 归属由左树决定；Id 永不展示（双关键字准则 A）
          !['Id', 'TemplateCode'].includes(c.prop) &&
          // SourceSpec 是 320 宽的 JSON 串、SourceSummary 是恒空的视图列 —— 表格里只有噪音
          !['SourceSpec', 'SourceSummary'].includes(c.prop),
      )
      .map((c: any) =>
        c.prop === 'IsValid'
          ? { ...c, tagMap: { 1: '启用', 0: '禁用' }, tagTypeMap: { 1: 'success', 0: 'info' } }
          : c,
      )
  }

  // ========================================================
  // 覆盖点④：表单字段（4 个受控值列 → 下拉；IsValid → switch）
  // ========================================================

  override get formFields(): YzhFormField[] {
    return super.formFields
      // 模板由左树注入
      .filter((f) => f.prop !== 'TemplateCode')
      .map((f) => {
        switch (f.prop) {
          // ★ 受控值改下拉：后端有 L3 校验，让用户「选」而不是「猜着敲」
          //   （EntityConfig 里是 TextBox + Placeholder，因为库里没有对应字典）
          case 'AnchorType':
            return { ...f, type: 'select' as any, options: toOptions(ANCHOR_TYPES) }
          case 'AnchorKind':
            return { ...f, type: 'select' as any, options: toOptions(ANCHOR_KINDS) }
          case 'WriteMode':
            return { ...f, type: 'select' as any, options: toOptions(WRITE_MODES) }
          case 'ValueType':
            return { ...f, type: 'select' as any, options: toOptions(VALUE_TYPES) }
          // 页眉页脚：空串 = 不适用，必须是**可清空**的下拉
          case 'HeaderKind':
            return {
              ...f,
              type: 'select' as any,
              options: [{ label: '（不适用）', value: '' }, ...toOptions(HEADER_KINDS)],
              clearable: true,
            }
          // ★ IsValid 是 int（0/1）—— switch 必须显式声明 active/inactive value，
          //   否则写入布尔 true/false（DB 容得下，但语义上不该依赖隐式转换）
          case 'IsValid':
            return { ...f, type: 'switch' as any, fieldProps: { 'active-value': 1, 'inactive-value': 0 } }
          default:
            return f
        }
      })
  }

  // ========================================================
  // 对外：登记完模板后刷新树并选中新模板
  // ========================================================

  /** 登记模板成功 → 重载树（新模板要立刻出现在左树里） */
  async reloadTree(): Promise<void> {
    await this.loadTreeRoot()
    if (!this.templateNode && this.treeData.length > 0) {
      ElMessage.success('模板已登记，请在左侧展开并选择它')
    }
  }
}

export default DocFillRuleLogic
