<template>
  <div class="auditor-workspace">
    <!-- 顶部 Header -->
    <div class="workspace-header">
      <div class="header-left">
        <div class="header-logo">审核员工作台</div>
      </div>
      <div class="header-right">
        <span class="user-name">{{ userName }}</span>
        <el-button text @click="handleLogout">退出登录</el-button>
      </div>
    </div>

    <!-- 内容区 -->
    <div class="workspace-content">
      <!-- 欢迎卡片 -->
      <div class="welcome-card">
        <div class="welcome-title">你好，{{ userTrueName || userName }}！</div>
        <div class="welcome-desc">欢迎使用映智汇认证审核系统，今天也要高效完成审核工作。</div>
      </div>

      <!-- 统计卡片行 -->
      <div class="stat-row">
        <div class="stat-card">
          <div class="stat-icon" style="background: #fef3c7; color: #d97706">
            <el-icon><clock /></el-icon>
          </div>
          <div class="stat-info">
            <div class="stat-value">{{ stats.pendingCount }}</div>
            <div class="stat-label">待审核任务</div>
          </div>
        </div>
        <div class="stat-card">
          <div class="stat-icon" style="background: #dcfce7; color: #059669">
            <el-icon><circle-check /></el-icon>
          </div>
          <div class="stat-info">
            <div class="stat-value">{{ stats.completedCount }}</div>
            <div class="stat-label">已完成任务</div>
          </div>
        </div>
        <div class="stat-card">
          <div class="stat-icon" style="background: #fee2e2; color: #dc2626">
            <el-icon><warning /></el-icon>
          </div>
          <div class="stat-info">
            <div class="stat-value">{{ stats.ncCount }}</div>
            <div class="stat-label">NC 不符合项</div>
          </div>
        </div>
        <div class="stat-card">
          <div class="stat-icon" style="background: #dbeafe; color: #2563eb">
            <el-icon><document /></el-icon>
          </div>
          <div class="stat-info">
            <div class="stat-value">{{ stats.totalTasks }}</div>
            <div class="stat-label">总任务数</div>
          </div>
        </div>
      </div>

      <!-- 快捷入口 -->
      <div class="quick-entry">
        <div class="section-title">快捷入口</div>
        <div class="entry-grid">
          <div class="entry-item" @click="goToTaskList">
            <el-icon class="entry-icon"><list /></el-icon>
            <span>我的任务</span>
          </div>
          <div class="entry-item" @click="goToMessages">
            <el-icon class="entry-icon"><bell /></el-icon>
            <span>消息中心</span>
          </div>
          <div class="entry-item" @click="goToProfile">
            <el-icon class="entry-icon"><user /></el-icon>
            <span>个人中心</span>
          </div>
        </div>
      </div>

      <!-- 最近任务列表 -->
      <div class="recent-tasks">
        <div class="section-title">最近待审任务</div>
        <div v-if="recentTasks.length === 0" class="empty-state">
          暂无待审任务
        </div>
        <div v-else class="task-list">
          <div
            v-for="task in recentTasks"
            :key="task.id"
            class="task-item"
            @click="goToTaskDetail(task.id)"
          >
            <div class="task-name">{{ task.taskName }}</div>
            <div class="task-meta">
              <span>企业：{{ task.enterpriseName }}</span>
              <span>类型：{{ task.taskType }}</span>
              <el-tag :type="task.status === '待审核' ? 'warning' : 'info'" size="small">
                {{ task.status }}
              </el-tag>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessageBox } from 'element-plus'
import store from '../../../store/index'
import http from '../../../api/http.js'

const router = useRouter()

// 用户信息
const userInfo = computed(() => store.getters.getUserInfo || {})
const userName = computed(() => userInfo.value.userName || '')
const userTrueName = computed(() => userInfo.value.userTrueName || '')

// 统计数据
const stats = ref({
  pendingCount: 0,
  completedCount: 0,
  ncCount: 0,
  totalTasks: 0
})

// 最近任务
const recentTasks = ref([])

// 页面加载时获取数据
onMounted(() => {
  loadUserInfo()
  loadStats()
  loadRecentTasks()
})

const loadUserInfo = () => {
  http.get('/api/AuditorAuth/GetCurrentUser', null, false).then((res) => {
    if (res.status && res.data) {
      store.commit('setUserInfo', {
        ...userInfo.value,
        roleId: res.data.roleId,
        userTrueName: res.data.userTrueName,
        userName: res.data.userName
      })
    }
  }).catch(err => {
    console.error('获取用户信息失败:', err)
  })
}

const loadStats = () => {
  // TODO: 后续接入真实 API
  stats.value = {
    pendingCount: 3,
    completedCount: 12,
    ncCount: 2,
    totalTasks: 15
  }
}

