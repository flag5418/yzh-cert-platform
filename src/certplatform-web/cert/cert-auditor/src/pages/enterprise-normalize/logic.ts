/**
 * 企业资料规范化 · **任务列表页 Logic**（专家端 · 路由 `/enterprise-normalize`）
 *
 * 菜单 `MENU_AUD_12`｜后端 `/api/Auditor/EnterpriseNormalize`
 * 规格：`docs/20-体系认证/03-详细设计/05-企业资料规范化/{54,55,60}`
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 2026-10-11 用户裁决：`/enterprise-normalize` 拆成两个页面
 *
 * 用户原话：「这个页面，我发现了，应该将该页面做成 2 个，第一个是企业资料规范化，
 * 第二个是企业资料审核」＋「第一个页面我们按 `/tasks` 进行向导型的进行任务引导，
 * 核心是开启任务」。
 *
 * ⇒ 本页 = **任务列表**（版式对齐 `pages/tasks/index.vue`）：
 *   「我开过哪些规范化任务、各自跑到哪一步了」＋ 开新任务 / 审核 / 重新执行 / 取消。
 *   ⛔ 范围勾选与干跑预览**不在本页** —— 走向导 `/enterprise-normalize/create`。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 术语对齐：**「任务」= `yzh_queue` 里一条 `enterprise_normalize` 批次**
 *
 * `QueueCode` 即任务号。⛔ 不是专家任务系统的 `cert_expert_task`
 * （那是 NC 检查 / 报告生成，另一套表、另一个页面 `/tasks`）。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 为什么覆写 `loadConfig`：本页数据**不走** `YzhControllerBase` 的 `/filter`
 *
 * 本控制器是 `YzhControllerBase<DocFillValue>`（`/config` 返回的是**值账本实体**的表单
 * 配置），而本页要的是「规范化批次列表」，口径完全不同 ⇒
 * 列 / 搜索 / 工具栏 / 行按钮全部在本类**静态声明**，数据走 `GET tasks`。
 * 同族先例：`cert-admin` 的 `foundation/standard-manage`、`workflow/skill-manage`
 * 都覆写了 `dataLoader`。
 *
 * ⚠️⚠️ **大小写**：`YzhTable.dataLoader` 收 `PageParams{page,rows}`、要回
 *   `Page{rows,total}`（**camelCase**），而 `fetchNormalizeTasks` 返回
 *   `{Rows,Total}`（**PascalCase**）⇒ 必须显式转换。
 *   ⛔ 直接 `return res` 的后果是**表格恒空且零报错**（最难查的一类）。
 */
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import { SingleTableCore, confirmOrFalse } from '@yzh-core'
import type { Page, PageParams, SearchField, YzhAction, YzhTableColumn } from '@yzh-core'
import router from '@/router'
import { fetchStageTree } from '@share/api/ent/enterprise-original'
import {
  cancelNormalize,
  fetchNormalizeTasks,
  type NormalizeTaskRow,
} from '@share/api/ent/enterprise-normalize'

/**
 * 有未完成任务时的自动刷新间隔（ms）。
 *
 * ★ 为什么要有：`run` 只把任务投进队列就返回了，真正执行在后台 Worker 跑
 *   （单个文档可能到分钟级）。此前用户必须**自己反复点「刷新」**才知道跑完没有 ——
 *   而「不知道跑没跑完」正是最容易被误判成「点了没反应 / 系统卡死」的状态。
 */
const POLL_INTERVAL_MS = 15_000

/**
 * 单轮自动刷新最多跑多少次（15s × 40 = 10 分钟）。
 *
 * ★ 为什么要封顶：轮询是「锦上添花」，⛔ 不能变成永远不关的定时器
 *   （用户切走页面、队列卡住、后端挂了…都会让它白跑）。
 */
const POLL_MAX_TICKS = 40

/** 任务状态筛选项（`yzh_queue.Status` 五态） */
const STATUS_OPTIONS = [
  { label: '排队中', value: 'pending' },
  { label: '执行中', value: 'running' },
  { label: '已完成', value: 'completed' },
  { label: '执行失败', value: 'failed' },
  { label: '已取消', value: 'cancelled' },
]

export class NormalizeTaskListLogic extends SingleTableCore<NormalizeTaskRow> {
  controllerName = 'Auditor/EnterpriseNormalize'

  /**
   * 企业下拉选项（搜索栏用）。
   *
   * ★ 为什么要拉一次：`GET tasks` 的企业是**查询期解析**（后端从 `ScopeKey` 拆），
   *   ⛔ 不是可用的字典列 ⇒ 搜索项只能由页面自己给选项。
   */
  readonly enterpriseOptions = ref<Array<{ label: string; value: string }>>([])

