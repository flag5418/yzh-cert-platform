<script setup lang="ts">
/**
 * 队列监控（专家端 · 路由 `/queue`）—— 双 Tab
 *
 * - Tab1「文件队列」（默认）：`yzh_queue`（file_convert / doc_extract /
 *   enterprise_original_file 等实际在跑的队列），复用 @share/useQueueMonitor
 *   → `POST /api/Admin/System/QueueMonitor/*`（PermissionFilter 无 RequirePermission
 *   ⇒ 仅要求已认证；MessageBell 已有专家端调 /api/Admin/* 的先例）。
 *   ⚠️ 不传 OrgCode：yzh_queue.OrgCode 当前全空，传工作区 Code 会把 48 行全过滤成空。
 * - Tab2「任务队列」：`cert_expert_task_queue`（NC 检查 / 报告生成），
 *   走 ExpertTaskController /queue/*；工作区隔离由后端 ResolveWorkspace 自动收敛。
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

/** 顶层数据源切换：file = yzh_queue 文件队列（默认），task = cert_expert_task_queue */
const sourceTab = ref<'file' | 'task'>('file')

// ========================================================
// Tab1 文件队列（yzh_queue）
// ========================================================
const fileLoading = ref(false)
const fileStats = ref<QueueStatus>({
  runningQueues: 0,
  pendingQueues: 0,
  todayCompleted: 0,
  todayFailed: 0,
  todayCancelled: 0,
  maxConcurrent: 0,
  runningWorkers: 0,
})
const fileStatusTab = ref('all')
const fileType = ref('')
const fileTimeRange = ref<[Date, Date] | null>(null)
const fileRows = ref<QueueListItem[]>([])
const fileTotal = ref(0)
const filePage = ref(1)
const filePageSize = ref(10)
const fileDetailVisible = ref(false)
const fileDetailLoading = ref(false)
const fileDetail = ref<QueueDetailResult | null>(null)

const loadFileStats = async () => {
  try {
    const data = await getQueueStatus()
    if (data) fileStats.value = data
  } catch {
    // 静默失败
  }
}

const loadFileData = async () => {
  fileLoading.value = true
  try {
    const params: Record<string, any> = {
      type: fileType.value,
      status: fileStatusTab.value === 'all' ? '' : fileStatusTab.value,
      page: filePage.value,
      rows: filePageSize.value,
    }
    if (fileTimeRange.value && fileTimeRange.value.length === 2) {
      params.startTime = formatDateTime(fileTimeRange.value[0])
      params.endTime = formatDateTime(fileTimeRange.value[1])
    }
    const data = await getQueueList(params)
    if (data) {
      fileRows.value = data.rows || []
      fileTotal.value = data.total || 0
    }
    loadFileStats()
  } catch {
    ElMessage.error('获取文件队列失败')
  } finally {
    fileLoading.value = false
  }
}

const onFileStatusTabChange = () => {
  filePage.value = 1
  loadFileData()
}

const onFileTypeChange = () => {
  filePage.value = 1
  loadFileData()
}

const resetFileFilter = () => {
  fileTimeRange.value = null
  fileType.value = ''
  fileStatusTab.value = 'all'
  filePage.value = 1
  loadFileData()
}

const openFileDetail = async (row: QueueListItem) => {
  fileDetailVisible.value = true
  fileDetailLoading.value = true
  fileDetail.value = null
  try {
    const data = await getQueueDetail(row.queueCode)
    if (data) fileDetail.value = data
  } catch {
    ElMessage.error('获取队列详情失败')
  } finally {
    fileDetailLoading.value = false
  }
}

const handleFileRetryQueue = async (row: QueueListItem) => {
  const ok = await confirmOrFalse(`确定要重新执行队列 ${row.queueCode} 吗？`, '整队重试', { type: 'warning' })
  if (!ok) return
  try {
    await retryQueue(row.queueCode)
    ElMessage.success('队列已重新排队')
    loadFileData()
  } catch {
    ElMessage.error('重试失败')
  }
}

const handleFileCancelQueue = async (row: QueueListItem) => {
  const ok = await confirmOrFalse(`确定要取消队列 ${row.queueCode} 吗？正在执行的任务将被终止。`, '取消队列', { type: 'warning' })
  if (!ok) return
  try {
    await cancelQueue(row.queueCode)
    ElMessage.success('队列已取消')
    loadFileData()
  } catch {
    ElMessage.error('取消失败')
  }
}

const handleFileRetryTask = async (row: { code: string }) => {
  try {
    await retryTask(row.code)
    ElMessage.success('任务已重新排队')
    if (fileDetail.value) {
      const d = await getQueueDetail(fileDetail.value.queue.queueCode)
      if (d) fileDetail.value = d
    }
    loadFileData()
  } catch {
    ElMessage.error('重试失败')
  }
}

