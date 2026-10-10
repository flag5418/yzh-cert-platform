<template>
  <el-dropdown trigger="click" :popper-class="'yzh-msg-bell__popper'" @command="handleCommand">
    <span class="yzh-msg-bell">
      <el-badge :value="unreadCount" :max="99" :hidden="unreadCount === 0" class="yzh-msg-bell__badge">
        <el-icon :size="18" class="yzh-msg-bell__icon"><Bell /></el-icon>
      </el-badge>
    </span>
    <template #dropdown>
      <el-dropdown-menu>
        <div class="yzh-msg-dropdown">
          <div class="yzh-msg-dropdown__header">
            <span class="yzh-msg-dropdown__title">消息通知</span>
            <el-button v-if="unreadCount > 0" type="primary" link size="small" @click.stop="handleMarkAllRead">
              全部已读
            </el-button>
          </div>
          <el-scrollbar :height="320" class="yzh-msg-dropdown__list">
            <div v-if="loading" class="yzh-msg-dropdown__empty">
              <el-empty description="加载中..." :image-size="40" />
            </div>
            <div v-else-if="messageList.length === 0" class="yzh-msg-dropdown__empty">
              <el-empty description="暂无消息" :image-size="40" />
            </div>
            <div
              v-for="msg in messageList"
              :key="msg.id"
              class="yzh-msg-dropdown__item"
              :class="{ 'yzh-msg-dropdown__item--unread': msg.isRead === 0 }"
              @click.stop="handleMarkRead(msg)"
            >
              <span v-if="msg.isRead === 0" class="yzh-msg-dropdown__dot" />
              <div class="yzh-msg-dropdown__title">{{ msg.title }}</div>
              <div class="yzh-msg-dropdown__content">{{ msg.content }}</div>
              <div class="yzh-msg-dropdown__meta">
                <el-tag :type="getMessageTypeTag(msg.messageType)" size="small">{{ getMessageTypeLabel(msg.messageType) }}</el-tag>
                <span class="yzh-msg-dropdown__time">{{ formatTime(msg.createTime) }}</span>
              </div>
            </div>
          </el-scrollbar>
          <div class="yzh-msg-dropdown__footer">
            <el-button type="primary" link size="small" @click.stop="handleViewAll">查看全部消息</el-button>
          </div>
        </div>
      </el-dropdown-menu>
    </template>
  </el-dropdown>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { Bell } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { getUnreadCount, getMessageList, markMessageRead, markAllRead, type MessageItem } from '../../api/message'
import { yzhPush } from '@yzh-core/api/push'

const unreadCount = ref(0)
const messageList = ref<MessageItem[]>([])
const loading = ref(false)
let refreshTimer: ReturnType<typeof setInterval> | null = null
let offQueueProgress: (() => void) | null = null
let offLogout: (() => void) | null = null

async function fetchUnreadCount() {
  try {
    const res = await getUnreadCount()
    unreadCount.value = res.data ?? 0
  } catch {
    // 静默失败，不影响主流程
  }
}

async function fetchMessageList() {
  loading.value = true
  try {
    const res = await getMessageList({ page: 1, pageSize: 10 })
    messageList.value = res.data ?? []
  } catch {
    // 静默失败
  } finally {
    loading.value = false
  }
}

async function handleMarkRead(msg: MessageItem) {
  if (msg.isRead === 1) return
  try {
    const res = await markMessageRead(msg.code)
    if (res.data) {
      msg.isRead = 1
      unreadCount.value = Math.max(0, unreadCount.value - 1)
    }
  } catch {
    ElMessage.error('标记已读失败')
  }
}

async function handleMarkAllRead() {
  try {
    const res = await markAllRead()
    if (res.data) {
      unreadCount.value = 0
      messageList.value = messageList.value.map((m) => ({ ...m, isRead: 1 }))
      ElMessage.success('已全部标记为已读')
    }
  } catch {
    ElMessage.error('全部已读失败')
  }
}

