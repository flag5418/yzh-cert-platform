<script setup lang="ts">
/**
 * 任务详情（专家端 · 任务系统第 3 页 · 路由 `/tasks/:code`）
 *
 * 5 个 Tab（用户裁决的「任务导航 + 队列启动 + 多埋点日志」三条全在这里）：
 *   ① 标准子任务 —— 1 任务跨 N 标准，看每个标准各自跑到哪
 *   ② 执行队列 —— **队列的启动/暂停在这里**（★ 高级用法：允许分批跑）
 *   ③ 运行日志 —— 埋点全量可见（含跳过原因、失败原因），支持按队列过滤 + 自动刷新
 *   ④ 补录清单 —— 依赖的企业资料缺失项（专家据此补录或跳过）
 *   ⑤ 未执行清单 —— ★ 2026-10-07 裁 2：队列跑完后**逐条**列出哪些规则 / 条款没执行、为什么
 *
 * ★ 数据来源：`POST /detail` 一次性返回任务头 + 标准子任务 + 队列（Tab 1/2 不再各发一次请求）；
 *   日志 / 补录清单 / 未执行清单是**可能很大**的表，按 Tab 懒加载。
 *
 * ★ 所有「能不能点」都读后端视图字段（`CanSubmit` / `CanRetry` / `CanStart` / `CanPause`），
 *   ⛔ 前端不得自己按状态枚举判断。
 *
 * ★ 2026-10-07 用户裁决在本页的落点：
 *   · **主路径已搬到任务列表的「启动任务」** —— 本页保留的是**高级路径**：
 *     「只提交不启动」（分批跑）与队列级启停。所以 Tab 2 的按钮明确叫「启动队列」，
 *     ⛔ 不再和列表上的「启动任务」混为一谈。
 *   · **补录界面去掉「字段编码 / 表格编码」两列** —— 用户原话「全是字段的英文和看不懂的编号」。
 *     中文名权威来源 = 后台「文档提取规则」页，由后端 `GapLabelResolver` 解析后下发。
 *   · **表格型补录不再让用户手写 JSON** —— 改用「关键信息补录」抽屉里的可编辑表格。
 */
