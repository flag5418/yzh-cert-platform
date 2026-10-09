/**
 * 企业资料规范化 · 页面逻辑（专家端）
 *
 * 菜单 `MENU_AUD_12`｜路由 `/enterprise-normalize`｜后端 `/api/Auditor/EnterpriseNormalize`
 * 规格：`docs/20-体系认证/03-详细设计/05-企业资料规范化/{54,55,60}`
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 2026-10-07 用户裁决（**推翻了本页此前的形态**）
 *
 * 用户原话：「我们点击一个企业的阶段，**不是应该按标准显示 tab 页面，针对不同的标准，
 * 不同的文件夹，文件，或针对该阶段，选择需要进行规范化的文件，进行规范化处理吗**，
 * 当前这个页面和我设想的差异较大」。
 *
 * ⇒ 三条裁决：
 *   ① 标准维度 → **右区 Tab**
 *   ② 未配填写规则的标准文件 → **不显示**
 *   ③ 本轮范围 → **完整目标形态**（`tree` + `plan` + `run` 三端点 + 页面改造）
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★ 页面结构（左树 | 右区）
 *
 *   左树：企业 › 阶段（复用 `/api/Auditor/EnterpriseOriginal/stage-tree`，⛔ 不复制第二份）
 *   右区：统计条 → 标准 Tab（`el-tabs`）→ 该标准下的文件夹/文件勾选树 → 底部操作条
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 选择为什么由本类持有（而不是交给某个组件）
 *
 * 选择要**跨标准 Tab 保持**（用户可能同时勾 9001 和食品的文件，一次入队），
 * 而每个 Tab 渲染的是独立的一棵 `el-tree` ⇒ 状态必须上提到这里。
 * `selectionToken` 用于「程序化改选择后强制重挂载树」（见 `NormalizeScopeTree` 文件头注释）。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 为什么「干跑」是强制前置
 *
 * 规范化是**覆盖性**操作（重新生成会覆盖企业侧已有产物）。
 * 没有干跑，用户点「一键规范化」就是盲签 —— 尤其「已锁定 / 未配规则 / 无锚点」
 * 这三类会被静默跳过，跑完只看到数字对不上。
 * ⇒ `canRun` 要求「已预览 且 选择没变过」⇒ 改了勾选必须重新预览。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ⛔ 本页**不使用** `useSingleTable` / `useTreeTable`：右区是「范围树 + 干跑预览」，
 *    不是可增删改的单表，硬套内核只会让两边都变形。
 */
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { confirmOrFalse, type YzhTreeNode } from '@yzh-core'
import { fetchStageTree } from '@share/api/ent/enterprise-original'
import {
  fetchNormalizeTree,
  fillOneNormalize,
  planNormalize,
  runNormalize,
  type NormalizeFileNode,
  type NormalizeFolderNode,
  type NormalizeFillOneResult,
  type NormalizePlanResult,
  type NormalizeRunResult,
  type NormalizeStandardNode,
} from '@share/api/ent/enterprise-normalize'

/** 单文件同步执行结果（挂在 `results` 上，⛔ 不做全局单例 —— 否则点第二个会覆盖第一个的证据） */
export interface RowResult {
  StandardFileCode: string
  FileName: string
  Result: NormalizeFillOneResult
}

/**
 * 入队后自动刷新的轮询间隔（ms）。
 *
 * ★ 为什么是 15s：后台串行跑「AI 取值 + 文档写入」，单个文件可能到分钟级
 *   ⇒ 太密没意义（还没跑完），太疏用户会以为页面卡死。
 */
const POLL_INTERVAL_MS = 15_000

/**
 * 单次入队后最多轮询多少次（15s × 40 = 10 分钟）。
 *
 * ★ 为什么要封顶：轮询是「锦上添花」，⛔ 不能变成永远不关的定时器
 *   （用户切走页面、队列卡住、后端挂了…都会让轮询白跑）。
 */
const POLL_MAX_TICKS = 40

