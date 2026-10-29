/**
 * 企业文档页 · 逻辑层（专家端 `/resources`，菜单 `MENU_AUD_04`）
 *
 * 依据：`docs/70-V2设计/36-企业资料上传重写-V1.md`（§6.3 落地实现）
 * 实施：`docs/70-V2设计/实施-TODO-上传重写-V1.md`（P3）
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★★ 这一版与老 `resources/logic.ts` 的结构差别（36 号 D-1）
 *
 * 老：企业 → **阶段** → 标准 Tab → 槽位表（四层，每层一套状态机）
 * 新：企业（**一层**）→ 文件表 → 按标准看提取结果（**视图筛选**，不是存储层）
 *
 *   ⇒ 阶段维度已废除（一阶段与二阶段是同一批文件），
 *     上传不带标准，标准只从「当前有效认证计划」来。
 *   ⇒ 「按标准查看」只是给 `list` 传 `StandardCode` 的**筛子**，⛔ 不新增存储层。
 * ════════════════════════════════════════════════════════════════════════
 *
 * ★ 三条不变量
 *   ① **标准只有一个来源** = `plan-standards`（当前有效计划）。⛔ 不许前端自己查计划表。
 *   ② **状态文案只有一个口径** = `./pure` 的 `convertBadge` / `extractBadge`。
 *      页面模板 ⛔ 不写 `status === 'completed' ? …`（老页上表与文件夹树各判一套，
 *      同一份文件显示两种状态 —— 用户投诉过）。
 *   ③ **轮询只刷状态条**；只有状态签名真的变了才刷表（否则 5s 一次全表重建，
 *      用户的勾选与滚动位置每 5 秒被清一次）。
 */
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { confirmOrFalse, SingleTableCore } from '@yzh-core'
import type { YzhAction, YzhTableColumn } from '@yzh-core'
import {
  entDocBatchDelete,
  entDocDelete,
  entDocDownload,
  entDocExtract,
  entDocExtractions,
  entDocList,
  entDocMarkdown,
  entDocPlanStandards,
  entDocPreviewBlob,
  entDocQueueDetail,
  entDocRegenerate,
  entDocRestore,
  entDocStatusBar,
  entDocTree,
  entDocVersions,
  type EntDocClauseHit,
  type EntDocExtractionFull,
  type EntDocNode,
  type EntDocPlanInfo,
  type EntDocQueueDetail,
  type EntDocRow,
  type EntDocStandardRef,
  type EntDocStatusBar,
  type EntDocVersionRow,
} from '@share/api/ent/enterprise-doc'
import {
  extractBadge,
  folderLabel,
  formatSize,
  parseExtractContent,
  sortClauses,
  splitByAnchor,
  summarize,
  type AnchorHit,
} from './pure'
import { createUploadState, type UploadHost } from './useUpload'

/** 提取抽屉里的一个「标准 Tab」 */
export interface ExtractionTab {
  standardCode: string
  /** 标准编号短码（`iso13485`） */
  shortCode: string
  standardName: string
  /** 该标准的产物行（`null` = 计划内但还没提取） */
  row: EntDocExtractionFull | null
  clauses: EntDocClauseHit[]
  /** JSON 解析失败的原文（排障用；`null` = 正常） */
  broken: string | null
}

/** 原文核对抽屉的分段（锚点高亮） */
export interface MarkdownParts {
  before: string
  hit: string | null
  after: string
  mode: AnchorHit['mode'] | null
  /** 定位目标条款号（展示用） */
  clauseNumber: string | null
}

export class EnterpriseDocLogic extends SingleTableCore<any> implements UploadHost {
  /**
   * 本页没有 EntityConfig（右表是自定义业务表），
   * 仅为满足 R24 的基类契约而声明；实际请求一律走 `@share/api/ent/enterprise-doc`。
   */
  controllerName = 'Auditor/EnterpriseDoc'

  /** 本页无 `/config` 端点 ⇒ 空实现，避免 `init()` 打 404 */
  protected async loadConfig(): Promise<void> {}

  protected async onAfterInit(): Promise<void> {
    await this.loadTree()
  }

