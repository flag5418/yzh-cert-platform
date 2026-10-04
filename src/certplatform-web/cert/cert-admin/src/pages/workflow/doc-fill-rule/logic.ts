/**
 * ★ 标准文档填写规则 — 左树右表 Logic
 *
 * 【页面定位】
 *   在**标准资料清单**的文件上，挂「空白模板 + 填写锚点 + 全文提示词」。
 *   产出物 = `cert_doc_template_anchor` 规则行 —— 它是 `DocumentFillEngine` 的**唯一输入**。
 *
 * 【★★★ 左树 = 资料清单树（2026-10-04 用户裁定，重做）】
 *   用户原话：「我们所有的文档来源首先得有**标准文档资料清单**，我们是根据这个资料清单的文件，
 *   下载后进行空白文档设置，上传后，再定义规则的，**而不是想当然的空白的**」。
 *
 *   ⇒ 左树骨架必须是**资料清单**（机构 → 标准 → 阶段 → 文件夹 → 文件），
 *     ⛔ **不是** `cert_doc_template`（模板表）。
 *
 *   【为什么这是必须的 —— 实测数据】
 *   按模板表构树时，树里只有**已上传过模板的文件**：模板表 1 行 ⇒ 树几乎为空；
 *   而资料清单有 **168 份**标准文档。更要命的是「还没上传模板的文件在树上根本不存在」
 *   ⇒ 用户连「下载 → 加工 → 上传」的**入口都找不到**。
 *
 * 【数据源】
 *   `GET /DocTemplate/directory-tree` —— 后端复用 `StandardDirectoryService.GetOrganizationTreeAsync`
 *   + 同一份保留段过滤口径 ⇒ 与「标准资料清单」页（`directory-manager`）**同源同形**，
 *   差别只有：① 载荷 PascalCase；② 文件叶子多带空白模板状态。
 *
 * 【架构】
 *   - 左树：机构 → 标准 → 阶段 → 文件夹 → 文件（只读；文件来自资料清单，⛔ 不在树上新建）
 *   - 右栏：中预览 + 4 页签（锚点与字段规则 / 全文填写规则 / 文档契约 / 校验结果）
 *   - 列 / 表单 / 搜索由后端 EntityConfig 反射生成，页面**零手写 CRUD handler**
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
import { getDirectoryTree } from '@share/api/workflow/doc-fill-rule'

// ──── 左树：树行为配置 ────
const TREE_BEHAVIOR: TreeBehaviorConfig = {
  Lazy: false,
  AllowEdit: false,
  AllowAddChild: false,
  AllowDelete: false,
  AllowRename: false,
  // ★ 必须显式 false：否则 resolveTreeActions 会回落到 config.EnableField(IsValid)，
  //   在树节点上吐出「启用/禁用」按钮 —— 本页树是资料清单的**只读投影**，无启停语义
  AllowToggle: false,
  NameField: 'Name',
  CodeField: 'Code',
  ParentCodeField: 'ParentCode',
  // 右表 `cert_doc_template_anchor.TemplateCode` ↔ 文件叶子的 `Extra.templateCode` 关联
  // （⚠️ 关联值由 `relatedValue()` 覆写给出 —— 树节点的 `Code` 是**标准文件** Code，不是模板 Code）
  RelateField: 'TemplateCode',
  NoSelectionBehavior: 'empty',
  // ★ 五级：机构 → 标准 → 阶段 → 文件夹 → 文件（资料清单实测最深 6 层：文件夹可嵌套 2 层）
  MaxLevel: 6,
  AllowDeleteWithChildren: false,
}

/** 节点类型 → 图标（Element Plus 图标已在宿主 main.ts 全局注册） */
const NODE_ICON: Record<string, string> = {
  org: 'OfficeBuilding',
  standard: 'Collection',
  stage: 'Flag',
  folder: 'Folder',
  file: 'Document',
}