  /** 是否正在自动刷新（页面据此显示提示条 + 「停止」按钮） */
  readonly autoRefreshing = ref(false)

  private pollTimer: number | null = null
  private pollTicks = 0

  /**
   * 主键字段（本页行唯一键是任务号，⛔ 不是实体表的 `Code`）。
   * ⚠️ 基类里是 **public** getter ⇒ 覆写也必须 public（`protected` 会让整个子类
   *   不再满足 `SingleTableCore` 契约，`useSingleTable` 的类型推导随之崩掉，
   *   症状是「页面里 `logic.stopAutoRefresh` 之类全报不存在」）。
   */
  override get primaryKey(): string {
    return 'QueueCode'
  }

  /** 确认弹窗里显示的实体名称字段 */
  protected override get entityNameField(): string {
    return 'QueueCode'
  }

  // ══════════════════════════════════════════════════════════════════════
  // 覆盖点：本页不走 /config、不走 /filter
  // ══════════════════════════════════════════════════════════════════════

  /**
   * ★ 刻意**不发请求**。
   *
   * 内核默认实现会打 `/api/Auditor/EnterpriseNormalize/config`，那是
   * `YzhControllerBase<DocFillValue>` 的值账本表单配置 —— 与任务列表毫无关系。
   * 拉回来反而会让 `config.value` 带着一份「看着像配置、其实不对口」的数据，
   * 后面谁若不小心用了 `super.columns` 就会渲染出一堆无关列。
   */
  protected override async loadConfig(): Promise<void> {
    // no-op（列 / 搜索 / 按钮见下方 getter）
  }

  /** 配置加载后的钩子：拉一次企业选项（搜索栏用） */
  protected override async onAfterInit(): Promise<void> {
    try {
      const nodes = await fetchStageTree()
      // ⚠️ `TreeNode` 的名称字段是 **`Label`**（⛔ 没有 `Name`）
      this.enterpriseOptions.value = (nodes ?? []).map((n) => ({
        label: String(n.Label ?? ''),
        value: String(n.Code ?? ''),
      }))
    } catch {
      // ★ 搜索项拉不到**不影响列表**（列表里企业名由后端解析）⇒ 静默降级为空选项
      this.enterpriseOptions.value = []
    }
  }

  // ══════════════════════════════════════════════════════════════════════
  // 列 / 搜索 / 按钮（静态声明）
  // ══════════════════════════════════════════════════════════════════════

  override get columns(): YzhTableColumn<NormalizeTaskRow>[] {
    return [
      { prop: 'QueueCode', label: '任务号', width: 190 },
      { prop: 'EnterpriseName', label: '企业', minWidth: 150, showOverflowTooltip: true },
      { prop: 'StageName', label: '认证阶段', minWidth: 130, showOverflowTooltip: true },
      { prop: 'Status', label: '状态', width: 100, slot: true },
      { prop: 'Progress', label: '进度', minWidth: 210, slot: true },
      { prop: 'CreateTime', label: '入队时间', width: 170, slot: true },
      { prop: 'OperatorName', label: '操作人', width: 110 },
    ]
  }

  override get searchFields(): SearchField[] {
    return [
      {
        prop: 'EnterpriseCode',
        label: '企业',
        type: 'select',
        options: this.enterpriseOptions.value,
        placeholder: '全部企业',
      },
      {
        prop: 'Status',
        label: '状态',
        type: 'select',
        options: STATUS_OPTIONS,
        placeholder: '全部状态',
      },
    ]
  }

  override get toolbarActions(): YzhAction[] {
    return [
      { key: 'custom:create', text: '创建规范化任务', type: 'primary' },
      { key: 'custom:refresh', text: '刷新', type: 'default' },
    ]
  }

  /**
   * 行按钮。
   *
   * ★「取消」只在**未到终态**时出现 —— 已结束批次去取消会被后端**业务拒绝**，
   *   摆一个点了必报错的按钮比不摆更糟（用户会以为系统坏了）。
   */
  override get rowActions(): (row: NormalizeTaskRow) => YzhAction[] {
    return (row: NormalizeTaskRow): YzhAction[] => [
      { key: 'custom:review', text: '审核', type: 'primary' },
      { key: 'custom:rerun', text: '重新执行', type: 'default' },
      { key: 'custom:cancel', text: '取消', type: 'danger', visible: row.IsFinished !== true },
    ]
  }

  // ══════════════════════════════════════════════════════════════════════
  // 数据加载
  // ══════════════════════════════════════════════════════════════════════