/**
 * 范围树节点（`el-tree` 的数据形状）。
 *
 * ⚠️ 字段名是 `Name` / `Children` / `Disabled`（⛔ 不是后端原样字段）——
 *    这三个是 `el-tree` 的 `props` 映射目标，改名就要同步改 `TREE_PROPS`。
 */
export interface ScopeTreeNode {
  /** ★ 唯一键：文件夹 `F:{folderCode}`｜文件 `S:{standardFileCode}` */
  Key: string
  Name: string
  Type: 'folder' | 'file'
  /** ★ 无可规范化文件的文件夹 ⇒ true（灰显且不可勾选） */
  Disabled: boolean
  /** 文件夹：可规范化文件数（含子文件夹） */
  FillableCount: number
  /** 文件夹：标准域文件总数（含子）—— 用于解释「为什么只有这几个能跑」 */
  TotalFileCount: number
  /** 文件节点专属 */
  File?: NormalizeFileNode
  Children?: ScopeTreeNode[]
}

// ════════════════════════════════════════════════════════════════════════
// 范围树 → el-tree 节点（纯函数，⛔ 不碰状态）
// ════════════════════════════════════════════════════════════════════════

function toFileNode(f: NormalizeFileNode): ScopeTreeNode {
  return {
    Key: `S:${f.StandardFileCode}`,
    Name: f.FileName,
    Type: 'file',
    Disabled: false,
    FillableCount: 0,
    TotalFileCount: 0,
    File: f,
  }
}

function toFolderNode(fo: NormalizeFolderNode): ScopeTreeNode {
  return {
    Key: `F:${fo.FolderCode}`,
    Name: fo.FolderName,
    Type: 'folder',
    // ★ 0 个可规范化 ⇒ 灰显不可勾选（能勾但勾了没反应，比灰显更糟）
    Disabled: fo.FillableCount === 0,
    FillableCount: fo.FillableCount,
    TotalFileCount: fo.TotalFileCount,
    Children: [...fo.Children.map(toFolderNode), ...fo.Files.map(toFileNode)],
  }
}

/** 递归取一个标准下的**全部可规范化文件**（含根级与各级子文件夹） */
function collectFiles(s: NormalizeStandardNode | null): NormalizeFileNode[] {
  if (!s) return []
  const out: NormalizeFileNode[] = [...(s.RootFiles ?? [])]
  const walk = (folders: NormalizeFolderNode[]): void => {
    for (const fo of folders ?? []) {
      out.push(...(fo.Files ?? []))
      walk(fo.Children ?? [])
    }
  }
  walk(s.Folders ?? [])
  return out
}

export class EnterpriseNormalizeLogic {
  // ══════════════ 一、左树（企业 › 阶段） ══════════════
  treeNodes = ref<YzhTreeNode[]>([])
  treeHint = ref('')
  treeLoading = ref(false)

  // ══════════════ 二、当前作用域 ══════════════
  enterpriseCode = ref('')
  stageCode = ref('')
  scopeLabel = ref('')

  /** 作用域是否已选到「阶段」这一级（范围加载的前置条件） */
  scopeReady = computed(() => !!this.enterpriseCode.value && !!this.stageCode.value)

  // ══════════════ 三、规范化范围（标准 › 文件夹 › 文件） ══════════════
  standards = ref<NormalizeStandardNode[]>([])
  totalFillable = ref(0)
  totalFiles = ref(0)

  loading = ref(false)
  /** 是否已经真正查过一次（用于区分「还没选阶段」与「查了但是空」） */
  loaded = ref(false)
  /**
   * ★ 范围是否以**业务拒绝**告终（`success=false`）。
   *
   * · `true`  = 后端**没能执行**这次查询 —— 例如全库还没有任何已发布的空白模板
   * · `false` = 后端执行成功，只是这个范围里没有可规范化的文档
   *
   * 用途：空态标题要说对话 —— 前置未就绪时写「这个阶段下没有可规范化的文档」是错的
   * （问题不在阶段，在后台还没发布模板）。
   */
  listBlocked = ref(false)
  /** 业务拒绝原因（⛔ 不是「空集」的说明 —— 空集走各 Tab 自己的空态文案） */
  listMessage = ref('')

