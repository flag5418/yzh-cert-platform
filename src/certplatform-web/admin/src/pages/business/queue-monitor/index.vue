<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { YzhPageLayout } from '@yzh-core/components/layout'
import {
  getQueueTaskPage,
  cancelQueueTask,
  retryQueueTask,
  type QueueTask
} from '@share/api/queue'

const loading = ref(false)
const tableData = ref<QueueTask[]>([])
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const filterStatus = ref('')
const filterType = ref('')

const columns = [
  { prop: 'taskNo', label: '任务编号', width: 180 },
  { prop: 'taskType', label: '类型', width: 120 },
  { prop: 'fileName', label: '文件名', width: 200 },
  { prop: 'status', label: '状态', width: 100 },
  { prop: 'progress', label: '进度', width: 120 },
  { prop: 'createDate', label: '创建时间', width: 160 },
  { prop: 'message', label: '消息', minWidth: 150, showOverflowTooltip: true },
  { prop: 'actions', label: '操作', width: 150, fixed: 'right' }
]

const searchFields = [
  { prop: 'taskType', label: '任务类型', type: 'select', options: [
    { label: '全部', value: '' },
    { label: '文件转换', value: 'file_convert' },
    { label: 'AI提取', value: 'ai_extract' },
    { label: 'AI分析', value: 'ai_analyze' }
  ]},
  { prop: 'status', label: '状态', type: 'select', options: [
    { label: '全部', value: '' },
    { label: '待处理', value: 'pending' },
    { label: '处理中', value: 'processing' },
    { label: '成功', value: 'success' },
    { label: '失败', value: 'failed' }
  ]}
]

async function loadData(params: any) {
  loading.value = true
  try {
    const res = await getQueueTaskPage({ ...params, page: page.value, rows: pageSize.value })
    tableData.value = res?.rows || []
    total.value = res?.total || 0
  } catch (e: any) {
    ElMessage.error(e?.message || '加载失败')
  } finally {
    loading.value = false
  }
}

async function handleCancel(row: QueueTask) {
  try {
    await ElMessageBox.confirm(`确定取消任务「${row.taskNo}」？`, '确认', { type: 'warning' })
    await cancelQueueTask(row.id!)
    ElMessage.success('已取消')
    loadData({})
  } catch (e: any) {
    if (e !== 'cancel') ElMessage.error(e?.message || '操作失败')
  }
}

async function handleRetry(row: QueueTask) {
  try {
    await retryQueueTask(row.id!)
    ElMessage.success('已重新入队')
    loadData({})
  } catch (e: any) {
    ElMessage.error(e?.message || '操作失败')
  }
}

function getStatusTag(type: string) {
  const map: Record<string, string> = {
    pending: 'info',
    processing: 'warning',
    success: 'success',
    failed: 'danger'
  }
  return map[type] || ''
}

function getStatusText(type: string) {
  const map: Record<string, string> = {
    pending: '待处理',
    processing: '处理中',
    success: '成功',
    failed: '失败'
  }
  return map[type] || type
}

function formatDate(dateStr: string) {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleString('zh-CN')
}

onMounted(() => loadData({}))
</script>

<template>
  <YzhPageLayout title="队列监控">
    <template #toolbar>
      <el-button type="primary" @click="loadData({})">
        <el-icon><Refresh /></el-icon> 刷新
      </el-button>
    </template>

    <el-card shadow="never">
      <el-form :inline="true" :model="{ taskType: filterType, status: filterStatus }" style="margin-bottom: 16px">
        <el-form-item label="任务类型">
          <el-select v-model="filterType" placeholder="全部" clearable style="width: 140px">
            <el-option label="文件转换" value="file_convert" />
            <el-option label="AI提取" value="ai_extract" />
            <el-option label="AI分析" value="ai_analyze" />
          </el-select>
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="filterStatus" placeholder="全部" clearable style="width: 140px">
            <el-option label="待处理" value="pending" />
            <el-option label="处理中" value="processing" />
            <el-option label="成功" value="success" />
            <el-option label="失败" value="failed" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="loadData({})">查询</el-button>
          <el-button @click="filterType=''; filterStatus=''; loadData({})">重置</el-button>
        </el-form-item>
      </el-form>

      <el-table :data="tableData" stripe border v-loading="loading">
        <el-table-column prop="taskNo" label="任务编号" width="180" />
        <el-table-column prop="taskType" label="类型" width="120">
          <template #default="{ row }">
            <el-tag size="small">{{ row.taskType }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="fileName" label="文件名" width="200" show-overflow-tooltip />
        <el-table-column prop="status" label="状态" width="100" align="center">
          <template #default="{ row }">
            <el-tag :type="getStatusTag(row.status)" size="small">{{ getStatusText(row.status) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="progress" label="进度" width="120">
          <template #default="{ row }">
            <el-progress
              :percentage="row.progress || 0"
              :status="row.status === 'success' ? 'success' : row.status === 'failed' ? 'exception' : undefined"
              :stroke-width="8"
            />
          </template>
        </el-table-column>
        <el-table-column prop="createDate" label="创建时间" width="160">
          <template #default="{ row }">{{ formatDate(row.createDate) }}</template>
        </el-table-column>
        <el-table-column prop="message" label="消息" min-width="150" show-overflow-tooltip />
        <el-table-column label="操作" width="150" fixed="right">
          <template #default="{ row }">
            <el-button v-if="row.status === 'pending'" link type="primary" size="small" @click="handleCancel(row)">取消</el-button>
            <el-button v-if="row.status === 'failed'" link type="warning" size="small" @click="handleRetry(row)">重试</el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-pagination
        v-model:current-page="page"
        :page-size="pageSize"
        :total="total"
        layout="total, prev, pager, next"
        style="margin-top: 16px; justify-content: flex-end"
        @current-change="loadData({})"
      />
    </el-card>
  </YzhPageLayout>
</template>

<style scoped>
</style>