  // ═══════════════════════ 一、左树（企业一层） ═══════════════════════

  treeNodes = ref<EntDocNode[]>([])
  treeLoading = ref(false)
  /** 工作区是否已配置企业（`false` 是正常状态，不是错误） */
  treeConfigured = ref(true)
  treeMessage = ref<string | null>(null)
  selectedNode = ref<EntDocNode | null>(null)

  selectedCode = computed(() => this.selectedNode.value?.Code ?? '')
  selectedName = computed(() => this.selectedNode.value?.Name ?? '')

  /** 当前企业的计划标准（左树节点自带，⛔ 不另开请求） */
  planStandards = computed<EntDocStandardRef[]>(() => this.selectedNode.value?.Standards ?? [])
  /** 是否有有效认证计划 —— 没有就提取不了（标准从计划来） */
  hasPlan = computed(() => this.selectedNode.value?.HasPlan === true)
  /** 没有计划时的**人话原因**（`plan-standards` 下发） */
  planReason = ref<string | null>(null)

  /** 加载左树（保留当前选中项：刷新后不跳回顶部） */
  async loadTree(): Promise<void> {
    this.treeLoading.value = true
    try {
      const r = await entDocTree()
      this.treeConfigured.value = r.configured
      this.treeMessage.value = r.message
      this.treeNodes.value = r.nodes ?? []

      // 刷新后重挂选中项：用 Code 回查新对象（节点上的计数已变，必须换成新引用）
      const keep = this.selectedCode.value
      const again = keep ? this.treeNodes.value.find((n) => n.Code === keep) ?? null : null
      this.selectedNode.value = again

      // 首屏自动选第一家（有文件或有计划的那家优先，避免默认落在空企业上）
      if (!again && this.treeNodes.value.length > 0) {
        const preferred =
          this.treeNodes.value.find((n) => n.HasPlan && n.FileCount > 0)
          ?? this.treeNodes.value.find((n) => n.HasPlan)
          ?? this.treeNodes.value[0]
        await this.onNodeClick(preferred)
      }
    } catch (e) {
      this.reportError(e, '加载企业列表失败')
    } finally {
      this.treeLoading.value = false
    }
  }

  /**
   * 点企业 → 重置筛选 → 全量刷新。
   *
   * ⚠️ 入参声明成 `any`：`YzhTreeTableLayout` 的 `tree-node-click` 回传的是
   *   组件自己的 `YzhTreeNode` 结构类型（零领域依赖），与本页的 `EntDocNode`
   *   没有名义关系；写成 `EntDocNode` 会让模板里 `$event` 报 TS2345。
   */
  async onNodeClick(node: any): Promise<void> {
    if (!node) return
    this.selectedNode.value = node
    this.activeStandardCode.value = ''
    this.keyword.value = ''
    this.onlyFailed.value = false
    await this.refreshAll()
    await this.loadPlanReason()
  }

  /**
   * 取「为什么没有标准」的原因。
   *
   * ★ 只在**没有有效计划**时查：有计划时标准清单已在树节点上，
   *   再查一次纯属浪费（且会让左树点击变慢）。
   */
  private async loadPlanReason(): Promise<void> {
    this.planReason.value = null
    const ent = this.selectedCode.value
    if (!ent || this.hasPlan.value) return
    try {
      const r = await entDocPlanStandards(ent)
      this.planReason.value = r.reason
    } catch {
      // 原因查不到不影响主流程：页面已有「请先创建认证计划」的通用提示
    }
  }

  // ═══════════════════════ 二、列表与筛选 ═══════════════════════

  /*
   * ⚠️ `rows` / `selectedRows` **不在这里声明** —— 基类 `SingleTableCore` 已有
   *   `rows = ref<V[]>([])` 与 `selectedRows = ref<V[]>([])`（`V = any`）。
   *   子类重复声明同名属性在 `useDefineForClassFields` 下会**覆盖基类字段**
   *   （等于把基类的 ref 换成一个新 ref，基类方法里持有的引用就断了）。
   *   ⇒ 直接用继承来的，需要强类型时在调用处 `as EntDocRow[]`。
   */