  /** 当前选中的标准 Tab（= `NormalizeStandardNode.StandardCode`） */
  activeStandardCode = ref('')

  activeStandard = computed<NormalizeStandardNode | null>(
    () => this.standards.value.find((s) => s.StandardCode === this.activeStandardCode.value) ?? null,
  )

  /** 当前 Tab 的 `el-tree` 数据 */
  activeTreeNodes = computed<ScopeTreeNode[]>(() => {
    const s = this.activeStandard.value
    if (!s) return []
    return [...(s.Folders ?? []).map(toFolderNode), ...(s.RootFiles ?? []).map(toFileNode)]
  })

  /** 当前 Tab 的空态文案（★ 「这个标准下没有可规范化文件」与「还没有已发布模板」是两回事） */
  activeEmptyText = computed(() => {
    const s = this.activeStandard.value
    if (!s) return '请先选择标准'
    // ★ 主数据缺失优先说 —— 这种标准**永远不可能**有文件，说「暂无可规范化文件」会让人去白找
    if (s.StandardRegistered === false) {
      return '该标准在企业阶段里的关联还在，但标准主数据已被删除（未登记），因此没有可规范化的文件'
    }
    if (!s.Mounted) return '该标准未挂到当前企业阶段，仅因存在已发布的填写规则模板而显示'
    return '该标准下暂无可规范化的文件（尚未配置填写规则，或模板未发布）'
  })

  // ══════════════ 三·补、标准主数据健康度（2026-10-09） ══════════════
  //
  // ★★ 为什么要有这一段（用户报障根因）
  //
  // `cert_enterprise_stage` / `cert_doc_template` 里的 `StandardCode` 只是**引用**，
  // 不保证 `cert_iso_standard` 里的主数据还在。主数据被删而关联没清时，
  // 后端此前 `StandardName = iso?.StandardName ?? code` ⇒ **把裸 GUID 当标准名回传**
  // ⇒ Tab 上直接显示 `475da4fe-8f50-4bf7-bf2b-b39869d5ddf7`。
  //
  // 用户看到的是「系统坏了」，而真相是「数据缺了」—— 两者处置完全不同：
  //   · 系统坏了 → 找人修系统
  //   · 数据缺了 → 去清关联 / 补主数据
  // ⇒ 页面必须**说破**，且⛔ 不能把 GUID 当名字显示。

  /** ★ 当前标准是否登记在册（`false` ⇒ 关联指向了一个已被删除的标准） */
  activeStandardRegistered = computed(() => {
    const s = this.activeStandard.value
    return s ? s.StandardRegistered !== false : true
  })

  /** ★ 当前标准是否挂到本企业阶段（`false` = 仅因存在已发布模板而显示） */
  activeStandardMounted = computed(() => this.activeStandard.value?.Mounted === true)

  /** ★ 本阶段里所有「引用了一个已不存在标准」的标准 —— 顶部一次性说清，⛔ 不逐个弹 */
  unregisteredStandards = computed(() =>
    this.standards.value.filter((s) => s.StandardRegistered === false),
  )

  // ══════════════ 四、选择（★ 跨 Tab 保持） ══════════════
  /** 已勾选的标准域行 Code（只含文件） */
  checkedCodes = ref<string[]>([])
  /** ★ 程序化改选择后 +1 ⇒ 换 `el-tree` 的 key 强制重挂载（`default-checked-keys` 才会生效） */
  selectionToken = ref(0)

  selectedCount = computed(() => this.checkedCodes.value.length)

  /**
   * ★ 当前 Tab 已勾选的文件数。
   *
   * 用途：选择是**跨标准**的（一次可勾多个标准），但用户眼里只有当前 Tab ⇒
   * 当「已选总数 ≠ 当前标准已选」时必须**说破**，否则会看到
   * 「这个标准的树是空的，底部却说已选 3 个」——前后矛盾。
   */
  selectedInActiveStandard = computed(() => {
    const inActive = new Set(collectFiles(this.activeStandard.value).map((f) => f.StandardFileCode))
    return this.checkedCodes.value.filter((c) => inActive.has(c)).length
  })

