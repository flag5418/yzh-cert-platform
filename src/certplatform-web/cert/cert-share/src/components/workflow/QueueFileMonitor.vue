<script setup lang="ts">
/**
 * QueueFileMonitor —— 文件队列监控面板（`yzh_queue`，双端共用）
 *
 * 2026-10-17 由 admin `pages/workflow/queue` 与 auditor `pages/queue`（Tab1 文件队列）
 * 两份近似重复实现合并上移到 cert-share，供两个独立系统共用。
 * 取数统一走 `@share/composables/useQueueMonitor`（→ /api/Admin/System/QueueMonitor/*）。
 *
 * 布局：占满父容器剩余高度，仅表格体内部滚动（父页面 `.yzh-page-layout__content`
 * 需为 flex 纵向容器，见两个宿主页面的 :deep 覆写）。
 */
import { ref, onMounted, onUnmounted } from 'vue'
import { ElMessage } from 'element-plus'
import { confirmOrFalse, YzhDrawer, YzhStatusBadge, YzhTable, type Page, type PageParams, type YzhAction, type YzhTableColumn } from '@yzh-core'
import {
  getQueueList,
  getQueueStatus,
  getQueueDetail,
  cancelQueue,
  retryQueue,
  retryTask,
  type QueueListItem,
  type QueueDetailResult,
} from '@share/composables/useQueueMonitor'
import type { QueueStatus } from '@share/types'

interface TypeOption {
  label: string
  value: string
}

const props = withDefaults(
  defineProps<{
    /** 默认选中的队列类型（admin 页面默认只看 file_convert；专家端默认全部） */
    defaultType?: string
    /** 队列类型下拉选项 */
    typeOptions?: TypeOption[]
  }>(),
  {
    defaultType: '',
    typeOptions: () => [
      { label: '全部类型', value: '' },
      { label: '文件转换', value: 'file_convert' },
      { label: '内容提取', value: 'doc_extract' },
      { label: '企业原始资料', value: 'enterprise_original_file' },
    ],
  },
)

const stats = ref<QueueStatus>({
  runningQueues: 0,
  pendingQueues: 0,
  todayCompleted: 0,
  todayFailed: 0,
  todayCancelled: 0,
  maxConcurrent: 0,
  runningWorkers: 0,
})
const activeTab = ref('all')
const queueType = ref(props.defaultType)
const timeRange = ref<[string, string] | null>(null)
/** 主表实例：分页/页码由 YzhTable 自持，外部只调 refresh / loadData */
const tableRef = ref<any>(null)

// 详情
const detailVisible = ref(false)
const detailLoading = ref(false)
const detail = ref<QueueDetailResult | null>(null)

let pollTimer: ReturnType<typeof setInterval> | null = null

const loadStats = async () => {
  try {
    const data = await getQueueStatus()
    if (data) stats.value = data
  } catch {
    // 静默失败
  }
}

/**
 * YzhTable 数据加载器：分页/页码由组件驱动，这里只把筛选条件透给后端。
 * ⛔ 失败不要吞掉返回空数组（会把「查询失败」渲染成「暂无数据」），直接 throw。
 */
const dataLoader = async (params: PageParams): Promise<Page<QueueListItem>> => {
  const query: Record<string, any> = {
    type: queueType.value,
    status: activeTab.value === 'all' ? '' : activeTab.value,
    page: params.page,
    rows: params.rows,
  }
  if (timeRange.value && timeRange.value.length === 2) {
    query.startTime = timeRange.value[0]
    query.endTime = timeRange.value[1]
  }
  const data = await getQueueList(query)
  loadStats()
  return { rows: data?.rows || [], total: data?.total || 0 }
}

/** 用户主动重查（筛选变化 / 查询按钮）：回到第 1 页 */
const requery = () => tableRef.value?.refresh()
/**
 * 轮询静默刷新：**保持当前页**。
 * ⛔ 不能用 `refresh()` —— 它会把页码重置为 1，用户翻到第 3 页会被每 5 秒弹回去。
 */
const pollReload = () => tableRef.value?.loadData()

// ========================================================
// 列定义（R6：一律 YzhTable；无字段列用 slotKey 命名插槽）
// ========================================================