  /** 过滤前口径（用于「共 N 份，其中符合筛选 M 份」） */
  totalAll = ref(0)
  statusBar = ref<EntDocStatusBar | null>(null)
  planInfo = ref<EntDocPlanInfo | null>(null)

  /** 按标准查看：空串 = 全部（⛔ 不是存储层，只是 `list` 的筛子） */
  activeStandardCode = ref('')
  keyword = ref('')
  onlyFailed = ref(false)

  /** 表格上方的一句话汇总（数字全部来自 `pure.summarize`） */
  summary = computed(() => summarize(this.rows.value))

  /** 筛选是否生效（决定空态文案与「清空筛选」出口） */
  filterActive = computed(
    () => this.activeStandardCode.value !== '' || this.keyword.value.trim() !== '' || this.onlyFailed.value,
  )

  /** 当前筛选的中文摘要 */
  filterSummary = computed(() => {
    const parts: string[] = []
    if (this.activeStandardCode.value) {
      const std = this.planStandards.value.find((s) => s.Code === this.activeStandardCode.value)
      parts.push(`标准=${std?.StandardName ?? this.activeStandardCode.value}`)
    }
    if (this.keyword.value.trim()) parts.push(`关键词=${this.keyword.value.trim()}`)
    if (this.onlyFailed.value) parts.push('只看需处理')
    return parts.join('，')
  })

  /**
   * 文件表列定义。
   *
   * ⚠️ 属性名必须叫 `fileColumns`，⛔ 不能叫 `columns` ——
   *   基类 `SingleTableCore` 的 `columns` 是 **getter**（由 `config` 推导），
   *   子类用实例属性覆盖 getter ⇒ TS2610（accessor 与 property 不能互相覆盖）。
   */
  /*
   * ⚠️ 列宽合计（含操作列 ~88）必须 ≤ 右面板可用宽度（1280 屏 ≈ 900px），
   *   否则「操作 ▾」被推到横向溢出区，用户点不到（2026-10-29 真机实测踩到）。
   *   ⇒ 文件名 / 文件夹用 `minWidth` 让宽屏自动撑开，其余给固定窄宽。
   */
  fileColumns: YzhTableColumn<EntDocRow>[] = [
    { prop: 'FileName', label: '文件名', minWidth: 200, slot: true },
    { prop: 'RelFolderPath', label: '文件夹', minWidth: 130, slot: true },
    { prop: 'FileSize', label: '大小', width: 84, align: 'right', slot: true },
    { prop: 'ConvertStatus', label: '转换', width: 92, slot: true },
    { prop: 'ExtractStatus', label: '提取', width: 128, slot: true },
    { prop: 'VersionNumber', label: '版本', width: 60, align: 'center', slot: true },
    // ⛔ 操作列不手写：YzhTable 内置行动作列（`row-action-buttons` + `action-dropdown-only`）
  ]

  /**
   * ★ 刷新表格 —— **必须覆盖基类实现**。
   *
   * <para><b>为什么</b>：页面在「零行」时用 `YzhEmptyState` 占位、`YzhTable` 不渲染，
   * 此时 `_tableRef` 是 `null`；基类实现是 `await this._tableRef?.refresh()`，
   * 对 `null` 直接**静默无操作** ⇒ `dataLoader` 永远没人调 ⇒ 第一次进来永远是空页，
   * 而且「共 0 份文件」看起来像真的没有文件（2026-10-29 真机实测踩到）。</para>
   *
   * <para>⇒ 没有表格引用时**直接跑一次 dataLoader**，让 `rows` 有数据，
   * 表格随即挂载（之后走正常 `refresh()` 通道）。代价是首屏多一次请求，可以接受。</para>
   */
  override async refresh(): Promise<void> {
    if (this._tableRef) {
      await this._tableRef.refresh()
      return
    }
    await this.dataLoader()
  }