const loadRecentTasks = () => {
  // TODO: 后续接入真实 API
  recentTasks.value = [
    { id: 1, taskName: 'ISO9001 第一阶段审核 - 华为技术有限公司', enterpriseName: '华为技术有限公司', taskType: '第一阶段审核', status: '待审核' },
    { id: 2, taskName: 'ISO14001 监督审核 - 比亚迪股份有限公司', enterpriseName: '比亚迪股份有限公司', taskType: '监督审核', status: '待审核' },
    { id: 3, taskName: 'ISO45001 再认证 - 腾讯科技有限公司', enterpriseName: '腾讯科技有限公司', taskType: '再认证', status: '待审核' }
  ]
}

// 导航
const goToTaskList = () => {
  // TODO: 跳转到任务列表页
  alert('任务列表功能开发中...')
}

const goToMessages = () => {
  // TODO: 跳转到消息中心
  alert('消息中心功能开发中...')
}

const goToProfile = () => {
  // TODO: 跳转到个人中心
  alert('个人中心功能开发中...')
}

const goToTaskDetail = (id) => {
  // TODO: 跳转到任务详情
  alert('任务详情功能开发中...')
}

// 退出登录
const handleLogout = () => {
  ElMessageBox.confirm('确定要退出登录吗？', '提示', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning'
  }).then(() => {
    // 清除 store 中的登录信息
    store.commit('setUserInfo', null)
    // 跳转回审核员登录页
    router.push('/auditor-login')
  }).catch(() => {})
}

// 引入图标
import { Clock, CircleCheck, Warning, Document, List, Bell, User } from '@element-plus/icons-vue'
</script>

<style lang="less" scoped>
.auditor-workspace {
  width: 100%;
  height: 100%;
  background: #f8fafc;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  box-sizing: border-box;
}

.workspace-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  height: 56px;
  padding: 0 24px;
  background: #ffffff;
  border-bottom: 1px solid #e5e7eb;
  flex-shrink: 0;

  .header-left {
    .header-logo {
      font-size: 18px;
      font-weight: 600;
      color: #065f46;
    }
  }

  .header-right {
    display: flex;
    align-items: center;
    gap: 12px;

    .user-name {
      font-size: 14px;
      color: #374151;
    }
  }
}

.workspace-content {
  flex: 1;
  min-height: 0;
  padding: 24px;
  overflow-y: auto;
  box-sizing: border-box;
}

.welcome-card {
  background: linear-gradient(135deg, #059669 0%, #10b981 100%);
  border-radius: 12px;
  padding: 28px 32px;
  color: #fff;
  margin-bottom: 24px;

  .welcome-title {
    font-size: 22px;
    font-weight: 600;
    margin-bottom: 8px;
  }

  .welcome-desc {
    font-size: 14px;
    opacity: 0.9;
  }
}

.stat-row {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 16px;
  margin-bottom: 24px;
}

.stat-card {
  background: #fff;
  border-radius: 10px;
  padding: 20px;
  display: flex;
  align-items: center;
  gap: 16px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
  border: 1px solid #f3f4f6;

  .stat-icon {
    width: 48px;
    height: 48px;
    border-radius: 10px;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 22px;
  }

  .stat-info {
    .stat-value {
      font-size: 24px;
      font-weight: 700;
      color: #111827;
      line-height: 1.2;
    }

    .stat-label {
      font-size: 13px;
      color: #6b7280;
      margin-top: 4px;
    }
  }
}

.section-title {
  font-size: 16px;
  font-weight: 600;
  color: #111827;
  margin-bottom: 16px;
}

.quick-entry {
  background: #fff;
  border-radius: 10px;
  padding: 20px;
  margin-bottom: 24px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
  border: 1px solid #f3f4f6;
}

.entry-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
}

.entry-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 24px;
  border: 1px dashed #e5e7eb;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.3s;

  &:hover {
    border-color: #059669;
    background: #f0fdf4;
  }

  .entry-icon {
    font-size: 28px;
    color: #059669;
    margin-bottom: 8px;
  }

  span {
    font-size: 14px;
    color: #374151;
  }
}

.recent-tasks {
  background: #fff;
  border-radius: 10px;
  padding: 20px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
  border: 1px solid #f3f4f6;
}

.empty-state {
  text-align: center;
  padding: 40px;
  color: #9ca3af;
}

.task-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.task-item {
  padding: 16px;
  border: 1px solid #f3f4f6;
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.3s;

  &:hover {
    border-color: #059669;
    background: #f9fdf9;
  }

  .task-name {
    font-size: 14px;
    font-weight: 500;
    color: #111827;
    margin-bottom: 8px;
  }

  .task-meta {
    display: flex;
    align-items: center;
    gap: 16px;
    font-size: 13px;
    color: #6b7280;
  }
}

@media screen and (max-width: 768px) {
  .stat-row {
    grid-template-columns: repeat(2, 1fr);
  }
  .entry-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}
</style>
