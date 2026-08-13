<template>
  <transition name="slide-up">
    <div v-if="visible" class="convert-progress-panel">
      <div class="panel-header">
        <div class="panel-title">
          <el-icon v-if="!isFinished" class="is-loading"><IconLoading /></el-icon>
          <el-icon v-else color="var(--yzh-color-success)"><IconCircleSuccess /></el-icon>
          <span>文档转换进度</span>
        </div>
        <div class="panel-actions">
          <el-button v-if="!isFinished" type="danger" link size="small" @click="handleCancel">
            取消队列
          </el-button>
          <el-button type="info" link size="small" @click="handleMinimize">
            {{ minimized ? '展开' : '最小化' }}
          </el-button>
          <el-button type="info" link size="small" @click="handleClose">
            <el-icon><IconClose /></el-icon>
          </el-button>
        </div>
      </div>

      <div v-show="!minimized" class="panel-body">
        <div class="progress-bar">
          <el-progress
            :percentage="percentage"
            :status="progressStatus"
            :stroke-width="18"
            text-inside
          />
        </div>
        <div class="progress-stats">
          <span class="stat-item completed"><el-icon><IconCircleSuccess /></el-icon> 已完成 {{ progress.completed }}</span>
          <span class="stat-item pending"><el-icon><IconPending /></el-icon> 等待中 {{ progress.pending }}</span>
          <span class="stat-item failed" v-if="progress.failed > 0"><el-icon><IconError /></el-icon> 失败 {{ progress.failed }}</span>
          <span class="stat-item total">共 {{ progress.total }}</span>
        </div>
        <div class="current-file" v-if="currentFileName">
          <el-icon><IconFile /></el-icon>
          <span>{{ currentFileName }}</span>
        </div>
      </div>

      <div v-show="minimized" class="panel-minimized">
        <span>{{ progress.completed }}/{{ progress.total }} ({{ percentage }}%)</span>
      </div>
    </div>
  </transition>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted, getCurrentInstance } from 'vue'
import { IconLoading, IconCircleSuccess, IconFile, IconClose, IconPending, IconError } from '@/yzh'
import { ElMessageBox, ElNotification } from 'element-plus'

const { proxy } = getCurrentInstance()

const props = defineProps({
  taskId: { type: String, default: '' }
})

const visible = ref(false)
const minimized = ref(false)
const progress = ref({
  total: 0,
  completed: 0,
  failed: 0,
  processing: 0,
  pending: 0,
  cancelled: 0,
  isFinished: false
})
const currentFileName = ref('')
let pollTimer = null

const percentage = computed(() => {
  if (progress.value.total === 0) return 0
  return Math.round((progress.value.completed / progress.value.total) * 100)
})

const isFinished = computed(() => progress.value.isFinished)

const progressStatus = computed(() => {
  if (progress.value.failed > 0 && progress.value.completed === progress.value.total - progress.value.failed) {
    return 'warning'
  }
  return isFinished.value ? 'success' : ''
})

const start = (taskId) => {
  visible.value = true
  minimized.value = false
  startPolling(taskId)
}

const startPolling = (taskId) => {
  stopPolling()
  pollTimer = setInterval(async () => {
    await fetchProgress(taskId)
    if (isFinished.value) {
      stopPolling()
      // 完成通知
      ElNotification.success({
        title: '转换完成',
        message: `共 ${progress.value.total} 个文件，成功 ${progress.value.completed}，失败 ${progress.value.failed}`,
        duration: 5000
      })
    }
  }, 3000) // 3秒轮询
  fetchProgress(taskId) // 立即获取一次
}

const fetchProgress = async (taskId) => {
  try {
    const res = await proxy.http.post('api/standard-directory/convert/progress', {
      taskId
    }, true)
    // 接口经 JsonNormal 返回 PascalCase：{ Status, Data }，需同时兼容两种大小写
    const data = res.Data || res.data
    if (data) {
      progress.value = data
    }
  } catch (e) {
    console.error('获取进度失败', e)
  }
}

const stopPolling = () => {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
}

const handleCancel = () => {
  ElMessageBox.confirm(
    `确定要取消当前转换队列吗？正在处理的文件将被强制终止。`,
    '取消转换队列',
    { type: 'warning', confirmButtonText: '确定取消', cancelButtonText: '继续转换' }
  ).then(async () => {
    try {
      await proxy.http.post(`api/standard-directory/convert/cancel?taskId=${props.taskId}`, {}, true)
      proxy.$message.success('已取消转换队列')
    } catch (e) {
      proxy.$message.error('取消失败')
    }
  }).catch(() => {})
}

const handleMinimize = () => {
  minimized.value = !minimized.value
}

const handleClose = () => {
  visible.value = false
  stopPolling()
}

onUnmounted(() => {
  stopPolling()
})

defineExpose({ start })
</script>

<style scoped lang="less">
.slide-up-enter-active,
.slide-up-leave-active {
  transition: all 0.3s ease;
}
.slide-up-enter-from,
.slide-up-leave-to {
  transform: translateY(100%);
  opacity: 0;
}

.convert-progress-panel {
  position: fixed;
  bottom: 0;
  right: 20px;
  width: 420px;
  background: var(--yzh-color-bg-card, #fff);
  border-radius: var(--yzh-radius-lg, 8px) var(--yzh-radius-lg, 8px) 0 0;
  box-shadow: var(--yzh-shadow-lg, 0 6px 24px rgba(0, 0, 0, 0.1));
  z-index: 2000;
  overflow: hidden;

  .panel-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 10px 15px;
    background: var(--yzh-color-bg-page, #f5f7fa);
    border-bottom: 1px solid var(--yzh-color-border-lighter, #eee);

    .panel-title {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 14px;
      font-weight: 600;
    }

    .panel-actions {
      display: flex;
      gap: 4px;
    }
  }

  .panel-body {
    padding: 15px;

    .progress-bar {
      margin-bottom: 10px;
    }

    .progress-stats {
      display: flex;
      gap: 15px;
      font-size: 12px;
      color: var(--yzh-color-text-regular, #606266);

      .stat-item { display: inline-flex; align-items: center; gap: 4px; }
      .completed { color: var(--yzh-color-success, #67c23a); }
      .pending { color: var(--yzh-color-warning, #e6a23c); }
      .failed { color: var(--yzh-color-danger, #f56c6c); }
      .total { color: var(--yzh-color-text-secondary, #909399); }
    }

    .current-file {
      margin-top: 8px;
      font-size: 12px;
      color: var(--yzh-color-text-secondary, #909399);
      display: flex;
      align-items: center;
      gap: 4px;
      overflow: hidden;
      white-space: nowrap;
      text-overflow: ellipsis;
    }
  }

  .panel-minimized {
    padding: 8px 15px;
    font-size: 13px;
    color: var(--yzh-color-text-regular, #606266);
  }
}
</style>