  /** `YzhTable` 数据加载器（`show-pagination=false` ⇒ 一次给全量） */
  async dataLoader(): Promise<{ rows: EntDocRow[]; total: number }> {
    const ent = this.selectedCode.value
    if (!ent) {
      this.rows.value = []
      this.totalAll.value = 0
      return { rows: [], total: 0 }
    }
    try {
      const r = await entDocList({
        enterpriseCode: ent,
        standardCode: this.activeStandardCode.value,
        keyword: this.keyword.value.trim(),
        onlyFailed: this.onlyFailed.value,
      })
      this.rows.value = r.rows ?? []
      this.totalAll.value = r.total ?? this.rows.value.length
      this.planInfo.value = r.plan ?? null
      return { rows: this.rows.value, total: this.rows.value.length }
    } catch (e) {
      this.reportError(e, '查询失败')
      return { rows: [], total: 0 }
    }
  }

  async loadStatusBar(): Promise<void> {
    const ent = this.selectedCode.value
    if (!ent) {
      this.statusBar.value = null
      return
    }
    try {
      this.statusBar.value = await entDocStatusBar(ent)
    } catch {
      // 状态条失败不影响主流程（表格已给出真实状态）
    }
  }

  /** 全量刷新：状态条 + 表格 */
  async refreshAll(): Promise<void> {
    await Promise.all([this.loadStatusBar(), this.refresh()])
    this.lastSig = this.signature()
  }

  /** 筛选变化 → 回第一页并刷新 */
  async onFilterChange(): Promise<void> {
    await this.refresh()
  }

  /** 清空筛选（空态里的唯一正确出口 —— 给「上传文件」会让用户以为文件丢了） */
  async clearFilters(): Promise<void> {
    this.activeStandardCode.value = ''
    this.keyword.value = ''
    this.onlyFailed.value = false
    await this.refresh()
  }

  /**
   * 勾选变化（批量删除用）。
   *
   * ⚠️ 名字不能叫 `onSelectionChange`：基类把它声明成**实例属性**（回调字段），
   *   子类用方法覆盖 ⇒ TS2425。
   */
  onRowsSelected(rows: EntDocRow[]): void {
    this.selectedRows.value = rows ?? []
  }

  // ═══════════════════════ 三、轮询 ═══════════════════════

  private timer: number | null = null
  private pollBusy = false
  private lastSig = ''

  /** 忙碌判据 = 后端 `IsBusy`（该 scope 下有非终态队列） */
  isBusy = computed(() => this.statusBar.value?.IsBusy === true)

  queueProgress = computed(() => this.statusBar.value?.QueueProgress ?? 0)

  queueLabel = computed(() => {
    const t = this.statusBar.value?.QueueType
    if (t === 'enterprise_clause_extract') return '正在按标准提取条款'
    return '正在转换文件'
  })

  private signature(): string {
    const b = this.statusBar.value
    if (!b) return ''
    return [
      b.Total, b.ConvertingCount, b.FailedCount, b.ExtractableCount,
      b.ExtractDoneCount, b.ExtractFailedCount,
      b.QueueCode, b.QueueStatus, b.QueueCompleted,
    ].join('|')
  }

  startPolling(): void {
    if (this.timer != null) return
    this.timer = window.setInterval(() => void this.tick(), 5000)
  }

  stopPolling(): void {
    if (this.timer == null) return
    window.clearInterval(this.timer)
    this.timer = null
  }

  /**
   * 5 秒心跳：**先只刷状态条**，签名变了才刷表。
   *
   * ⛔ 不能每 5 秒无脑 `refresh()`：会清掉用户的勾选与滚动位置，
   *   而且长文档提取要跑几分钟，全程抖动根本没法操作。
   */
  private async tick(): Promise<void> {
    if (this.pollBusy) return
    if (typeof document !== 'undefined' && document.hidden) return
    const ent = this.selectedCode.value
    if (!ent) return

    this.pollBusy = true
    try {
      await this.loadStatusBar()
      const sig = this.signature()
      if (sig !== this.lastSig) {
        this.lastSig = sig
        await this.refresh()
      }
    } catch {
      // 轮询失败静默：下一次再试（⛔ 不弹提示，否则网络抖一下满屏红字）
    } finally {
      this.pollBusy = false
    }
  }