/** 只有「文件」叶子可操作 —— 机构/标准/阶段/文件夹只是导航 */
const FILE_KIND = 'file'

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

  /**
   * 当前选中的**文件叶子**（`Extra.kind === 'file'`）。
   *
   * ⚠️ 它是**资料清单里的文件行**（`cert_standard_directory_file`），不是模板行。
   * 该文件可能还没上传空白模板 —— 那时 `hasTemplate === false`。
   */
  get fileNode(): TreeNode | null {
    const node = this.selectedNode
    return node && node.Extra?.kind === FILE_KIND ? node : null
  }

  /** 右栏可操作的前置条件：选中了「文件」叶子 */
  get anySelected(): boolean {
    return !!this.fileNode
  }

  /**
   * ★★ 该文件是否**已上传空白模板** —— 页面操作条的分级依据。
   *
   * `false` 时只放行「下载标准文档 + 上传空白模板」；
   * 扫描 / 自动分析 / 校验 / 发布 全部禁用（没有模板就没有可扫可校验的对象）。
   */
  get hasTemplate(): boolean {
    return this.fileNode?.Extra?.hasTemplate === true
  }

  /** 空白模板 Code（= `cert_doc_template.Code`）；**未上传模板时为空串** */
  get templateCode(): string {
    return (this.fileNode?.Extra?.templateCode as string) ?? ''
  }

  /** 宿主标准文件 Code（= `cert_standard_directory_file.Code`）—— 下载原始文档 / 上传模板 / 读写契约都用它 */
  get standardFileCode(): string {
    return (this.fileNode?.Extra?.standardFileCode as string) ?? ''
  }

  /** 资料清单里**原始文档**的存储路径（「下载原始件」用） */
  get standardStoragePath(): string {
    return (this.fileNode?.Extra?.standardStoragePath as string) ?? ''
  }

  /**
   * ★ 归一产物路径（`.doc` → `editable/xxx.doc.docx`）——「下载可编辑版」的正确起点。
   *
   * 【为什么必须优先下载它】
   *   用户第 ② 步是「下载 → 本地加工成空白模板」。原始件里 **143/168 是 `.doc`、11 是 `.xls`**，
   *   而填写引擎只认 `.docx`/`.xlsx`；且 NPOI 2.7.2 **没有 `NPOI.HWPF`** ⇒ `.doc` 连读都不行。
   *   归一产物已经生成好了（`ConvertStatus=completed`），下载原始件等于让用户先自己转一次。
   */
  get standardEditablePath(): string {
    return (this.fileNode?.Extra?.standardEditablePath as string) ?? ''
  }

  /** 归一产物状态（`pending` / `processing` / `completed` / `failed` / `unsupported`） */
  get standardEditableStatus(): string {
    return (this.fileNode?.Extra?.standardEditableStatus as string) ?? ''
  }

  /**
   * 「可编辑版」是否可以下载。
   *
   * ⚠️ 判据 = **路径非空**，⛔ 不硬判 `status==='completed'` ——
   * 历史数据里 `EditableStatus` 可能是空串但路径已存在（回填过），
   * 此时能下就得让下（下载失败还有明确的错误出口），不该被一个状态列挡住。
   */
  get hasEditable(): boolean {
    return this.standardEditablePath.length > 0
  }

  /** 空白模板在 MinIO 的路径（`…/_template/xxx.docx`）；未上传时为空串 */
  get templateStoragePath(): string {
    return (this.fileNode?.Extra?.templateStoragePath as string) ?? ''
  }

  /**
   * 空白模板的**文件名**（含扩展名，如 `陪审人员.docx`）。
   *
   * ⚠️ 预览时必须与 `templateStoragePath` **成对**下发：
   *   `preview-by-path` 端点靠文件名判扩展名 —— 只给路径的话，
   *   `…/_template/xxx` 这种没有扩展名的路径会被判成「不支持在线预览」。
   *   （`…/editable/x.doc.docx` 这类双重扩展名恰好能蒙对，所以这个坑只在模板上暴露。）
   */
  get templateFileName(): string {
    return (this.fileNode?.Extra?.templateFileName as string) ?? ''
  }

  /**
   * ★★ 文件**真实文件名**（含扩展名，**不带**左树徽标）—— 下载 / 预览只许用它。
   *
   * 【为什么必须单独给一个 getter（2026-10-04 实测缺陷）】
   *   左树组件不支持自定义节点内容 ⇒ 徽标只能拼进 `Name`（见 `toCoreNode`）：
   *     `附录一 质量管理体系过程识别图.doc  ⬜未上传模板`
   *   而**文件名是下游的判据**：`DocPreview` 用 `name.split('.').pop()` 推扩展名、
   *   `preview-by-path` 也靠它判扩展名。
   *   用带徽标的 `Name` 当文件名 ⇒ 扩展名变成 `doc  ⬜未上传模板`
   *   ⇒ 不在 Office 白名单 ⇒ 中栏报「暂不支持在线预览 .doc ⬜未上传模板 格式」。
   *
   * ⚠️ 更阴的是**它看起来像间歇性故障**：契约接口回来后 `contract.FileName` 会覆盖成
   *   干净名字（标题栏正常了），但错误状态已经落定、没有任何东西触发重载 ⇒
   *   **标题对、内容错**。所以修完这个还得让 `PreviewPane` 把 `fileName` 纳入 key。
   */
  get fileName(): string {
    return (this.fileNode?.Extra?.rawName as string) || ''
  }

  /** 文件扩展名（小写无点）：`doc` / `docx` / `xls` / `xlsx` … */
  get fileType(): string {
    return (this.fileNode?.Extra?.fileType as string) ?? ''
  }

  /** 模板上挂的全文提示词编码（可能为空 —— 表示该模板只走锚点填充） */
  get promptCode(): string {
    return (this.fileNode?.Extra?.fillPromptCode as string) ?? ''
  }

  /** 文件所属机构（用于提示词「更具体优先」的选取） */
  get templateOrgCode(): string {
    return (this.fileNode?.Extra?.orgCode as string) ?? ''
  }

  /**
   * 该文件的**文档分类**（`editable` / `fixed`）——「这个文档是否不需要编辑」。
   *
   * ⚠️ 权威列在 `cert_standard_directory_file.DocCategory`，由后端 `directory-tree` 端点
   * 随 `Extra.docCategory` 一起下发（契约表同名列只是副本）。
   */
  get docCategory(): string {
    return (this.fileNode?.Extra?.docCategory as string) || 'editable'
  }

  /** 是否固定格式（免填）文档 —— 操作条与右栏据此换形态（37 号 §3.6） */
  get isFixedDoc(): boolean {
    return this.docCategory === 'fixed'
  }

  /** 模板扫描状态（`pending` / `processing` / `completed` / `failed`）；未上传时为空串 */
  get scanStatus(): string {
    return (this.fileNode?.Extra?.scanStatus as string) ?? ''
  }

  /** 模板发布状态（`draft` / `scanned` / `ready` / `published`）；未上传时为空串 */
  get publishStatus(): string {
    return (this.fileNode?.Extra?.publishStatus as string) ?? ''
  }

  /**
   * 左树**默认展开**的节点 key：机构 + 标准两级。
   *
   * 为什么不 `treeDefaultExpandAll`：资料清单实测 168 份文件 / 11 个文件夹，
   * 全展开会一次铺开近 200 个节点 —— 与「标准资料清单」页保持一致（只展开前两级），
   * 阶段及其以下由用户点击展开。
   */
  get defaultExpandedKeys(): string[] {
    const keys: string[] = []
    for (const org of this.treeData) {
      keys.push(String(org.Code))
      for (const std of org.Children ?? []) keys.push(String(std.Code))
    }
    return keys
  }

  constructor() {
    super()
    // 新增锚点前必须选中**已上传模板**的文件 —— 否则 `TemplateCode` 无处可注入
    this.registerHandler('add', () => {
      if (!this.anySelected) {
        ElMessage.warning('请先在左侧选择一个文件')
        return
      }
      if (!this.hasTemplate) {
        ElMessage.warning('该文件还没有空白模板，请先「下载可编辑版」→ 加工 → 「上传空白模板」')
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
  // 覆盖点②：树加载（树**不是**本实体树 —— 数据源是标准资料清单）
  // ========================================================

  override async loadTreeRoot(): Promise<void> {
    this.treeSide.treeLoading.value = true
    try {
      const res = await getDirectoryTree()
      expectOk(res, '加载标准资料清单失败')
      const nodes = res.data?.Nodes ?? []
      this.treeSide.setNodes(nodes.map((n) => this.toCoreNode(n)))
    } finally {
      this.treeSide.treeLoading.value = false
    }
  }

  /**
   * 后端 `TemplateDirectoryNode` → 内核 `TreeNode`。
   *
   * 字段一律 **PascalCase**（守卫 R2）。后端已按同形返回，这里只补 `Icon`
   * 并把 `Extra` 原样透传（`kind` 是判据，其余给页面用）。
   */
  private toCoreNode(n: any): TreeNode {
    const children = (n.Children ?? []).map((c: any) => this.toCoreNode(c))
    const kind = n.Extra?.kind ?? ''
    return {
      Code: String(n.Code),
      // ★ 文件叶子把「模板状态」拼进 Name —— 左树组件不支持自定义节点内容，
      //   这是唯一能在树上显示徽标的位置（37 号 §3.6「左树加分类徽标」）。
      //   ⚠️ 正因如此，**Name 不再是纯文件名** ⇒ 任何「当文件名用」的地方
      //     一律走 `logic.fileName`（读 `Extra.rawName`），⛔ 不要用 `Name`。
      Name: kind === FILE_KIND ? `${n.Name}${this.badgeOf(n.Extra)}` : n.Name,
      NodeType: kind,
      IsLeaf: children.length === 0,
      // `rawName` = 后端下发的**未加徽标**原名（`StandardDirectoryService.BuildTemplateFileNode`
      // 里 `Name = f.FileName`）—— 供下载文件名 / 预览判扩展名使用。
      Extra: { ...(n.Extra ?? {}), rawName: n.Name, Icon: NODE_ICON[kind] },
      Children: children,
    }
  }

  /**
   * 文件叶子的徽标后缀。
   *
   * - **未上传模板**：`⬜未上传模板`（页面会引导「下载 → 加工 → 上传」）
   * - **已上传**：`📝可编辑 / 📎免填 · 发布状态 · N 锚点`
   *   - `📝` 可编辑（要配填写规则）｜`📎` 固定格式（免填，只配指纹）
   *   - 发布状态：`draft` 草稿 / `scanned` 已扫描 / `ready` 可发布 / `published` 已发布
   */
  private badgeOf(extra: any): string {
    if (extra?.hasTemplate !== true) return '  ⬜未上传模板'

    const parts: string[] = []
    parts.push(extra?.docCategory === 'fixed' ? '📎免填' : '📝可编辑')

    const scan = String(extra?.scanStatus ?? '')
    const pub = String(extra?.publishStatus ?? '')
    if (pub === 'published') parts.push('🟢已发布')
    else if (pub === 'ready') parts.push('🔵可发布')
    else if (scan === 'failed') parts.push('🔴扫描失败')
    else if (scan === 'completed') parts.push('⚪已扫描')
    else parts.push('⚪未扫描')

    const anchors = Number(extra?.anchorCount ?? 0)
    if (anchors > 0) parts.push(`${anchors} 锚点`)

    return `  ${parts.join(' · ')}`
  }

  // ========================================================
  // 树 → 表格联动过滤
  // ========================================================

  /**
   * 只有**已上传模板**的文件才去查锚点。
   *
   * ⛔ 不能用基类的「选中即过滤」：资料清单里的文件多数还没上传模板，
   * 那时 `TemplateCode` 为空 —— 若照常发请求，`eq ''` 会命中一批脏数据，
   * 或（若跳过过滤）拉回**全库锚点**。两种情况都是错的，正确行为是**不发请求、表格留空**。
   * （基类 `dataLoader` 在 `shouldApplyTreeFilter()===false && NoSelectionBehavior==='empty'`
   * 时直接返回空集，不再发请求 —— 见 `TreeTableCore.dataLoader`。）
   */
  protected override shouldApplyTreeFilter(): boolean {
    return this.hasTemplate
  }

  /**
   * 关联值 = **模板** Code。
   *
   * ⚠️ 必须覆写：树节点的 `Code` 是**标准文件** Code（`cert_standard_directory_file.Code`），
   * 而锚点表存的是**模板** Code（`cert_doc_template.Code`）—— 两者是不同实体。
   * 用基类默认实现（返回 `node.Code`）会得到**恒空**的右表且不报错。
   */
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
          // SourceSummary 是恒空的视图列（后端从不写它）—— 来源摘要改由前端按
          // SourceSpec 现算（见 AnchorRuleTab 的 #column-SourceSpec 插槽），
          // 所以这里保留 SourceSpec、丢掉 SourceSummary。
          !['SourceSummary'].includes(c.prop),
      )
      .map((c: any) =>
        c.prop === 'IsValid'
          ? { ...c, tagMap: { 1: '启用', 0: '禁用' }, tagTypeMap: { 1: 'success', 0: 'info' } }
          : c,
      )
  }

  // ========================================================
  // 公开：取该模板的**全量锚点**（不分页）
  // ========================================================

  /**
   * 取当前模板的全部锚点行。
   *
   * 【为什么需要它 —— 而不是让 `YzhTable` 自己分页取】
   *   Tab1 顶部要显示统计（「本模板 42 个锚点：31 自动 / 9 人工 / 2 计算」），
   *   快速筛选（只看未配来源 / 只看孤儿）也必须作用在**全量**上。
   *   若只看当前页，统计和筛选都是错的 —— 而「错的统计」比「没有统计」更糟。
   *
   * 【规模假设】单个模板的锚点数量在**几十**量级（一个 Word 模板的可填位置有限），
   *   一次性取回不构成压力；⛔ 不做服务端分页统计。
   */
  async loadAnchors(): Promise<any[]> {
    if (!this.templateCode) return []
    const res = await this.apiPost<ApiResponse<{ Items: any[]; TotalCount: number }>>('/filter', {
      Page: 1,
      PageSize: 1000,
      Filters: [{ Field: 'TemplateCode', Operator: 'eq', Value: this.templateCode }],
    })
    expectOk(res, '加载锚点清单失败')
    return res.data?.Items ?? []
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
  // 对外：上传模板 / 扫描 / 保存锚点后刷新树徽标
  // ========================================================

  /**
   * 重载左树。
   *
   * 【为什么不保留选中】树是**资料清单**（数据源与模板无关），重载不会改变文件集合；
   * 但 `setNodes` 会重建节点对象 ⇒ `selectedNode` 指向旧对象，右栏会失联。
   * 这里按 `Code` 找回并重新选中，保证「上传模板后树徽标刷新，但右栏不跳走」。
   */
  async reloadTree(): Promise<void> {
    const prevCode = this.fileNode ? String(this.fileNode.Code) : ''
    await this.loadTreeRoot()
    if (!prevCode) return

    const found = this.findByCode(this.treeData, prevCode)
    if (found) {
      this.selectedNode = found
    }
  }

  /** 在树里按 Code 深度优先查找（`loadTreeRoot` 会重建节点对象，必须重新定位） */
  private findByCode(nodes: TreeNode[], code: string): TreeNode | null {
    for (const n of nodes) {
      if (String(n.Code) === code) return n
      const hit = this.findByCode(n.Children ?? [], code)
      if (hit) return hit
    }
    return null
  }
}

export default DocFillRuleLogic