const queueColumns: YzhTableColumn<QueueListItem>[] = [
  { prop: 'queueCode', label: '队列编码', width: 180, showOverflowTooltip: true },
  { slotKey: 'queueType', label: '类型', width: 120, slot: true },
  { prop: 'queueName', label: '队列名称', minWidth: 200, showOverflowTooltip: true },
  { slotKey: 'scope', label: '范围', minWidth: 180, slot: true },
  { slotKey: 'createBy', label: '创建人', width: 100, showOverflowTooltip: true, slot: true },
  { slotKey: 'status', label: '状态', width: 90, align: 'center', slot: true },
  { slotKey: 'progress', label: '进度', minWidth: 150, slot: true },
  { slotKey: 'completed', label: '成功', width: 60, align: 'center', slot: true },
  { slotKey: 'failed', label: '失败', width: 60, align: 'center', slot: true },
  { slotKey: 'cancelled', label: '取消', width: 60, align: 'center', slot: true },
  { prop: 'startTime', label: '开始时间', width: 160, slot: true },
  { prop: 'endTime', label: '结束时间', width: 160, slot: true },
]

/** 主表行操作：详情常驻；失败可重试；待执行/执行中可取消 */
const queueRowActions = (row: QueueListItem): YzhAction[] => {
  const acts: YzhAction[] = [{ key: 'detail', text: '详情', type: 'primary' }]
  if (row.status === 'failed') acts.push({ key: 'retry', text: '重试' })
  if (['pending', 'running'].includes(row.status)) acts.push({ key: 'cancel', text: '取消', type: 'danger' })
  return acts
}

const onQueueRowAction = (key: string, row: QueueListItem) => {
  if (key === 'detail') openDetail(row)
  else if (key === 'retry') handleRetryQueue(row)
  else if (key === 'cancel') handleCancelQueue(row)
}

/** 详情抽屉：子任务明细 */
const taskColumns: YzhTableColumn<any>[] = [
  { prop: 'taskNo', label: '#', width: 50, align: 'center' },
  { prop: 'fileName', label: '文件', minWidth: 220, showOverflowTooltip: true },
  { slotKey: 'convertType', label: '类型', width: 90, slot: true },
  { slotKey: 'taskStatus', label: '状态', width: 90, align: 'center', slot: true },
  { prop: 'retryCount', label: '重试', width: 60, align: 'center' },
  { slotKey: 'errorMessage', label: '错误信息', minWidth: 200, slot: true },
]

/** 子任务行操作：只有失败的可重试 */
const taskRowActions = (row: any): YzhAction[] =>
  row.status === 'failed' ? [{ key: 'retry', text: '重试' }] : []

const onTaskRowAction = (key: string, row: any) => {
  if (key === 'retry') handleRetryTask(row)
}

/** 详情抽屉：资源锁 */const lockColumns: YzhTableColumn<any>[] = [
  { prop: 'resourceTable', label: '资源表', width: 220 },
  { prop: 'resourceName', label: '资源', minWidth: 200, showOverflowTooltip: true },
  { prop: 'taskNo', label: '任务', width: 70, align: 'center', slot: true },
  { slotKey: 'lockStatus', label: '状态', width: 90, align: 'center', slot: true },
  { prop: 'createTime', label: '加锁时间', width: 160 },
  { slotKey: 'releaseTime', label: '释放时间', width: 160, slot: true },
]

const onTabChange = () => {
  requery()
}

const onTypeChange = () => {
  requery()
}

const resetFilter = () => {
  timeRange.value = null
  queueType.value = props.defaultType
  activeTab.value = 'all'
  requery()
}

const openDetail = async (row: QueueListItem) => {
  detailVisible.value = true
  detailLoading.value = true
  detail.value = null
  try {
    const data = await getQueueDetail(row.queueCode)
    if (data) detail.value = data
  } catch {
    ElMessage.error('获取队列详情失败')
  } finally {
    detailLoading.value = false
  }
}

const handleRetryQueue = async (row: QueueListItem) => {
  const ok = await confirmOrFalse(`确定要重新执行队列 ${row.queueCode} 吗？`, '整队重试', { type: 'warning' })
  if (!ok) return
  try {
    await retryQueue(row.queueCode)
    ElMessage.success('队列已重新排队')
    pollReload()
  } catch {
    ElMessage.error('重试失败')
  }
}

const handleCancelQueue = async (row: QueueListItem) => {
  const ok = await confirmOrFalse(`确定要取消队列 ${row.queueCode} 吗？正在执行的任务将被终止。`, '取消队列', { type: 'warning' })
  if (!ok) return
  try {
    await cancelQueue(row.queueCode)
    ElMessage.success('队列已取消')
    pollReload()
  } catch {
    ElMessage.error('取消失败')
  }
}

