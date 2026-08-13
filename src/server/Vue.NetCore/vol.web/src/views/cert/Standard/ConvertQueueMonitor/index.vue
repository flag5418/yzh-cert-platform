<template>
  <div class="queue-monitor-page">
    <CertPageHeader title="转换队列监控" :icon="IconPending">
      <template #actions>
        <el-button type="primary" @click="loadStatus" :loading="loading">刷新</el-button>
      </template>
    </CertPageHeader>

    <!-- 全局状态卡片 -->
    <el-row :gutter="15" class="status-cards">
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card pending">
            <div class="stat-value">{{ status.totalPending }}</div>
            <div class="stat-label">等待中</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card processing">
            <div class="stat-value">{{ status.totalProcessing }}</div>
            <div class="stat-label">处理中</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card completed">
            <div class="stat-value">{{ status.totalCompleted }}</div>
            <div class="stat-label">已完成</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card failed">
            <div class="stat-value">{{ status.totalFailed }}</div>
            <div class="stat-label">失败</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card workers">
            <div class="stat-value">{{ status.runningWorkers }}/{{ status.maxConcurrent }}</div>
            <div class="stat-label">并发 Worker</div>
          </div>
        </el-card>
      </el-col>
      <el-col :span="4">
        <el-card shadow="hover">
          <div class="stat-card timeout">
            <div class="stat-value">{{ status.timeoutSeconds }}s</div>
            <div class="stat-label">超时阈值</div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 失败任务列表 -->
    <div class="failed-section" v-if="failedJobs.length > 0">
      <h4>失败任务（{{ failedJobs.length }}）</h4>
      <el-table :data="failedJobs" border size="small">
        <el-table-column prop="fileCode" label="文件编码" width="300" show-overflow-tooltip />
        <el-table-column prop="convertType" label="类型" width="100" />
        <el-table-column prop="errorMessage" label="错误信息" show-overflow-tooltip />
        <el-table-column prop="retryCount" label="重试" width="60" />
        <el-table-column label="操作" width="100">
          <template #default="{ row }">
            <el-button type="primary" link size="small" @click="retryJob(row)">重试</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, getCurrentInstance } from 'vue'
import { ElMessage } from 'element-plus'
import { CertPageHeader } from '@/certcore'
import { IconPending } from '@/yzh'

const { proxy } = getCurrentInstance()
const loading = ref(false)
const status = ref({
  totalPending: 0, totalProcessing: 0, totalCompleted: 0,
  totalFailed: 0, totalCancelled: 0,
  maxConcurrent: 5, timeoutSeconds: 300, runningWorkers: 0
})
const failedJobs = ref([])
let pollTimer = null

const loadStatus = async () => {
  loading.value = true
  try {
    const res = await proxy.http.post('api/standard-directory/convert/queue-status', {}, true)
    // 接口经 JsonNormal 返回 PascalCase：{ Status, Data }，需同时兼容两种大小写
    const data = res.Data || res.data
    if (data) {
      status.value = data
    }
  } catch (e) {
    ElMessage.error('获取队列状态失败')
  } finally {
    loading.value = false
  }
}

const retryJob = async (row) => {
  ElMessage.info('重试功能开发中')
}

onMounted(() => {
  loadStatus()
  pollTimer = setInterval(loadStatus, 5000) // 5秒轮询
})

onUnmounted(() => {
  if (pollTimer) clearInterval(pollTimer)
})
</script>

<style scoped lang="less">
.queue-monitor-page {
  padding: var(--yzh-space-5, 20px);

  .status-cards {
    margin-bottom: var(--yzh-space-5, 20px);
    margin-top: var(--yzh-space-5, 20px);
  }

  .stat-card {
    text-align: center;
    padding: 10px 0;

    .stat-value {
      font-size: 28px;
      font-weight: bold;
    }
    .stat-label {
      font-size: var(--yzh-font-size-sm, 13px);
      color: var(--yzh-color-text-secondary, #909399);
      margin-top: 5px;
    }

    &.pending .stat-value { color: var(--yzh-color-warning, #e6a23c); }
    &.processing .stat-value { color: var(--yzh-color-primary, #409eff); }
    &.completed .stat-value { color: var(--yzh-color-success, #67c23a); }
    &.failed .stat-value { color: var(--yzh-color-danger, #f56c6c); }
    &.workers .stat-value { color: var(--yzh-color-text-secondary, #909399); }
    &.timeout .stat-value { color: var(--yzh-color-text-secondary, #909399); }
  }

  .failed-section {
    margin-top: var(--yzh-space-5, 20px);
    h4 { margin-bottom: var(--yzh-space-2, 8px); }
  }
}
</style>
