<template>
  <div class="home-container">
    <el-scrollbar style="height: 100%">
      <div class="home-content">
        <div class="home-left">
          <!-- 1. 配置管理概览横幅 -->
          <div class="welcome-banner">
            <div class="banner-text">
              <h2>后台管理控制台</h2>
              <p>
                映智汇认证核心引擎配置中心已就绪，当前系统运行稳定，包含
                <span class="highlight">12</span> 套认证标准定义。
              </p>
            </div>
            <div class="banner-actions">
              <el-button type="primary" size="large" @click="handleAction('standard')"
                >标准管理</el-button
              >
            </div>
          </div>

          <!-- 2. 系统资产指标 -->
          <div class="home-list">
            <div class="list-item" v-for="(item, index) in list" :key="index">
              <div class="content" :class="'item-' + (index + 1)">
                <div class="content-left">
                  <div class="name">{{ item.name }}</div>
                  <div class="data">
                    {{ (item.qty + '').replace(/\B(?=(\d{3})+(?!\d))/g, ',') }}
                  </div>
                </div>
                <div class="content-icon">
                  <el-icon :size="32"><component :is="item.icon" /></el-icon>
                </div>
              </div>
            </div>
          </div>

          <!-- 3. 配置更新趋势 -->
          <div class="home-list-chart">
            <div class="chart-header">
              <div class="title-main">配置资产分布与更新</div>
            </div>
            <div id="h-chart1" style="height: 320px; width: 100%"></div>
          </div>

          <!-- 4. 核心配置状态 -->
          <div class="table-container">
            <div class="table-header">
              <div class="title-main">认证标准定义状态</div>
              <el-button link type="primary">进入规则库</el-button>
            </div>

            <table class="yzh-table">
              <thead>
                <tr>
                  <th style="width: 60px">#</th>
                  <th>标准名称</th>
                  <th>版本号</th>
                  <th>规则数量</th>
                  <th>工作流定义</th>
                  <th>最后修改人</th>
                  <th>最后更新时间</th>
                  <th>状态</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(row, index) in tableData" :key="index">
                  <td>{{ index + 1 }}</td>
                  <td class="bold">{{ row.name }}</td>
                  <td>{{ row.version }}</td>
                  <td>{{ row.ruleCount }}</td>
                  <td>
                    <el-tag size="small" :type="row.workflow ? 'success' : 'info'">{{
                      row.workflow ? '已部署' : '未配置'
                    }}</el-tag>
                  </td>
                  <td>{{ row.updater }}</td>
                  <td>{{ row.updateTime }}</td>
                  <td>
                    <el-tag :type="row.statusType" size="small" effect="dark">{{
                      row.status
                    }}</el-tag>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <div class="home-right">
          <!-- 5. 资源分布 -->
          <div class="right-card">
            <div class="card-header">
              <div class="title-main">配置类型占比</div>
            </div>
            <div id="chart-pie" style="height: 320px"></div>
          </div>

          <!-- 6. AI 引擎状态与日志 -->
          <div class="right-card msg-card">
            <div class="card-header">
              <div class="title-main">系统日志与 AI 监控</div>
            </div>
            <div class="msg-list">
              <div v-for="(item, index) in msg" :key="index" class="msg-item">
                <div class="msg-icon" :class="item.type">
                  <el-icon><component :is="item.icon" /></el-icon>
                </div>
                <div class="msg-body">
                  <div class="msg-title">{{ item.name }}</div>
                  <div class="msg-time">{{ item.time }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- 7. 核心配置入口 -->
          <div class="right-card quick-actions">
            <div class="card-header">
              <div class="title-main">核心配置入口</div>
            </div>
            <div class="action-grid">
              <div class="action-item" @click="handleAction('prompt')">
                <el-icon><Cpu /></el-icon>
                <span>AI 提示词</span>
              </div>
              <div class="action-item" @click="handleAction('nc')">
                <el-icon><Connection /></el-icon>
                <span>NC 逻辑</span>
              </div>
              <div class="action-item" @click="handleAction('report')">
                <el-icon><Files /></el-icon>
                <span>报告章节</span>
              </div>
              <div class="action-item" @click="handleAction('setting')">
                <el-icon><Setting /></el-icon>
                <span>系统参数</span>
              </div>
            </div>
          </div>
        </div>
      </div>
      <div class="footer-spacer"></div>
    </el-scrollbar>
  </div>
</template>

<script setup>
import * as echarts from 'echarts'
import { getCurrentInstance, onBeforeUnmount, onMounted, reactive, ref } from 'vue'

const { proxy } = getCurrentInstance()
const userInfo = proxy.$store.getters.getUserInfo()

const handleAction = (type) => {
  console.log('Navigate to:', type)
}

const msg = ref([
  { name: 'AI 审核引擎 3.0 模型参数已更新', time: '10分钟前', type: 'success', icon: 'Cpu' },
  { name: 'ISO 9001:2015 规则库完成版本冻结', time: '1小时前', type: 'info', icon: 'Lock' },
  { name: '系统检测到 AI 接口响应延迟波动', time: '3小时前', type: 'warning', icon: 'Warning' },
  { name: '新增 5 条 NC 自动检查逻辑定义', time: '昨天', type: 'info', icon: 'Connection' },
  { name: '管理员执行了全局参数备份', time: '2026-08-30', type: 'info', icon: 'Box' }
])

const list = ref([
  { name: '认证标准总数', qty: 12, icon: 'Document' },
  { name: 'NC 规则库条目', qty: 850, icon: 'Connection' },
  { name: '工作流定义', qty: 24, icon: 'Share' },
  { name: 'AI 提示词模板', qty: 156, icon: 'Cpu' }
])

let chart1, chartPie

const initCharts = () => {
  chart1 = echarts.init(document.getElementById('h-chart1'))
  chart1.setOption(getChartData())

  chartPie = echarts.init(document.getElementById('chart-pie'))
  chartPie.setOption(getChartPieData())

  window.addEventListener('resize', () => {
    chart1.resize()
    chartPie.resize()
  })
}

onMounted(() => {
  initCharts()
})

onBeforeUnmount(() => {
  window.removeEventListener('resize', () => {
    chart1?.resize()
    chartPie?.resize()
  })
})

const tableData = reactive([
  {
    name: 'ISO 9001:2015',
    version: 'V2.1',
    ruleCount: 124,
    workflow: true,
    updater: 'Admin',
    updateTime: '2026-09-01',
    status: '已发布',
    statusType: 'success'
  },
  {
    name: 'ISO 14001:2015',
    version: 'V1.5',
    ruleCount: 98,
    workflow: true,
    updater: 'Admin',
    updateTime: '2026-08-28',
    status: '已发布',
    statusType: 'success'
  },
  {
    name: 'ISO 45001:2018',
    version: 'V1.2',
    ruleCount: 112,
    workflow: false,
    updater: 'Yzh',
    updateTime: '2026-08-30',
    status: '草稿',
    statusType: 'info'
  },
  {
    name: 'ISO 27001:2022',
    version: 'V1.0',
    ruleCount: 156,
    workflow: true,
    updater: 'Admin',
    updateTime: '2026-08-25',
    status: '已下线',
    statusType: 'danger'
  }
])

const getChartData = () => {
  return {
    tooltip: { trigger: 'axis' },
    legend: { data: ['配置更新次数', '规则增长'], right: 0 },
    grid: { left: '0%', right: '2%', bottom: '0%', containLabel: true },
    xAxis: {
      type: 'category',
      data: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul'],
      axisLine: { lineStyle: { color: '#e2e8f0' } }
    },
    yAxis: {
      type: 'value',
      splitLine: { lineStyle: { type: 'dashed', color: '#f1f5f9' } }
    },
    series: [
      {
        name: '配置更新次数',
        type: 'line',
        smooth: true,
        data: [45, 52, 48, 70, 65, 85, 80],
        itemStyle: { color: '#2f54eb' },
        areaStyle: {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: 'rgba(47, 84, 235, 0.2)' },
            { offset: 1, color: 'rgba(47, 84, 235, 0)' }
          ])
        }
      }
    ]
  }
}

