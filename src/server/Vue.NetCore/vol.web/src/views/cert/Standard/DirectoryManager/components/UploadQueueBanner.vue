<template>
  <transition name="slide-down">
    <div v-if="visible" class="upload-banner">
      <div class="banner-header">
        <div class="banner-title">
          <el-icon :class="{ 'is-spinning': isAnyRunning }"><Upload /></el-icon>
          <span>上传队列进行中 ({{ activeCount }} 个任务)</span>
        </div>
        <div class="banner-actions">
          <el-button
            v-if="expanded && activeTasks.length > 0"
            type="primary"
            link
            size="small"
            @click="showPanel = true"
          >
            查看详情
          </el-button>
          <el-button type="info" link size="small" @click="expanded = !expanded">
            {{ expanded ? '收起' : '展开' }}
          </el-button>
          <el-button type="info" link size="small" @click="handleClose">✕</*>
          </el-button>
        </div>
      </div>

      <div v-if="expanded && activeTasks.length > 0" class="banner-body">
        <div v-for="task in activeTasks" :key="task.taskId" class="task-item">
          <div class="task-info">
            <span class="task-name">{{ task.name || task.directoryCode }}</span>
            <span class="task-status" :class="task.status">{{ task.statusText }}</span>
          </div>
          <el-progress
            :percentage="task.percent"
            :status="task.status === 'failed' ? 'exception' : task.status === 'done' ? 'success' : ''"
            :stroke-width="8"
            style="flex: 1; margin: 0 12px;"
          />
          <el-button
            v-if="task.status === 'uploading'"
            type="danger"
            link
            size="small"
            @click="handleCancel(task.taskId)"
          >
            取消
          </el-button>
        </div>
      </div>
    </div>
  </transition>

  <!-- 任务详情面板 -->
  <el-drawer
    v-model="showPanel"
    title="上传队列详情"
    direction="btt"
    size="480px"
    :before-close="handleClosePanel"
  >
    <div class="panel-content">
      <div v-if="activeTasks.length === 0" class="empty-state">
        <el-empty description="当前没有活跃的上传任务" />
      </div>
      <div v-for="task in activeTasks" :key="task.taskId" class="task-detail">
        <div class="task-detail-header">
          <span class="task-detail-name">{{ task.name || task.directoryCode }}</span>
          <el-tag :type="statusTagType(task.status)" size="small">{{ task.statusText }}</el-tag>
        </div>
        <el-progress
          :percentage="task.percent"
          :status="task.status === 'failed' ? 'exception' : task.status === 'done' ? 'success' : ''"
          :stroke-width="12"
          style="margin: 8px 0;"
        />
        <div class="task-detail-stats">
          <span>已上传: {{ task.uploadedFiles }}/{{ task.totalFiles }}</span>
          <span v-if="task.failedFiles > 0" class="text-danger">失败: {{ task.failedFiles }}</span>
          <span v-if="task.convertCount > 0">转换中: {{ task.convertCount }}</span>
        </div>
        <div class="task-detail-files" v-if="task.files && task.files.length > 0">
          <div v-for="f in task.files" :key="f.fileCode" class="file-row">
            <el-icon v-if="f.status === 'uploaded' || f.status === 'active'" color="#67c23a"><Check /></el-icon>
            <el-icon v-else-if="f.status === 'uploading'" color="#409eff" class="is-spinning"><Loading /></el-icon>
            <el-icon v-else-if="f.status === 'converting'" color="#409eff"><Document /></el-icon>
            <el-icon v-else-if="f.status === 'failed'" color="#f56c6c"><Close /></el-icon>
            <span class="file-name">{{ f.fileName }}</span>
            <span class="file-status">{{ fileStatusText(f.status) }}</span>
          </div>
        </div>
        <el-button
          v-if="task.status === 'uploading'"
          type="danger"
          plain
          size="small"
          @click="handleCancel(task.taskId)"
          style="margin-top: 8px;"
        >
          取消此任务
        </el-button>
      </div>
      <el-button
        v-if="activeTasks.length > 0"
        type="danger"
        plain
        size="small"
        @click="handleCancelAll"
        style="margin-top: 16px;"
      >
        取消全部任务
      </el-button>
    </div>
  </el-drawer>
</template>

<script setup>
import { ref, computed, onUnmounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Upload, Check, Close, Loading } from '@element-plus/icons-vue'
import http from '@/api/http'

const props = defineProps({
  tasks: { type: Array, default: () => [] }
})

const emit = defineEmits(['cancel'])

const visible = computed(() => props.tasks.length > 0)
const activeCount = computed(() => props.tasks.filter(t => t.status !== 'done' && t.status !== 'failed').length)
const expanded = ref(false)
const showPanel = ref(false)

const isAnyRunning = computed(() =>
  props.tasks.some(t => t.status === 'uploading' || t.status === 'converting')
)

const activeTasks = computed(() => props.tasks.filter(t => t.status !== 'done'))

const handleClose = () => {
  expanded.value = false
}

const handleCancel = (taskId) => {
  emit('cancel', taskId)
}

const handleCancelAll = async () => {
  try {
    await ElMessageBox.confirm('确定要取消所有上传任务吗？', '提示', { type: 'warning' })
    for (const task of activeTasks.value) {
      emit('cancel', task.taskId)
    }
  } catch {}
}

const handleClosePanel = () => {
  showPanel.value = false
}

const statusTagType = (status) => {
  const map = { uploading: 'warning', done: 'success', failed: 'danger', converting: 'primary' }
  return map[status] || 'info'
}

const fileStatusText = (status) => {
  const map = {
    pending: '等待中',
    uploading: '上传中',
    uploaded: '已上传',
    active: '已完成',
    converting: '转换中',
    converted: '已转换',
    failed: '失败'
  }
  return map[status] || status
}

onUnmounted(() => {
  // SignalR 连接由父组件管理
})
</script>

<style scoped>
.slide-down-enter-active,
.slide-down-leave-active {
  transition: all 0.3s ease;
}
.slide-down-enter-from,
.slide-down-leave-to {
  transform: translateY(-100%);
  opacity: 0;
}

.upload-banner {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 2000;
  background: #fff;
  border-bottom: 1px solid #ebeef5;
  box-shadow: 0 2px 12px rgba(0, 0, 0, 0.1);
}

.banner-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 16px;
}

.banner-title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  font-weight: 600;
  color: #303133;
}

.banner-title .el-icon {
  color: #409eff;
}

.is-spinning {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.banner-actions {
  display: flex;
  gap: 4px;
}

.banner-body {
  padding: 0 16px 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.task-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}

.task-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 120px;
}

.task-name {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 160px;
}

.task-status {
  font-size: 12px;
  color: #909399;
}

.task-status.uploading { color: #e6a23c; }
.task-status.converting { color: #409eff; }
.task-status.done { color: #67c23a; }
.task-status.failed { color: #f56c6c; }

.panel-content {
  padding: 8px 0;
}

.empty-state {
  padding: 40px 0;
}

.task-detail {
  padding: 12px 16px;
  border-bottom: 1px solid #f0f0f0;
}

.task-detail:last-child {
  border-bottom: none;
}

.task-detail-header {
  display: flex;
  align-items: center;
  gap: 8px;
}

.task-detail-name {
  font-weight: 600;
  font-size: 14px;
}

.task-detail-stats {
  display: flex;
  gap: 16px;
  font-size: 12px;
  color: #909399;
  margin-top: 4px;
}

.text-danger {
  color: #f56c6c !important;
}

.task-detail-files {
  margin-top: 8px;
  max-height: 200px;
  overflow-y: auto;
  border: 1px solid #ebeef5;
  border-radius: 4px;
  padding: 4px 0;
}

.file-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 8px;
  font-size: 12px;
}

.file-row:hover {
  background: #f5f7fa;
}

.file-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-status {
  color: #909399;
  min-width: 60px;
  text-align: right;
}
</style>