const handleRetryTask = async (row: { code: string }) => {
  try {
    await retryTask(row.code)
    ElMessage.success('任务已重新排队')
    if (detail.value) {
      const d = await getQueueDetail(detail.value.queue.queueCode)
      if (d) detail.value = d
    }
    pollReload()
  } catch {
    ElMessage.error('重试失败')
  }
}

// ========== 展示工具 ==========
const statusText = (s: string) =>
  ({ pending: '等待中', running: '执行中', completed: '已完成', failed: '已失败', cancelled: '已取消' }[s] || s || '—')

const statusBadgeType = (s: string): 'success' | 'warning' | 'danger' | 'info' =>
  ({ running: 'warning', completed: 'success', failed: 'danger', cancelled: 'info', pending: 'info' }[s] as
    | 'success'
    | 'warning'
    | 'danger'
    | 'info') || 'info'

const taskStatusText = (s: string) =>
  ({ pending: '等待中', processing: '处理中', completed: '成功', failed: '失败', cancelled: '已取消' }[s] || s || '—')

const taskStatusBadgeType = (s: string): 'success' | 'warning' | 'danger' | 'info' =>
  ({ processing: 'warning', completed: 'success', failed: 'danger', cancelled: 'info', pending: 'info' }[s] as
    | 'success'
    | 'warning'
    | 'danger'
    | 'info') || 'info'

const progressStatus = (row: QueueListItem): 'success' | 'exception' | undefined => {
  if (row.status === 'failed') return 'exception'
  if (row.status === 'completed') return 'success'
  return undefined
}

const typeText = (t: string) =>
  ({ file_convert: '文件转换', doc_extract: '内容提取', enterprise_original_file: '企业原始资料' }[t] || t || '—')

const convertTypeText = (t: string) => ({ doc2docx: 'doc→docx', xls2xlsx: 'xls→xlsx' }[t] || t || '—')

const formatScopeKey = (scopeKey: string) => {
  if (!scopeKey) return '—'
  const parts = scopeKey.split('|')
  if (parts.length < 3) return scopeKey
  return [parts[0], parts[1], parts[2]].join(' / ')
}

const startPolling = () => {
  stopPolling()
  pollTimer = setInterval(() => {
    if (stats.value.runningQueues > 0 || stats.value.pendingQueues > 0) pollReload()
  }, 5000)
}

const stopPolling = () => {
  if (pollTimer) clearInterval(pollTimer)
  pollTimer = null
}

onMounted(() => {
  // 表格数据由 YzhTable 自己的 onMounted 加载，这里只管轮询
  startPolling()
})

onUnmounted(stopPolling)
</script>

