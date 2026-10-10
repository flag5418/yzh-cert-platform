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
  getDirectoryTree,
  getDocFillPreviewInfo,
  listFillParamDefs,
  saveAnchorBatch,
} from '@share/api/workflow/doc-fill-rule'
import {
  TreeTableLogic,
  expectOk,
  unwrapOk,
  type ApiResponse,
  type EntityConfigDto,
  type FilterItem,
  type TreeBehaviorConfig,
  type TreeNode,
  type YzhFormField,
  type YzhTableColumn,
} from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, ref } from 'vue'
import {
  buildAnchorViews,
  computeAnchorStats,
  type AnchorStats,
  type AnchorView,
} from './components/anchorStats'
import { isAiKind } from './components/sourceSpec'

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

/**
 * ★ 扫描器 / 填写引擎都**解析不了**的扩展名（C3 类型自动判定）。
 *
 * ⛔ 用「**否定清单**」而不是「肯定清单」：资料清单里存在 `txt`（实测 1 份）
 *   以及未来可能出现的未知扩展名。肯定清单会把它们一并判成「不可解析」⇒
 *   把一份本来能配规则的文档**锁死成固定文档**（用户无从解锁）。
 *   否定清单只会锁住「确知解析不了」的那几种，未知扩展名按「可解析」放行。
 */
const UNPARSEABLE_TYPES = [
  'pdf',
  'jpg',
  'jpeg',
  'png',
  'tif',
  'tiff',
  'bmp',
  'gif',
  'webp',
  'heic',
]

/** 某扩展名是否不可解析（纯函数，`missingItems` 与 getter 共用同一口径） */
function isUnparseableType(fileType: unknown): boolean {
  const t = String(fileType ?? '').toLowerCase().replace(/^\./, '')
  return t !== '' && UNPARSEABLE_TYPES.includes(t)
}

/**
 * ★ 按「文件是否保留」裁剪树（左栏「类型 / 状态」筛选用）。
 *
 * 规则：
 * - **文件叶子**：`keepFile` 为假 ⇒ 丢弃；
 * - **非文件节点**（机构/标准/阶段/文件夹）：自身没有可筛属性，**只裁剪子树**；
 *   子树被裁空 ⇒ **该节点一并丢弃**，否则筛选后树上会留下一串点不开的空壳分支。
 *
 * ⛔ 必须**重建对象**（`{ ...n, Children: kids }`），⛔ 不能就地改 `Children`：
 *   树数据是 `logic.treeData` 的引用，就地改会污染源数据 —— 清空筛选后回不来。
 * ⛔ 也**不能**只写 `{ ...n }`：展开运算符会把**未裁剪的原始 Children** 一起带回来，
 *   表现为「筛选状态=已完成 → 161 份；再叠加筛选类型 → 还是 161 份」（筛选失效）。
 */
export function pruneTree(
  nodes: TreeNode[],
  keepFile: (node: TreeNode) => boolean,
): TreeNode[] {
  const out: TreeNode[] = []
  for (const n of nodes) {
    const kids = n.Children ?? []
    const kept = pruneTree(kids, keepFile)
    if (n.NodeType === FILE_KIND) {
      if (keepFile(n)) out.push({ ...n, Children: [] })
      continue
    }
    // 原本就没有子节点的导航节点**保留** —— 否则「无筛选」时视图也会变（空节点被抹掉）
    if (kept.length || kids.length === 0) out.push({ ...n, Children: kept })
  }
  return out
}

/**
 * 文档的**设置状态**三态（原型 V6 §②）。
 *
 * ⛔ 不落库：`cert_standard_doc_contract.Status` 会被别处改掉而无人回退 ⇒ 必然漂移。
 * ✅ 实时算（见 `calcSetupStatus` / `missingItems`）。
 */
export type DocSetupStatus = 'draft' | 'setting' | 'done'

// ──── L3 受控值（与后端 `DocTemplateAnchorController` 的静态集合逐字一致）────
//
// ⛔ 改这里必须同步改后端：后端才是权威校验（前端下拉只是防手滑）。
const ANCHOR_TYPES = ['scalar', 'block', 'table', 'table_total', 'domain']
const ANCHOR_KINDS = ['token', 'bookmark', 'range']
const WRITE_MODES = ['replace', 'overwrite', 'append', 'remove']
const VALUE_TYPES = ['text', 'number', 'date', 'bool', 'enum']
const HEADER_KINDS = ['default', 'first', 'even']

/** 受控值 → `{ label, value }`（空选项由调用方决定是否加） */
const toOptions = (values: string[]) =>
  values.map((v) => ({ label: v, value: v }))

export class DocFillRuleLogic extends TreeTableLogic<any> {
  /** 后端锚点控制器路由（★ 与目录同名同路径） */
  controllerName = 'Admin/Workflow/DocTemplateAnchor'