  // ═══════════════════════ 四、上传（五段式） ═══════════════════════

  /** 上传状态机（`UploadHost` 的两个方法由本类实现） */
  upload = createUploadState(this)

  enterpriseCode(): string {
    return this.selectedCode.value
  }

  async afterUploaded(): Promise<void> {
    await this.loadTree()
    await this.refreshAll()
  }

  /** 打开上传弹窗（无计划也允许上传 —— 文件照样能转换，只是提不出来） */
  openUpload(): void {
    if (!this.selectedCode.value) {
      ElMessage.warning('请先在左侧选择企业')
      return
    }
    this.upload.open()
  }

  // ═══════════════════════ 五、提取 ═══════════════════════

  /** 入队提取：`rows` 空 = 整家企业 */
  async startExtract(rows?: EntDocRow[]): Promise<void> {
    const ent = this.selectedCode.value
    if (!ent) {
      ElMessage.warning('请先在左侧选择企业')
      return
    }
    if (!this.hasPlan.value) {
      ElMessage.warning(this.planReason.value || '该企业还没有有效认证计划，请先创建计划')
      return
    }
    try {
      const codes = rows && rows.length > 0 ? rows.map((r) => r.Code) : undefined
      const r = await entDocExtract(ent, codes)
      // ⚠️ 「没有需要提取的内容」不是失败：后端刻意返回 success=true + 原因
      if (r.Queued > 0) ElMessage.success(r.Message)
      else ElMessage.info(r.Message || '没有需要提取的内容')
      await this.refreshAll()
    } catch (e) {
      this.reportError(e, '提取入队失败')
    }
  }

  // ═══════════════════════ 六、提取结果抽屉 ═══════════════════════

  extractionVisible = ref(false)
  extractionLoading = ref(false)
  extractionFile = ref<EntDocRow | null>(null)
  extractionTabs = ref<ExtractionTab[]>([])
  extractionActive = ref('')

  /** 打开某份文件的提取结果（按标准分 Tab） */
  async openExtractions(row: EntDocRow): Promise<void> {
    if (!row) return
    this.extractionFile.value = row
    this.extractionVisible.value = true
    this.extractionLoading.value = true
    this.extractionTabs.value = []
    this.extractionActive.value = ''

    try {
      const list = await entDocExtractions(row.Code)
      const byStd = new Map<string, EntDocExtractionFull>()
      for (const e of list ?? []) byStd.set(e.StandardCode, e)

      // ★ Tab 顺序以**计划标准顺序**为准（用户建计划时的顺序），
      //   计划里没有但历史跑过产物的标准补在最后（⛔ 不能让老结果凭空消失）
      const ordered: Array<{ code: string; short: string; name: string }> = []
      const seen = new Set<string>()
      for (const s of this.planStandards.value) {
        ordered.push({ code: s.Code, short: s.StandardCode, name: s.StandardName })
        seen.add(s.Code)
      }
      for (const code of byStd.keys()) {
        if (seen.has(code)) continue
        ordered.push({ code, short: code.slice(0, 8), name: `标准 ${code.slice(0, 8)}` })
      }

      this.extractionTabs.value = ordered.map((o) => {
        const r = byStd.get(o.code) ?? null
        const content = parseExtractContent(r?.ContentJson)
        return {
          standardCode: o.code,
          shortCode: o.short,
          standardName: o.name,
          row: r,
          clauses: content?.Clauses ? sortClauses(content.Clauses) : [],
          broken: r?.ContentJson && !content ? r.ContentJson : null,
        }
      })

      const firstWithData = this.extractionTabs.value.find((t) => t.clauses.length > 0)
      this.extractionActive.value = (firstWithData ?? this.extractionTabs.value[0])?.standardCode ?? ''
    } catch (e) {
      this.reportError(e, '读取提取结果失败')
    } finally {
      this.extractionLoading.value = false
    }
  }

  closeExtractions(): void {
    this.extractionVisible.value = false
    this.extractionTabs.value = []
    this.extractionFile.value = null
  }