  /** 本阶段全部可规范化文件（「全选本阶段」用） */
  allFillableCodes = computed<string[]>(() =>
    this.standards.value.flatMap((s) => collectFiles(s).map((f) => f.StandardFileCode)),
  )

  // ══════════════ 五、干跑 / 入队 ══════════════
  planResult = ref<NormalizePlanResult | null>(null)
  /** ★ 勾选变过 ⇒ 上次干跑作废（否则会拿旧结论去执行新范围 —— 静默错配） */
  planStale = ref(false)
  planning = ref(false)
  runResult = ref<NormalizeRunResult | null>(null)
  running = ref(false)

  /** ★ 只有「已预览 且 选择没变过 且 有可执行项」才允许真跑 */
  canRun = computed(
    () => !!this.planResult.value && !this.planStale.value && (this.planResult.value.Queued ?? 0) > 0,
  )

  // ══════════════ 六、单文件同步执行（P1 验证口，保留） ══════════════
  /** 正在执行的文件（`StandardFileCode`）；空 = 无执行中 */
  runningCode = ref('')
  results = ref<Record<string, RowResult>>({})

  /** 最近几次单文件执行结果（新的在前，便于渲染） */
  recentResults = computed<RowResult[]>(() =>
    Object.values(this.results.value).slice(-5).reverse(),
  )

  // ══════════════ 七、入队后自动刷新（2026-10-09） ══════════════
  //
  // ★ 为什么要有（用户 2026-10-09 选「功能补全」）
  //
  // `run` 只把任务**投进队列**就返回了，真正执行在后台串行跑（单个文件可能到分钟级）。
  // 此前用户必须**自己反复点「刷新」**才知道跑完没有 —— 而「不知道跑没跑完」
  // 正是最容易被误判成「点了没反应 / 系统卡死」的状态。
  //
  // ★ 三条约束（⛔ 别丢）：
  //   ① **只在入队成功后启动** —— 平时⛔ 不轮询（白耗接口 + 无意义重渲染）
  //   ② **封顶 10 分钟** + **无在跑文件即提前收工** ⇒ 定时器一定有个头
  //   ③ **换作用域 / 组件卸载 / 用户手动停** 都要停 —— 否则会拿 A 阶段的选择去刷 B 阶段

  /** 是否正在自动刷新（页面据此显示提示条 + 「停止」按钮） */
  polling = ref(false)
  /** 本轮自动刷新已执行的次数（⛔ 不显示「预计还要多久」—— 后端不给进度，编时间就是骗人） */
  pollCount = ref(0)
  /** 最近一次刷新完成的时刻（本地时间字符串，用于让用户确认「它真的在动」） */
  lastRefreshAt = ref('')

  private pollTimer: number | null = null

  /**
   * ★ 本轮入队时刻（浏览器 epoch ms）—— 「跑完了没」的判据基准。
   *
   * ⛔ **为什么不用 `InstanceState ∈ {filling, pending}` 判**（第一版写法，已废弃）：
   * 后端**从来不写**这两个值 —— 全仓只有 `instance.InstanceState = "filled"` /
   * `"archived"`（`DocumentFillOrchestrator.cs:449/603/690`）⇒ 该判据**恒为 false**
   * ⇒ 第一次轮询（15s）就「提前收工」，功能等于没做。
   * （这类错误 `vue-tsc` / `guards` / `vite build` **全都发现不了**，只能靠读后端源码 + 真机。）
   */
  private runStartedAtMs = 0

  /** 本轮真正入队的标准域行 Code（`fill` / `regenerate` 两类）—— 完成判据只看它们 */
  private queuedCodes: string[] = []