  /**
   * 当前选中的**文件叶子**（支持 `Extra.kind` 或业务树的 `Type`）。
   *
   * ⚠️ 它是**资料清单里的文件行**（`cert_standard_directory_file`），不是模板行。
   * 该文件可能还没上传空白模板 —— 那时 `hasTemplate === false`。
   */
  get fileNode(): any {
    const node = this.selectedNode as any
    const isFile =
      node?.Type === FILE_KIND ||
      node?.NodeType === FILE_KIND ||
      node?.Extra?.kind === FILE_KIND
    return isFile ? node : null
  }

  /** 右栏可操作的前置条件：选中了「文件」叶子 */
  get anySelected(): boolean {
    return !!this.fileNode
  }

  /**
   * 左树加载态（`v-loading` 用）。
   *
   * ⚠️ 基类的 `treeLoading` getter 返回的是 **Ref 对象**（恒真）——
   *    模板里直接 `v-loading="logic.treeLoading"` 会让遮罩**常驻**，必须取 `.value`。
   */
  get isTreeLoading(): boolean {
    return this.treeSide.treeLoading.value
  }

  /** 刷新左树（带 loading 态，供刷新按钮绑定） */
  async refreshTree(): Promise<void> {
    this.treeSide.treeLoading.value = true
    try {
      await this.reloadTree()
    } finally {
      this.treeSide.treeLoading.value = false
    }
  }

  // ========================================================
  // ★★ 锚点清单的**唯一共享仓库**（2026-10-05 第 28 轮新增）
  // ========================================================
  //
  // 【为什么从 `AnchorRuleTab` 上移到 logic】
  //   C 组把右栏改成 2 Tab 后，「锚点是否配齐」（C8 闸）与「有没有 ai 节点」（C11）
  //   都成了**页面级**判据：底部保存条、Tab 徽标、中栏都要读它。
  //   若仍留在 `AnchorRuleTab` 内部，则：
  //     ① 默认落「全局规则」Tab 时 `AnchorRuleTab` **未挂载** ⇒ 读不到任何锚点；
  //     ② 父页只能通过函数 ref 去掏子组件内部状态 —— 这正是 `PromptPanel` 注释里
  //        记过的坑（`v-if` 互斥导致 ref 恒为 `null`）。
  //   ⇒ 上移到 logic：一处加载，多处读取，口径唯一。

  /** 锚点原始行（PascalCase 整行；`save-batch` 必须整行提交） */
  private readonly anchorRowsRef = ref<any[]>([])
  /** 锚点清单加载态 */
  private readonly anchorLoadingRef = ref(false)

  // ──── ★ 2026-10-10：「来源已失效」判据所需的两块状态 ────
  //
  //  【为什么必须在 logic 里持有一份】
  //    `AnchorSidePanel` / `MaterialArea` 各自 `listFillParamDefs()` 拉过一遍，
  //    但那是**组件内部**状态，页面级的「已配齐」判定读不到。
  //    而「来源已失效」是**跨行聚合**结论（统计条 / 筛选 chip / C8 闸都要用）
  //    ⇒ 上移到 logic：一处加载，多处读取，口径唯一。
  //
  //  ⚠️ `ready` 与「清单为空」是**两件事**：拉失败时清单也是空的，
  //     但那时**不能**判失效（会把所有真实参数标红）⇒ 必须靠 `ready` 区分。

  /** `cert_fill_param_def` 未软删的 `ParamCode` */
  private readonly fillParamCodesRef = ref<string[]>([])
  /** 参数目录是否**已成功**加载（false ⇒ 不判「失效」，⛔ 不报假警） */
  private readonly fillParamReadyRef = ref(false)

  /**
   * 幂等拉取参数目录 —— 判「来源已失效」的**唯一数据源**。
   *
   * ⛔ 失败时**保持 `ready=false`**：宁可少报，不可把每一个真实参数都标成失效。
   */
  async ensureFillParamCatalog(): Promise<void> {
    if (this.fillParamReadyRef.value) return
    try {
      const res = await listFillParamDefs()
      const items: any[] = (res as any)?.data?.Items ?? []
      this.fillParamCodesRef.value = items
        .map((p: any) => String(p?.ParamCode ?? p?.Code ?? ''))
        .filter((c: string) => !!c)
      this.fillParamReadyRef.value = true
    } catch {
      // 静默保持未就绪 —— 界面照常可用，只是不显示「来源已失效」
    }
  }