  // ═══════════════════════ 七、原文核对（含锚点定位） ═══════════════════════

  markdownVisible = ref(false)
  markdownLoading = ref(false)
  markdownFile = ref<EntDocRow | null>(null)
  markdownText = ref('')
  markdownMessage = ref<string | null>(null)
  markdownParts = ref<MarkdownParts | null>(null)

  /** 打开原文（不带定位） */
  async openMarkdown(row: EntDocRow): Promise<void> {
    this.markdownFile.value = row
    this.markdownParts.value = null
    await this.loadMarkdown(row)
    this.markdownVisible.value = true
  }

  private async loadMarkdown(row: EntDocRow): Promise<void> {
    this.markdownLoading.value = true
    this.markdownMessage.value = null
    try {
      const r = await entDocMarkdown(row.Code, this.selectedCode.value)
      this.markdownText.value = r.markdown
    } catch (e: any) {
      // 失败文案来自后端（「Markdown 尚未生成」/「该格式无法自动提取原文」）
      this.markdownText.value = ''
      this.markdownMessage.value = e?.message || '读取原文失败'
    } finally {
      this.markdownLoading.value = false
    }
  }

  /**
   * ★ 「在原文中定位」—— 用锚点回到原文那一句。
   *
   * 这是 P2 遗留 #1 的消费侧修复：模型偶尔把原文的 `"` 输出成中文引号 `“”`，
   * 直接 `indexOf` 会 miss。定位一律走 `pure.locateAnchor`（三级递进：
   * 逐字 → 标点归一 → 折叠空白），⛔ 页面不许自己 `indexOf`。
   */
  async locateInMarkdown(clause: EntDocClauseHit, fileRow?: EntDocRow | null): Promise<void> {
    const row = fileRow ?? this.extractionFile.value
    if (!row) return
    if (this.markdownFile.value?.Code !== row.Code || this.markdownText.value.length === 0) {
      this.markdownFile.value = row
      await this.loadMarkdown(row)
    }
    const parts = splitByAnchor(this.markdownText.value, clause.Anchor)
    this.markdownParts.value = {
      before: parts.before,
      hit: parts.hit,
      after: parts.after,
      mode: parts.mode,
      clauseNumber: clause.ClauseNumber ?? null,
    }
    this.markdownVisible.value = true
    if (!parts.hit) {
      ElMessage.warning(
        `没能在原文里定位到条款 ${clause.ClauseNumber ?? ''} 的锚点（原文可能已被替换，请重新提取）`,
      )
    }
  }

  closeMarkdown(): void {
    this.markdownVisible.value = false
    this.markdownText.value = ''
    this.markdownParts.value = null
    this.markdownFile.value = null
  }

  /** 定位级别 → 中文（`punctuation` / `reflow` 说明模型改过标点或 PDF 断行） */
  locateModeText(mode: AnchorHit['mode'] | null): string {
    switch (mode) {
      case 'exact': return '逐字命中'
      case 'punctuation': return '命中（标点归一后）'
      case 'reflow': return '命中（跨行重排后）'
      default: return '未命中'
    }
  }

  // ═══════════════════════ 八、PDF 预览 ═══════════════════════

  previewVisible = ref(false)
  previewLoading = ref(false)
  previewUrl = ref('')
  previewName = ref('')

  async openPreview(row: EntDocRow): Promise<void> {
    if (!row?.CanPreview) {
      ElMessage.warning('PDF 预览还没生成，请等待转换完成或点「重新生成」')
      return
    }
    this.previewName.value = row.FileName
    this.previewVisible.value = true
    this.previewLoading.value = true
    this.releasePreview()
    try {
      const blob = await entDocPreviewBlob(row.Code, this.selectedCode.value)
      this.previewUrl.value = URL.createObjectURL(blob)
    } catch (e: any) {
      ElMessage.error(e?.message || '预览失败')
      this.previewVisible.value = false
    } finally {
      this.previewLoading.value = false
    }
  }

  private releasePreview(): void {
    if (this.previewUrl.value) {
      URL.revokeObjectURL(this.previewUrl.value)
      this.previewUrl.value = ''
    }
  }