  /**
   * ★ **完成判据**：本轮入队的文件，**每一个**都已产生「晚于本次入队时刻」的填充留痕。
   *
   * 为什么用留痕时间：它是**可观测的既成事实**（`cert_doc_fill_log` 新行），
   * ⛔ 不是对内部状态的猜测。跳过（锁定/未配规则）的文件不会产生留痕 ⇒ 本方法返回 false
   * ⇒ 靠 10 分钟上限兜底，⛔ 不会误判成「跑完了」而提前收工。
   *
   * ⚠️ 留痕时间是**服务端 UTC**，基准是**浏览器本地 epoch**；两者都是 epoch，
   *    但可能有秒级时钟偏差 ⇒ 留 10 秒容差。
   */
  private allQueuedDone(): boolean {
    if (this.queuedCodes.length === 0) return false

    const byCode = new Map<string, NormalizeFileNode>()
    for (const s of this.standards.value) {
      for (const f of collectFiles(s)) byCode.set(f.StandardFileCode, f)
    }

    const toleranceMs = 10_000
    return this.queuedCodes.every((c) => {
      const f = byCode.get(c)
      if (!f || !f.LastFillTime) return false
      const t = new Date(f.LastFillTime).getTime()
      return Number.isFinite(t) && t >= this.runStartedAtMs - toleranceMs
    })
  }

  /**
   * 启动自动刷新（入队成功后调用；重复调用 = 重新计时）。
   *
   * @param queuedCodes 本轮真正入队的标准域行 Code（来自 `run` 回执的 `Items`）
   */
  startAutoRefresh(queuedCodes: string[] = []): void {
    this.stopAutoRefresh()
    if (!this.scopeReady.value) return
    this.queuedCodes = [...queuedCodes]
    this.runStartedAtMs = Date.now()
    this.pollCount.value = 0
    this.polling.value = true
    this.pollTimer = window.setInterval(() => {
      void this.pollOnce()
    }, POLL_INTERVAL_MS)
  }

  /** 停止自动刷新（用户点「停止」/ 换作用域 / 组件卸载时调用；幂等） */
  stopAutoRefresh(): void {
    if (this.pollTimer !== null) {
      window.clearInterval(this.pollTimer)
      this.pollTimer = null
    }
    this.polling.value = false
    this.queuedCodes = []
  }

  /** 一次轮询：刷新范围树 → 判断是否该收工 */
  private async pollOnce(): Promise<void> {
    if (!this.scopeReady.value) {
      this.stopAutoRefresh()
      return
    }

    this.pollCount.value += 1
    try {
      await this.loadRange()
      this.lastRefreshAt.value = new Date().toLocaleTimeString('zh-CN')
    } catch {
      // loadRange 自己会把业务拒绝写进 listMessage/listBlocked（并弹红）
      // ⇒ 这里只负责停掉轮询，⛔ 不重复报错、不刷屏
      this.stopAutoRefresh()
      return
    }

    // ① 本轮入队的文件都出了新留痕 ⇒ 跑完了，收工
    if (this.allQueuedDone()) {
      this.stopAutoRefresh()
      return
    }
    // ② 兜底上限（10 分钟）—— 保证定时器一定有个头
    if (this.pollCount.value >= POLL_MAX_TICKS) this.stopAutoRefresh()
  }

  async init(): Promise<void> {
    await this.loadTree()
  }

  // ════════════════════════ 左树 ════════════════════════

  async loadTree(): Promise<void> {
    this.treeLoading.value = true
    try {
      const nodes = await fetchStageTree()
      this.treeNodes.value = (nodes as unknown as YzhTreeNode[]) ?? []
      this.treeHint.value = this.treeNodes.value.length === 0 ? '当前工作区下还没有企业档案' : ''
    } catch (e) {
      this.treeNodes.value = []
      this.treeHint.value = (e as Error).message
    } finally {
      this.treeLoading.value = false
    }
  }