const getChartPieData = () => {
  return {
    tooltip: { trigger: 'item' },
    legend: { bottom: '0%', left: 'center', icon: 'circle' },
    series: [
      {
        name: '资源占比',
        type: 'pie',
        radius: ['50%', '75%'],
        itemStyle: { borderRadius: 10, borderColor: '#fff', borderWidth: 2 },
        label: { show: false },
        data: [
          { value: 40, name: '标准定义', itemStyle: { color: '#2f54eb' } },
          { value: 30, name: '规则配置', itemStyle: { color: '#1890ff' } },
          { value: 20, name: '工作流', itemStyle: { color: '#13c2c2' } },
          { value: 10, name: 'AI 模板', itemStyle: { color: '#722ed1' } }
        ]
      }
    ]
  }
}
</script>

<style lang="less" scoped>
.home-container {
  height: 100%;
  background-color: #f8fafc;
}

.home-content {
  padding: 32px;
  display: flex;
  gap: 32px;
  max-width: 1680px;
  margin: 0 auto;
}

.home-left {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 32px;
}

.home-right {
  width: 420px;
  display: flex;
  flex-direction: column;
  gap: 32px;
}

.title-main {
  font-size: 20px;
  font-weight: 800;
  color: #0f172a;
  letter-spacing: -0.5px;
}