  closePreview(): void {
    this.previewVisible.value = false
    this.releasePreview()
  }

  // ═══════════════════════ 九、下载 / 版本 / 删除 / 重转 ═══════════════════════

  /** 下载原件（⛔ 不走裸链接：鉴权在 Authorization 头里） */
  async download(row: EntDocRow): Promise<void> {
    if (!row?.StoragePath) {
      ElMessage.warning('该文件没有存储路径')
      return
    }
    try {
      const blob = await entDocDownload(row.StoragePath, this.selectedCode.value)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = row.FileName || 'download'
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
      URL.revokeObjectURL(url)
    } catch (e: any) {
      ElMessage.error(e?.message || '下载失败')
    }
  }

  versionVisible = ref(false)
  versionLoading = ref(false)
  versionFile = ref<EntDocRow | null>(null)
  versionRows = ref<EntDocVersionRow[]>([])
  versionCurrent = ref(0)
  versionMessage = ref<string | null>(null)

  async openVersions(row: EntDocRow): Promise<void> {
    this.versionFile.value = row
    this.versionVisible.value = true
    this.versionLoading.value = true
    this.versionRows.value = []
    this.versionMessage.value = null
    try {
      const r = await entDocVersions(row.Code, this.selectedCode.value)
      this.versionRows.value = r.rows
      this.versionCurrent.value = r.currentVersion
    } catch (e: any) {
      this.versionMessage.value = e?.message || '读取版本失败'
    } finally {
      this.versionLoading.value = false
    }
  }

  closeVersions(): void {
    this.versionVisible.value = false
    this.versionRows.value = []
    this.versionFile.value = null
  }

  /** 回滚到某版本（⚠️ 版本号不回退：回滚是「又改了一次」） */
  async restoreVersion(version: EntDocVersionRow): Promise<void> {
    const row = this.versionFile.value
    if (!row || !version) return
    const ok = await confirmOrFalse(
      `确定把【${row.FileName}】回滚到 v${version.VersionNumber}？\n`
      + '回滚后会重新转换并重新提取，版本号继续 +1（历史版本保留）。',
      '回滚确认',
      { confirmButtonText: '确定回滚', cancelButtonText: '取消' },
    )
    if (!ok) return
    try {
      await entDocRestore(row.Code, this.selectedCode.value, version.VersionNumber, '人工回滚')
      ElMessage.success('已回滚，正在重新转换')
      this.closeVersions()
      await this.refreshAll()
    } catch (e) {
      this.reportError(e, '回滚失败')
    }
  }

  /** 重新转换（不动源文件、不改版本号） */
  async regenerate(row: EntDocRow): Promise<void> {
    if (!row) return
    const ok = await confirmOrFalse(
      `确定重新生成【${row.FileName}】的 PDF 与原文？\n源文件不动，转换完成后会提示重新提取。`,
      '重新生成确认',
      { confirmButtonText: '确定', cancelButtonText: '取消' },
    )
    if (!ok) return
    try {
      await entDocRegenerate(row.Code, this.selectedCode.value, true)
      ElMessage.success('已入队重新转换')
      await this.refreshAll()
    } catch (e) {
      this.reportError(e, '重新生成失败')
    }
  }

  async deleteDoc(row: EntDocRow): Promise<void> {
    if (!row) return
    const ok = await confirmOrFalse(
      `确定删除【${row.FileName}】？\n该文件的 PDF / 原文与全部提取结果都会一起删除（历史版本保留在归档区）。`,
      '删除确认',
      { confirmButtonText: '确定删除', cancelButtonText: '取消' },
    )
    if (!ok) return
    try {
      await entDocDelete(row.Code, this.selectedCode.value, '人工删除')
      ElMessage.success('已删除')
      await this.loadTree()
      await this.refreshAll()
    } catch (e) {
      this.reportError(e, '删除失败')
    }
  }