  /**
   * 点树节点。
   * · 企业节点（有子级）→ 只提示「请选择具体阶段」，并**清空**范围（防止看到上一个阶段的旧数据）
   * · 阶段节点（叶子）→ 定位 (企业, 阶段) 并加载范围树
   */
  async onNodeClick(node: YzhTreeNode): Promise<void> {
    // ★ 换作用域 ⇒ 立刻停掉自动刷新（否则会拿 A 阶段的选择去刷 B 阶段）
    this.stopAutoRefresh()

    const children = (node?.Children as YzhTreeNode[] | undefined) ?? []
    if (children.length > 0) {
      this.scopeLabel.value = `${node.Label ?? node.Name ?? ''}（请选择具体阶段）`
      this.resetScope()
      return
    }

    const entCode = (node.EnterpriseCode as string | undefined) ?? node.Code
    if (!entCode || !node.Code) {
      ElMessage.warning('树节点缺少企业/阶段标识，请刷新后重试')
      return
    }

    this.enterpriseCode.value = entCode
    this.stageCode.value = node.Code
    this.scopeLabel.value = (node.Label as string) ?? node.Name ?? ''
    // ★ 换作用域 ⇒ 上一次的选择与预览全部作废（否则会拿 A 阶段的选择去跑 B 阶段）
    this.checkedCodes.value = []
    this.selectionToken.value += 1
    this.dismissAll()
    await this.loadRange()
  }

  private resetScope(): void {
    this.enterpriseCode.value = ''
    this.stageCode.value = ''
    this.standards.value = []
    this.totalFillable.value = 0
    this.totalFiles.value = 0
    this.activeStandardCode.value = ''
    this.checkedCodes.value = []
    this.selectionToken.value += 1
    this.listMessage.value = ''
    this.listBlocked.value = false
    this.loaded.value = false
    this.dismissAll()
  }

  // ════════════════════════ 范围树 ════════════════════════

  /**
   * 加载范围树（标准 › 文件夹 › 文件）。
   *
   * ★ 驱动源 = `cert_doc_template`（**已发布**），⛔ 不是全部标准文件。
   *
   * ★★ 2026-10-06 用户裁决：后端在「全库没有已发布模板」时返回 `success=false` + `err`
   *   （业务拒绝），⛔ 不是 `success=true` + `data.message`（那是把错误藏在成功里）。
   *   ⇒ 本方法 catch 分支要**接住**这条业务拒绝：① 弹红 ② **同时**写进 `listMessage`
   *   让页面**持久**展示原因（toast 三秒就没了，而用户需要照着这句话去后台发布模板）。
   */
  async loadRange(): Promise<void> {
    if (!this.scopeReady.value) return
    this.loading.value = true
    try {
      const r = await fetchNormalizeTree(this.enterpriseCode.value, this.stageCode.value)
      const list = r.standards ?? []
      this.standards.value = list
      this.totalFillable.value = r.totalFillable ?? 0
      this.totalFiles.value = r.totalFiles ?? 0
      this.listMessage.value = ''
      this.listBlocked.value = false
      this.loaded.value = true

      // ★ 保持当前 Tab（刷新后停在原地）；它没了才回落到「第一个有可规范化文件的标准」
      const stillThere = list.some((s) => s.StandardCode === this.activeStandardCode.value)
      if (!stillThere) {
        const first = list.find((s) => s.FillableCount > 0) ?? list[0]
        this.activeStandardCode.value = first?.StandardCode ?? ''
      }

      // ★ 剪掉已消失的勾选（刷新后模板可能被取消发布 ⇒ 那些文件不该留在选择里）
      const alive = new Set(this.allFillableCodes.value)
      const pruned = this.checkedCodes.value.filter((c) => alive.has(c))
      if (pruned.length !== this.checkedCodes.value.length) {
        this.checkedCodes.value = pruned
        this.selectionToken.value += 1
        this.planStale.value = true
      }
    } catch (e) {
      const msg = (e as Error).message
      this.standards.value = []
      this.totalFillable.value = 0
      this.totalFiles.value = 0
      this.activeStandardCode.value = ''
      // ★ 持久展示（页面空态会渲染它）—— 一次性 toast 会丢掉「该去哪儿修」这条信息
      this.listMessage.value = msg
      this.listBlocked.value = true
      this.loaded.value = true
      ElMessage.error(msg)
    } finally {
      this.loading.value = false
    }
  }

  // ════════════════════════ 选择 ════════════════════════