  /** 解析后视图（`SourceSpec` 一行只解析一次 —— 口径在 `anchorStats`） */
  private readonly anchorViewsComputed = computed<AnchorView[]>(() =>
    buildAnchorViews(this.anchorRowsRef.value, {
      paramCodes: this.fillParamCodesRef.value,
      ready: this.fillParamReadyRef.value,
    }),
  )
  private readonly anchorStatsComputed = computed<AnchorStats>(() =>
    computeAnchorStats(this.anchorViewsComputed.value),
  )

  get anchorRows(): any[] {
    return this.anchorRowsRef.value
  }
  set anchorRows(rows: any[]) {
    this.anchorRowsRef.value = rows
  }
  get anchorViews(): AnchorView[] {
    return this.anchorViewsComputed.value
  }
  get anchorStats(): AnchorStats {
    return this.anchorStatsComputed.value
  }
  get isAnchorLoading(): boolean {
    return this.anchorLoadingRef.value
  }

  /**
   * ★ C8 闸：锚点是否**配齐**（配齐才谈得上自动填充 / 预览）。
   *
   * 判据五条**全部**成立：① 已上传模板 ② 扫描完成 ③ 至少 1 个锚点
   * ④ 无未配来源、无孤儿 ⑤ **无「来源已失效」**。
   * ⛔ 「未配」/「失效」的判据不在本类复写 —— 唯一口径在 `anchorStats.toAnchorView`
   *    （域自动值 `domain + auto` 不算未配）。
   *
   * ★ 2026-10-10 新增第 ⑤ 条：`ref` 指向的参数被软删（或属性名写错）时，
   *   **真填必然取不到值**，与「未配来源」在运行期后果完全一样 ⇒ 同样必须挡住。
   *   ⚠️ 该条只在参数目录**已加载成功**时才可能为真（`anchorStats` 的 `catalog.ready`），
   *   目录拉失败时 `staleRef` 恒 0 ⇒ 闸门行为与本条引入前一致（⛔ 不会把用户锁死）。
   */
  get anchorReadiness(): {
    total: number
    unconfigured: number
    staleRef: number
    orphan: number
    ready: boolean
  } {
    const s = this.anchorStats
    return {
      total: s.total,
      unconfigured: s.unconfigured,
      staleRef: s.staleRef,
      orphan: s.orphan,
      ready:
        this.hasTemplate &&
        this.scanStatus === 'completed' &&
        s.total > 0 &&
        s.unconfigured === 0 &&
        s.staleRef === 0 &&
        s.orphan === 0,
    }
  }

  /**
   * ★ C11：本模板是否存在 **AI 节点**（来源是 `ai_semantic` / `ai_field` / `ai_table` 之一）。
   *
   * 存在才显示「全局填写规则」块 —— 没有 AI 节点时，整块与用户无关。
   *
   * ⚠️ 2026-10-09：来源模型改为「一个锚点 = 一个来源」后，判据从
   *   `sources.some(kind === 'ai')` 改为**单个来源**判 `isAiKind()`；
   *   同时把旧值 `ai` 也算进来 —— 库里可能还留着 `22` 号时代的老数据。
   */
  get hasAiNode(): boolean {
    return this.anchorViews.some((v) => {
      const k = v.model.source?.kind
      return k === 'ai' || isAiKind(String(k ?? ''))
    })
  }

  /**
   * 已加载锚点清单对应的模板 Code（空串 = 尚无有效加载）。
   *
   * ★ 存在的意义 = 让「父页加载 + 子组件兜底」**不会变成两次请求**（见 `ensureAnchors()`）。
   * ⛔ 只有**成功**才记 —— 失败不记，下次兜底才会重试。
   */
  private anchorLoadedFor = ''

  /**
   * 重新加载锚点清单（写进共享仓库）—— **强制重拉**。
   *
   * 调用时机 = 「数据确实变了」：点树节点 / 扫描完成 / 保存锚点之后。
   * ⛔ 只想「确保有数据」的场景（组件挂载兜底）请用 `ensureAnchors()`。
   *
   * ⛔ 未上传模板时**不发请求**（与 `shouldApplyTreeFilter` 同一口径）：
   *   那时 `TemplateCode` 为空，`eq ''` 会命中一批脏数据。
   */
  async reloadAnchors(): Promise<void> {
    const tpl = this.templateCode
    if (!tpl) {
      this.anchorRowsRef.value = []
      this.anchorLoadedFor = ''
      return
    }
    this.anchorLoadingRef.value = true
    try {
      // ★ 参数目录与锚点清单**并行**拉（幂等）：判「来源已失效」要用它。
      //   ⛔ 不 fire-and-forget —— 那会让首屏先渲染成「全部正常」再跳成「失效 N」，
      //   用户看到一次闪烁，且那一瞬间的统计条是**错的**（错的统计比没有统计更糟）。
      const [rows] = await Promise.all([
        this.loadAnchors(),
        this.ensureFillParamCatalog(),
      ])
      this.anchorRowsRef.value = rows
      this.anchorLoadedFor = tpl
    } catch {
      // 锚点取不到只影响右栏清单与闸门判据，⛔ 不该把整页打挂
      this.anchorRowsRef.value = []
      this.anchorLoadedFor = ''
    } finally {
      this.anchorLoadingRef.value = false
    }
  }