// ========================================================
// Tab2 任务队列（cert_expert_task_queue · NC 检查 / 报告生成）
// ========================================================
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

// ========================================================
// 文件队列展示工具（camelCase 字段 = QueueManager 匿名对象）
// ========================================================
const fileStatusText = (s: string) =>
  ({ pending: '等待中', running: '执行中', completed: '已完成', failed: '已失败', cancelled: '已取消' }[s] || s || '—')

const fileStatusTagType = (s: string): 'primary' | 'success' | 'danger' | 'warning' | 'info' => {
  const map: Record<string, 'primary' | 'success' | 'danger' | 'warning' | 'info'> = {
    pending: 'info', running: 'primary', completed: 'success', failed: 'danger', cancelled: 'warning',
  }
  return map[s] || 'info'
}

const fileTypeText = (t: string) =>
  ({ file_convert: '文件转换', doc_extract: '内容提取', enterprise_original_file: '企业原始资料' }[t] || t || '—')

const fileProgressStatus = (row: QueueListItem) => {
  if (row.status === 'failed') return 'exception'
  if (row.status === 'completed') return 'success'
  return undefined
}

const fileTaskStatusText = (s: string) =>
  ({ pending: '等待中', processing: '处理中', completed: '成功', failed: '失败', cancelled: '已取消' }[s] || s || '—')

const fileTaskStatusTagType = (s: string): 'primary' | 'success' | 'danger' | 'warning' | 'info' => {
  const map: Record<string, 'primary' | 'success' | 'danger' | 'warning' | 'info'> = {
    pending: 'info', processing: 'primary', completed: 'success', failed: 'danger', cancelled: 'warning',
  }
  return map[s] || 'info'
}

const convertTypeText = (t: string) =>
  ({ doc2docx: 'doc→docx', xls2xlsx: 'xls→xlsx' }[t] || t || '—')

const formatScopeKey = (scopeKey: string) => {
  if (!scopeKey) return '—'
  const parts = scopeKey.split('|')
  if (parts.length < 3) return scopeKey
  return [parts[0], parts[1], parts[2]].join(' / ')
}

const formatDateTime = (d: Date | string) => {
  if (!d) return null
  const date = new Date(d)
  if (isNaN(date.getTime())) return d as string
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
}

// ========================================================
// Tab 切换加载 + 自动轮询
// ========================================================
const loadActiveTab = () => {
  if (sourceTab.value === 'file') loadFileData()
  else loadData()
}

const onSourceChange = () => {
  loadActiveTab()
}

// 自动轮询（当前 Tab 有运行中/等待中队列时 5s 刷一次）
const startPolling = () => {
  stopPolling()
  pollTimer = setInterval(() => {
    if (sourceTab.value === 'file') {
      if (fileStats.value.runningQueues > 0 || fileStats.value.pendingQueues > 0) loadFileData()
    } else if (rows.value.some(r => r.queueStatus === 'running' || r.queueStatus === 'pending')) {
      loadData()
    }
  }, 5000)
}

const stopPolling = () => {
  if (pollTimer) clearInterval(pollTimer)
  pollTimer = null
}

onMounted(() => {
  loadActiveTab()
  startPolling()
})
onUnmounted(stopPolling)
</script>