  /**
   * `el-tree` 勾选变化。
   *
   * ⚠️ 组件回传的只是**当前 Tab 这一棵树**里的勾选（`getCheckedKeys(true)` 的叶子）。
   *    所以这里必须**按当前标准做「局部替换」**，⛔ **不能整体覆盖** ——
   *    选择是跨标准 Tab 保持的（用户可能同时勾 9001 与食品的文件、一次入队），
   *    整体覆盖会把「另一个标准已勾的」静默冲掉：
   *    在 9001 勾 A → 切食品勾 B ⇒ `checkedCodes` 变成 `['B']`，**A 丢了**。
   *
   * 判据：`新的 = 不属于当前标准的旧勾选（保留） + 当前标准的新勾选`
   */
  onCheckedChange(codes: string[]): void {
    const inActive = new Set(
      collectFiles(this.activeStandard.value).map((f) => f.StandardFileCode),
    )
    const kept = this.checkedCodes.value.filter((c) => !inActive.has(c))
    this.checkedCodes.value = [...kept, ...codes]
    this.markSelectionDirty()
  }

  /** ★ 全选本阶段（跨全部标准 Tab 的可规范化文件） */
  selectAllStage(): void {
    this.checkedCodes.value = [...this.allFillableCodes.value]
    this.selectionToken.value += 1
    this.markSelectionDirty()
  }

  clearSelection(): void {
    this.checkedCodes.value = []
    this.selectionToken.value += 1
    this.markSelectionDirty()
  }

  /** 选择一变：上次干跑作废 + 上次入队回执收起 */
  private markSelectionDirty(): void {
    this.planStale.value = true
    this.runResult.value = null
  }

  // ════════════════════════ 干跑 / 入队 ════════════════════════

  private buildRequest() {
    return {
      EnterpriseCode: this.enterpriseCode.value,
      StageCode: this.stageCode.value,
      StandardFileCodes: [...this.checkedCodes.value],
    }
  }

  /**
   * ★ **干跑预览** —— 回答「选中这批文件，真跑会发生什么」。
   * ⛔ 零副作用：不写库 / 不产文件 / 不调 LLM / 不入队。
   */
  async previewPlan(): Promise<void> {
    if (!this.scopeReady.value) {
      ElMessage.warning('请先在左侧选择「企业 › 阶段」')
      return
    }
    if (this.selectedCount.value === 0) {
      ElMessage.warning('请先勾选需要规范化的文件')
      return
    }

    this.planning.value = true
    try {
      const r = await planNormalize(this.buildRequest())
      this.planResult.value = r
      this.planStale.value = false
      this.runResult.value = null
    } catch (e) {
      this.planResult.value = null
      this.planStale.value = true
      ElMessage.error((e as Error).message)
    } finally {
      this.planning.value = false
    }
  }

  /**
   * ★ **开始规范化（整批入队）**。
   *
   * ⚠️ 服务端会**按当前事实重算一遍范围**（⛔ 不信任前端清单）：页面可能已停留很久，
   *   期间模板可能被取消发布、文件可能被锁定 ⇒ 若重算后无可执行项，后端返回**业务拒绝**。
   */
  async executeRun(): Promise<void> {
    if (!this.canRun.value) {
      ElMessage.warning('请先点「预览将做什么」，确认预览结果后再执行')
      return
    }

    this.running.value = true
    try {
      const r = await runNormalize(this.buildRequest())
      this.runResult.value = r
      ElMessage.success(`已入队 ${r.Queued} 个文件（批次 ${r.QueueCode}）`)
      // ★ 已入队 ⇒ 干跑预览**立即作废并清空**。
      //   ⛔ 只置 `planStale` 不够：卡片是「run 非空显示回执 / 否则显示预览」的互斥结构，
      //   预览残留会让「收起回执」后**立刻冒出一张过期的预览**（用户以为没关掉）。
      this.planResult.value = null
      this.planStale.value = true
      // 刷新范围（实例态 / 产物路径可能已变）
      await this.loadRange()
      // ★★ 入队后**启动自动刷新** —— 后台在串行跑，用户不该靠反复点「刷新」来猜进度。
      //    ⚠️ 只把**真正会执行**的（fill / regenerate）作为「完成判据」的观察对象 ——
      //      被跳过的文件永远不会产生新留痕，算进去就永远等不到「完成」。
      this.startAutoRefresh(
        (r.Items ?? [])
          .filter((i) => i.Action === 'fill' || i.Action === 'regenerate')
          .map((i) => i.StandardFileCode),
      )
    } catch (e) {
      ElMessage.error((e as Error).message)
    } finally {
      this.running.value = false
    }
  }