  /**
   * ★ 确保锚点清单已加载（**幂等**）—— 给「只读它、不该负责拉它」的组件用。
   *
   * 【为什么需要它（2026-10-06 代码评审 #2）】
   *   `PromptPanel` 原先自持 `anchors` ref，并在 `onMounted` 与
   *   `watch(templateCode…)` 里各拉一次；而父页 `handleNodeClick` **已经**调过
   *   `reloadAnchors()` ⇒ 同一次点树对 `DocTemplateAnchor/filter` 发**两次**，
   *   且两份数据在请求返回前**可能短暂不一致**（素材区还显示旧锚点，底部闸门已按新锚点算）。
   *
   *   ⇒ 改成「读共享仓库 + 挂载兜底」后，若兜底也用 `reloadAnchors()`，
   *     只是把重复请求**换了个地方**（且「该模板本来就没锚点」时每次挂载都会重拉）。
   *   ⇒ 用 `anchorLoadedFor` 标记去重：**同一模板已加载过就不再请求**。
   *
   * 与 `reloadAnchors()` 的分工：
   *   · `reloadAnchors()` = 强制重拉（数据确实变了）
   *   · `ensureAnchors()` = 没拉过才拉（兜底）
   */
  async ensureAnchors(): Promise<void> {
    const tpl = this.templateCode
    if (!tpl) return
    if (this.anchorLoadedFor === tpl) return
    await this.reloadAnchors()
  }

  /**
   * 获取节点上的业务属性（兼容业务树的 Raw 和 框架树的 Extra）。
   */
  get nodeExtra(): any {
    return this.fileNode?.Extra || this.fileNode?.Raw || {}
  }

  /**
   * ★★ 该文件是否**已上传空白模板** —— 页面操作条的分级依据。
   */
  get hasTemplate(): boolean {
    const ex = this.nodeExtra
    return ex.hasTemplate === true || ex.HasTemplate === true
  }

  /** 空白模板 Code（= `cert_doc_template.Code`）；**未上传模板时为空串** */
  get templateCode(): string {
    const ex = this.nodeExtra
    return (ex.templateCode || ex.TemplateCode || '') as string
  }

  /** 宿主标准文件 Code（= `cert_standard_directory_file.Code`） */
  get standardFileCode(): string {
    const node = this.fileNode
    const ex = this.nodeExtra
    return (node?.FileCode ||
      ex.standardFileCode ||
      ex.FileCode ||
      '') as string
  }

  /** 资料清单里**原始文档**的存储路径（「下载原始件」用） */
  get standardStoragePath(): string {
    const ex = this.nodeExtra
    return (ex.standardStoragePath || ex.StoragePath || '') as string
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
    const ex = this.nodeExtra
    return (ex.standardEditablePath || ex.StandardEditablePath || '') as string
  }

  /** 归一产物状态（`pending` / `processing` / `completed` / `failed` / `unsupported`） */
  get standardEditableStatus(): string {
    const ex = this.nodeExtra
    return (ex.standardEditableStatus || ex.ConvertStatus || '') as string
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
    const ex = this.nodeExtra
    return (ex.templateStoragePath || ex.TemplateStoragePath || '') as string
  }

  /**
   * 空白模板的**文件名**（含扩展名，如 `陪审人员.docx`）。
   */
  get templateFileName(): string {
    const ex = this.nodeExtra
    return (ex.templateFileName || ex.TemplateFileName || '') as string
  }

  // ════════════════════════════════════════════════════════════════════
  //  ★ 试填预览（`52` B7 + D1，2026-10-06）
  //    中栏「填充后预览」视图的路径来源。⛔ 不在树节点上带（见 `reloadPreviewInfo` 注释）。
  // ════════════════════════════════════════════════════════════════════

  private readonly previewPdfPathRef = ref('')
  private readonly previewTimeRef = ref('')

  /** 试填预览 PDF 路径（`…/_preview/x.docx.pdf`）；**空串 = 从未试填过** */
  get previewPdfPath(): string {
    return this.previewPdfPathRef.value
  }
  set previewPdfPath(v: string) {
    this.previewPdfPathRef.value = v ?? ''
  }

  /** 最近一次试填时间（ISO 字符串；空串 = 从未试填过） */
  get previewTime(): string {
    return this.previewTimeRef.value
  }
  set previewTime(v: string) {
    this.previewTimeRef.value = v ?? ''
  }

