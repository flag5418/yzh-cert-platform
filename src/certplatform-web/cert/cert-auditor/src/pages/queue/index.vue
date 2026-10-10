<script setup lang="ts">
/**
 * 队列监控（专家端 · 路由 `/queue`）
 *
 * 监控 cert_expert_task_queue（NC 检查 / 报告生成）的执行状态。
 * ★ 工作区隔离：后端 WorkspaceContextService.Resolve 自动收敛，前端不传 OrgCode。
 */
import { ref, onMounted, onUnmounted } from 'vue'
import { ElMessage } from 'element-plus'
import { confirmOrFalse, YzhPageLayout, YzhDrawer, YzhStatusBadge } from '@yzh-core'
import {
  getExpertQueueList,
  getExpertQueueStats,
  getExpertQueueDetail,
  cancelExpertQueue,
  type ExpertQueueItem,
  type ExpertQueueStats,
  type ExpertQueueTaskItem,
} from '@share/api/expert/queueMonitor'

const loading = ref(false)
const stats = ref<ExpertQueueStats>({
  running: 0,
  pending: 0,
  completed: 0,
  failed: 0,
  cancelled: 0,
  todayTotal: 0,
  todayCompleted: 0,
  todayFailed: 0,
})
const activeTab = ref('all')
const timeRange = ref<[Date, Date] | null>(null)
const rows = ref<ExpertQueueItem[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)

// 详情
const detailVisible = ref(false)
const detailLoading = ref(false)
const detailQueue = ref<ExpertQueueItem | null>(null)
const detailItems = ref<ExpertQueueTaskItem[]>([])

let pollTimer: ReturnType<typeof setInterval> | null = null

const loadStats = async () => {
  try {
    const data = await getExpertQueueStats()
    if (data) stats.value = data
  } catch {
    // 静默失败
  }
}

const fmt = (d: string | null | undefined) => {
  if (!d) return '—'
  const dt = new Date(d)
  if (isNaN(dt.getTime())) return '—'
  return dt.toLocaleString('zh-CN', { hour12: false })
}