  dismissPlan(): void {
    this.planResult.value = null
    this.planStale.value = true
  }

  dismissRun(): void {
    this.runResult.value = null
  }

  /**
   * 收起「干跑预览 / 入队回执」卡。
   *
   * ★ **两张数据都清** —— 「收起」的语义是「**关掉这张卡**」。
   *   ⛔ 不能只清一张：卡片有两段互斥显示（`v-if="run"` 入队回执 / `v-if="plan && !run"` 干跑预览），
   *   只清 `run` 的话，卡片会**变成显示干跑预览** ⇒ 用户点「收起」看到的是「卡片还在，只是内容变了」
   *   （= 点了没反应）。
   *   ⚠️ 且此刻那张预览**已经过期**（`planStale`），显示它本身就是误导。
   */
  dismissCard(): void {
    this.dismissRun()
    this.dismissPlan()
  }

  private dismissAll(): void {
    this.planResult.value = null
    this.planStale.value = true
    this.runResult.value = null
    this.results.value = {}
  }

  // ════════════════════════ 单文件同步执行 ════════════════════════

  /**
   * ★ **规范化这一个文件** —— 直接调编排器七步，同步返回结果（P1 验证口，保留）。
   *
   * ⚠️ 已完成过的文件再点一次 = **重新生成**（产物覆盖上一版）⇒ 先二次确认；首次生成不打扰。
   * ⚠️ 锁定的文件由编排器在第一件事就拦下（返回 `skipped_locked`），⛔ 前端不重复判锁。
   */
  async runOne(file: NormalizeFileNode): Promise<void> {
    if (this.runningCode.value) {
      ElMessage.warning('有文件正在规范化，请等它跑完')
      return
    }

    if (file.OutputPath) {
      const ok = await confirmOrFalse(
        `「${file.FileName}」已经生成过一版产物，继续会**重新生成并覆盖**当前产物（旧值仍保留在取值账本里，可追溯）。是否继续？`,
        '重新生成',
        { type: 'warning', confirmButtonText: '继续', cancelButtonText: '取消' },
      )
      if (!ok) return
    }

    this.runningCode.value = file.StandardFileCode
    try {
      const result = await fillOneNormalize({
        EnterpriseCode: this.enterpriseCode.value,
        StandardCode: file.StandardCode,
        // ⚠️ 模板可声明「不限阶段」（StageCode 为空）⇒ 回落到当前树选中的阶段
        StageCode: file.StageCode || this.stageCode.value,
        StandardFileCode: file.StandardFileCode,
      })

      this.results.value = {
        ...this.results.value,
        [file.StandardFileCode]: {
          StandardFileCode: file.StandardFileCode,
          FileName: file.FileName,
          Result: result,
        },
      }

      // ★ 六态分流提示：跳过是设计内（不报红），失败才是异常（报红）
      if (result.Status === 'failed') {
        ElMessage.error(result.Message ?? '规范化失败')
      } else if (result.Status.startsWith('skipped_')) {
        ElMessage.info(result.Message ?? '本次跳过')
      } else if (result.Status === 'partial') {
        ElMessage.warning(`已生成，但有 ${result.PendingCount} 处待补`)
      } else {
        ElMessage.success('已生成')
      }

      await this.loadRange()
    } catch (e) {
      ElMessage.error((e as Error).message)
    } finally {
      this.runningCode.value = ''
    }
  }

  /** 收起某文件的单文件执行结果 */
  dismissResult(code: string): void {
    const next = { ...this.results.value }
    delete next[code]
    this.results.value = next
  }
}