import { computed, nextTick, onActivated, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { Page, PageParams, YzhTableColumn } from '@yzh-core'
import { YzhStatusBadge, YzhTable, confirmOrFalse } from '@yzh-core'
import {
  batchFillGaps,
  fillGap,
  getGapChangeLogs,
  getGapList,
  getTaskDetail,
  getTaskLogs,
  getUnexecuted,
  pauseQueue,
  retryFailed,
  skipAllGaps,
  skipGap,
  startQueue,
  submitTask,
  type GapChangeLog,
  type TaskDetail,
  type TaskGap,
  type TaskGapList,
  type TaskLog,
  type TaskQueue,
  type TaskStandard,
  type TaskUnexecuted,
} from '@share/api/auditor/expert-task'
import {
  CHANGE_ACTION_MAP,
  CHANGE_ACTION_TAG,
  EXEC_STATUS_MAP,
  EXEC_STATUS_TAG,
  GAP_STATUS_MAP,
  GAP_STATUS_TAG,
  GAP_TYPE_MAP,
  LOG_ACTION_MAP,
  LOG_LEVEL_MAP,
  LOG_LEVEL_TAG,
  QUEUE_STATUS_MAP,
  QUEUE_STATUS_TAG,
  TASK_SOURCE_OPTIONS,
  VALUE_SOURCE_MAP,
  VALUE_SOURCE_TAG,
} from '@share/constants/expert-task'
import { formatDateTime } from '@share/utils/format'
import KeyInfoFillDrawer from './components/KeyInfoFillDrawer.vue'

const route = useRoute()
const router = useRouter()

const taskCode = computed(() => String(route.params.code ?? ''))

const loading = ref(false)
const detail = ref<TaskDetail | null>(null)
// ★ 2026-09-30：支持深链 `?tab=gaps`（结果页「去补录」按钮直接跳过来）
const TAB_NAMES = ['standards', 'queues', 'logs', 'gaps', 'unexecuted'] as const
const initialTab = String(route.query.tab ?? '')
const activeTab = ref(TAB_NAMES.includes(initialTab as never) ? initialTab : 'standards')

const task = computed(() => detail.value?.Task ?? null)

/** 任务来源中文（D25） */
const sourceLabel = computed(
  () => TASK_SOURCE_OPTIONS.find((o) => o.value === task.value?.TaskSource)?.label
    ?? task.value?.TaskSource
    ?? '—',
)

// ══════════════════════════════════════════════════════════════════════
// 一、加载
// ══════════════════════════════════════════════════════════════════════

/**
 * ★★ 4 个 YzhTable 的实例引用 —— **必须显式 refresh()**，原因是一个必踩的时序坑：
 *
 * <para>子组件的 `onMounted` 先于父组件执行 ⇒ `YzhTable` 挂载时立刻 `loadData()`，
 * 而此刻 `detail` / `logs` / `gaps` 都还是**空数组**（数据是异步填的），
 * 于是表格渲染成「暂无数据」并**再也不会自己重载**（`dataLoader` 是稳定函数引用，
 * 不构成 `watch` 依赖）。现象：接口明明返回了 1 条标准子任务，界面却是空的。</para>
 *
 * <para>对策：数据到位后主动 `refresh()`。`refresh` 由 `YzhTable` 的 `defineExpose` 暴露。</para>
 */
const standardTableRef = ref<any>(null)
const queueTableRef = ref<any>(null)
const logTableRef = ref<any>(null)
const gapFieldTableRef = ref<any>(null)
const gapTableTableRef = ref<any>(null)
const unexecutedTableRef = ref<any>(null)

/** 数据到位后统一刷新 6 张表（等待一帧，确保 v-if/v-for 已把数据渲染进 DOM） */
async function refreshTables() {
  await nextTick()
  standardTableRef.value?.refresh?.()
  queueTableRef.value?.refresh?.()
  logTableRef.value?.refresh?.()
  gapFieldTableRef.value?.refresh?.()
  gapTableTableRef.value?.refresh?.()
  unexecutedTableRef.value?.refresh?.()
}

async function loadDetail() {
  if (!taskCode.value) return
  loading.value = true
  try {
    detail.value = await getTaskDetail(taskCode.value)
    await refreshTables()
  } catch (e) {
    ElMessage.error((e as Error)?.message || '加载任务详情失败')
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await loadDetail()
  await loadLogs()
})

/**
 * 回到本页时重新拉数据。
 *
 * ⚠️ `AuditorLayout` 用 `<keep-alive>` 缓存路由组件 ⇒ 从别处返回时 `onMounted`
 *    ⛔ 不再触发（同一 `taskCode` 的 URL 复用同一个缓存实例）。
 *    ⚠️ 换任务（`/tasks/A` → `/tasks/B`）由布局的 `:key="route.fullPath"` 保证新建实例，
 *       但**同一个任务**跑完再回来必须靠这里刷新，否则看到的是旧进度。
 */
let activatedOnce = false
onActivated(async () => {
  if (!activatedOnce) {
    activatedOnce = true
    return
  }
  await loadDetail()
  if (logs.value.length > 0) await loadLogs()
  if (gapsLoaded.value) await loadGaps()
  if (unexecutedLoaded.value) await loadUnexecuted()
})

// ══════════════════════════════════════════════════════════════════════
// 二、Tab 1 · 标准子任务（本地数据，不发请求）
// ══════════════════════════════════════════════════════════════════════

const standardColumns: YzhTableColumn<TaskStandard>[] = [
  { prop: 'StandardName', label: '标准', minWidth: 200 },
  { prop: 'ItemCount', label: '检查项', width: 90, align: 'center' },
  { prop: 'DoneCount', label: '已完成', width: 90, align: 'center' },
  { prop: 'FailedCount', label: '失败', width: 80, align: 'center' },
  { prop: 'SkippedCount', label: '跳过', width: 80, align: 'center' },
  { prop: 'Progress', label: '进度', width: 160, slot: true },
  { prop: 'ExecStatus', label: '执行状态', width: 110, slot: true },
  { prop: 'QueueStatus', label: '队列状态', width: 110, slot: true },
]

async function standardLoader(params: PageParams): Promise<Page<TaskStandard>> {
  const all = detail.value?.Standards ?? []
  const page = params.page ?? 1
  const rows = params.rows ?? 20
  return { rows: all.slice((page - 1) * rows, page * rows), total: all.length }
}

// ══════════════════════════════════════════════════════════════════════
// 三、Tab 2 · 执行队列（启动 / 暂停）
// ══════════════════════════════════════════════════════════════════════

const queueColumns: YzhTableColumn<TaskQueue>[] = [
  { prop: 'StandardName', label: '标准', minWidth: 180 },
  { prop: 'QueueStatus', label: '队列状态', width: 110, slot: true },
  { prop: 'TotalCount', label: '总数', width: 80, align: 'center' },
  { prop: 'DoneCount', label: '完成', width: 80, align: 'center' },
  { prop: 'FailedCount', label: '失败', width: 80, align: 'center' },
  { prop: 'SkippedCount', label: '跳过', width: 80, align: 'center' },
  { prop: 'Progress', label: '进度', width: 160, slot: true },
  { prop: 'RetryCount', label: '重试', width: 80, align: 'center' },
  { prop: 'StartTime', label: '开始时间', width: 170, slot: true },
  { prop: 'FinishTime', label: '结束时间', width: 170, slot: true },
  { prop: 'LastError', label: '最后错误', minWidth: 200, showOverflowTooltip: true },
]

async function queueLoader(params: PageParams): Promise<Page<TaskQueue>> {
  const all = detail.value?.Queues ?? []
  const page = params.page ?? 1
  const rows = params.rows ?? 20
  return { rows: all.slice((page - 1) * rows, page * rows), total: all.length }
}

/**
 * 行按钮：由 `CanStart` / `CanPause` 决定（后端算，前端不判）。
 *
 * ★ 文案刻意用「启动队列 / 暂停队列」而不是「启动」——
 *   列表页那个按钮叫「启动任务」（一次做完整条流程），
 *   这里是**队列级**的高级操作（分批跑），名字必须能区分开。
 */
function queueRowActions(row: TaskQueue) {
  const list: Array<{ key: string; text: string; type?: 'primary' | 'default' }> = []
  if (row.CanStart) list.push({ key: 'start', text: '启动队列', type: 'primary' })
  if (row.CanPause) list.push({ key: 'pause', text: '暂停队列', type: 'default' })
  return list
}

async function onQueueAction(key: string, row: TaskQueue) {
  if (key === 'start') {
    await startQueue(row.Code)
    ElMessage.success(`已启动队列（${row.StandardName}）`)
  } else if (key === 'pause') {
    const ok = await confirmOrFalse(
      `确定暂停【${row.StandardName}】的队列？正在执行的项会跑完当前这一条。`,
      '暂停队列',
      { confirmButtonText: '确定暂停' },
    )
    if (!ok) return
    await pauseQueue(row.Code)
    ElMessage.success('队列已暂停')
  }
  await loadDetail()
  await loadLogs()
}

/** 批量启动全部「待启动」队列（用户「允许分批跑」的反面：一键跑完） */
async function startAllPending() {
  const pending = (detail.value?.Queues ?? []).filter((q) => q.CanStart)
  if (pending.length === 0) {
    ElMessage.warning('没有待启动的队列')
    return
  }
  const ok = await confirmOrFalse(
    `将启动 ${pending.length} 个队列（${pending.map((q) => q.StandardName).join('、')}）。\n\n确定？`,
    '启动全部队列',
    { confirmButtonText: '全部启动' },
  )
  if (!ok) return
  for (const q of pending) {
    await startQueue(q.Code)
  }
  ElMessage.success(`已启动 ${pending.length} 个队列`)
  await loadDetail()
  await loadLogs()
}

// ══════════════════════════════════════════════════════════════════════
// 四、Tab 3 · 运行日志
// ══════════════════════════════════════════════════════════════════════

const logs = ref<TaskLog[]>([])
const logsLoading = ref(false)
const logQueueCode = ref<string>('')
const autoRefresh = ref(false)

const logColumns: YzhTableColumn<TaskLog>[] = [
  { prop: 'OperateTime', label: '时间', width: 170, slot: true },
  { prop: 'LogLevel', label: '级别', width: 80, slot: true },
  { prop: 'LogAction', label: '动作', width: 150, slot: true },
  { prop: 'Message', label: '说明', minWidth: 280, showOverflowTooltip: true },
  { prop: 'ItemName', label: '检查项 / 章节', minWidth: 200, showOverflowTooltip: true },
  { prop: 'QueueCode', label: '队列', width: 150, slot: true },
  { prop: 'StandardCode', label: '标准', width: 150, slot: true },
  { prop: 'OperatorName', label: '操作人', width: 110 },
  { prop: 'DurationMs', label: '耗时(ms)', width: 100, align: 'right' },
]

async function loadLogs() {
  if (!taskCode.value) return
  logsLoading.value = true
  try {
    logs.value = await getTaskLogs(taskCode.value, logQueueCode.value || null, 300)
    await refreshTables()
  } catch (e) {
    ElMessage.error((e as Error)?.message || '加载日志失败')
  } finally {
    logsLoading.value = false
  }
}

async function logLoader(params: PageParams): Promise<Page<TaskLog>> {
  const page = params.page ?? 1
  const rows = params.rows ?? 20
  return {
    rows: logs.value.slice((page - 1) * rows, page * rows),
    total: logs.value.length,
  }
}

/** 标准 Code → 标准名（详情里已有映射，避免日志列显示 GUID） */
function standardNameOf(code: string | null | undefined): string {
  if (!code) return '—'
  return (
    detail.value?.Standards.find((s) => s.StandardCode === code)?.StandardName ?? code
  )
}

/** 队列 Code → 标准名 */
function queueNameOf(code: string | null | undefined): string {
  if (!code) return '—'
  const q = detail.value?.Queues.find((x) => x.Code === code)
  return q ? q.StandardName : code
}

// 自动刷新（执行中盯日志用）
let logTimer: number | null = null

function syncAutoRefresh(on: boolean) {
  if (logTimer !== null) {
    clearInterval(logTimer)
    logTimer = null
  }
  if (on) {
    logTimer = window.setInterval(() => {
      void loadLogs()
      void loadDetail()
    }, 5000)
  }
}

watch(autoRefresh, (on) => syncAutoRefresh(on))

// ══════════════════════════════════════════════════════════════════════
// 五、Tab 4 · 补录清单（★ 2026-09-30 裁决 J1：两张扁平表，不分文档）
// ══════════════════════════════════════════════════════════════════════
//
// ★ 与旧版的 3 处差异：
//  ① 记录粒度 = 1 条 = 1 个待补字段/表格（不再是「字段 × 依赖它的规则」）
//  ② ⛔ 不按文档分组、不做文档树 → 拆成**字段表** + **表格表**
//     `ExpectedFileName` / `StandardFileCode` 降为**只读提示列**
//  ③ 「影响 N 条规则」由后端**实时反查**工作流 DAG（⛔ 不落库）
//
// ★ 补录去向：`cert_extraction_result` / `cert_table_extraction_result`，
//   `ValueSource='manual'`（裁决 J2）。⛔ 不建独立补录表。
// ★ 补录**可跳过**（客户可以忽略）—— 提交不阻断，门禁在执行期。

const gapList = ref<TaskGapList>({ Fields: [], Tables: [], PendingCount: 0, FilledCount: 0, SkippedCount: 0 })
const gapsLoading = ref(false)
const gapsLoaded = ref(false)
/** 行内编辑中的值：gapCode → 值 */
const editing = ref<Record<string, string>>({})
const savingCode = ref<string>('')

// ★ 2026-10-07：删掉「字段编码 / 表格编码」两列 —— 用户原话「全是字段的英文和看不懂的编号」。
//   中文名由后端 `GapLabelResolver` 三级解析后放进 `GapLabel`，界面⛔ 不再暴露 Code。
const gapFieldColumns: YzhTableColumn<TaskGap>[] = [
  { prop: 'GapLabel', label: '字段', minWidth: 220, slot: true },
  { prop: 'ExpectedFileName', label: '应来自', minWidth: 170, showOverflowTooltip: true },
  { prop: 'ImpactedItemCount', label: '影响检查项', width: 110, slot: true },
  { prop: 'GapStatus', label: '状态', width: 90, slot: true },
  { prop: '操作', label: '操作', width: 230, slot: true },
]

const gapTableColumns: YzhTableColumn<TaskGap>[] = [
  { prop: 'GapLabel', label: '表格', minWidth: 200, slot: true },
  { prop: 'ExpectedFileName', label: '应来自', minWidth: 170, showOverflowTooltip: true },
  { prop: 'ImpactedItemCount', label: '影响检查项', width: 110, slot: true },
  { prop: 'GapStatus', label: '状态', width: 90, slot: true },
  { prop: '操作', label: '操作', width: 190, slot: true },
]

async function loadGaps() {
  if (!taskCode.value) return
  gapsLoading.value = true
  try {
    gapList.value = await getGapList(taskCode.value)
    gapsLoaded.value = true
    editing.value = {}
    await refreshTables()
  } catch (e) {
    ElMessage.error((e as Error)?.message || '加载补录清单失败')
  } finally {
    gapsLoading.value = false
  }
}

// ── 补录 ──────────────────────────────────────────────

/** 「影响 N 条规则」的人话（数据来自实时反查，不是落库的 SourceItemCode） */
function impactText(row: TaskGap): string {
  const n = row.ImpactedItemCount ?? 0
  if (n === 0) return '—'
  if (n === 1) return row.ImpactedItems[0]?.ItemName || '1 条'
  return `影响 ${n} 条检查项`
}

/**
 * 字段型补录：单行输入 → 直接保存。
 *
 * ⚠️ 表格型**不走这里** —— 它改用「关键信息补录」抽屉的可编辑表格
 *    （旧实现要求用户手写 `[{"列名":"值"}]`，用户明确反馈看不懂）。
 */
async function doFill(row: TaskGap) {
  const value = (editing.value[row.Code] ?? '').trim()
  if (!value) {
    ElMessage.warning('请输入补录值')
    return
  }

  savingCode.value = row.Code
  try {
    const r = await fillGap(row.Code, value)
    ElMessage.success(`已补录「${row.GapLabel}」`)
    delete editing.value[row.Code]
    await loadGaps()
    await loadDetail()

    // ★ 只重跑受影响的项，不重跑全部（一个任务几十条，全跑一次可能几十分钟）
    if (r.ImpactedItemCodes.length > 0) {
      const ok = await confirmOrFalse(
        `已补录 ${r.ImpactedItemCodes.length} 项数据，影响 ${r.ImpactedItemCodes.length} 条检查项。是否立即重跑这些项？`,
        '补录成功',
        { confirmButtonText: '立即重跑', cancelButtonText: '稍后', type: 'info' },
      )
      if (ok) {
        await retryFailed(taskCode.value)
        ElMessage.success('已重置为待执行，请在「执行队列」中启动')
      }
    }
  } catch (e) {
    ElMessage.error((e as Error)?.message || '补录失败')
  } finally {
    savingCode.value = ''
  }
}

// ── 表格型补录：复用「关键信息补录」抽屉（★ 2026-10-07）─────────────
//
// ★ 为什么复用而不是再写一套：抽屉已经把「列定义 → 中文列头 → 可编辑小表格」
//   做完了；这里只是把范围收窄成**单张表**（`Fields: []` + `Tables: [row]`）。
//   ⛔ 不复制 UI、不复制校验逻辑。

const tableGapVisible = ref(false)
const tableGapList = ref<TaskGapList | null>(null)
const tableGapSaving = ref(false)

function openTableFill(row: TaskGap) {
  tableGapList.value = {
    Fields: [],
    Tables: [row],
    PendingCount: 1,
    FilledCount: 0,
    SkippedCount: 0,
  }
  tableGapVisible.value = true
}

async function onTableGapFill(items: { GapCode: string; Value: string }[]) {
  tableGapSaving.value = true
  try {
    await batchFillGaps(items)
    ElMessage.success('已补录')
    tableGapVisible.value = false
    await loadGaps()
    await loadDetail()
  } catch (e) {
    ElMessage.error((e as Error)?.message || '补录失败')
  } finally {
    tableGapSaving.value = false
  }
}

async function onTableGapSkip() {
  const row = tableGapList.value?.Tables[0]
  tableGapVisible.value = false
  if (!row) return
  await doSkip(row)
}

async function doSkip(row: TaskGap) {
  const ok = await confirmOrFalse(
    `跳过「${row.GapLabel}」后，依赖它的检查项将标记为「数据不足，未检查」，不会产生审核结果，且不计入「符合」数量。\n\n` +
      `任务跑完后可在「未执行清单」里看到这条原因。`,
    '确认跳过',
    { confirmButtonText: '确认跳过', cancelButtonText: '取消', type: 'warning' },
  )
  if (!ok) return
  try {
    await skipGap(row.Code)
    ElMessage.success('已跳过')
    await loadGaps()
    await loadDetail()
  } catch (e) {
    ElMessage.error((e as Error)?.message || '跳过失败')
  }
}

async function doSkipAll() {
  if (gapList.value.PendingCount === 0) return
  const ok = await confirmOrFalse(
    `⚠ 确认跳过全部 ${gapList.value.PendingCount} 项待补录数据？\n\n` +
      `跳过后，依赖这些数据的检查项将标记为「数据不足，未检查」，不会产生审核结果，` +
      `导出报告时结论列显示「数据不足，未检查」。\n\n这些检查项不会计入「符合」数量。`,
    '确认跳过全部',
    { confirmButtonText: '确认跳过', cancelButtonText: '取消', type: 'warning' },
  )
  if (!ok) return
  try {
    const n = await skipAllGaps(taskCode.value)
    ElMessage.success(`已跳过 ${n} 项`)
    await loadGaps()
    await loadDetail()
  } catch (e) {
    ElMessage.error((e as Error)?.message || '批量跳过失败')
  }
}

// ── 补录留痕抽屉 ──────────────────────────────────────

const logDrawerVisible = ref(false)
const changeLogs = ref<GapChangeLog[]>([])
const changeLogsLoading = ref(false)

async function openChangeLogs() {
  logDrawerVisible.value = true
  changeLogsLoading.value = true
  try {
    changeLogs.value = await getGapChangeLogs(taskCode.value)
  } catch (e) {
    ElMessage.error((e as Error)?.message || '加载补录留痕失败')
  } finally {
    changeLogsLoading.value = false
  }
}

const changeActionTag = (a: string) => CHANGE_ACTION_TAG[a] ?? 'info'

// ★ 不可变表（只 INSERT/SELECT）—— 铁律：不 UPDATE / 不 DELETE
const changeLogColumns: YzhTableColumn<GapChangeLog>[] = [
  { prop: 'OperateTime', label: '时间', width: 160 },
  { prop: 'ChangeAction', label: '动作', width: 130 },
  { prop: 'Target', label: '对象', minWidth: 160, showOverflowTooltip: true },
  { prop: 'NewValueSource', label: '新值来源', width: 100 },
  { prop: 'Diff', label: '旧值 → 新值', minWidth: 240, showOverflowTooltip: true },
  { prop: 'OperatorName', label: '操作人', width: 100 },
]

async function changeLogLoader(params: PageParams): Promise<Page<GapChangeLog>> {
  const page = params.page ?? 1
  const rows = params.rows ?? 20
  const all = changeLogs.value
  return { rows: all.slice((page - 1) * rows, page * rows), total: all.length }
}

// ── 分页数据源 ────────────────────────────────────────

async function gapFieldLoader(params: PageParams): Promise<Page<TaskGap>> {
  const page = params.page ?? 1
  const rows = params.rows ?? 20
  const all = gapList.value.Fields
  return { rows: all.slice((page - 1) * rows, page * rows), total: all.length }
}

async function gapTableLoader(params: PageParams): Promise<Page<TaskGap>> {
  const page = params.page ?? 1
  const rows = params.rows ?? 20
  const all = gapList.value.Tables
  return { rows: all.slice((page - 1) * rows, page * rows), total: all.length }
}

watch(activeTab, async (tab) => {
  if (tab === 'gaps' && !gapsLoaded.value) await loadGaps()
  if (tab === 'logs' && logs.value.length === 0) await loadLogs()
  if (tab === 'unexecuted' && !unexecutedLoaded.value) await loadUnexecuted()
})

// ══════════════════════════════════════════════════════════════════════
// 五之二、Tab 5 · 未执行清单（★ 2026-10-07 裁 2「运行跳过」）
// ══════════════════════════════════════════════════════════════════════
//
// 用户逐字要求：「运行跳过，针对工作流缺失关键信息的，该工作流不执行，
//   再队列完成后，详细记录，哪些规则或条款未执行成功，什么原因」
//
// ★ 为什么必须有这个出口（这是本轮最重要的「防静默」改动）：
//   `ExpertTaskQueueRunner.RefreshQueueAsync` 在「全部项 skipped、failed = 0」时
//   会把队列置为 `completed` —— **报告看起来跑完了，实际有检查项根本没做**，
//   而且完全静默。本 Tab 就是把这个静默缺口显式摊开。
//
// ★ 粒度是「规则 / 条款」而不是「缺口」：用户要的是「哪条没跑成」，
//   一条规则可能同时缺 3 个字段，按缺口罗列会读不出结论。

const unexecuted = ref<TaskUnexecuted[]>([])
const unexecutedMeta = ref<{ TotalCount: number; DataGapCount: number; IsQueueFinished: boolean }>({
  TotalCount: 0,
  DataGapCount: 0,
  IsQueueFinished: false,
})
const unexecutedLoading = ref(false)
const unexecutedLoaded = ref(false)

const unexecutedColumns: YzhTableColumn<TaskUnexecuted>[] = [
  { prop: 'ItemName', label: '规则 / 章节', minWidth: 240, showOverflowTooltip: true },
  { prop: 'StandardCode', label: '标准', width: 170, slot: true },
  { prop: 'ClauseCode', label: '条款', width: 110, slot: true },
  { prop: 'SkipCategoryLabel', label: '未执行原因', width: 130, slot: true },
  { prop: 'Reason', label: '说明', minWidth: 240, showOverflowTooltip: true },
  { prop: 'MissingItems', label: '具体缺什么', minWidth: 260, slot: true },
]

async function loadUnexecuted() {
  if (!taskCode.value) return
  unexecutedLoading.value = true
  try {
    const r = await getUnexecuted(taskCode.value)
    unexecuted.value = r.Items
    unexecutedMeta.value = {
      TotalCount: r.TotalCount,
      DataGapCount: r.DataGapCount,
      IsQueueFinished: r.IsQueueFinished,
    }
    unexecutedLoaded.value = true
    await refreshTables()
  } catch (e) {
    ElMessage.error((e as Error)?.message || '加载未执行清单失败')
  } finally {
    unexecutedLoading.value = false
  }
}

async function unexecutedLoader(params: PageParams): Promise<Page<TaskUnexecuted>> {
  const page = params.page ?? 1
  const rows = params.rows ?? 20
  const all = unexecuted.value
  return { rows: all.slice((page - 1) * rows, page * rows), total: all.length }
}

/** 未执行分类 → 徽标语义（后端给中文名，前端只挑配色） */
function skipBadgeType(category: string): 'success' | 'warning' | 'danger' | 'info' {
  if (category === 'exec_failed') return 'danger'
  if (category === 'data_gap' || category === 'data_gap_skipped') return 'warning'
  return 'info'
}

// ══════════════════════════════════════════════════════════════════════
// 六、任务级动作（提交执行 / 重试失败 / 看结果）
// ══════════════════════════════════════════════════════════════════════

/**
 * 任务级动作（★ 本页保留的是**高级路径**）。
 *
 * ★ 为什么还留着「仅生成队列」：把「提交」与「启动」合并进列表的「启动任务」后，
 *   会跳过 `pending_run` 这一跳（`27-任务启动流程重构方案` §五 坑 2）。
 *   分批跑 / 先检查范围再跑这类能力**必须有独立入口**，否则能力丢失。
 *   所以列表 = 一步到底（日常），本页 = 拆开跑（高级），两者文案必须能区分。
 */
const submitting = ref(false)

async function onSubmit() {
  const t = task.value
  if (!t) return
  const ok = await confirmOrFalse(
    `将为「${t.StandardCount} 个标准」生成执行队列，共 ${t.TotalItemCount} 个检查项。\n\n` +
      '⚠ 这一步**只生成队列、不启动**。生成后到「执行队列」逐个启动（可分批跑）。\n' +
      '若想一步跑完，回任务列表点「启动任务」即可。\n\n确定生成？',
    '仅生成执行队列',
    { confirmButtonText: '确定生成' },
  )
  if (!ok) return
  submitting.value = true
  try {
    const r = await submitTask(t.Code)
    if (r.PendingGapCount > 0) {
      ElMessageBox.alert(
        `${r.Warning ?? `有 ${r.PendingGapCount} 项数据未补录`}\n\n` +
          `这些检查项执行时会标记为「数据不足，未检查」，不会产生审核结果。\n` +
          `可现在去「补录清单」处理，或先执行、稍后再补。\n` +
          `跑完后可在「未执行清单」里逐条看到是哪些规则 / 条款没执行、为什么。`,
        '已生成队列，但有数据待补录',
        { confirmButtonText: '知道了' },
      ).catch(() => undefined)
    } else {
      ElMessage.success(r.Message || '已生成执行队列')
    }
    await loadDetail()
    if (!gapsLoaded.value) await loadGaps()
  } finally {
    submitting.value = false
  }
}

async function onRetry() {
  const t = task.value
  if (!t) return
  const ok = await confirmOrFalse(
    `将把【${t.TaskName}】的失败项重置为「待执行」，已成功/已跳过的项不受影响。\n\n确定重试？`,
    '重试失败项',
    { confirmButtonText: '确定重试' },
  )
  if (!ok) return
  await retryFailed(t.Code)
  ElMessage.success('失败项已重置')
  await loadDetail()
  await loadLogs()
}

function goResult() {
  const t = task.value
  if (!t) return
  const name = t.TaskType === 'REPORT_GENERATE' ? 'ReportResults' : 'NcResults'
  void router.push({ name, query: { task: t.Code } })
}
</script>

<template>
  <div v-loading="loading" class="td">
    <!-- ════ 头部：任务摘要 + 任务级动作 ════ -->
    <div class="td__head">
      <div class="td__head-left">
        <el-button link @click="router.push({ name: 'Tasks' })">← 返回任务列表</el-button>
        <div class="td__name">
          <span class="td__number">{{ task?.TaskNumber }}</span>
          <span>{{ task?.TaskName }}</span>
          <el-tag
            v-if="task"
            :type="EXEC_STATUS_TAG[task.ExecStatus] ?? 'info'"
            size="small"
            class="td__status"
          >
            {{ task.ExecStatusLabel }}
          </el-tag>
          <el-tag v-if="task?.IsBlocking" type="warning" size="small" effect="plain">
            占用业务锁
          </el-tag>
        </div>
      </div>
      <div class="td__head-right">
        <el-button v-if="task?.CanSubmit" type="primary" :loading="submitting" @click="onSubmit">
          仅生成队列
        </el-button>
        <el-button v-if="task?.CanRetry" type="default" @click="onRetry">重试失败项</el-button>
        <el-button v-if="task?.CanViewResult" type="default" plain @click="goResult">
          查看结果
        </el-button>
      </div>
    </div>

    <!-- ════ 摘要 ════ -->
    <el-descriptions v-if="task" :column="4" border size="small" class="td__desc">
      <el-descriptions-item label="企业">{{ detail?.EnterpriseName }}</el-descriptions-item>
      <el-descriptions-item label="认证阶段">{{ detail?.StageName }}</el-descriptions-item>
      <el-descriptions-item label="分类">{{ task.TaskTypeLabel }}</el-descriptions-item>
      <el-descriptions-item label="来源">{{ sourceLabel }}</el-descriptions-item>
      <el-descriptions-item label="标准数">{{ task.StandardCount }}</el-descriptions-item>
      <el-descriptions-item label="检查项">{{ task.TotalItemCount }}</el-descriptions-item>
      <el-descriptions-item label="进度">
        {{ Number(task.Progress ?? 0).toFixed(0) }}%
      </el-descriptions-item>
      <el-descriptions-item label="待审批结论">
        {{ detail?.PendingReviewCount ?? 0 }} / {{ detail?.TotalResultCount ?? 0 }}
      </el-descriptions-item>
      <el-descriptions-item label="创建人">{{ task.CreateName || '—' }}</el-descriptions-item>
      <el-descriptions-item label="创建时间">
        {{ formatDateTime(task.CreateTime) }}
      </el-descriptions-item>
      <el-descriptions-item label="提交时间">
        {{ task.SubmitTime ? formatDateTime(task.SubmitTime) : '—' }}
      </el-descriptions-item>
      <el-descriptions-item label="结束时间">
        {{ task.FinishTime ? formatDateTime(task.FinishTime) : '—' }}
      </el-descriptions-item>
      <el-descriptions-item label="备注" :span="4">{{ task.Remark || '—' }}</el-descriptions-item>
    </el-descriptions>

    <!-- ════ 4 个 Tab ════ -->
    <el-tabs v-model="activeTab" class="td__tabs">
      <!-- ── Tab 1 ── -->
      <el-tab-pane label="标准子任务" name="standards">
        <div class="td__pane">
          <YzhTable
            ref="standardTableRef"
            :columns="standardColumns"
            :data-loader="standardLoader"
            :show-pagination="false"
            :toolbar="false"
            empty-text="暂无标准子任务"
          >
            <template #column-Progress="{ row }">
              <el-progress
                :percentage="Math.min(100, Math.max(0, Number(row.Progress ?? 0)))"
                :stroke-width="12"
              />
            </template>
            <template #column-ExecStatus="{ row }">
              <el-tag :type="EXEC_STATUS_TAG[row.ExecStatus] ?? 'info'" size="small">
                {{ EXEC_STATUS_MAP[row.ExecStatus] ?? row.ExecStatus }}
              </el-tag>
            </template>
            <template #column-QueueStatus="{ row }">
              <el-tag v-if="row.QueueStatus" :type="QUEUE_STATUS_TAG[row.QueueStatus] ?? 'info'" size="small">
                {{ QUEUE_STATUS_MAP[row.QueueStatus] ?? row.QueueStatus }}
              </el-tag>
              <span v-else>—</span>
            </template>
          </YzhTable>
        </div>
      </el-tab-pane>

      <!-- ── Tab 2 · ★ 队列级操作（高级用法：分批跑）── -->
      <el-tab-pane label="执行队列（分批）" name="queues">
        <div class="td__pane">
          <YzhTable
            ref="queueTableRef"
            :columns="queueColumns"
            :data-loader="queueLoader"
            :row-action-buttons="queueRowActions"
            :show-pagination="false"
            empty-text="还没有队列 —— 回任务列表点「启动任务」即可生成并启动"
            @row-action="onQueueAction"
          >
            <template #toolbar-left>
              <el-button size="small" type="primary" @click="startAllPending">
                启动全部待启动队列
              </el-button>
              <el-button size="small" type="default" @click="loadDetail()">刷新</el-button>
            </template>

            <template #column-QueueStatus="{ row }">
              <el-tag :type="QUEUE_STATUS_TAG[row.QueueStatus] ?? 'info'" size="small">
                {{ QUEUE_STATUS_MAP[row.QueueStatus] ?? row.QueueStatus }}
              </el-tag>
            </template>
            <template #column-Progress="{ row }">
              <el-progress
                :percentage="Math.min(100, Math.max(0, Number(row.Progress ?? 0)))"
                :stroke-width="12"
                :status="row.QueueStatus === 'failed' ? 'exception' : undefined"
              />
            </template>
            <template #column-StartTime="{ row }">
              {{ row.StartTime ? formatDateTime(row.StartTime) : '—' }}
            </template>
            <template #column-FinishTime="{ row }">
              {{ row.FinishTime ? formatDateTime(row.FinishTime) : '—' }}
            </template>
          </YzhTable>
        </div>
      </el-tab-pane>

      <!-- ── Tab 3 ── -->
      <el-tab-pane label="运行日志" name="logs">
        <div class="td__pane">
          <div class="td__logbar">
            <span class="td__logbar-label">队列</span>
            <el-select
              v-model="logQueueCode"
              size="small"
              style="width: 220px"
              @change="loadLogs"
            >
              <el-option label="全部队列" value="" />
              <el-option
                v-for="q in detail?.Queues ?? []"
                :key="q.Code"
                :label="q.StandardName"
                :value="q.Code"
              />
            </el-select>
            <el-checkbox v-model="autoRefresh" size="small">自动刷新（5 秒）</el-checkbox>
            <el-button size="small" :loading="logsLoading" @click="loadLogs">刷新</el-button>
            <span class="td__logbar-count">共 {{ logs.length }} 条</span>
          </div>

          <YzhTable
            ref="logTableRef"
            :columns="logColumns"
            :data-loader="logLoader"
            :show-pagination="false"
            :toolbar="false"
            empty-text="暂无日志"
          >
            <template #column-OperateTime="{ row }">
              {{ formatDateTime(row.OperateTime) }}
            </template>
            <template #column-LogLevel="{ row }">
              <el-tag :type="LOG_LEVEL_TAG[row.LogLevel] ?? 'info'" size="small" effect="plain">
                {{ LOG_LEVEL_MAP[row.LogLevel] ?? row.LogLevel }}
              </el-tag>
            </template>
            <template #column-LogAction="{ row }">
              {{ LOG_ACTION_MAP[row.LogAction] ?? row.LogAction }}
            </template>
            <template #column-QueueCode="{ row }">
              {{ queueNameOf(row.QueueCode) }}
            </template>
            <template #column-StandardCode="{ row }">
              {{ standardNameOf(row.StandardCode) }}
            </template>
          </YzhTable>
        </div>
      </el-tab-pane>

      <!-- ── Tab 4 · 补录清单（★ 两张扁平表，不分文档）── -->
      <el-tab-pane name="gaps">
        <template #label>
          <span>
            补录清单
            <el-badge
              v-if="gapList.PendingCount > 0"
              :value="gapList.PendingCount"
              type="warning"
            />
          </span>
        </template>

        <div class="td__pane">
          <!-- 顶部：状态汇总 + 全局操作 -->
          <div class="gap__bar">
            <div class="gap__stats">
              <el-tag type="warning" size="small">待补录 {{ gapList.PendingCount }}</el-tag>
              <el-tag type="success" size="small">已补录 {{ gapList.FilledCount }}</el-tag>
              <el-tag type="info" size="small">已跳过 {{ gapList.SkippedCount }}</el-tag>
              <span class="gap__hint">
                ⓘ 缺这些数据的检查项<strong>不会执行</strong>（标「数据不足，未检查」，
                <strong>不计入「符合」</strong>）。跑完可在「未执行清单」看逐条原因。
              </span>
            </div>
            <div class="gap__actions">
              <el-button size="small" type="default" :disabled="!gapsLoaded" @click="openChangeLogs">
                补录留痕
              </el-button>
              <el-button
                size="small"
                type="default"
                plain
                :disabled="gapList.PendingCount === 0"
                @click="doSkipAll"
              >
                跳过全部（{{ gapList.PendingCount }}）
              </el-button>
            </div>
          </div>

          <!-- 表 1：字段清单 -->
          <div class="gap__block">
            <div class="gap__block-title">
              字段清单
              <el-tag size="small" type="info">{{ gapList.Fields.length }} 项</el-tag>
            </div>
            <YzhTable
              ref="gapFieldTableRef"
              :columns="gapFieldColumns"
              :data-loader="gapFieldLoader"
              :show-pagination="gapList.Fields.length > 20"
              :toolbar="false"
              empty-text="没有待补录的字段"
            >
              <!-- ★ 2026-10-07：中文名 + 未登记提示（⛔ 不再显示 `FieldCode`） -->
              <template #column-GapLabel="{ row }">
                <span>{{ row.GapLabel }}</span>
                <YzhStatusBadge
                  v-if="row.IsUnnamed"
                  type="warning"
                  text="未登记中文名"
                  class="gap__unnamed"
                />
              </template>
              <template #column-ImpactedItemCount="{ row }">
                <el-tooltip
                  v-if="row.ImpactedItemCount > 0"
                  placement="top"
                >
                  <template #content>
                    <div v-for="it in row.ImpactedItems" :key="it.ItemCode">
                      · {{ it.ItemName }}{{ it.ClauseCode ? `（${it.ClauseCode}）` : '' }}
                    </div>
                  </template>
                  <span class="gap__impact">{{ impactText(row) }}</span>
                </el-tooltip>
                <span v-else class="gap__muted">—</span>
              </template>
              <template #column-GapStatus="{ row }">
                <el-tag :type="GAP_STATUS_TAG[row.GapStatus] ?? 'info'" size="small">
                  {{ GAP_STATUS_MAP[row.GapStatus] ?? row.GapStatus }}
                </el-tag>
              </template>
              <template #column-操作="{ row }">
                <div v-if="row.GapStatus === 'pending'" class="gap__row-actions">
                  <el-input
                    v-model="editing[row.Code]"
                    size="small"
                    :placeholder="row.GapLabel"
                    :disabled="savingCode === row.Code"
                    @keyup.enter="doFill(row)"
                  />
                  <el-button
                    size="small"
                    type="primary"
                    :loading="savingCode === row.Code"
                    @click="doFill(row)"
                  >
                    保存
                  </el-button>
                  <el-button size="small" type="default" @click="doSkip(row)">跳过</el-button>
                </div>
                <span v-else-if="row.GapStatus === 'filled'" class="gap__done">
                  <el-tag :type="VALUE_SOURCE_TAG.manual" size="small">
                    {{ VALUE_SOURCE_MAP.manual }}
                  </el-tag>
                  <span v-if="row.FilledTime" class="gap__muted">
                    {{ formatDateTime(row.FilledTime) }}
                  </span>
                </span>
                <span v-else class="gap__muted">
                  {{ row.SkipReason || '已跳过' }}
                </span>
              </template>
            </YzhTable>
          </div>

          <!-- 表 2：表格清单（★ 2026-10-07：改用「关键信息补录」抽屉的可编辑表格，⛔ 不再手写 JSON） -->
          <div class="gap__block">
            <div class="gap__block-title">
              表格清单
              <el-tag size="small" type="info">{{ gapList.Tables.length }} 项</el-tag>
            </div>
            <YzhTable
              ref="gapTableTableRef"
              :columns="gapTableColumns"
              :data-loader="gapTableLoader"
              :show-pagination="gapList.Tables.length > 20"
              :toolbar="false"
              empty-text="没有待补录的表格"
            >
              <template #column-GapLabel="{ row }">
                <span>{{ row.GapLabel }}</span>
                <YzhStatusBadge
                  v-if="row.IsUnnamed"
                  type="warning"
                  text="未登记中文名"
                  class="gap__unnamed"
                />
              </template>
              <template #column-ImpactedItemCount="{ row }">
                <span class="gap__impact">{{ impactText(row) }}</span>
              </template>
              <template #column-GapStatus="{ row }">
                <el-tag :type="GAP_STATUS_TAG[row.GapStatus] ?? 'info'" size="small">
                  {{ GAP_STATUS_MAP[row.GapStatus] ?? row.GapStatus }}
                </el-tag>
              </template>
              <template #column-操作="{ row }">
                <div v-if="row.GapStatus === 'pending'" class="gap__row-actions">
                  <el-button size="small" type="primary" plain @click="openTableFill(row)">
                    填写
                  </el-button>
                  <el-button size="small" type="default" @click="doSkip(row)">跳过</el-button>
                </div>
                <span v-else-if="row.GapStatus === 'filled'" class="gap__done">
                  <el-tag :type="VALUE_SOURCE_TAG.manual" size="small">
                    {{ VALUE_SOURCE_MAP.manual }}
                  </el-tag>
                </span>
                <span v-else class="gap__muted">
                  {{ row.SkipReason || '已跳过' }}
                </span>
              </template>
            </YzhTable>
          </div>
        </div>
      </el-tab-pane>

      <!-- ── Tab 5 · 未执行清单（★ 2026-10-07 裁 2「运行跳过」）── -->
      <el-tab-pane name="unexecuted">
        <template #label>
          <span>
            未执行清单
            <el-badge
              v-if="unexecutedMeta.TotalCount > 0"
              :value="unexecutedMeta.TotalCount"
              type="warning"
            />
          </span>
        </template>

        <div class="td__pane">
          <div class="gap__bar">
            <div class="gap__stats">
              <YzhStatusBadge type="info" :text="`未执行 ${unexecutedMeta.TotalCount} 条`" />
              <YzhStatusBadge
                v-if="unexecutedMeta.DataGapCount > 0"
                type="warning"
                :text="`其中缺企业数据 ${unexecutedMeta.DataGapCount} 条`"
              />
              <YzhStatusBadge
                :type="unexecutedMeta.IsQueueFinished ? 'success' : 'warning'"
                :text="unexecutedMeta.IsQueueFinished ? '队列已结束' : '执行中，清单还会变化'"
              />
            </div>
            <div class="gap__actions">
              <el-button
                size="small"
                type="default"
                :loading="unexecutedLoading"
                @click="loadUnexecuted()"
              >
                刷新
              </el-button>
            </div>
          </div>

          <div class="gap__hint gap__hint--block">
            ⓘ 队列显示「已完成」<strong>不等于每条规则都跑过</strong> ——
            缺企业数据的工作流按裁决「运行跳过」，不执行但必须留痕。
            下面逐条列出<strong>哪些规则 / 条款没执行、什么原因</strong>。
          </div>

          <YzhTable
            ref="unexecutedTableRef"
            :columns="unexecutedColumns"
            :data-loader="unexecutedLoader"
            :show-pagination="unexecuted.length > 20"
            :toolbar="false"
            empty-text="所有规则 / 条款都已执行成功。"
          >
            <template #column-StandardCode="{ row }">
              {{ standardNameOf(row.StandardCode) }}
            </template>
            <template #column-ClauseCode="{ row }">
              <span v-if="row.ClauseCode">{{ row.ClauseCode }}</span>
              <span v-else class="gap__muted">—</span>
            </template>
            <template #column-SkipCategoryLabel="{ row }">
              <YzhStatusBadge :type="skipBadgeType(row.SkipCategory)" :text="row.SkipCategoryLabel" />
            </template>
            <template #column-MissingItems="{ row }">
              <div v-if="row.MissingItems?.length" class="gap__missing">
                <div v-for="(m, i) in row.MissingItems" :key="i" class="gap__missing-item">
                  · {{ m }}
                </div>
              </div>
              <span v-else class="gap__muted">—</span>
            </template>
          </YzhTable>
        </div>
      </el-tab-pane>

      <!-- 「关键信息补录」抽屉（★ 表格型补录复用，⛔ 不让用户手写 JSON） -->
      <KeyInfoFillDrawer
        v-model="tableGapVisible"
        mode="fill"
        :gap-list="tableGapList"
        :submitting="tableGapSaving"
        @fill="onTableGapFill"
        @skip="onTableGapSkip"
      />

      <!-- 补录留痕抽屉（cert_extraction_change_log · 不可变表，只追加） -->
      <el-drawer v-model="logDrawerVisible" title="补录留痕" size="720px">
        <el-alert
          type="info"
          :closable="false"
          show-icon
          title="留痕表只允许追加，不可修改"
          description="专家每次补录都会留一条记录；值相同也会记（这是有意的确认动作）。"
        />
        <YzhTable
          v-loading="changeLogsLoading"
          :columns="changeLogColumns"
          :data-loader="changeLogLoader"
          :show-pagination="changeLogs.length > 20"
          :toolbar="false"
          empty-text="还没有补录留痕"
        >
          <template #column-OperateTime="{ row }">
            {{ formatDateTime(row.OperateTime) }}
          </template>
          <template #column-ChangeAction="{ row }">
            <el-tag :type="changeActionTag(row.ChangeAction)" size="small">
              {{ CHANGE_ACTION_MAP[row.ChangeAction] ?? row.ChangeAction }}
            </el-tag>
          </template>
          <template #column-Target="{ row }">
            <el-tag size="small" type="info">{{ GAP_TYPE_MAP[row.ResultType] ?? row.ResultType }}</el-tag>
            <span class="gap__target">{{ row.FieldLabel || row.FieldCode || row.TableCode }}</span>
          </template>
          <template #column-NewValueSource="{ row }">
            <el-tag v-if="row.NewValueSource" :type="VALUE_SOURCE_TAG[row.NewValueSource] ?? 'info'" size="small">
              {{ VALUE_SOURCE_MAP[row.NewValueSource] ?? row.NewValueSource }}
            </el-tag>
            <span v-else class="gap__muted">—</span>
          </template>
          <template #column-Diff="{ row }">
            <span class="gap__muted">{{ row.OldValue || '（原无）' }}</span>
            <span class="gap__arrow">→</span>
            <span>{{ row.NewValue || '（已清除）' }}</span>
          </template>
        </YzhTable>
      </el-drawer>
    </el-tabs>
  </div>
</template>

<style scoped>
.td {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: var(--yzh-color-bg-container, #fff);
  border-radius: 4px;
  overflow: hidden;
}

.td__head {
  flex-shrink: 0;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding: 12px 16px 8px;
}

.td__head-left {
  min-width: 0;
}

.td__name {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  font-size: 15px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-top: 4px;
}

.td__number {
  font-family: monospace;
  font-size: 13px;
  color: var(--el-text-color-secondary);
  font-weight: 400;
}

.td__status {
  font-weight: 400;
}

.td__head-right {
  flex-shrink: 0;
  display: flex;
  gap: 8px;
}

.td__desc {
  flex-shrink: 0;
  margin: 0 16px 8px;
}

.td__tabs {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  padding: 0 16px;
}

.td__tabs :deep(.el-tabs__content) {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.td__tabs :deep(.el-tab-pane) {
  height: 100%;
}

.td__pane {
  height: 100%;
  min-height: 420px;
}

.td__logbar {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 4px 0 10px;
}

.td__logbar-label {
  font-size: 13px;
  color: var(--el-text-color-regular);
}

.td__logbar-count {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

/* ── Tab 4 · 补录清单（★ 两张扁平表） ── */
.gap__bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 8px 12px;
  margin-bottom: 10px;
  background: var(--el-fill-color-light);
  border-radius: 4px;
}

.gap__stats {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.gap__hint {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}

.gap__actions {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
}

.gap__block {
  margin-bottom: 18px;
}

.gap__block-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-bottom: 8px;
}

.gap__row-actions {
  display: flex;
  align-items: center;
  gap: 6px;
}

.gap__row-actions .el-textarea {
  flex: 1;
  min-width: 220px;
}

.gap__done {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.gap__impact {
  color: var(--el-color-primary);
  cursor: help;
}

.gap__muted {
  color: var(--el-text-color-secondary);
  font-size: var(--yzh-font-size-xs, 12px);
}

.gap__arrow {
  margin: 0 var(--yzh-space-1, 4px);
  color: var(--el-text-color-secondary);
}

/* ★ 2026-10-07 新增：中文名缺失提示 / 未执行清单 / 留痕对象 */
.gap__unnamed {
  margin-left: var(--yzh-space-1, 4px);
}

.gap__target {
  margin-left: var(--yzh-space-1, 4px);
}

.gap__hint--block {
  display: block;
  margin-bottom: var(--yzh-space-3, 12px);
  line-height: 1.7;
}

.gap__missing {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
}

.gap__missing-item {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-regular);
}
</style>
