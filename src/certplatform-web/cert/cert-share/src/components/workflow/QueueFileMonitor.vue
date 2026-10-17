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
import { confirmOrFalse, YzhDrawer, YzhStatusBadge } from '@yzh-core'
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

const loading = ref(false)
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
const rows = ref<QueueListItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)

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

const loadData = async () => {
  loading.value = true
  try {
    const params: Record<string, any> = {
      type: queueType.value,
      status: activeTab.value === 'all' ? '' : activeTab.value,
      page: page.value,
      rows: pageSize.value,
    }
    if (timeRange.value && timeRange.value.length === 2) {
      params.startTime = timeRange.value[0]
      params.endTime = timeRange.value[1]
    }
    const data = await getQueueList(params)
    if (data) {
      rows.value = data.rows || []
      total.value = data.total || 0
    }
    loadStats()
  } catch {
    ElMessage.error('获取队列列表失败')
  } finally {
    loading.value = false
  }
}

const onTabChange = () => {
  page.value = 1
  loadData()
}

const onTypeChange = () => {
  page.value = 1
  loadData()
}

const resetFilter = () => {
  timeRange.value = null
  queueType.value = props.defaultType
  activeTab.value = 'all'
  page.value = 1
  loadData()
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
    loadData()
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
    loadData()
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
    loadData()
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
    if (stats.value.runningQueues > 0 || stats.value.pendingQueues > 0) loadData()
  }, 5000)
}

const stopPolling = () => {
  if (pollTimer) clearInterval(pollTimer)
  pollTimer = null
}

onMounted(() => {
  loadData()
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
        <el-button type="primary" @click="loadData">查询</el-button>
        <el-button type="default" @click="resetFilter">重置</el-button>
      </div>
    </div>

    <!-- 队列主表（占满剩余高度，表体内部滚动） -->
    <el-card shadow="never" class="table-card">
      <div class="table-body">
        <el-table :data="rows" v-loading="loading" size="default" stripe height="100%">
          <el-table-column prop="queueCode" label="队列编码" width="180" show-overflow-tooltip />
          <el-table-column label="类型" width="120">
            <template #default="{ row }">
              <YzhStatusBadge type="info" :text="typeText(row.queueType)" size="small" />
            </template>
          </el-table-column>
          <el-table-column prop="queueName" label="队列名称" min-width="200" show-overflow-tooltip />
          <el-table-column label="范围" min-width="180">
            <template #default="{ row }">
              <span class="scope-text">{{ row.scopeDisplay || formatScopeKey(row.scopeKey) || '—' }}</span>
            </template>
          </el-table-column>
          <!-- 创建人：createBy 是用户 Code（契约），必须显示后端解析后的 createByName -->
          <el-table-column label="创建人" width="100" show-overflow-tooltip>
            <template #default="{ row }">{{ row.createByName || '—' }}</template>
          </el-table-column>
          <el-table-column label="状态" width="90" align="center">
            <template #default="{ row }">
              <YzhStatusBadge :type="statusBadgeType(row.status)" :text="statusText(row.status)" size="small" />
            </template>
          </el-table-column>
          <el-table-column label="进度" min-width="150">
            <template #default="{ row }">
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
          </el-table-column>
          <el-table-column label="成功" width="60" align="center">
            <template #default="{ row }">
              <span class="count-success">{{ row.completedCount ?? 0 }}</span>
            </template>
          </el-table-column>
          <el-table-column label="失败" width="60" align="center">
            <template #default="{ row }">
              <span class="count-failed">{{ row.failedCount ?? 0 }}</span>
            </template>
          </el-table-column>
          <el-table-column label="取消" width="60" align="center">
            <template #default="{ row }">
              <span class="count-cancelled">{{ row.cancelledCount ?? 0 }}</span>
            </template>
          </el-table-column>
          <el-table-column prop="startTime" label="开始时间" width="160">
            <template #default="{ row }">{{ row.startTime || '—' }}</template>
          </el-table-column>
          <el-table-column prop="endTime" label="结束时间" width="160">
            <template #default="{ row }">{{ row.endTime || '—' }}</template>
          </el-table-column>
          <el-table-column label="操作" min-width="130" align="center" fixed="right">
            <template #default="{ row }">
              <el-button link type="primary" size="small" @click="openDetail(row)">详情</el-button>
              <el-button
                v-if="row.status === 'failed'"
                link
                type="default"
                size="small"
                @click="handleRetryQueue(row)"
              >重试</el-button>
              <el-button
                v-if="['pending', 'running'].includes(row.status)"
                link
                type="danger"
                size="small"
                @click="handleCancelQueue(row)"
              >取消</el-button>
            </template>
          </el-table-column>
        </el-table>
      </div>

      <div class="pagination-row">
        <el-pagination
          v-model:current-page="page"
          v-model:page-size="pageSize"
          :total="total"
          :page-sizes="[10, 20, 50]"
          layout="total, sizes, prev, pager, next"
          @current-change="loadData"
          @size-change="loadData"
        />
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
          <el-table :data="detail.tasks" border size="small" max-height="300">
            <el-table-column prop="taskNo" label="#" width="50" align="center" />
            <el-table-column prop="fileName" label="文件" min-width="220" show-overflow-tooltip />
            <el-table-column label="类型" width="90">
              <template #default="{ row }">{{ convertTypeText(row.convertType) }}</template>
            </el-table-column>
            <el-table-column label="状态" width="90" align="center">
              <template #default="{ row }">
                <YzhStatusBadge :type="taskStatusBadgeType(row.status)" :text="taskStatusText(row.status)" size="small" />
              </template>
            </el-table-column>
            <el-table-column prop="retryCount" label="重试" width="60" align="center" />
            <el-table-column label="错误信息" min-width="200">
              <template #default="{ row }">
                <span v-if="row.errorMessage" class="error-text" :title="row.errorMessage">{{ row.errorMessage }}</span>
                <span v-else>—</span>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="90" align="center">
              <template #default="{ row }">
                <el-button
                  v-if="row.status === 'failed'"
                  link
                  type="default"
                  size="small"
                  @click="handleRetryTask(row)"
                >重试</el-button>
              </template>
            </el-table-column>
          </el-table>
        </div>

        <div class="detail-section">
          <h4>资源锁（{{ detail.locks.length }}）</h4>
          <el-table :data="detail.locks" border size="small" max-height="220">
            <el-table-column prop="resourceTable" label="资源表" width="220" />
            <el-table-column prop="resourceName" label="资源" min-width="200" show-overflow-tooltip />
            <el-table-column prop="taskNo" label="任务" width="70" align="center">
              <template #default="{ row }">{{ row.taskNo || '队列级' }}</template>
            </el-table-column>
            <el-table-column label="状态" width="90" align="center">
              <template #default="{ row }">
                <YzhStatusBadge
                  :type="row.status === 'locked' ? 'warning' : 'info'"
                  :text="row.status === 'locked' ? '锁定中' : '已释放'"
                  size="small"
                />
              </template>
            </el-table-column>
            <el-table-column prop="createTime" label="加锁时间" width="160" />
            <el-table-column label="释放时间" width="160">
              <template #default="{ row }">{{ row.releaseTime || '—' }}</template>
            </el-table-column>
          </el-table>
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

  .pagination-row {
    flex-shrink: 0;
    display: flex;
    justify-content: flex-end;
    padding: var(--yzh-space-3, 12px);
  }
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
