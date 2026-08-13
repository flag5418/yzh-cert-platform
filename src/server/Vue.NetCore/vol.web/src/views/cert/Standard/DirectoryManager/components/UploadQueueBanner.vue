<template>
  <transition name="slide-down">
    <div v-if="visible" class="upload-banner">
      <div class="banner-header">
        <div class="banner-title">
          <el-icon :class="{ 'is-spinning': isAnyRunning }"><IconUpload /></el-icon>
          <span v-if="runningTasks.length > 0">上传队列进行中 ({{ runningTasks.length }} 个任务)</span>
          <span v-else>上传队列已完成 ({{ finishedTasks.length }} 个任务)</span>
        </div>
        <div class="banner-actions">
          <el-button
            v-if="finishedTasks.length > 0"
            type="info"
            link
            size="small"
            @click="clearFinished"
          >
            清除已完成
          </el-button>
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
          <el-button type="info" link size="small" @click="handleClose">
            <el-icon><IconClose /></el-icon>
          </el-button>
        </div>
      </div>

      <div v-if="expanded && activeTasks.length > 0" class="banner-body">
        <div v-for="task in activeTasks" :key="task.taskId" class="task-item">
          <div class="task-info">
            <span class="task-name">{{ task.name || task.directoryCode }}</span>
            <span class="task-status" :class="task.status">{{ statusText(task.status) }}</span>
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
        <el-empty description="当前没有上传任务" />
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
            <el-icon v-if="f.status === 'uploaded' || f.status === 'active'" color="var(--yzh-color-success)"><IconSuccess /></el-icon>
            <el-icon v-else-if="f.status === 'uploading'" color="var(--yzh-color-primary)" class="is-spinning"><IconLoading /></el-icon>
            <el-icon v-else-if="f.status === 'converting'" color="var(--yzh-color-primary)"><IconFile /></el-icon>
            <el-icon v-else-if="f.status === 'failed'" color="var(--yzh-color-danger)"><IconClose /></el-icon>
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
      <div v-if="activeTasks.length > 0" class="drawer-actions">
        <el-button
          v-if="finishedTasks.length > 0"
          type="info"
          plain
          size="small"
          @click="clearFinished"
        >
          清除已完成任务
        </el-button>
        <el-button
          v-if="runningTasks.length > 0"
          type="danger"
          plain
          size="small"
          @click="handleCancelAll"
        >
          取消全部任务
        </el-button>
      </div>
    </div>
  </el-drawer>
</template>

<script setup>
import { ref, computed, onUnmounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { IconUpload, IconSuccess, IconClose, IconLoading, IconFile } from '@/yzh'
import http from '@/api/http'

const props = defineProps({
  tasks: { type: Array, default: () => [] }
})

const emit = defineEmits(['cancel', 'clear-done'])

// 父组件传入的是当前阶段的任务（含已完成），这里直接展示完成/失败状态
const visible = computed(() => props.tasks.length > 0)
const runningTasks = computed(() => props.tasks.filter(t => t.status === 'uploading' || t.status === 'converting'))
const finishedTasks = computed(() => props.tasks.filter(t => t.status === 'done' || t.status === 'failed'))
const expanded = ref(false)
const showPanel = ref(false)

const isAnyRunning = computed(() => runningTasks.value.length > 0)

const activeTasks = computed(() => props.tasks)

const statusText = (status) => {
  const map = { uploading: '上传中', converting: '转换中', done: '已完成', failed: '失败', cancelled: '已取消', pending: '等待中' }
  return map[status] || status || ''
}

// 供父组件“上传队列”按钮打开详情面板
const openPanel = () => {
  showPanel.value = true
}
defineExpose({ openPanel })

const clearFinished = () => {
  emit('clear-done')
}

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
  const map = { uploading: 'warning', done: 'success', failed: 'danger', converting: 'primary', cancelled: 'info', pending: 'info' }
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
  background: var(--yzh-color-bg-card, #fff);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  box-shadow: var(--yzh-shadow-md, 0 2px 12px rgba(0, 0, 0, 0.06));
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
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: var(--yzh-font-weight-bold, 600);
  color: var(--yzh-color-text-primary, #303133);
}

.banner-title .el-icon {
  color: var(--yzh-color-primary, #409eff);
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
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #909399);
}

.task-status.uploading { color: var(--yzh-color-warning, #e6a23c); }
.task-status.converting { color: var(--yzh-color-primary, #409eff); }
.task-status.done { color: var(--yzh-color-success, #67c23a); }
.task-status.failed { color: var(--yzh-color-danger, #f56c6c); }

.panel-content {
  padding: 8px 0;
}

.drawer-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 16px 16px;
}

.empty-state {
  padding: 40px 0;
}

.task-detail {
  padding: 12px 16px;
  border-bottom: 1px solid var(--yzh-color-border-lighter, #f0f0f0);
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
  gap: var(--yzh-space-4, 16px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #909399);
  margin-top: 4px;
}

.text-danger {
  color: var(--yzh-color-danger, #f56c6c) !important;
}

.task-detail-files {
  margin-top: 8px;
  max-height: 200px;
  overflow-y: auto;
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
  border-radius: var(--yzh-radius-sm, 4px);
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
  background: var(--yzh-color-bg-hover, #f5f7fa);
}

.file-name {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-status {
  color: var(--yzh-color-text-secondary, #909399);
  min-width: 60px;
  text-align: right;
}
</style>