<template>
  <YzhPageLayout title="队列监控">
    <!-- 数据源切换 -->
    <el-tabs v-model="sourceTab" class="source-tabs" @tab-change="onSourceChange">
      <el-tab-pane label="文件队列" name="file" />
      <el-tab-pane label="任务队列" name="task" />
    </el-tabs>

    <!-- ============ Tab1 文件队列（yzh_queue） ============ -->
    <template v-if="sourceTab === 'file'">
      <el-row :gutter="15" class="status-cards">
        <el-col :span="4">
          <el-card shadow="hover">
            <div class="stat-card running">
              <div class="stat-value">{{ fileStats.runningQueues }}</div>
              <div class="stat-label">执行中队列</div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="4">
          <el-card shadow="hover">
            <div class="stat-card pending">
              <div class="stat-value">{{ fileStats.pendingQueues }}</div>
              <div class="stat-label">等待中</div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="4">
          <el-card shadow="hover">
            <div class="stat-card completed">
              <div class="stat-value">{{ fileStats.todayCompleted }}</div>
              <div class="stat-label">今日完成</div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="4">
          <el-card shadow="hover">
            <div class="stat-card failed">
              <div class="stat-value">{{ fileStats.todayFailed }}</div>
              <div class="stat-label">今日失败</div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="4">
          <el-card shadow="hover">
            <div class="stat-card cancelled">
              <div class="stat-value">{{ fileStats.todayCancelled }}</div>
              <div class="stat-label">今日取消</div>
            </div>
          </el-card>
        </el-col>
        <el-col :span="4">
          <el-card shadow="hover">
            <div class="stat-card running">
              <div class="stat-value">{{ fileStats.runningWorkers }}/{{ fileStats.maxConcurrent }}</div>
              <div class="stat-label">并发 Worker</div>
            </div>
          </el-card>
        </el-col>
      </el-row>

      <div class="filter-bar">
        <el-tabs v-model="fileStatusTab" class="queue-tabs" @tab-change="onFileStatusTabChange">
          <el-tab-pane label="全部" name="all" />
          <el-tab-pane label="执行中" name="executing" />
          <el-tab-pane label="已完成" name="completed" />
          <el-tab-pane label="已失败" name="failed" />
          <el-tab-pane label="已取消" name="cancelled" />
        </el-tabs>
        <div class="time-filter">
          <span class="filter-label">队列类型</span>
          <el-select v-model="fileType" style="width: 150px" @change="onFileTypeChange">
            <el-option label="全部类型" value="" />
            <el-option label="文件转换" value="file_convert" />
            <el-option label="内容提取" value="doc_extract" />
            <el-option label="企业原始资料" value="enterprise_original_file" />
          </el-select>
          <el-date-picker
            v-model="fileTimeRange"
            type="datetimerange"
            range-separator="至"
            start-placeholder="开始时间"
            end-placeholder="结束时间"
            value-format="YYYY-MM-DDTHH:mm:ss"
            size="default"
            style="width: 360px"
          />
          <el-button type="primary" @click="loadFileData">查询</el-button>
          <el-button type="default" @click="resetFileFilter">重置</el-button>
        </div>
      </div>

      <el-card shadow="never" class="table-card" :body-style="{ padding: '0' }">
        <el-table :data="fileRows" v-loading="fileLoading" size="default" stripe>
          <el-table-column prop="queueCode" label="队列编码" width="180" show-overflow-tooltip />
          <el-table-column label="类型" width="120">
            <template #default="{ row }">
              <YzhStatusBadge type="info" :text="fileTypeText(row.queueType)" size="small" />
            </template>
          </el-table-column>
          <el-table-column prop="queueName" label="队列名称" min-width="200" show-overflow-tooltip />
          <el-table-column label="范围" min-width="180">
            <template #default="{ row }">
              <span class="scope-text">{{ row.scopeDisplay || formatScopeKey(row.scopeKey) || '—' }}</span>
            </template>
          </el-table-column>
          <!-- 创建人：createBy 是用户 Code，必须显示解析后的 createByName -->
          <el-table-column label="创建人" width="100" show-overflow-tooltip>
            <template #default="{ row }">{{ row.createByName || '—' }}</template>
          </el-table-column>
          <el-table-column label="状态" width="90" align="center">
            <template #default="{ row }">
              <YzhStatusBadge :type="fileStatusTagType(row.status)" :text="fileStatusText(row.status)" size="small" />
            </template>
          </el-table-column>
          <el-table-column label="进度" min-width="150">
            <template #default="{ row }">
              <div class="progress-cell">
                <el-progress
                  :percentage="Number(row.progress) || 0"
                  :status="fileProgressStatus(row)"
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
          <el-table-column prop="startTime" label="开始时间" width="160">
            <template #default="{ row }">{{ row.startTime || '—' }}</template>
          </el-table-column>
          <el-table-column prop="endTime" label="结束时间" width="160">
            <template #default="{ row }">{{ row.endTime || '—' }}</template>
          </el-table-column>
          <el-table-column label="操作" min-width="130" align="center" fixed="right">
            <template #default="{ row }">
              <el-button link type="primary" size="small" @click="openFileDetail(row)">详情</el-button>
              <el-button
                v-if="row.status === 'failed'"
                link
                type="default"
                size="small"
                @click="handleFileRetryQueue(row)"
              >重试</el-button>
              <el-button
                v-if="['pending', 'running'].includes(row.status)"
                link
                type="danger"
                size="small"
                @click="handleFileCancelQueue(row)"
              >取消</el-button>
            </template>
          </el-table-column>
        </el-table>

        <div class="pagination-row">
          <el-pagination
            v-model:current-page="filePage"
            v-model:page-size="filePageSize"
            :total="fileTotal"
            :page-sizes="[10, 20, 50]"
            layout="total, sizes, prev, pager, next"
            @current-change="loadFileData"
            @size-change="loadFileData"
          />
        </div>
      </el-card>

      <!-- 文件队列详情抽屉 -->
      <YzhDrawer
        :model-value="fileDetailVisible"
        title="队列详情"
        size="60%"
        :show-footer="false"
        @update:model-value="fileDetailVisible = $event"
      >
        <template v-if="fileDetail">
          <el-descriptions :column="2" border size="small" class="detail-desc">
            <el-descriptions-item label="队列编码">{{ fileDetail.queue.queueCode }}</el-descriptions-item>
            <el-descriptions-item label="队列名称">{{ fileDetail.queue.queueName }}</el-descriptions-item>
            <el-descriptions-item label="类型">{{ fileTypeText(fileDetail.queue.queueType) }}</el-descriptions-item>
            <el-descriptions-item label="范围">
              {{ fileDetail.queue.scopeDisplay || formatScopeKey(fileDetail.queue.scopeKey) || '—' }}
            </el-descriptions-item>
            <el-descriptions-item label="创建人">{{ fileDetail.queue.createByName || '—' }}</el-descriptions-item>
            <el-descriptions-item label="状态">
              <YzhStatusBadge :type="fileStatusTagType(fileDetail.queue.status)" :text="fileStatusText(fileDetail.queue.status)" size="small" />
            </el-descriptions-item>
            <el-descriptions-item label="进度">
              {{ fileDetail.queue.completedCount }}/{{ fileDetail.queue.totalCount }}
              （成功 {{ fileDetail.queue.completedCount }} / 失败 {{ fileDetail.queue.failedCount }} / 取消 {{ fileDetail.queue.cancelledCount }}）
            </el-descriptions-item>
            <el-descriptions-item label="开始时间">{{ fileDetail.queue.startTime || '—' }}</el-descriptions-item>
            <el-descriptions-item label="结束时间">{{ fileDetail.queue.endTime || '—' }}</el-descriptions-item>
          </el-descriptions>

          <div class="detail-section" v-loading="fileDetailLoading">
            <h4>子任务明细（{{ fileDetail.tasks.length }}）</h4>
            <el-table :data="fileDetail.tasks" border size="small" max-height="300">
              <el-table-column prop="taskNo" label="#" width="50" align="center" />
              <el-table-column prop="fileName" label="文件" min-width="220" show-overflow-tooltip />
              <el-table-column label="类型" width="90">
                <template #default="{ row }">{{ convertTypeText(row.convertType) }}</template>
              </el-table-column>
              <el-table-column label="状态" width="80" align="center">
                <template #default="{ row }">
                  <YzhStatusBadge :type="fileTaskStatusTagType(row.status)" :text="fileTaskStatusText(row.status)" size="small" />
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
                    @click="handleFileRetryTask(row)"
                  >重试</el-button>
                </template>
              </el-table-column>
            </el-table>
          </div>

          <div class="detail-section">
            <h4>资源锁（{{ fileDetail.locks.length }}）</h4>
            <el-table :data="fileDetail.locks" border size="small" max-height="220">
              <el-table-column prop="resourceTable" label="资源表" width="220" />
              <el-table-column prop="resourceName" label="资源" min-width="200" show-overflow-tooltip />
              <el-table-column prop="taskNo" label="任务" width="70" align="center">
                <template #default="{ row }">{{ row.taskNo || '队列级' }}</template>
              </el-table-column>
              <el-table-column label="状态" width="90" align="center">
                <template #default="{ row }">
                  <YzhStatusBadge :type="row.status === 'locked' ? 'warning' : 'info'" :text="row.status === 'locked' ? '锁定中' : '已释放'" size="small" />
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
    </template>

    <!-- ============ Tab2 任务队列（cert_expert_task_queue） ============ -->
    <template v-else>
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

      <!-- 任务队列详情抽屉 -->
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
              <el-table-column prop="retryCount" label="重试" width="50" />
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
    </template>
  </YzhPageLayout>
</template>

<style scoped lang="less">
:deep(.yzh-page-layout__content) {
  background: var(--yzh-color-bg-container, #fff);
}

.source-tabs {
  :deep(.el-tabs__header) {
    margin-bottom: var(--yzh-space-2, 8px);
  }
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

  .filter-label {
    font-size: var(--yzh-font-size-sm, 13px);
    color: var(--yzh-color-text-tertiary, #909399);
  }
}

.table-card {
  margin-top: var(--yzh-space-3, 12px);
  border: 1px solid var(--yzh-color-border-light, #ebeef5);

  .scope-text {
    font-family: monospace;
    font-size: var(--yzh-font-size-xs, 12px);
  }

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