  override async dataLoader(params: PageParams): Promise<Page<NormalizeTaskRow>> {
    const { page = 1, rows = this.pagination.pageSize, ...searchValues } = params
    this.loading.value = true
    try {
      const res = await fetchNormalizeTasks({
        enterpriseCode: (searchValues.EnterpriseCode as string) || null,
        // ★ 阶段不进搜索栏：任务号已经能唯一定位，多一个筛选只增加「筛完发现没数据」的困惑
        stageCode: null,
        status: (searchValues.Status as string) || null,
        page,
        rows,
      })

      this.pagination.page = page
      this.pagination.pageSize = rows
      this.pagination.total = res.Total
      this.rows.value = res.Rows

      // ★ 只在「有未完成任务」时挂定时器（见 scheduleAutoRefresh）
      this.scheduleAutoRefresh()

      // ⚠️ PascalCase → camelCase（⛔ 不能直接 return res，否则表格恒空且零报错）
      return { rows: res.Rows, total: res.Total }
    } finally {
      this.loading.value = false
    }
  }

  // ══════════════════════════════════════════════════════════════════════
  // 自动刷新（★ 三条约束：只在有运行时 / 封顶 / 卸载即停）
  // ══════════════════════════════════════════════════════════════════════

  /**
   * 按当前页数据决定要不要挂自动刷新。
   *
   * ⛔ 三件事不做：
   *   ① 没有未完成任务时**不挂**（白耗接口 + 无意义重渲染）
   *   ② 已有定时器时**不重复挂**（否则每次 dataLoader 都会多一个定时器）
   *   ③ 不显示「预计还要多久」—— 剩余时间只能靠编，编了就是骗人
   */
  private scheduleAutoRefresh(): void {
    const hasRunning = this.rows.value.some((r) => r.IsFinished !== true)
    if (!hasRunning) {
      this.stopAutoRefresh()
      return
    }
    if (this.pollTimer !== null) return

    this.pollTicks = 0
    this.autoRefreshing.value = true
    this.pollTimer = window.setInterval(() => {
      this.pollTicks += 1
      if (this.pollTicks >= POLL_MAX_TICKS) {
        this.stopAutoRefresh()
        ElMessage.warning(
          '已连续自动刷新 10 分钟，可能仍有任务在后台执行；可点「刷新」查看最新进度，或到「队列监控」查看',
        )
        return
      }
      void this.refresh()
    }, POLL_INTERVAL_MS)
  }

  /** 停止自动刷新（幂等）—— 页面卸载、手动停、无未完成任务时调用 */
  stopAutoRefresh(): void {
    if (this.pollTimer !== null) {
      window.clearInterval(this.pollTimer)
      this.pollTimer = null
    }
    this.pollTicks = 0
    this.autoRefreshing.value = false
  }

  // ══════════════════════════════════════════════════════════════════════
  // 动作
  // ══════════════════════════════════════════════════════════════════════

  constructor() {
    super()

    // ── 工具栏：开新任务 ──
    this.registerHandler('custom:create', () => {
      void router.push({ name: 'EnterpriseNormalizeCreate' })
    })

    // ── 工具栏：刷新（顺带重置自动刷新的计时上限） ──
    this.registerHandler('custom:refresh', async () => {
      this.pollTicks = 0
      await this.refresh()
    })

    // ── 行：审核（左树右表页，带任务号定位） ──
    this.registerHandler('custom:review', (row) => {
      if (!row) return
      void router.push({ name: 'EnterpriseReview', query: { queueCode: row.QueueCode } })
    })

    // ── 行：重新执行（带 ?from= 进向导，预填企业 / 阶段 / 文档） ──
    this.registerHandler('custom:rerun', (row) => {
      if (!row) return
      void router.push({ name: 'EnterpriseNormalizeCreate', query: { from: row.QueueCode } })
    })

    // ── 行：取消未完成任务 ──
    this.registerHandler('custom:cancel', async (row) => {
      if (!row) return
      const ok = await confirmOrFalse(
        `取消任务 ${row.QueueCode}？未跑完的文档会被终止（已完成的保留产物与账本）。\n\n` +
          `取消后该「企业 + 阶段」才能重新开启规范化任务。`,
        '取消任务',
        { type: 'warning', confirmButtonText: '确认取消', cancelButtonText: '继续执行' },
      )
      if (!ok) return

      const r = await cancelNormalize(row.QueueCode)
      let msg = `已取消任务 ${row.QueueCode}（终止 ${r.CancelledCount} 个文档）`
      // ★ AuditWarning 非空 = 取消已生效但**审计链断了** —— 必须显式提示，⛔ 不能吞
      if (r.AuditWarning) msg += `；⚠️ ${r.AuditWarning}`
      ElMessage.success(msg)
      this.pollTicks = 0
      await this.refresh()
    })
  }
}

export default NormalizeTaskListLogic