  /** 已查询过试填状态的模板 Code（去重，口径与 `anchorLoadedFor` 一致） */
  private previewLoadedFor = ''

  /**
   * ★ 读「这个模板试填过没有」（幂等）。
   *
   * 【为什么单独一次请求，而不是塞进左树节点】
   *   `directory-tree` 的文件叶子已带模板元信息。若把 `PreviewPdfPath` 也塞进去，
   *   每次**试填**都要让整棵树失效重取（`reloadTree` 是重请求），而试填是高频动作
   *   ⇒ 单文件粒度查询把刷新限制在**被试填的那一个文件**上。
   *
   * 【为什么按模板 Code 去重】
   *   `handleNodeClick` 会在每次点树时调它。同一次点树可能因筛选/重渲染被调多次，
   *   而 `templateCode` 相同 ⇒ 结果必然相同 ⇒ 第二次起直接跳过。
   *   ⚠️ 点**同一个文件**后重跑试填的情况由调用方**显式赋值**（`previewPdfPath = …`），
   *      ⛔ 不走这里 —— 否则会被「已加载过」挡掉。
   */
  async reloadPreviewInfo(force = false): Promise<void> {
    const tpl = this.templateCode
    if (!tpl) {
      this.previewPdfPathRef.value = ''
      this.previewTimeRef.value = ''
      this.previewLoadedFor = ''
      return
    }
    if (!force && this.previewLoadedFor === tpl) return

    try {
      const res = await getDocFillPreviewInfo(tpl)
      const d = unwrapOk(res)
      this.previewPdfPathRef.value = d?.PreviewPdfPath || ''
      this.previewTimeRef.value = d?.PreviewTime || ''
      this.previewLoadedFor = tpl
    } catch {
      // 试填状态取不到**只影响中栏第三个视图**（禁用即可），⛔ 不该把整页打挂。
      // ⛔ 不记 `previewLoadedFor` —— 失败要允许下次重试（与 `reloadAnchors` 同口径）。
      this.previewPdfPathRef.value = ''
      this.previewTimeRef.value = ''
      this.previewLoadedFor = ''
    }
  }

  /** 试填产物就绪时，把新状态写进仓库（避免再打一次接口） */
  applyPreviewResult(pdfPath: string, previewTime: string): void {
    this.previewPdfPathRef.value = pdfPath || ''
    this.previewTimeRef.value = previewTime || ''
    this.previewLoadedFor = this.templateCode
  }

  /**
   * ★★ 文件**真实文件名**（含扩展名，**不带**左树徽标）—— 下载 / 预览只许用它。
   */
  get fileName(): string {
    const ex = this.nodeExtra
    return (ex.rawName || ex.FileName || '') as string
  }

  /** 文件扩展名（小写无点）：`doc` / `docx` / `xls` / `xlsx` … */
  get fileType(): string {
    const ex = this.nodeExtra
    return (ex.fileType || ex.FileType || '') as string
  }

  /** 模板上挂的全文提示词编码（可能为空 —— 表示该模板只走锚点填充） */
  get promptCode(): string {
    const ex = this.nodeExtra
    return (ex.fillPromptCode || ex.FillPromptCode || '') as string
  }

  /** 文件所属机构（用于提示词「更具体优先」的选取） */
  get templateOrgCode(): string {
    const ex = this.nodeExtra
    return (ex.orgCode || ex.OrgCode || '') as string
  }

  /**
   * 该文件的**文档分类**（`editable` / `fixed`）——「这个文档是否不需要编辑」。
   *
   * ⚠️ 这是**库里存的值**（`cert_standard_directory_file.DocCategory`）。
   *   界面判定请用 `effectiveDocCategory` —— 不可解析的文件会被强制成 `fixed`。
   */
  get docCategory(): string {
    const ex = this.nodeExtra
    return (ex.docCategory || ex.DocCategory || 'editable') as string
  }

  /**
   * ★ C3：这份文件**能不能被解析**（决定「文档类型」是否允许人工切换）。
   *
   * 图片 / PDF 扫描件没有可编辑内容 ⇒ 只能是固定文档，类型开关**置灰锁定**。
   * 判据 = 扩展名在 `UNPARSEABLE_TYPES` 里（见顶部注释：否定清单，未知扩展名放行）。
   */
  get isParseable(): boolean {
    return !isUnparseableType(this.fileType)
  }

  /** 文档类型是否被**锁死**（不可解析 ⇒ 只能是固定文档，⛔ 不允许人工改成可编辑） */
  get docTypeLocked(): boolean {
    return !this.isParseable
  }