function handleViewAll() {
  window.dispatchEvent(new CustomEvent('yzh-navigate', { detail: '/system/message' }))
}

function onPushMessage() {
  fetchUnreadCount()
  if (messageList.value.length > 0) {
    fetchMessageList()
  }
}

function startPolling() {
  refreshTimer = setInterval(() => {
    fetchUnreadCount()
  }, 30000)
}

function stopPolling() {
  if (refreshTimer !== null) {
    clearInterval(refreshTimer)
    refreshTimer = null
  }
}

function handleCommand(_cmd: string) {
  // 预留扩展
}

function getMessageTypeTag(type: string) {
  const map: Record<string, 'success' | 'warning' | 'primary' | 'info'> = {
    queue: 'primary',
    convert: 'success',
    system: 'info',
    task: 'warning',
  }
  return map[type] ?? 'info'
}

function getMessageTypeLabel(type: string) {
  const map: Record<string, string> = {
    queue: '队列通知',
    convert: '文件转换',
    system: '系统消息',
    task: '任务通知',
  }
  return map[type] ?? '消息'
}

function formatTime(timeStr: string) {
  if (!timeStr) return ''
  const d = new Date(timeStr)
  const now = new Date()
  const diff = now.getTime() - d.getTime()
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)}分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}小时前`
  return `${d.getMonth() + 1}/${d.getDate()} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`
}

onMounted(() => {
  fetchUnreadCount()
  startPolling()
  offQueueProgress = yzhPush.on('queue_progress', onPushMessage)
  offLogout = yzhPush.on('logout', onPushMessage)
})

onUnmounted(() => {
  stopPolling()
  offQueueProgress?.()
  offLogout?.()
})
</script>

<style scoped>
.yzh-msg-bell {
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  padding: 4px;
  border-radius: 4px;
  transition: background-color 0.2s;
}

.yzh-msg-bell:hover {
  background-color: var(--yzh-hover-bg, rgba(0, 0, 0, 0.04));
}

.yzh-msg-bell__icon {
  color: var(--yzh-text-secondary, #909399);
}

.yzh-msg-dropdown {
  width: 340px;
}

.yzh-msg-dropdown__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px 8px;
  border-bottom: 1px solid var(--el-border-color-light, #e4e7ed);
}

.yzh-msg-dropdown__title {
  font-size: var(--yzh-font-size-base, 14px);
  font-weight: 600;
  color: var(--yzh-text-primary, #303133);
}

.yzh-msg-dropdown__list {
  min-height: 80px;
}

.yzh-msg-dropdown__empty {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px 0;
}

.yzh-msg-dropdown__item {
  padding: 10px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter, #f2f6fc);
  cursor: pointer;
  transition: background-color 0.15s;
}

.yzh-msg-dropdown__item:hover {
  background-color: var(--yzh-hover-bg, #f5f7fa);
}

.yzh-msg-dropdown__item--unread {
  background-color: var(--yzh-primary-light, #ecf5ff);
}

.yzh-msg-dropdown__dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background-color: #f56c6c;
  margin-right: 6px;
  flex-shrink: 0;
}

.yzh-msg-dropdown__title {
  font-size: var(--yzh-font-size-base, 14px);
  color: var(--yzh-text-primary, #303133);
  font-weight: 500;
  line-height: 1.5;
  display: flex;
  align-items: center;
}

.yzh-msg-dropdown__content {
  font-size: var(--yzh-font-size-sm, 12px);
  color: var(--yzh-text-secondary, #909399);
  margin-top: 4px;
  line-height: 1.4;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.yzh-msg-dropdown__meta {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 6px;
}

.yzh-msg-dropdown__time {
  font-size: var(--yzh-font-size-xs, 11px);
  color: var(--yzh-text-placeholder, #c0c4cc);
}

.yzh-msg-dropdown__footer {
  padding: 8px 16px;
  border-top: 1px solid var(--el-border-color-light, #e4e7ed);
  text-align: center;
}
</style>