/* 欢迎横幅 - 修正为管理后台风格 */
.welcome-banner {
  background: #ffffff;
  padding: 60px 64px; /* 增加内边距 */
  border-radius: 32px; /* 更大的圆角 */
  color: #0f172a;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: var(--yzh-shadow-lg);
  border: 1px solid #f1f5f9;
  position: relative;
  overflow: hidden;
  background-image:
    radial-gradient(at 0% 0%, rgba(47, 84, 235, 0.05) 0px, transparent 50%),
    radial-gradient(at 100% 0%, rgba(24, 144, 255, 0.05) 0px, transparent 50%);

  &::before {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    width: 8px;
    height: 100%;
    background: var(--yzh-gradient-primary);
  }

  .banner-text {
    h2 {
      font-size: 40px; /* 进一步增大标题 */
      margin: 0 0 16px 0;
      font-weight: 900;
      letter-spacing: -1px;
    }
    p {
      font-size: 19px; /* 提升正文字号 */
      margin: 0;
      color: #475569;
      .highlight {
        color: var(--yzh-color-primary);
        font-weight: 900;
        font-size: 28px;
        margin: 0 6px;
      }
    }
  }
}

/* 指标卡片 */
.home-list {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 24px;
}

.list-item .content {
  background: #fff;
  padding: 28px;
  border-radius: 24px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.02);
  border: 1px solid #f1f5f9;
  transition: all 0.3s ease;

  &:hover {
    transform: translateY(-4px);
    box-shadow: var(--yzh-shadow-md);
    border-color: var(--yzh-color-primary);
  }

  .name {
    color: #64748b;
    font-size: 15px;
    font-weight: 600;
    margin-bottom: 6px;
  }

  .data {
    font-size: 30px;
    font-weight: 800;
    color: #0f172a;
  }

  .content-icon {
    width: 56px;
    height: 56px;
    border-radius: 16px;
    display: flex;
    align-items: center;
    justify-content: center;
  }
}

.item-1 .content-icon {
  background: #eff6ff;
  color: #2563eb;
}
.item-2 .content-icon {
  background: #f0fdf4;
  color: #16a34a;
}
.item-3 .content-icon {
  background: #fffbeb;
  color: #d97706;
}
.item-4 .content-icon {
  background: #f5f3ff;
  color: #7c3aed;
}

/* 容器通用样式 */
.home-list-chart,
.right-card,
.table-container {
  background: #fff;
  padding: 32px;
  border-radius: 32px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.02);
  border: 1px solid #f1f5f9;
}

.chart-header,
.table-header,
.card-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

/* 表格样式 */
.yzh-table {
  width: 100%;
  border-collapse: collapse;

  th {
    text-align: left;
    padding: 16px;
    color: #64748b;
    font-weight: 700;
    border-bottom: 2px solid #f1f5f9;
    font-size: 14px;
  }

  td {
    padding: 18px 16px;
    color: #334155;
    border-bottom: 1px solid #f1f5f9;
    font-size: 15px;
    &.bold {
      font-weight: 700;
      color: #0f172a;
    }
  }

  tr:hover td {
    background-color: #f8fafc;
  }
}

/* 消息列表 */
.msg-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.msg-item {
  display: flex;
  gap: 14px;
  padding: 10px;
  border-radius: 12px;

  .msg-icon {
    width: 40px;
    height: 40px;
    border-radius: 10px;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;

    &.info {
      background: #eff6ff;
      color: #2563eb;
    }
    &.success {
      background: #f0fdf4;
      color: #16a34a;
    }
    &.warning {
      background: #fffbeb;
      color: #d97706;
    }
  }

  .msg-title {
    font-size: 15px;
    font-weight: 600;
    color: #1e293b;
    margin-bottom: 2px;
  }
  .msg-time {
    font-size: 13px;
    color: #94a3b8;
  }
}

/* 快速操作 */
.action-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.action-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 20px;
  background: #f8fafc;
  border-radius: 16px;
  cursor: pointer;
  transition: all 0.2s;
  gap: 10px;

  i {
    font-size: 24px;
    color: #2f54eb;
  }
  span {
    font-size: 14px;
    font-weight: 700;
    color: #334155;
  }

  &:hover {
    background: #2f54eb;
    i,
    span {
      color: #fff;
    }
    transform: translateY(-2px);
  }
}

.footer-spacer {
  height: 40px;
}
</style>