  /**
   * ★ 界面/规则判定用的**生效**文档分类。
   *
   * 不可解析 ⇒ 恒 `fixed`（覆盖库里可能残留的 `editable`）—— 否则一份 PDF
   * 会带着「可编辑」类型进入锚点配置流程，而它根本没有锚点可扫。
   */
  get effectiveDocCategory(): string {
    return this.docTypeLocked ? 'fixed' : this.docCategory
  }

  /** 是否固定格式（免填）文档 —— 操作条与右栏据此换形态（37 号 §3.6） */
  get isFixedDoc(): boolean {
    return this.effectiveDocCategory === 'fixed'
  }

  /**
   * ★ 完成度（2026-10-04 原型 V6 口径）
   *
   * ⛔ 状态是「算出来的」不是「存出来的」：
   * 删锚点 / 换空白模板 / 解绑提示词 都会让它自动回退，永不漂移。
   *
   * ★★ 分母由 `requiredItems()` 给出（**与 `missingItems()` 同一组条件**）。
   *   ⛔ 不要写死 `isFixedDoc ? 4 : 3`：必配项本身是**条件化**的 ——
   *     不可解析（图片 / PDF）不需要「类型确认」；未上传模板的文档没有「锚点扫描」。
   *     写死分母会让这些文档**永远差 1 项**（`done` 少算、进度条永远不满）。
   */
  get completion() {
    if (!this.anySelected) return { done: 0, total: 0, miss: [] as string[] }
    const ex = this.nodeExtra
    const total = this.requiredItems(ex).length
    const miss = this.missingItems(ex)
    return { done: Math.max(0, total - miss.length), total, miss }
  }

  /**
   * ★ 提示词引用锚点校验（2026-10-04）
   *
   * 扫描 `prompt` 里的 `{{__FILL__.AnchorRef}}`，检查这些锚点是否在本模板中存在。
   * 返回非法引用列表（不存在的 AnchorRef）。
   */
  validatePromptAnchors(prompt: string, allAnchors: any[]): string[] {
    if (!prompt) return []
    const regex = /\{\{__FILL__\.([^}]+)\}\}/g
    const matches = prompt.matchAll(regex)
    const invalid: string[] = []
    const existingRefs = new Set(allAnchors.map((a) => a.AnchorRef))