<template>
  <div class="queue-file-monitor">
    <!-- 统计卡 -->
    <el-row :gutter="15" class="status-cards">
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card running">
            <div class="stat-value">{{ stats.runningQueues }}</div>
            <div class="stat-label">执行中队列</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card pending">
            <div class="stat-value">{{ stats.pendingQueues }}</div>
            <div class="stat-label">等待中</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card completed">
            <div class="stat-value">{{ stats.todayCompleted }}</div>
            <div class="stat-label">今日完成</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card failed">
            <div class="stat-value">{{ stats.todayFailed }}</div>
            <div class="stat-label">今日失败</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card cancelled">
            <div class="stat-value">{{ stats.todayCancelled }}</div>
            <div class="stat-label">今日取消</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card workers">
            <div class="stat-value">{{ stats.runningWorkers }}/{{ stats.maxConcurrent }}</div>
            <div class="stat-label">并发 Worker</div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 状态 Tabs + 过滤 -->
    <div class="filter-bar">
      <el-tabs v-model="activeTab" class="queue-tabs" @tab-change="onTabChange">
        <el-tab-pane label="全部" name="all" />
        <el-tab-pane label="执行中" name="executing" />
        <el-tab-pane label="已完成" name="completed" />
        <el-tab-pane label="已失败" name="failed" />
        <el-tab-pane label="已取消" name="cancelled" />
      </el-tabs>
      <div class="time-filter">
        <span class="filter-label">队列类型</span>
        <el-select v-model="queueType" style="width: 150px" @change="onTypeChange">
          <el-option v-for="opt in props.typeOptions" :key="opt.value" :label="opt.label" :value="opt.value" />
        </el-select>
        <span class="filter-label">创建时间</span>
        <el-date-picker
          v-model="timeRange"
          type="datetimerange"
          range-separator="至"
          start-placeholder="开始时间"
          end-placeholder="结束时间"
          value-format="YYYY-MM-DDTHH:mm:ss"
          size="default"
          style="width: 360px"
        />
        <el-button type="primary" @click="requery">查询</el-button>
        <el-button type="default" @click="resetFilter">重置</el-button>
      </div>
    </div>

    <!-- 队列主表（占满剩余高度，表体内部滚动） -->
    <el-card shadow="never" class="table-card">
      <div class="table-body">
        <YzhTable
          ref="tableRef"
          :columns="queueColumns"
          :data-loader="dataLoader"
          :toolbar="false"
          :page-size="10"
          size="default"
          row-key="queueCode"
          :row-action-buttons="queueRowActions"
          empty-text="暂无队列"
          @row-action="onQueueRowAction"
        >
          <template #column-queueType="{ row }">
            <YzhStatusBadge type="info" :text="typeText(row.queueType)" size="small" />
          </template>
          <template #column-scope="{ row }">
            <span class="scope-text">{{ row.scopeDisplay || formatScopeKey(row.scopeKey) || '—' }}</span>
          </template>
          <!-- 创建人：createBy 是用户 Code（契约），必须显示后端解析后的 createByName -->
          <template #column-createBy="{ row }">{{ row.createByName || '—' }}</template>
          <template #column-status="{ row }">
            <YzhStatusBadge :type="statusBadgeType(row.status)" :text="statusText(row.status)" size="small" />
          </template>
          <template #column-progress="{ row }">
            <div class="progress-cell">
              <el-progress
                :percentage="Number(row.progress) || 0"
                :status="progressStatus(row)"
                :stroke-width="8"
                style="flex: 1"
              />
              <span class="progress-count">{{ row.completedCount }}/{{ row.totalCount }}</span>
            </div>
          </template>
          <template #column-completed="{ row }">
            <span class="count-success">{{ row.completedCount ?? 0 }}</span>
          </template>
          <template #column-failed="{ row }">
            <span class="count-failed">{{ row.failedCount ?? 0 }}</span>
          </template>
          <template #column-cancelled="{ row }">
            <span class="count-cancelled">{{ row.cancelledCount ?? 0 }}</span>
          </template>
          <template #column-startTime="{ row }">{{ row.startTime || '—' }}</template>
          <template #column-endTime="{ row }">{{ row.endTime || '—' }}</template>
        </YzhTable>
      </div>
    </el-card>

    <!-- 队列详情抽屉 -->
    <YzhDrawer
      :model-value="detailVisible"
      title="队列详情"
      size="60%"
      :show-footer="false"
      @update:model-value="detailVisible = $event"
    >
      <template v-if="detail">
        <el-descriptions :column="2" border size="small" class="detail-desc">
          <el-descriptions-item label="队列编码">{{ detail.queue.queueCode }}</el-descriptions-item>
          <el-descriptions-item label="队列名称">{{ detail.queue.queueName }}</el-descriptions-item>
          <el-descriptions-item label="类型">{{ typeText(detail.queue.queueType) }}</el-descriptions-item>
          <el-descriptions-item label="范围">
            {{ detail.queue.scopeDisplay || formatScopeKey(detail.queue.scopeKey) || '—' }}
          </el-descriptions-item>
          <el-descriptions-item label="创建人">{{ detail.queue.createByName || '—' }}</el-descriptions-item>
          <el-descriptions-item label="状态">
            <YzhStatusBadge :type="statusBadgeType(detail.queue.status)" :text="statusText(detail.queue.status)" size="small" />
          </el-descriptions-item>
          <el-descriptions-item label="进度">
            {{ detail.queue.completedCount }}/{{ detail.queue.totalCount }}
            （成功 {{ detail.queue.completedCount }} / 失败 {{ detail.queue.failedCount }} / 取消 {{ detail.queue.cancelledCount }}）
          </el-descriptions-item>
          <el-descriptions-item label="开始时间">{{ detail.queue.startTime || '—' }}</el-descriptions-item>
          <el-descriptions-item label="结束时间">{{ detail.queue.endTime || '—' }}</el-descriptions-item>
        </el-descriptions>

        <div class="detail-section" v-loading="detailLoading">
          <h4>子任务明细（{{ detail.tasks.length }}）</h4>
          <YzhTable
            :columns="taskColumns"
            :data="detail.tasks"
            :toolbar="false"
            :show-pagination="false"
            :row-action-buttons="taskRowActions"
            size="small"
            height="300"
            row-key="code"
            empty-text="暂无子任务"
            @row-action="onTaskRowAction"
          >
            <template #column-convertType="{ row }">{{ convertTypeText(row.convertType) }}</template>
            <template #column-taskStatus="{ row }">
              <YzhStatusBadge :type="taskStatusBadgeType(row.status)" :text="taskStatusText(row.status)" size="small" />
            </template>
            <template #column-errorMessage="{ row }">
              <span v-if="row.errorMessage" class="error-text" :title="row.errorMessage">{{ row.errorMessage }}</span>
              <span v-else>—</span>
            </template>
          </YzhTable>
        </div>

        <div class="detail-section">
          <h4>资源锁（{{ detail.locks.length }}）</h4>
          <YzhTable
            :columns="lockColumns"
            :data="detail.locks"
            :toolbar="false"
            :show-pagination="false"
            size="small"
            height="220"
            empty-text="暂无资源锁"
          >
            <template #column-taskNo="{ row }">{{ row.taskNo || '队列级' }}</template>
            <template #column-lockStatus="{ row }">
              <YzhStatusBadge
                :type="row.status === 'locked' ? 'warning' : 'info'"
                :text="row.status === 'locked' ? '锁定中' : '已释放'"
                size="small"
              />
            </template>
            <template #column-releaseTime="{ row }">{{ row.releaseTime || '—' }}</template>
          </YzhTable>
        </div>
      </template>
    </YzhDrawer>
  </div>