const loadData = async () => {
  loading.value = true
  try {
    const params: { status?: string; startTime?: string; endTime?: string; page: number; rows: number } = {
      status: activeTab.value === 'all' ? undefined : activeTab.value,
      page: page.value,
      rows: pageSize.value,
    }
    if (timeRange.value && timeRange.value.length === 2) {
      params.startTime = timeRange.value[0].toISOString()
      params.endTime = timeRange.value[1].toISOString()
    }
    const data = await getExpertQueueList(params)
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

const resetFilter = () => {
  timeRange.value = null
  activeTab.value = 'all'
  page.value = 1
  loadData()
}

const openDetail = async (row: ExpertQueueItem) => {
  detailVisible.value = true
  detailLoading.value = true
  try {
    const data = await getExpertQueueDetail(row.code)
    if (data) {
      detailQueue.value = data.queue
      detailItems.value = data.items
    }
  } catch {
    ElMessage.error('获取队列详情失败')
  } finally {
    detailLoading.value = false
  }
}

const statusTagType = (status: string): 'primary' | 'success' | 'danger' | 'warning' | 'info' => {
  const map: Record<string, 'primary' | 'success' | 'danger' | 'warning' | 'info'> = {
    pending: 'info',
    running: 'primary',
    completed: 'success',
    failed: 'danger',
    cancelled: 'warning',
  }
  return map[status] || 'info'
}

const statusText = (status: string) => {
  const map: Record<string, string> = {
    pending: '等待中',
    running: '运行中',
    completed: '已完成',
    failed: '失败',
    cancelled: '已取消',
  }
  return map[status] || status
}

const progressStatus = (row: ExpertQueueItem) => {
  if (row.queueStatus === 'completed') return 'success'
  if (row.queueStatus === 'failed') return 'exception'
  return undefined
}

const handleCancel = async (row: ExpertQueueItem) => {
  const ok = await confirmOrFalse('确定取消该队列吗？未完成的子任务将不再执行。', '取消队列', { type: 'warning' })
  if (!ok) return
  try {
    await cancelExpertQueue(row.code)
    ElMessage.success('已取消队列')
    loadData()
  } catch {
    // 错误已由拦截器处理
  }
}

// 自动轮询（运行中时 5s 刷一次）
const startPolling = () => {
  stopPolling()
  pollTimer = setInterval(() => {
    if (rows.value.some(r => r.queueStatus === 'running' || r.queueStatus === 'pending')) {
      loadData()
    }
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
  <YzhPageLayout title="队列监控">
    <!-- 统计卡 -->
    <el-row :gutter="15" class="status-cards">
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card running">
            <div class="stat-value">{{ stats.running }}</div>
            <div class="stat-label">运行中</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card pending">
            <div class="stat-value">{{ stats.pending }}</div>
            <div class="stat-label">等待中</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card completed">
            <div class="stat-value">{{ stats.completed }}</div>
            <div class="stat-label">已完成</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card failed">
            <div class="stat-value">{{ stats.failed }}</div>
            <div class="stat-label">失败</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card cancelled">
            <div class="stat-value">{{ stats.cancelled }}</div>
            <div class="stat-label">已取消</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card today">
            <div class="stat-value">{{ stats.todayTotal }}</div>
            <div class="stat-label">今日生成</div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- Tabs + 时间过滤 -->
    <div class="filter-bar">
      <el-tabs v-model="activeTab" @tab-change="onTabChange" class="queue-tabs">
        <el-tab-pane label="全部" name="all" />
        <el-tab-pane label="等待中" name="pending" />
        <el-tab-pane label="运行中" name="running" />
        <el-tab-pane label="已完成" name="completed" />
        <el-tab-pane label="失败" name="failed" />
        <el-tab-pane label="已取消" name="cancelled" />
      </el-tabs>

      <div class="time-filter">
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

    <!-- 表格 -->
    <el-card shadow="never" class="table-card" :body-style="{ padding: '0' }">
      <el-table :data="rows" v-loading="loading" size="default" stripe>
        <el-table-column prop="code" label="队列编码" width="200" show-overflow-tooltip />
        <el-table-column prop="queueType" label="类型" width="100">
          <template #default="{ row }">
            <YzhStatusBadge :type="row.queueType === 'nc_check' ? 'info' : 'success'" :text="row.queueType === 'nc_check' ? 'NC 检查' : '报告生成'" size="small" />
          </template>
        </el-table-column>
        <el-table-column prop="standardCode" label="标准编码" width="180" show-overflow-tooltip />
        <el-table-column prop="createBy" label="创建人" width="100" show-overflow-tooltip />
        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <YzhStatusBadge :type="statusTagType(row.queueStatus)" :text="statusText(row.queueStatus)" size="small" />
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
              <span class="progress-count">{{ row.doneCount }}/{{ row.totalCount }}</span>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="成功" width="60" align="center">
          <template #default="{ row }">
            <span class="count-success">{{ row.doneCount ?? 0 }}</span>
          </template>
        </el-table-column>
        <el-table-column label="失败" width="60" align="center">
          <template #default="{ row }">
            <span class="count-failed">{{ row.failedCount ?? 0 }}</span>
          </template>
        </el-table-column>
        <el-table-column label="跳过" width="60" align="center">
          <template #default="{ row }">
            <span class="count-skipped">{{ row.skippedCount ?? 0 }}</span>
          </template>
        </el-table-column>
        <el-table-column label="开始时间" width="160">
          <template #default="{ row }">{{ fmt(row.startTime) }}</template>
        </el-table-column>
        <el-table-column label="结束时间" width="160">
          <template #default="{ row }">{{ fmt(row.finishTime) }}</template>
        </el-table-column>
        <el-table-column label="操作" min-width="130" align="center" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="openDetail(row)">详情</el-button>
            <el-button
              v-if="row.queueStatus === 'pending' || row.queueStatus === 'running'"
              link
              type="danger"
              size="small"
              @click="handleCancel(row)"
            >取消</el-button>
          </template>
        </el-table-column>
      </el-table>

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

    <!-- 详情抽屉 -->
    <YzhDrawer
      :model-value="detailVisible"
      title="队列详情"
      size="60%"
      :show-footer="false"
      @update:model-value="detailVisible = $event"
    >
      <template v-if="detailQueue">
        <el-descriptions :column="2" border size="small" class="detail-desc">
          <el-descriptions-item label="队列编码">{{ detailQueue.code }}</el-descriptions-item>
          <el-descriptions-item label="类型">
            <YzhStatusBadge :type="detailQueue.queueType === 'nc_check' ? 'info' : 'success'" :text="detailQueue.queueType === 'nc_check' ? 'NC 检查' : '报告生成'" size="small" />
          </el-descriptions-item>
          <el-descriptions-item label="状态">
            <YzhStatusBadge :type="statusTagType(detailQueue.queueStatus)" :text="statusText(detailQueue.queueStatus)" size="small" />
          </el-descriptions-item>
          <el-descriptions-item label="进度">{{ detailQueue.doneCount }}/{{ detailQueue.totalCount }}</el-descriptions-item>
          <el-descriptions-item label="创建人">{{ detailQueue.createBy || '—' }}</el-descriptions-item>
          <el-descriptions-item label="开始时间">{{ fmt(detailQueue.startTime) }}</el-descriptions-item>
          <el-descriptions-item label="结束时间">{{ fmt(detailQueue.finishTime) }}</el-descriptions-item>
          <el-descriptions-item label="创建时间">{{ fmt(detailQueue.createTime) }}</el-descriptions-item>
          <el-descriptions-item v-if="detailQueue.lastError" label="错误" :span="2">
            <span class="error-text">{{ detailQueue.lastError }}</span>
          </el-descriptions-item>
        </el-descriptions>

        <div class="detail-section">
          <h4>子任务明细（{{ detailItems.length }}）</h4>
          <el-table :data="detailItems" v-loading="detailLoading" border size="small" max-height="300">
            <el-table-column prop="seq" label="序号" width="50" align="center" />
            <el-table-column prop="itemType" label="类型" width="100">
              <template #default="{ row }">
                <YzhStatusBadge :type="row.itemType === 'nc_check' ? 'info' : 'success'" :text="row.itemType === 'nc_check' ? 'NC 检查' : '报告章节'" size="small" />
              </template>
            </el-table-column>
            <el-table-column prop="itemCode" label="项编码" min-width="180" show-overflow-tooltip />
            <el-table-column label="状态" width="90" align="center">
              <template #default="{ row }">
                <YzhStatusBadge :type="statusTagType(row.itemStatus)" :text="statusText(row.itemStatus)" size="small" />
              </template>
            </el-table-column>
            <el-table-column prop="retryCount" label="重试" width="50" align="center" />
            <el-table-column prop="errorMessage" label="错误" min-width="200" show-overflow-tooltip />
            <el-table-column label="开始时间" width="150">
              <template #default="{ row }">{{ fmt(row.startTime) }}</template>
            </el-table-column>
            <el-table-column label="结束时间" width="150">
              <template #default="{ row }">{{ fmt(row.finishTime) }}</template>
            </el-table-column>
          </el-table>
        </div>
      </template>
    </YzhDrawer>
  </YzhPageLayout>
</template>

<style scoped lang="less">
:deep(.yzh-page-layout__content) {
  background: var(--yzh-color-bg-container, #fff);
}

.status-cards {
  margin-bottom: var(--yzh-space-4, 16px);
  margin-top: var(--yzh-space-4, 16px);
}

.stat-card {
  text-align: center;
  padding: var(--yzh-space-2, 8px) 0;

  .stat-value {
    font-size: var(--yzh-font-size-3xl, 26px);
    font-weight: bold;
  }
  .stat-label {
    font-size: var(--yzh-font-size-sm, 13px);
    color: var(--yzh-color-text-tertiary, #909399);
    margin-top: var(--yzh-space-1, 5px);
  }

  &.running .stat-value { color: var(--yzh-color-primary, #409eff); }
  &.pending .stat-value { color: var(--yzh-color-text-tertiary, #909399); }
  &.completed .stat-value { color: var(--yzh-color-success, #67c23a); }
  &.failed .stat-value { color: var(--yzh-color-danger, #f56c6c); }
  &.cancelled .stat-value { color: var(--yzh-color-warning, #e6a23c); }
  &.today .stat-value { color: var(--yzh-color-text-primary, #303133); }
}

.filter-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 12px;

  .queue-tabs {
    :deep(.el-tabs__header) {
      margin-bottom: 0;
    }
  }

  .time-filter {
    display: flex;
    align-items: center;
    gap: 8px;
  }
}

.table-card {
  margin-top: var(--yzh-space-3, 12px);
  border: 1px solid var(--yzh-color-border-light, #ebeef5);

  .progress-cell {
    display: flex;
    align-items: center;
    gap: 8px;

    .progress-count {
      font-size: var(--yzh-font-size-xs, 12px);
      color: var(--yzh-color-text-tertiary, #909399);
      white-space: nowrap;
    }
  }

  .count-success { color: var(--yzh-color-success, #67c23a); font-weight: 500; }
  .count-failed { color: var(--yzh-color-danger, #f56c6c); font-weight: 500; }
  .count-skipped { color: var(--yzh-color-text-tertiary, #909399); }

  .pagination-row {
    display: flex;
    justify-content: flex-end;
    margin-top: var(--yzh-space-3, 12px);
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
