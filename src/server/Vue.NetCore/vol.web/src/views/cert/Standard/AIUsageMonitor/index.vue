<template>
  <div class="ai-usage-page">
    <CertPageHeader title="AI 费用监控" :icon="IconMoney" />

    <!-- 快捷时间范围 -->
    <el-card shadow="never" class="quick-range-card">
      <el-radio-group v-model="rangeType" @change="onRangeChange">
        <el-radio-button label="7d">近 7 天</el-radio-button>
        <el-radio-button label="30d">近 30 天</el-radio-button>
        <el-radio-button label="90d">近 90 天</el-radio-button>
        <el-radio-button label="custom">自定义</el-radio-button>
      </el-radio-group>
      <el-date-picker
        v-if="rangeType === 'custom'"
        v-model="customRange"
        type="daterange"
        range-separator="至"
        start-placeholder="开始日期"
        end-placeholder="结束日期"
        value-format="YYYY-MM-DD"
        style="margin-left: 12px; width: 260px"
        @change="loadData"
      />
      <el-button type="primary" size="small" :icon="IconRefresh" @click="loadData" style="margin-left: 12px">
        刷新
      </el-button>
      <el-link
        v-if="aliyunConfigured"
        :href="aliyunDashboardUrl"
        target="_blank"
        type="primary"
        style="margin-left: 12px"
      >
        查看阿里云实时余额
      </el-link>
    </el-card>

    <!-- 费用摘要卡片 -->
    <el-row :gutter="16" class="summary-row">
      <el-col :span="6">
        <el-card shadow="hover" class="summary-card">
          <div class="summary-label">累计总费用</div>
          <div class="summary-value" style="color: #f56c6c">
            ${{ summary.totalCost.toFixed(4) }}
          </div>
          <div class="summary-sub">累计 {{ summary.totalCalls }} 次调用</div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover" class="summary-card">
          <div class="summary-label">本月费用</div>
          <div class="summary-value" style="color: #e6a23c">
            ${{ summary.monthCost.toFixed(4) }}
          </div>
          <div class="summary-sub">{{ summary.monthCalls }} 次调用</div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover" class="summary-card">
          <div class="summary-label">本周费用</div>
          <div class="summary-value" style="color: #409eff">
            ${{ summary.weekCost.toFixed(4) }}
          </div>
          <div class="summary-sub">{{ summary.weekCalls }} 次调用</div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card shadow="hover" class="summary-card">
          <div class="summary-label">今日费用</div>
          <div class="summary-value" style="color: #67c23a">
            ${{ summary.todayCost.toFixed(4) }}
          </div>
          <div class="summary-sub">{{ summary.todayCalls }} 次调用</div>
        </el-card>
      </el-col>
    </el-row>

    <!-- 趋势图 -->
    <el-card shadow="never" class="chart-card">
      <template #header>
        <span class="card-title">费用趋势（按日）</span>
      </template>
      <div ref="chartRef" style="height: 300px"></div>
    </el-card>

    <!-- 调用明细表 -->
    <el-card shadow="never" class="table-card">
      <template #header>
        <span class="card-title">最近调用记录</span>
      </template>
      <el-table :data="callsList" border stripe v-loading="tableLoading" style="width: 100%">
        <el-table-column prop="createDate" label="时间" width="160">
          <template #default="{ row }">
            {{ formatDate(row.createDate) }}
          </template>
        </el-table-column>
        <el-table-column prop="skill" label="模式" width="80">
          <template #default="{ row }">
            <el-tag :type="row.skill === 'analyze' ? 'success' : 'warning'" size="small">
              {{ row.skill === 'analyze' ? '分析' : '提取' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="model" label="模型" width="120" />
        <el-table-column prop="promptTokens" label="输入tokens" width="110" align="right" />
        <el-table-column prop="completionTokens" label="输出tokens" width="110" align="right" />
        <el-table-column prop="totalTokens" label="总tokens" width="100" align="right" />
        <el-table-column prop="costUsd" label="费用(USD)" width="110" align="right">
          <template #default="{ row }">
            <span :style="{ color: row.success ? '#67c23a' : '#f56c6c' }">
              ${{ row.costUsd.toFixed(4) }}
            </span>
          </template>
        </el-table-column>
        <el-table-column prop="durationMs" label="耗时(ms)" width="100" align="right" />
        <el-table-column prop="success" label="状态" width="80" align="center">
          <template #default="{ row }">
            <el-tag :type="row.success ? 'success' : 'danger'" size="small">
              {{ row.success ? '成功' : '失败' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="errorMessage" label="错误信息" min-width="150" show-overflow-tooltip />
      </el-table>
      <el-pagination
        v-model:current-page="page"
        :page-size="pageSize"
        :total="total"
        layout="total, prev, pager, next"
        style="margin-top: 16px; justify-content: flex-end"
        @current-change="loadCalls"
      />
    </el-card>
  </div>
</template>

<script setup>
import { ref, onMounted, onBeforeUnmount, getCurrentInstance } from 'vue'
import { ElMessage } from 'element-plus'
import * as echarts from 'echarts'
import { CertPageHeader } from '@/certcore'
import { IconRefresh, IconMoney } from '@/yzh/icons'

const { proxy } = getCurrentInstance()

// 状态
const rangeType = ref('7d')
const customRange = ref(null)
const summary = ref({ totalCost: 0, monthCost: 0, weekCost: 0, todayCost: 0, totalCalls: 0, monthCalls: 0, weekCalls: 0, todayCalls: 0 })
const dailyCosts = ref([])
const callsList = ref([])
const tableLoading = ref(false)
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const aliyunConfigured = ref(false)
const aliyunDashboardUrl = ref('')
const chartRef = ref(null)
let chartInstance = null

// 计算日期范围
function getDateRange() {
  const end = new Date()
  let start = new Date()
  if (rangeType.value === '7d') start.setDate(end.getDate() - 7)
  else if (rangeType.value === '30d') start.setDate(end.getDate() - 30)
  else if (rangeType.value === '90d') start.setDate(end.getDate() - 90)
  else if (rangeType.value === 'custom' && customRange.value) {
    start = new Date(customRange.value[0])
    end.setDate(new Date(customRange.value[1]).getDate())
  }
  return { startDate: start.toISOString().split('T')[0], endDate: end.toISOString().split('T')[0] }
}

async function loadData() {
  try {
    const { startDate, endDate } = getDateRange()
    const [summaryRes, dailyRes, aliyunRes] = await Promise.all([
      proxy.http.get('api/ai-usage/summary', null, false),
      proxy.http.get(`api/ai-usage/daily-costs?startDate=${startDate}&endDate=${endDate}`, null, false),
      proxy.http.get('api/ai-usage/aliyun-status', null, false)
    ])
    if (summaryRes?.status) summary.value = summaryRes.data || {}
    if (dailyRes?.status) {
      dailyCosts.value = dailyRes.data || []
      renderChart()
    }
    if (aliyunRes?.status) aliyunConfigured.value = aliyunRes.data?.configured || false
  } catch (e) {
    console.error('加载数据失败', e)
  }
}

async function loadCalls() {
  tableLoading.value = true
  try {
    const { startDate, endDate } = getDateRange()
    const res = await proxy.http.get(
      `api/ai-usage/calls?page=${page.value}&pageSize=${pageSize.value}&startDate=${startDate}&endDate=${endDate}`,
      null, false
    )
    if (res?.status) {
      callsList.value = res.data || []
      total.value = res.total || 0
    }
  } catch (e) {
    console.error('加载调用记录失败', e)
  } finally {
    tableLoading.value = false
  }
}

async function loadAliyunStatus() {
  try {
    const res = await proxy.http.get('api/ai-usage/aliyun-status', null, false)
    if (res?.status) aliyunConfigured.value = res.data?.configured || false
  } catch (e) { /* ignore */ }
}

function onRangeChange() {
  page.value = 1
  loadData()
  loadCalls()
}

function renderChart() {
  if (!chartRef.value) return
  if (chartInstance) {
    chartInstance.dispose()
    chartInstance = null
  }
  chartInstance = echarts.init(chartRef.value)
  const dates = dailyCosts.value.map(d => d.date)
  const costs = dailyCosts.value.map(d => parseFloat(d.cost.toFixed(4)))
  const calls = dailyCosts.value.map(d => d.calls)

  chartInstance.setOption({
    tooltip: { trigger: 'axis', axisPointer: { type: 'cross' } },
    legend: { data: ['费用(USD)', '调用次数'] },
    grid: { left: '3%', right: '4%', bottom: '3%', containLabel: true },
    xAxis: { type: 'category', data: dates, axisLabel: { rotate: 30 } },
    yAxis: [
      { type: 'value', name: '费用(USD)', position: 'left' },
      { type: 'value', name: '调用次数', position: 'right' }
    ],
    series: [
      {
        name: '费用(USD)',
        type: 'line',
        smooth: true,
        data: costs,
        itemStyle: { color: '#409eff' },
        areaStyle: { color: 'rgba(64,158,255,0.1)' }
      },
      {
        name: '调用次数',
        type: 'bar',
        yAxisIndex: 1,
        data: calls,
        itemStyle: { color: 'rgba(103,194,58,0.6)' }
      }
    ]
  })
}

function formatDate(dateStr) {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  return d.toLocaleString('zh-CN', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
}

onMounted(() => {
  loadData()
  loadCalls()
})

onBeforeUnmount(() => {
  chartInstance?.dispose()
  window.removeEventListener('resize', () => chartInstance?.resize())
})
</script>

<style scoped lang="less">
.ai-usage-page {
  padding: 16px;
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}

.quick-range-card {
  margin-bottom: var(--yzh-space-4, 16px);
}

.summary-row {
  margin-bottom: var(--yzh-space-4, 16px);
}

.summary-card {
  text-align: center;
  padding: 8px 0;

  .summary-label {
    font-size: 13px;
    color: var(--yzh-color-text-secondary, #909399);
    margin-bottom: 8px;
  }

  .summary-value {
    font-size: 26px;
    font-weight: 700;
    line-height: 1.2;
  }

  .summary-sub {
    font-size: 12px;
    color: var(--yzh-color-text-placeholder, #c0c4cc);
    margin-top: 6px;
  }
}

.chart-card, .table-card {
  margin-bottom: var(--yzh-space-4, 16px);

  .card-title {
    font-size: 15px;
    font-weight: 600;
    color: var(--yzh-color-text-primary, #303133);
  }
}
</style>