  /** 批量删除（页面上「删除选中」/「删除整个文件夹」的落点） */
  async batchDelete(rows?: EntDocRow[]): Promise<void> {
    const target = (rows && rows.length > 0 ? rows : this.selectedRows.value) ?? []
    if (target.length === 0) {
      ElMessage.warning('请先勾选要删除的文件')
      return
    }
    const ok = await confirmOrFalse(
      `确定删除选中的 ${target.length} 个文件？\n它们的 PDF / 原文与全部提取结果都会一起删除。`,
      '批量删除确认',
      { confirmButtonText: `确定删除 ${target.length} 个`, cancelButtonText: '取消' },
    )
    if (!ok) return
    try {
      const r = await entDocBatchDelete(target.map((x) => x.Code), this.selectedCode.value, '批量删除')
      if (r.Failed && r.Failed.length > 0) {
        ElMessage.warning(`已删除 ${r.DeletedCount} 个，${r.Failed.length} 个失败：${r.Failed[0].Message}`)
      } else {
        ElMessage.success(`已删除 ${r.DeletedCount} 个文件`)
      }
      this.selectedRows.value = []
      await this.loadTree()
      await this.refreshAll()
    } catch (e) {
      this.reportError(e, '批量删除失败')
    }
  }

  // ═══════════════════════ 十、队列详情 ═══════════════════════

  queueVisible = ref(false)
  queueLoading = ref(false)
  queueDetail = ref<EntDocQueueDetail | null>(null)

  async openQueueDetail(): Promise<void> {
    const ent = this.selectedCode.value
    if (!ent) return
    this.queueVisible.value = true
    this.queueLoading.value = true
    try {
      this.queueDetail.value = await entDocQueueDetail(ent)
    } catch (e) {
      this.reportError(e, '读取处理进度失败')
    } finally {
      this.queueLoading.value = false
    }
  }

  closeQueueDetail(): void {
    this.queueVisible.value = false
    this.queueDetail.value = null
  }

  // ═══════════════════════ 十一、展示辅助（模板只调，不判） ═══════════════════════

  formatSize = formatSize
  folderLabel = folderLabel
  extractBadge = extractBadge

  /** 计划标准下拉（「按标准查看」筛选用） */
  standardOptions = computed(() =>
    this.planStandards.value.map((s) => ({
      value: s.Code,
      label: `${s.StandardName}（${s.StandardCode}）`,
    })),
  )

  /**
   * 行操作（纯下拉：一个「操作 ▾」装下全部）。
   *
   * ⚠️ 名字不能叫 `rowActions` / `rowActionButtons`：基类这两个成员都是 getter ⇒ TS2610。
   * ⚠️ 返回值必须显式标 `YzhAction[]`：不标会被推断成「联合元素的数组」，
   *   传给 `YzhTable` 的 `row-action-buttons` 时类型收窄不过（TS2416）。
   */
  docRowActions = (row: EntDocRow): YzhAction[] => {
    const list = [
      { key: 'extractions', text: '提取结果' },
      { key: 'preview', text: '预览 PDF', disabled: !row.CanPreview },
      { key: 'markdown', text: '查看原文' },
      { key: 'download', text: '下载原件' },
      { key: 'versions', text: '历史版本' },
      { key: 'regenerate', text: '重新生成' },
      { key: 'extract-one', text: '重新提取', disabled: !row.CanExtract },
      { key: 'delete', text: '删除', type: 'danger' as const },
    ]
    return list
  }

  /**
   * 行动作分派（页面 `@row-action` 直连）。
   *
   * ⚠️ 名字不能叫 `onRowAction`：基类同名成员是实例属性 ⇒ TS2425。
   */
  async handleRowAction(key: string, row: EntDocRow): Promise<void> {
    switch (key) {
      case 'extractions': return this.openExtractions(row)
      case 'preview': return this.openPreview(row)
      case 'markdown': return this.openMarkdown(row)
      case 'download': return this.download(row)
      case 'versions': return this.openVersions(row)
      case 'regenerate': return this.regenerate(row)
      case 'extract-one': return this.startExtract([row])
      case 'delete': return this.deleteDoc(row)
      default: return
    }
  }
}

export default EnterpriseDocLogic
