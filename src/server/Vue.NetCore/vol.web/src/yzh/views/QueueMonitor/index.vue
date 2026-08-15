<template>
  <div class="queue-monitor-page">
    <!--
      队列监控（yzh 队列中心通用页面）
      通用队列主表：全部 / 执行中 / 已完成 / 已失败 / 已取消 Tabs + 创建时间过滤 + 详情下钻（子任务 + 资源锁）+ 重试 / 取消
    -->
    <CertPageHeader title="队列监控" :icon="IconPending">
      <template #actions>
        <el-button type="primary" @click="loadData" :loading="loading">
          <el-icon style="margin-right: 4px"><IconRefresh /></el-icon>刷新
        </el-button>
      </template>
    </CertPageHeader>

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

    <!-- Tabs + 时间过滤 -->
    <div class="filter-bar">
      <el-tabs v-model="activeTab" class="queue-tabs" @tab-change="onTabChange">
        <el-tab-pane label="全部" name="all" />
        <el-tab-pane label="执行中" name="executing" />
        <el-tab-pane label="已完成" name="completed" />
        <el-tab-pane label="已失败" name="failed" />
        <el-tab-pane label="已取消" name="cancelled" />
      </el-tabs>
      <div class="time-filter">
        <span class="filter-label">创建时间</span>
        <el-date-picker
          v-model="timeRange"
          type="datetimerange"
          range-separator="至"
          start-placeholder="开始时间"
          end-placeholder="结束时间"
          size="default"
          style="width: 340px"
          @change="loadData"
        />
        <el-button size="default" @click="resetFilter">重置</el-button>
      </div>
    </div>

    <!-- 队列主表 -->
    <el-card shadow="never" class="table-card">
      <el-table :data="rows" v-loading="loading" size="default" stripe>
        <el-table-column prop="queueCode" label="队列编码" width="170" />
        <el-table-column prop="queueName" label="队列名称" min-width="150" show-overflow-tooltip />
        <el-table-column prop="scopeKey" label="范围" min-width="200">
          <template #default="{ row }">
            <span class="scope-text">{{ formatScopeKey(row.scopeKey) }}</span>
          </template>
        </el-table-column>
        <el-table-column prop="creator" label="创建人" width="110" />
        <el-table-column label="状态" width="90" align="center">
          <template #default="{ row }">
            <el-tag :type="statusTagType(row.status)" size="small">{{ statusText(row.status) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="进度" min-width="160">
          <template #default="{ row }">
            <div class="progress-cell">
              <el-progress
                :percentage="row.progress || 0"
                :status="progressStatus(row)"
                :stroke-width="8"
                style="flex: 1"
              />
              <span class="progress-count">{{ row.completedCount }}/{{ row.totalCount }}</span>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="成功" width="70" align="center">
          <template #default="{ row }">
            <span class="count-success">{{ row.completedCount ?? 0 }}</span>
          </template>
        </el-table-column>
        <el-table-column label="失败" width="70" align="center">
          <template #default="{ row }">
            <span class="count-failed">{{ row.failedCount ?? 0 }}</span>
          </template>
        </el-table-column>
        <el-table-column label="取消" width="70" align="center">
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
        <el-table-column label="操作" width="200" align="center" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="openDetail(row)">详情</el-button>
            <el-button
              link
              type="warning"
              size="small"
              v-if="row.status === 'failed'"
              @click="retryQueue(row)"
            >重试</el-button>
            <el-button
              link
              type="danger"
              size="small"
              v-if="['pending', 'running'].includes(row.status)"
              @click="cancelQueue(row)"
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

    <!-- 队列详情抽屉 -->
    <el-drawer v-model="detailVisible" title="队列详情" size="60%">
      <template v-if="detail">
        <el-descriptions :column="2" border size="small" class="detail-desc">
          <el-descriptions-item label="队列编码">{{ detail.queue.queueCode }}</el-descriptions-item>
          <el-descriptions-item label="队列名称">{{ detail.queue.queueName }}</el-descriptions-item>
          <el-descriptions-item label="范围">
            {{ formatScopeKey(detail.queue.scopeKey) || '—' }}
          </el-descriptions-item>
          <el-descriptions-item label="创建人">{{ detail.queue.creator || '—' }}</el-descriptions-item>
          <el-descriptions-item label="状态">
            <el-tag :type="statusTagType(detail.queue.status)" size="small">{{ statusText(detail.queue.status) }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="进度">{{ detail.queue.completedCount }}/{{ detail.queue.totalCount }}（成功 {{ detail.queue.completedCount }} / 失败 {{ detail.queue.failedCount }} / 取消 {{ detail.queue.cancelledCount }}）</el-descriptions-item>
          <el-descriptions-item label="开始时间">{{ detail.queue.startTime || '—' }}</el-descriptions-item>
          <el-descriptions-item label="结束时间">{{ detail.queue.endTime || '—' }}</el-descriptions-item>
        </el-descriptions>

        <div class="detail-section">
          <h4>子任务明细（{{ detail.tasks.length }}）</h4>
          <el-table :data="detail.tasks" border size="small" max-height="300">
            <el-table-column prop="taskNo" label="#" width="50" align="center" />
            <el-table-column prop="fileName" label="文件" min-width="220" show-overflow-tooltip />
            <el-table-column prop="convertType" label="类型" width="90">
              <template #default="{ row }">{{ convertTypeText(row.convertType) }}</template>
            </el-table-column>
            <el-table-column label="状态" width="80" align="center">
              <template #default="{ row }">
                <el-tag :type="taskStatusTagType(row.status)" size="small">{{ taskStatusText(row.status) }}</el-tag>
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
                  type="warning"
                  size="small"
                  @click="retryTask(row)"
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
            <el-table-column prop="taskNo" label="任务" width="60" align="center">
              <template #default="{ row }">{{ row.taskNo || '队列级' }}</template>
            </el-table-column>
            <el-table-column label="状态" width="80" align="center">
              <template #default="{ row }">
                <el-tag :type="row.status === 'locked' ? 'warning' : 'info'" size="small">
                  {{ row.status === 'locked' ? '锁定中' : '已释放' }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="createTime" label="加锁时间" width="150" />
            <el-table-column prop="releaseTime" label="释放时间" width="150">
              <template #default="{ row }">{{ row.releaseTime || '—' }}</template>
            </el-table-column>
          </el-table>
        </div>
      </template>
    </el-drawer>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { CertPageHeader } from '@/certcore'
import { IconPending, IconRefresh, useYzhQueue } from '@/yzh'

const {
  getQueueList, getQueueStats, getQueueDetail,
  cancelQueue: apiCancelQueue, retryQueue: apiRetryQueue, retryTask: apiRetryTask
} = useYzhQueue()

const loading = ref(false)
const stats = ref({
  runningQueues: 0, pendingQueues: 0, todayCompleted: 0,
  todayFailed: 0, todayCancelled: 0, maxConcurrent: 5, runningWorkers: 0
})
const activeTab = ref('all')
const timeRange = ref(null)
const rows = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)

// 详情
const detailVisible = ref(false)
const detail = ref(null)

let pollTimer = null

const loadStats = async () => {
  try {
    const data = await getQueueStats()
    if (data) stats.value = data
  } catch (e) {
    // 静默失败，不打断列表加载
  }
}

const loadData = async () => {
  loading.value = true
  try {
    const body = {
      type: 'file_convert',
      status: activeTab.value === 'all' ? '' : activeTab.value,
      page: page.value,
      rows: pageSize.value
    }
    if (timeRange.value && timeRange.value.length === 2) {
      body.startTime = formatDateTime(timeRange.value[0])
      body.endTime = formatDateTime(timeRange.value[1])
    }
    const data = await getQueueList(body)
    if (data) {
      rows.value = data.rows || []
      total.value = data.total || 0
    }
    loadStats()
  } catch (e) {
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
  page.value = 1
  loadData()
}

const openDetail = async (row) => {
  detailVisible.value = true
  detail.value = null
  try {
    const data = await getQueueDetail(row.queueCode)
    if (data) detail.value = data
  } catch (e) {
    ElMessage.error('获取队列详情失败')
  }
}

const retryQueue = async (row) => {
  try {
    await ElMessageBox.confirm(`确定要重新执行队列 ${row.queueCode} 吗？`, '整队重试', { type: 'warning' })
  } catch {
    return
  }
  try {
    const res = await apiRetryQueue(row.queueCode)
    if (res.Status === true || res.status === 0 || res.Status === 'True') {
      ElMessage.success('队列已重新排队')
      loadData()
    } else {
      ElMessage.error(res.Message || res.message || '重试失败')
    }
  } catch (e) {
    ElMessage.error('重试失败')
  }
}

const cancelQueue = async (row) => {
  try {
    await ElMessageBox.confirm(`确定要取消队列 ${row.queueCode} 吗？正在执行的任务将被终止。`, '取消队列', { type: 'warning' })
  } catch {
    return
  }
  try {
    const res = await apiCancelQueue(row.queueCode)
    if (res.Status === true || res.status === 0 || res.Status === 'True') {
      ElMessage.success('队列已取消')
      loadData()
    } else {
      ElMessage.error(res.Message || res.message || '取消失败')
    }
  } catch (e) {
    ElMessage.error('取消失败')
  }
}

const retryTask = async (row) => {
  try {
    const res = await apiRetryTask(row.id)
    if (res.Status === true || res.status === 0 || res.Status === 'True') {
      ElMessage.success('任务已重新排队')
      if (detail.value) {
        const dData = await getQueueDetail(detail.value.queue.queueCode)
        if (dData) detail.value = dData
      }
      loadData()
    } else {
      ElMessage.error(res.Message || res.message || '重试失败')
    }
  } catch (e) {
    ElMessage.error('重试失败')
  }
}

// ========== 工具函数 ==========
const statusText = (s) => ({
  pending: '等待中', running: '执行中', completed: '已完成',
  failed: '已失败', cancelled: '已取消'
}[s] || s || '—')

const statusTagType = (s) => ({
  pending: 'info', running: 'primary', completed: 'success',
  failed: 'danger', cancelled: 'info'
}[s] || 'info')

const taskStatusText = (s) => ({
  pending: '等待中', processing: '处理中', completed: '成功',
  failed: '失败', cancelled: '已取消'
}[s] || s || '—')

const taskStatusTagType = (s) => ({
  pending: 'info', processing: 'primary', completed: 'success',
  failed: 'danger', cancelled: 'info'
}[s] || 'info')

const progressStatus = (row) => {
  if (row.status === 'failed') return 'exception'
  if (row.status === 'completed') return 'success'
  return ''
}

const convertTypeText = (t) => ({
  doc2docx: 'doc→docx', xls2xlsx: 'xls→xlsx'
}[t] || t || '—')

const formatScopeKey = (scopeKey) => {
  if (!scopeKey) return '—'
  const parts = scopeKey.split('|')
  if (parts.length < 3) return scopeKey
  return [parts[0], parts[1], parts[2]].join(' / ')
}

const formatDateTime = (d) => {
  if (!d) return null
  const date = new Date(d)
  if (isNaN(date.getTime())) return d
  const pad = (n) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
}

onMounted(() => {
  loadData()
  pollTimer = setInterval(() => {
    // 有执行中/等待中队列时才轮询刷新，避免无谓请求
    if (stats.value.runningQueues > 0 || stats.value.pendingQueues > 0) {
      loadData()
    }
  }, 5000)
})

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer)
})
</script>

<style scoped lang="less">
.queue-monitor-page {
  padding: var(--yzh-space-5, 20px);

  .status-cards {
    margin-bottom: var(--yzh-space-4, 16px);
    margin-top: var(--yzh-space-4, 16px);
  }

  .stat-card {
    text-align: center;
    padding: 10px 0;

    .stat-value {
      font-size: 26px;
      font-weight: bold;
    }
    .stat-label {
      font-size: var(--yzh-font-size-sm, 13px);
      color: var(--yzh-color-text-secondary, #909399);
      margin-top: 5px;
    }

    &.running .stat-value { color: var(--yzh-color-primary, #409eff); }
    &.pending .stat-value { color: var(--yzh-color-warning, #e6a23c); }
    &.completed .stat-value { color: var(--yzh-color-success, #67c23a); }
    &.failed .stat-value { color: var(--yzh-color-danger, #f56c6c); }
    &.cancelled .stat-value { color: var(--yzh-color-text-secondary, #909399); }
    &.workers .stat-value { color: var(--yzh-color-text-secondary, #909399); }
  }

  .filter-bar {
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
        color: var(--yzh-color-text-secondary, #909399);
      }
    }
  }

  .table-card {
    margin-top: var(--yzh-space-3, 12px);
    border: 1px solid var(--yzh-color-border-light, #ebeef5);

    .scope-text {
      font-family: monospace;
      font-size: 12px;
    }

    .progress-cell {
      display: flex;
      align-items: center;
      gap: 8px;

      .progress-count {
        font-size: 12px;
        color: var(--yzh-color-text-secondary, #909399);
        white-space: nowrap;
      }
    }

    .count-success { color: var(--yzh-color-success, #67c23a); font-weight: 500; }
    .count-failed { color: var(--yzh-color-danger, #f56c6c); font-weight: 500; }
    .count-cancelled { color: var(--yzh-color-text-secondary, #909399); }

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
      font-size: 12px;
    }
  }
}
</style>