    for (const match of matches) {
      const ref = match[1]
      if (!existingRefs.has(ref)) {
        invalid.push(ref)
      }
    }
    return [...new Set(invalid)]
  }

  /** 建议项（不影响状态，只提示） */
  get advisoryItems(): string[] {
    if (!this.anySelected) return []
    const f = this.nodeExtra
    const adv: string[] = []

    if (!(f.fillPromptCode || f.FillPromptCode)) {
      adv.push(this.isFixedDoc ? '未写上传要求' : '全文规则未挂接')
    }
    if (!(f.docFillHint || f.DocFillHint)?.trim()) adv.push('填写规则为空')

    return adv
  }

  /** 模板扫描状态（`pending` / `processing` / `completed` / `failed` / `unsupported`）；未上传时为空串 */
  get scanStatus(): string {
    const ex = this.nodeExtra
    return (ex.scanStatus || ex.ScanStatus || '') as string
  }

  /** 模板发布状态（`draft` / `scanned` / `ready` / `published`）；未上传时为空串 */
  get publishStatus(): string {
    const ex = this.nodeExtra
    return (ex.publishStatus || ex.PublishStatus || '') as string
  }

  /**
   * 左树**默认展开**的节点 key：机构 + 标准 + 阶段三级。
   *
   * 为什么不 `treeDefaultExpandAll`：资料清单实测 **168 份文件 / 11 个文件夹**，
   * 全展开会一次铺开近 200 行，把左栏变成清单而不是导航。
   * 展开到阶段层 = 8 行，用户点阶段才看到「文件夹 → 文件」两级。
   */
  get defaultExpandedKeys(): string[] {
    const keys: string[] = []
    for (const org of this.treeData) {
      keys.push(String(org.Code))
      for (const std of org.Children ?? []) {
        keys.push(String(std.Code))
        for (const stage of std.Children ?? []) keys.push(String(stage.Code))
      }
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
        ElMessage.warning(
          '该文件还没有空白模板，请先「下载可编辑版」→ 加工 → 「上传空白模板」',
        )
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
      // ★ Name 保持**纯文件名/纯层级名**（原型 V5 起不再把状态拼进名称）——
      //   文件状态由左栏筛选 + 右栏 `YzhStatusBadge` 表达，⛔ 不再 emoji 拼串。
      Name: n.Name,
      NodeType: kind,
      IsLeaf: children.length === 0,
      // `rawName` = 后端下发的原名（供下载文件名 / 预览判扩展名使用）
      Extra: { ...(n.Extra ?? {}), rawName: n.Name, Icon: NODE_ICON[kind] },
      Children: children,
    }
  }

  /**
   * 当前选中文件的**祖先路径**（机构 → 标准 → 阶段 → 文件夹）。
   *
   * ⚠️ 后端 `Extra` **不下发** orgName / standardName / stageName ⇒ 前端从树上回溯，
   *    ⛔ 不要臆造字段名（读了永远是 undefined，面包屑就整条空掉）。
   */
  get breadcrumb(): string[] {
    const code = this.fileNode?.Code
    if (code == null) return []
    const path = this.findPath(this.treeData, String(code))
    return path.slice(0, -1).map((n) => n.Name)
  }

  /** 深度优先找出从根到目标节点的路径（含自身） */
  private findPath(nodes: TreeNode[], code: string, trail: TreeNode[] = []): TreeNode[] {
    for (const n of nodes) {
      const next = [...trail, n]
      if (String(n.Code) === code) return next
      const hit = this.findPath(n.Children ?? [], code, next)
      if (hit.length) return hit
    }
    return []
  }

  /** 文件叶子的**设置状态**（三态，实时算不落库 —— 口径唯一在这里） */
  statusOf(node: any): DocSetupStatus {
    const ex = node?.Extra ?? node?.Raw ?? {}
    return this.calcSetupStatus(ex)
  }

  /** 当前选中文件的设置状态 */
  get setupStatus(): DocSetupStatus {
    if (!this.anySelected) return 'draft'
    return this.calcSetupStatus(this.nodeExtra)
  }

  /**
   * ★ 完成度唯一口径（原型 V6 §③「状态是算出来的，不是存出来的」）。
   *
   * 为什么不读 `cert_standard_doc_contract.Status`：完成度会被**别处**改掉 ——
   * 删锚点 / 换空白模板 / 解绑提示词 都会让文档从「已完成」退回「正在设置」，
   * 而没有任何人负责回退那一列 ⇒ 状态必然漂移，左树筛选与实际能不能跑就对不上。
   */
  private calcSetupStatus(ex: any): DocSetupStatus {
    const missing = this.missingItems(ex)
    if (missing.length === 0) return 'done'
    // 未上传空白模板 = 规则还没开始配 ⇒ 归「未设置」而非「正在设置」
    if (ex?.hasTemplate !== true && ex?.HasTemplate !== true) return 'draft'
    return 'setting'
  }

  /**
   * ★ 该文档的**全部必配项**（完成度的分母）。
   *
   * ⛔ 与 `missingItems()` **共用同一组条件** —— 两处分开写必然漂移
   *   （这正是原实现写死 `isFixedDoc ? 4 : 3` 踩的坑：分母写死之后，
   *    一旦必配项随文档形态增减，`done` 就会**静默算错**）。
   *
   * 通用：① AI 语义分析 ② 类型确认（**不可解析时不需要** —— 类型由程序定）
   * `fixed`    追加：③ 分类标签 ④ 文档作用（⛔ 不要求指纹，否则 PDF 永远完不成）
   * `editable` 且**已上传模板**追加：③ 锚点槽位
   *   ⚠️ 「锚点扫描」与「锚点来源」是**同一个槽位的两种缺法**（`missingItems` 里
   *      `if/else if` ⇒ 最多报一个）⇒ 分母只加 **1**，⛔ 不能加成 2 项，
   *      否则「扫描通过但没配来源」的文档会被算成缺 2 项（分母虚高、进度条永远差一格）。
   *   ⚠️ 未上传模板时没有锚点可谈 ⇒ 不进分母。
   */
  requiredItems(ex: any): string[] {
    const items = ['AI 语义分析']
    const typeLocked = isUnparseableType(ex?.fileType || ex?.FileType)
    if (!typeLocked) items.push('类型确认')

    const isFixed =
      typeLocked || (ex?.docCategory || ex?.DocCategory) === 'fixed'
    if (isFixed) items.push('分类标签', '文档作用')
    else if (ex?.hasTemplate === true || ex?.HasTemplate === true)
      items.push('锚点')

    return items
  }

  /**
   * ★ 必配项缺失清单（空数组 = 已完成）。
   *
   * 口径见 `requiredItems()` 的注释 —— 本方法只做「逐项判定」。
   */
  missingItems(ex: any): string[] {
    const miss: string[] = []

    const analyzeStatus = ex?.analyzeStatus || ex?.AnalyzeStatus
    if (analyzeStatus !== 'completed') miss.push('AI 语义分析')

    // ★ C3：不可解析（图片 / PDF）⇒ 类型是**程序定的**，⛔ 不要求人工「确认」
    //   （开关本身就置灰了，把它列成缺失项会让这份文档**永远完不成**）
    const typeLocked = isUnparseableType(ex?.fileType || ex?.FileType)
    const typeConfirmed = ex?.typeConfirmed || ex?.TypeConfirmed
    if (!typeLocked && !typeConfirmed) miss.push('类型确认')

    const isFixed =
      typeLocked || (ex?.docCategory || ex?.DocCategory) === 'fixed'
    if (isFixed) {
      const tags = ex?.tagsJson || ex?.TagsJson
      if (!tags || tags === '[]' || tags === '') miss.push('分类标签')
      const purpose = ex?.docPurpose || ex?.DocPurpose
      if (!purpose?.trim()) miss.push('文档作用')
    } else if (ex?.hasTemplate === true || ex?.HasTemplate === true) {
      const scan = ex?.scanStatus || ex?.ScanStatus
      if (scan !== 'completed') miss.push('锚点扫描')
      else if (Number(ex?.orphanCount ?? ex?.OrphanCount ?? 0) > 0)
        miss.push('锚点来源')
    }

    return miss
  }

  /** 树里文件叶子总数（用于「显示 N / M 份」） */
  countFiles(nodes: TreeNode[]): number {
    let n = 0
    for (const node of nodes) {
      if (node.NodeType === FILE_KIND) n++
      if (node.Children?.length) n += this.countFiles(node.Children)
    }
    return n
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
          ? {
              ...c,
              tagMap: { 1: '启用', 0: '禁用' },
              tagTypeMap: { 1: 'success', 0: 'info' },
            }
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
    const res = await this.apiPost<
      ApiResponse<{ Items: any[]; TotalCount: number }>
    >('/filter', {
      Page: 1,
      PageSize: 1000,
      Filters: [
        { Field: 'TemplateCode', Operator: 'eq', Value: this.templateCode },
      ],
    })
    expectOk(res, '加载锚点清单失败')
    return res.data?.Items ?? []
  }

  /**
   * ★ 保存**单个**锚点行（「必填项」就地开关等）。
   *
   * 【为什么必须有这个方法】
   *   `AnchorRuleTab` 的「必填项」开关点一下就写库，走的是这个入口。
   *   此前该方法**只被调用、从未被定义** ⇒ 点开关必抛
   *   `TypeError: …updateAnchor is not a function`，被 catch 吞成「保存失败」，
   *   开关弹回原位 —— 表现为「必填项怎么点都不生效」。
   *
   * 【为什么走 `save-batch` 而不是通用 `/update`】
   *   `save-batch` 按 `uk_tpl_anchor` **整行 upsert**，语义 =「这一行 = 完整状态」。
   *   因此**必须提交完整行** —— 本方法的入参正是 `loadAnchors()` 返回的原始行，
   *   天然满足；⛔ 不要在这里只传 `{ Code, Required }`（其余字段会被写成 CLR 默认值）。
   */
  async updateAnchor(row: any): Promise<void> {
    if (!this.templateCode) throw new Error('未选中模板，无法保存锚点')
    const res = await saveAnchorBatch(this.templateCode, [row])
    expectOk(res, '保存锚点失败')
  }

  // ========================================================
  // 覆盖点④：表单字段（4 个受控值列 → 下拉；IsValid → switch）
  // ========================================================

  override get formFields(): YzhFormField[] {
    return (
      super.formFields
        // 模板由左树注入
        .filter((f) => f.prop !== 'TemplateCode')
        .map((f) => {
          switch (f.prop) {
            // ★ 受控值改下拉：后端有 L3 校验，让用户「选」而不是「猜着敲」
            //   （EntityConfig 里是 TextBox + Placeholder，因为库里没有对应字典）
            case 'AnchorType':
              return {
                ...f,
                type: 'select' as any,
                options: toOptions(ANCHOR_TYPES),
              }
            case 'AnchorKind':
              return {
                ...f,
                type: 'select' as any,
                options: toOptions(ANCHOR_KINDS),
              }
            case 'WriteMode':
              return {
                ...f,
                type: 'select' as any,
                options: toOptions(WRITE_MODES),
              }
            case 'ValueType':
              return {
                ...f,
                type: 'select' as any,
                options: toOptions(VALUE_TYPES),
              }
            // 页眉页脚：空串 = 不适用，必须是**可清空**的下拉
            case 'HeaderKind':
              return {
                ...f,
                type: 'select' as any,
                options: [
                  { label: '（不适用）', value: '' },
                  ...toOptions(HEADER_KINDS),
                ],
                clearable: true,
              }
            // ★ IsValid 是 int（0/1）—— switch 必须显式声明 active/inactive value，
            //   否则写入布尔 true/false（DB 容得下，但语义上不该依赖隐式转换）
            case 'IsValid':
              return {
                ...f,
                type: 'switch' as any,
                fieldProps: { 'active-value': 1, 'inactive-value': 0 },
              }
            default:
              return f
          }
        })
    )
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
