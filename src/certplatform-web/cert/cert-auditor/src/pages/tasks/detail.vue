<script setup lang="ts">
/**
 * 任务详情（专家端 · 任务系统第 3 页 · 路由 `/tasks/:code`）
 *
 * 4 个 Tab（用户裁决的「任务导航 + 队列启动 + 多埋点日志」三条全在这里）：
 *   ① 标准子任务 —— 1 任务跨 N 标准，看每个标准各自跑到哪
 *   ② 执行队列 —— **队列的启动/暂停在这里**（提交与启动拆开，允许分批跑）
 *   ③ 运行日志 —— 埋点全量可见（含跳过原因、失败原因），支持按队列过滤 + 自动刷新
 *   ④ 数据缺口 —— 依赖的企业资料缺失项（专家据此补录或跳过）
 *
 * ★ 数据来源：`POST /detail` 一次性返回任务头 + 标准子任务 + 队列（Tab 1/2 不再各发一次请求）；
 *   日志与缺口是**可能很大**的两张表，按 Tab 懒加载。
 *
 * ★ 所有「能不能点」都读后端视图字段（`CanSubmit` / `CanRetry` / `CanStart` / `CanPause`），
 *   ⛔ 前端不得自己按状态枚举判断。
 */
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { Page, PageParams, YzhTableColumn } from '@yzh-core'
import { YzhTable, confirmOrFalse } from '@yzh-core'
import {
  fillGap,
  getGapChangeLogs,
  getGapList,
  getTaskDetail,
  getTaskLogs,
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

const route = useRoute()
const router = useRouter()

const taskCode = computed(() => String(route.params.code ?? ''))

const loading = ref(false)
const detail = ref<TaskDetail | null>(null)
// ★ 2026-09-30：支持深链 `?tab=gaps`（结果页「去补录」按钮直接跳过来）
const TAB_NAMES = ['standards', 'queues', 'logs', 'gaps'] as const
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

/** 数据到位后统一刷新 5 张表（等待一帧，确保 v-if/v-for 已把数据渲染进 DOM） */
async function refreshTables() {
  await nextTick()
  standardTableRef.value?.refresh?.()
  queueTableRef.value?.refresh?.()
  logTableRef.value?.refresh?.()
  gapFieldTableRef.value?.refresh?.()
  gapTableTableRef.value?.refresh?.()
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

/** 行按钮：由 `CanStart` / `CanPause` 决定（后端算，前端不判） */
function queueRowActions(row: TaskQueue) {
  const list: Array<{ key: string; text: string; type?: 'primary' | 'warning' }> = []
  if (row.CanStart) list.push({ key: 'start', text: '启动', type: 'primary' })
  if (row.CanPause) list.push({ key: 'pause', text: '暂停', type: 'warning' })
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

const gapFieldColumns: YzhTableColumn<TaskGap>[] = [
  { prop: 'GapLabel', label: '字段', minWidth: 200, showOverflowTooltip: true },
  { prop: 'FieldCode', label: '字段编码', minWidth: 180, showOverflowTooltip: true },
  { prop: 'ExpectedFileName', label: '应来自', minWidth: 170, showOverflowTooltip: true },
  { prop: 'ImpactedItemCount', label: '影响检查项', width: 100, slot: true },
  { prop: 'GapStatus', label: '状态', width: 90, slot: true },
  { prop: '操作', label: '操作', width: 190, slot: true },
]

const gapTableColumns: YzhTableColumn<TaskGap>[] = [
  { prop: 'GapLabel', label: '表格', minWidth: 180, showOverflowTooltip: true },
  { prop: 'TableCode', label: '表格编码', minWidth: 160, showOverflowTooltip: true },
  { prop: 'ExpectedFileName', label: '应来自', minWidth: 170, showOverflowTooltip: true },
  { prop: 'ImpactedItemCount', label: '影响检查项', width: 100, slot: true },
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

async function doFill(row: TaskGap) {
  const isTable = row.GapType === 'table'
  const value = editing.value[row.Code] ?? ''

  if (!value.trim()) {
    ElMessage.warning(isTable ? '请输入表格内容（JSON 数组）' : '请输入补录值')
    return
  }
  if (isTable) {
    try {
      const parsed = JSON.parse(value)
      if (!Array.isArray(parsed)) throw new Error('不是数组')
    } catch {
      ElMessage.error('表格内容必须是 JSON 数组，例如 [{"列名":"值"}]')
      return
    }
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
      ElMessageBox.confirm(
        `已补录 ${r.ImpactedItemCodes.length} 项数据，影响 ${r.ImpactedItemCodes.length} 条检查项。是否立即重跑这些项？`,
        '补录成功',
        { confirmButtonText: '立即重跑', cancelButtonText: '稍后', type: 'info' },
      )
        .then(async () => {
          await retryFailed(taskCode.value)
          ElMessage.success('已重置为待执行，请在「执行队列」中启动')
        })
        .catch(() => undefined)
    }
  } catch (e) {
    ElMessage.error((e as Error)?.message || '补录失败')
  } finally {
    savingCode.value = ''
  }
}

async function doSkip(row: TaskGap) {
  try {
    await ElMessageBox.confirm(
      `跳过「${row.GapLabel}」后，依赖它的检查项将标记为「数据不足，未检查」，不会产生审核结果，且不计入「符合」数量。`,
      '确认跳过',
      { confirmButtonText: '确认跳过', cancelButtonText: '取消', type: 'warning' },
    )
  } catch {
    return
  }
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
  try {
    await ElMessageBox.confirm(
      `⚠ 确认跳过全部 ${gapList.value.PendingCount} 项待补录数据？\n\n` +
        `跳过后，依赖这些数据的检查项将标记为「数据不足，未检查」，不会产生审核结果，` +
        `导出报告时结论列显示「数据不足，未检查」。\n\n这些检查项不会计入「符合」数量。`,
      '确认跳过全部',
      { confirmButtonText: '确认跳过', cancelButtonText: '取消', type: 'warning' },
    )
  } catch {
    return
  }
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
})

// ══════════════════════════════════════════════════════════════════════
// 六、任务级动作（提交执行 / 重试失败 / 看结果）
// ══════════════════════════════════════════════════════════════════════

const submitting = ref(false)

async function onSubmit() {
  const t = task.value
  if (!t) return
  const ok = await confirmOrFalse(
    `将为「${t.StandardCount} 个标准」生成执行队列，共 ${t.TotalItemCount} 个检查项。\n` +
      '生成后需在「执行队列」里逐个启动（可分批跑）。\n\n确定提交？',
    '提交执行',
    { confirmButtonText: '确定提交' },
  )
  if (!ok) return
  submitting.value = true
  try {
    // ★ 提交【不阻断】（裁决 J1）：未补录的缺口只提示，不阻止入队。
    //   真正的门禁在执行期 —— 引用数据为空时任务项自动失败 + 提示「缺失必要数据」。
    const r = await submitTask(t.Code)
    if (r.PendingGapCount > 0) {
      ElMessageBox.alert(
        `${r.Warning ?? `有 ${r.PendingGapCount} 项数据未补录`}\n\n` +
          `这些检查项执行时会自动失败并标记为「数据不足，未检查」。\n` +
          `可现在去「补录清单」处理，或先执行、稍后再补。`,
        '已提交，但有数据待补录',
        { type: 'warning', confirmButtonText: '知道了' },
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
          提交执行
        </el-button>
        <el-button v-if="task?.CanRetry" type="warning" @click="onRetry">重试失败项</el-button>
        <el-button v-if="task?.CanViewResult" type="success" plain @click="goResult">
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

      <!-- ── Tab 2 ── -->
      <el-tab-pane label="执行队列" name="queues">
        <div class="td__pane">
          <YzhTable
            ref="queueTableRef"
            :columns="queueColumns"
            :data-loader="queueLoader"
            :row-action-buttons="queueRowActions"
            :show-pagination="false"
            empty-text="还没有队列 —— 点右上角「提交执行」生成"
            @row-action="onQueueAction"
          >
            <template #toolbar-left>
              <el-button size="small" type="primary" @click="startAllPending">
                启动全部待启动队列
              </el-button>
              <el-button size="small" @click="loadDetail()">刷新</el-button>
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
                ⓘ 补录<strong>不阻断</strong>执行；未补录的数据在执行时会自动失败并提示「缺失必要数据」
              </span>
            </div>
            <div class="gap__actions">
              <el-button size="small" :disabled="!gapsLoaded" @click="openChangeLogs">
                补录留痕
              </el-button>
              <el-button
                size="small"
                type="warning"
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
                  <el-button size="small" @click="doSkip(row)">跳过</el-button>
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

          <!-- 表 2：表格清单（★ 本期 textarea 输 JSON，10 号 Q11；后续可演进为可编辑表格） -->
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
                  <el-input
                    v-model="editing[row.Code]"
                    type="textarea"
                    :rows="2"
                    size="small"
                    placeholder='JSON 数组，例如 [{"项目":"值"}]'
                    :disabled="savingCode === row.Code"
                  />
                  <el-button
                    size="small"
                    type="primary"
                    :loading="savingCode === row.Code"
                    @click="doFill(row)"
                  >
                    保存
                  </el-button>
                  <el-button size="small" @click="doSkip(row)">跳过</el-button>
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
            <span style="margin-left: 6px">{{ row.FieldLabel || row.FieldCode || row.TableCode }}</span>
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
  background: #fff;
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
  font-size: 12px;
}

.gap__arrow {
  margin: 0 6px;
  color: var(--el-text-color-secondary);
}
</style>