</template>

<style scoped lang="less">
.queue-file-monitor {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.status-cards {
  flex-shrink: 0;
  margin-bottom: var(--yzh-space-3, 12px);
}

.stat-card {
  text-align: center;

  :deep(.el-card__body) {
    padding: var(--yzh-space-2, 8px) var(--yzh-space-1, 4px);
  }

  .stat-value {
    font-size: var(--yzh-font-size-2xl, 20px);
    font-weight: bold;
    line-height: 1.2;
  }

  .stat-label {
    font-size: var(--yzh-font-size-xs, 12px);
    color: var(--yzh-color-text-tertiary, #909399);
    margin-top: var(--yzh-space-1, 4px);
  }

  &.running .stat-value { color: var(--yzh-color-primary, #409eff); }
  &.pending .stat-value { color: var(--yzh-color-warning, #e6a23c); }
  &.completed .stat-value { color: var(--yzh-color-success, #67c23a); }
  &.failed .stat-value { color: var(--yzh-color-danger, #f56c6c); }
  &.cancelled .stat-value { color: var(--yzh-color-text-tertiary, #909399); }
  &.workers .stat-value { color: var(--yzh-color-text-tertiary, #909399); }
}

.filter-bar {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: var(--yzh-space-3, 12px);

  .queue-tabs {
    :deep(.el-tabs__header) {
      margin-bottom: 0;
    }
  }

  .time-filter {
    display: flex;
    align-items: center;
    gap: var(--yzh-space-2, 8px);

    .filter-label {
      font-size: var(--yzh-font-size-sm, 13px);
      color: var(--yzh-color-text-tertiary, #909399);
    }
  }
}

.table-card {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  margin-top: var(--yzh-space-3, 12px);
  border: 1px solid var(--yzh-color-border-light, #ebeef5);

  :deep(.el-card__body) {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    padding: 0;
  }

  .table-body {
    flex: 1;
    min-height: 0;
  }

  .scope-text {
    font-family: monospace;
    font-size: var(--yzh-font-size-xs, 12px);
  }

  .progress-cell {
    display: flex;
    align-items: center;
    gap: var(--yzh-space-2, 8px);

    .progress-count {
      font-size: var(--yzh-font-size-xs, 12px);
      color: var(--yzh-color-text-tertiary, #909399);
      white-space: nowrap;
    }
  }

  .count-success { color: var(--yzh-color-success, #67c23a); font-weight: 500; }
  .count-failed { color: var(--yzh-color-danger, #f56c6c); font-weight: 500; }
  .count-cancelled { color: var(--yzh-color-text-tertiary, #909399); }
}

.detail-desc {
  margin-bottom: var(--yzh-space-4, 16px);
}

.detail-section {
  margin-top: var(--yzh-space-4, 16px);

  h4 {
    margin-bottom: var(--yzh-space-2, 8px);
    font-weight: 500;
  }

  .error-text {
    color: var(--yzh-color-danger, #f56c6c);
    font-size: var(--yzh-font-size-xs, 12px);
  }
}
</style>
