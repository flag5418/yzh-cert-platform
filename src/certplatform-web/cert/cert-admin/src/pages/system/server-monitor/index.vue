<script setup lang="ts">
/**
 * 服务监控页（平台管理 → 服务监控）
 *
 * 展示运行机器的 CPU / 内存 / 磁盘 / 网络 实时指标。
 * 每 5s 轮询一次后端（后端通过 macOS 原生命令采集），前端保留 60 个采样点（5 分钟）绘制走势。
 *
 * 路由：business/server-monitor
 * 菜单：Sys_Menu.Url = /business/server-monitor
 *
 * ⚠️ 字段名 PascalCase 逐字一致（DB列 = C#属性 = TS 字段），⛔ 不要改成 camelCase。
 */
import { computed, onMounted, onUnmounted, ref, watch, type Component } from 'vue'
import { ElMessage } from 'element-plus'
import {
  Monitor,
  Cpu,
  Coin,
  FolderOpened,
  SwitchButton,
  Refresh,
  WarningFilled,
} from '@element-plus/icons-vue'
import { YzhTable } from '@yzh-core'
import type { YzhTableColumn } from '@yzh-core'
import { getServerMetrics, type ServerMonitorData, type DiskInfo } from '@share/api/system/server-monitor'
import {
  buildSeriesPath,
  diskPercentOf,
  formatBytes,
  formatDateTime,
  formatRate,
  humanizeUptime,
  levelOf,
  levelText,
  rateFromSamples,
  worstLevel,
  type Level,
  type MonitorSample,
} from './logic'

// ──── 常量 ────
/** 轮询周期（毫秒） */
const REFRESH_MS = 5000
/** 轮询周期（秒，用于文案） */
const REFRESH_SECONDS = REFRESH_MS / 1000
/** 走势保留的采样点数：60 × 5s = 5 分钟 */
const HISTORY_MAX = 60
/** 自动刷新开关持久化键 */
const AUTOREFRESH_KEY = 'yzh.server-monitor.autorefresh'
/** 采集未就绪时的占位文案 */
const WAITING = '正在采集…'

/** 指标卡片描述（CPU / 内存 / 磁盘 结构一致；网络卡额外带 in/out 速率） */
interface MetricCard {
  key: 'cpu' | 'memory' | 'disk' | 'network'
  label: string
  icon: Component
  value: string
  unit: string
  percent: number
  footer: string
  net?: { inRate: string; outRate: string }
}

// ──── 状态 ────
const loading = ref(false)
const metrics = ref<ServerMonitorData | null>(null)
const samples = ref<MonitorSample[]>([])
const lastUpdated = ref('')
const error = ref('')
const autoRefresh = ref(localStorage.getItem(AUTOREFRESH_KEY) !== '0')

let timer: ReturnType<typeof setInterval> | null = null
/** 请求互斥：上一轮未回来时跳过本轮，避免弱网下堆积 */
let inflight = false
/** 断流提示只弹一次，避免每 5s 弹一屏 */
let errorNotified = false

// ──── 工具 ────
function levelColorVar(level: Level): string {
  if (level === 'danger') return 'var(--yzh-color-danger, #dc2626)'
  if (level === 'warning') return 'var(--yzh-color-warning, #d97706)'
  return 'var(--yzh-color-success, #16a34a)'
}

function progressColor(pct: number): string {
  return levelColorVar(levelOf(pct))
}

const pad2 = (n: number) => String(n).padStart(2, '0')

/** 本地时钟 HH:mm:ss（页面「更新于」用） */
function formatClock(d: Date): string {
  return `${pad2(d.getHours())}:${pad2(d.getMinutes())}:${pad2(d.getSeconds())}`
}

// ──── 计算属性 ────
const diskUsagePercent = computed(() => diskPercentOf(metrics.value))

const sortedDisks = computed<DiskInfo[]>(() =>
  [...(metrics.value?.Disks ?? [])].sort((a, b) => b.UsagePercent - a.UsagePercent)
)

/** 最满的分区（磁盘卡的说明文字用） */
const fullestDisk = computed<DiskInfo | null>(() => sortedDisks.value[0] ?? null)

const uptimeText = computed(() => humanizeUptime(metrics.value?.UpTime))

/** 相邻两点的网络吞吐（字节/秒） */
const netRate = computed(() => rateFromSamples(samples.value))

/**
 * 综合健康度：CPU / 内存 / 整机磁盘使用率 三取最差。
 * ⚠️ 磁盘不按「单分区最大值」判定 —— macOS 的 /Library/Developer、/System/Volumes/*
 * 常驻 95%+，会把整机状态永久拖成「告警」，反而让真正的告警失去意义。
 * 单分区风险仍在磁盘卡与分区表里单独标注。
 */
const health = computed<{ level: Level; text: string }>(() => {
  const m = metrics.value
  if (!m) return { level: 'success', text: '等待数据' }
  const worst = worstLevel([
    levelOf(m.CpuUsage),
    levelOf(m.MemoryUsagePercent),
    levelOf(diskUsagePercent.value),
  ])
  return { level: worst, text: worst === 'danger' ? '资源告警' : worst === 'warning' ? '资源偏高' : '运行正常' }
})

const cards = computed<MetricCard[]>(() => {
  const m = metrics.value
  const rate = netRate.value
  return [
    {
      key: 'cpu',
      label: 'CPU 使用率',
      icon: Cpu,
      value: m ? String(m.CpuUsage) : '--',
      unit: m ? '%' : '',
      percent: m?.CpuUsage ?? 0,
      footer: m ? `${m.CpuCores} 核心 · ${levelText(m.CpuUsage)}` : WAITING,
    },
    {
      key: 'memory',
      label: '内存使用率',
      icon: Coin,
      value: m ? String(m.MemoryUsagePercent) : '--',
      unit: m ? '%' : '',
      percent: m?.MemoryUsagePercent ?? 0,
      footer: m ? `${formatBytes(m.MemoryUsed)} / ${formatBytes(m.MemoryTotal)}` : WAITING,
    },
    {
      key: 'disk',
      label: '磁盘使用率',
      icon: FolderOpened,
      value: m ? String(diskUsagePercent.value) : '--',
      unit: m ? '%' : '',
      percent: diskUsagePercent.value,
      footer: m
        ? `最满 ${fullestDisk.value?.UsagePercent ?? 0}% · ${fullestDisk.value?.Name ?? '--'}`
        : WAITING,
    },
    {
      key: 'network',
      label: '网络吞吐 (en0)',
      icon: SwitchButton,
      value: formatRate(rate.in, rate.ready),
      unit: '',
      percent: 0,
      footer: m ? `累计 ↓ ${formatBytes(m.NetworkIn)} · ↑ ${formatBytes(m.NetworkOut)}` : WAITING,
      net: { inRate: formatRate(rate.in, rate.ready), outRate: formatRate(rate.out, rate.ready) },
    },
  ]
})

/**
 * 走势折线：viewBox 固定 0..100（百分比坐标），x 按固定槽位排布。
 * 不足 60 点时**右对齐**（最新一点贴右边）—— 实时监控的约定是「时间向右流动」，
 * 左对齐会让刚打开的页面只有左上角一小段线，看上去像图表坏了。
 */
const series = computed(() => {
  const last = samples.value[samples.value.length - 1]
  return [
    { key: 'cpu', label: 'CPU', latest: last?.cpu ?? 0, path: buildSeriesPath(samples.value, (s) => s.cpu, HISTORY_MAX) },
    { key: 'memory', label: '内存', latest: last?.mem ?? 0, path: buildSeriesPath(samples.value, (s) => s.mem, HISTORY_MAX) },
    { key: 'disk', label: '磁盘', latest: last?.disk ?? 0, path: buildSeriesPath(samples.value, (s) => s.disk, HISTORY_MAX) },
  ]
})

// ──── 磁盘分区表 ────
/**
 * 磁盘列（总宽预算 540px：1280 窗口下右栏约 580，再减表格内边距）。
 * ⚠️ 曾开 6 列（挂载点/设备/总量/已用/可用/使用率，合计 850+），
 * 1280 与 1440 下「使用率」整列被面板 overflow 裁掉且不会滚动 —— 仪表列必须留在可见区。
 * 现改为：设备并入挂载点第二行、可用并入已用第二行。
 */
const diskColumns = computed<YzhTableColumn<DiskInfo>[]>(() => [
  { prop: 'Name', label: '挂载点 / 设备', minWidth: 170, slot: true },
  { label: '总量', minWidth: 84, slotKey: 'total', slot: true, align: 'right' },
  { label: '已用 / 可用', minWidth: 112, slotKey: 'used', slot: true, align: 'right' },
  { label: '使用率', width: 172, slotKey: 'pct', slot: true },
])

// ──── 数据加载 ────
function pushSample(d: ServerMonitorData) {
  const next: MonitorSample = {
    t: Date.now(),
    cpu: d.CpuUsage,
    mem: d.MemoryUsagePercent,
    disk: diskPercentOf(d),
    netIn: d.NetworkIn,
    netOut: d.NetworkOut,
  }
  samples.value = [...samples.value, next].slice(-HISTORY_MAX)
}

/**
 * 拉一次快照。
 * @param silent 后台轮询传 true —— 不点亮按钮 loading，否则「刷新」每 5 秒闪一次转圈。
 */
async function loadMetrics(silent = false) {
  if (inflight) return
  inflight = true
  if (!silent) loading.value = true
  try {
    const data = await getServerMetrics()
    metrics.value = data
    pushSample(data)
    lastUpdated.value = formatClock(new Date())
    error.value = ''
    errorNotified = false
  } catch (e: unknown) {
    // 保留最近一次快照上屏，只标注「已断开」，不把页面清空
    error.value = e instanceof Error ? e.message : String(e ?? '网络异常')
    if (!errorNotified) {
      ElMessage.error('采集系统指标失败：' + error.value)
      errorNotified = true
    }
  } finally {
    inflight = false
    loading.value = false
  }
}

function handleRefresh() {
  loadMetrics()
}

// ──── 轮询控制 ────
function stopTimer() {
  if (timer !== null) {
    clearInterval(timer)
    timer = null
  }
}

function startTimer() {
  stopTimer()
  timer = setInterval(() => {
    // 页面被切到后台时不采样：省掉一整天的无效轮询与日志
    if (document.hidden) return
    loadMetrics(true)
  }, REFRESH_MS)
}

watch(autoRefresh, (on) => {
  localStorage.setItem(AUTOREFRESH_KEY, on ? '1' : '0')
  if (on) {
    startTimer()
    loadMetrics()
  } else {
    stopTimer()
  }
})

onMounted(() => {
  loadMetrics()
  if (autoRefresh.value) startTimer()
})

onUnmounted(stopTimer)
</script>

<template>
  <div class="sm-page">
    <!-- ══════ 顶栏 ══════ -->
    <header class="sm-header">
      <div class="sm-header__left">
        <span class="sm-header__icon"><el-icon><Monitor /></el-icon></span>
        <div class="sm-header__titles">
          <div class="sm-header__title-row">
            <h1 class="sm-header__title">服务监控</h1>
            <span class="sm-status" :class="`sm-status--${health.level}`">
              <i class="sm-status__dot" />{{ health.text }}
            </span>
          </div>
          <p class="sm-header__subtitle">
            {{ metrics?.MachineName || '--' }} · CPU {{ metrics?.CpuCores ?? '--' }} 核 ·
            每 {{ REFRESH_SECONDS }} 秒采样
          </p>
        </div>
      </div>

      <div class="sm-header__actions">
        <span class="sm-header__updated">更新于 {{ lastUpdated || '--' }}</span>
        <label class="sm-toggle">
          <el-switch v-model="autoRefresh" size="small" />
          <span class="sm-toggle__text">自动刷新</span>
        </label>
        <el-button type="primary" :icon="Refresh" :loading="loading" plain @click="handleRefresh">
          刷新
        </el-button>
      </div>
    </header>

    <div v-loading="loading && !metrics" class="sm-body" element-loading-text="正在采集系统指标">
      <!-- 断流提示（保留上次快照） -->
      <div v-if="error" class="sm-alert" role="alert">
        <el-icon class="sm-alert__icon"><WarningFilled /></el-icon>
        <span class="sm-alert__text">
          采集失败：{{ error }}<template v-if="lastUpdated">，当前显示 {{ lastUpdated }} 的快照</template>
        </span>
        <el-button type="default" size="small" @click="handleRefresh">重试</el-button>
      </div>

      <!-- ══════ 指标卡 ══════ -->
      <section class="sm-metrics">
        <article
          v-for="card in cards"
          :key="card.key"
          class="sm-metric"
          :class="`sm-metric--${card.key}`"
        >
          <span class="sm-metric__icon"><el-icon><component :is="card.icon" /></el-icon></span>
          <div class="sm-metric__info">
            <span class="sm-metric__label">{{ card.label }}</span>
            <div class="sm-metric__value-row">
              <span
                class="sm-metric__value"
                :class="{ 'sm-metric__value--text': card.key === 'network' }"
              >{{ card.value }}<small v-if="card.unit" class="sm-metric__unit">{{ card.unit }}</small></span>
            </div>

            <!-- CPU / 内存 / 磁盘：水位条 -->
            <el-progress
              v-if="card.key !== 'network'"
              :percentage="card.percent"
              :color="progressColor(card.percent)"
              :stroke-width="6"
              :show-text="false"
            />
            <!-- 网络：上下行速率 -->
            <div v-else class="sm-metric__rates">
              <span class="sm-metric__rate"><i class="sm-metric__rate-key">↑</i>{{ card.net?.outRate }}</span>
            </div>

            <span class="sm-metric__footer" :title="card.footer">{{ card.footer }}</span>
          </div>
        </article>
      </section>

      <!-- ══════ 走势 ══════ -->
      <section class="sm-panel sm-trend">
        <div class="sm-panel__header sm-panel__header--row">
          <div class="sm-panel__heading">
            <h3 class="sm-panel__title">资源走势</h3>
            <span class="sm-panel__meta">最近 5 分钟 · {{ samples.length }}/{{ HISTORY_MAX }} 采样点</span>
          </div>
          <ul class="sm-legend">
            <li v-for="s in series" :key="s.key" class="sm-legend__item">
              <i class="sm-legend__dot" :class="`sm-legend__dot--${s.key}`" />
              <span class="sm-legend__label">{{ s.label }}</span>
              <span class="sm-legend__value">{{ samples.length ? `${s.latest}%` : '--' }}</span>
            </li>
          </ul>
        </div>

        <div class="sm-chart">
          <div class="sm-chart__axis">
            <span
              v-for="tick in [100, 50, 0]"
              :key="tick"
              class="sm-chart__tick"
              :style="{ top: `${100 - tick}%` }"
            >{{ tick }}</span>
          </div>
          <div class="sm-chart__plot">
            <span
              v-for="tick in [100, 50, 0]"
              :key="tick"
              class="sm-chart__gridline"
              :style="{ top: `${100 - tick}%` }"
            />
            <svg class="sm-chart__svg" viewBox="0 0 100 100" preserveAspectRatio="none">
              <path
                v-for="s in series"
                :key="s.key"
                :d="s.path"
                :class="['sm-chart__line', `sm-chart__line--${s.key}`]"
                vector-effect="non-scaling-stroke"
              />
            </svg>
            <p v-if="!series[0].path" class="sm-chart__empty">正在采集走势数据…</p>
          </div>
        </div>
        <div class="sm-chart__xaxis">
          <span>5 分钟前</span>
          <span>2 分 30 秒前</span>
          <span>现在</span>
        </div>
      </section>

      <!-- ══════ 详情 ══════ -->
      <section class="sm-details">
        <!-- 运行信息 + 容量概览（左列两张独立卡，避免拉伸后留大片空白） -->
        <div class="sm-side">
          <div class="sm-panel">
            <div class="sm-panel__header sm-panel__header--row">
              <h3 class="sm-panel__title">运行信息</h3>
              <span class="sm-panel__meta">进程启动至今 {{ uptimeText }}</span>
            </div>
            <div class="sm-panel__body">
              <dl class="sm-kv">
              <div class="sm-kv__row sm-kv__row--stack">
                <dt class="sm-kv__key">操作系统</dt>
                <dd class="sm-kv__val" :title="metrics?.OsName">{{ metrics?.OsName || '--' }}</dd>
              </div>
              <div class="sm-kv__row">
                <dt class="sm-kv__key">机器名</dt>
                <dd class="sm-kv__val">{{ metrics?.MachineName || '--' }}</dd>
              </div>
              <div class="sm-kv__row">
                <dt class="sm-kv__key">采集时间</dt>
                <dd class="sm-kv__val">{{ formatDateTime(metrics?.Timestamp) }}</dd>
              </div>
              <div class="sm-kv__row">
                <dt class="sm-kv__key">采样周期</dt>
                <dd class="sm-kv__val">{{ autoRefresh ? `每 ${REFRESH_SECONDS} 秒` : '手动刷新' }}</dd>
                </div>
              </dl>
            </div>
          </div>

          <div class="sm-panel">
            <div class="sm-panel__header sm-panel__header--row">
              <h3 class="sm-panel__title">容量概览</h3>
              <span class="sm-panel__meta">已用 {{ diskUsagePercent }}%</span>
            </div>
            <div class="sm-panel__body">
              <div class="sm-cap">
                <div class="sm-cap__head">
                  <span class="sm-cap__title">磁盘总量</span>
                  <span class="sm-cap__value">
                    {{ formatBytes(metrics?.DiskUsed ?? 0) }} / {{ formatBytes(metrics?.DiskTotal ?? 0) }}
                  </span>
                </div>
                <div class="sm-cap__bar">
                  <span
                    class="sm-cap__bar-used"
                    :style="{ width: `${diskUsagePercent}%`, background: levelColorVar(levelOf(diskUsagePercent)) }"
                  />
                </div>
                <div class="sm-cap__legend">
                  <span>已用 {{ formatBytes(metrics?.DiskUsed ?? 0) }}</span>
                  <span>可用 {{ formatBytes((metrics?.DiskTotal ?? 0) - (metrics?.DiskUsed ?? 0)) }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- 磁盘分区 -->
        <div class="sm-panel sm-panel--flex">
          <div class="sm-panel__header sm-panel__header--row">
            <h3 class="sm-panel__title">磁盘分区</h3>
            <span class="sm-panel__meta">
              {{ sortedDisks.length }} 个分区 · 已用 {{ formatBytes(metrics?.DiskUsed ?? 0) }}
            </span>
          </div>
          <div class="sm-panel__body sm-panel__body--scroll">
            <YzhTable
              :columns="diskColumns"
              :data="sortedDisks"
              :show-pagination="false"
              empty-text="无磁盘信息"
              no-padding
              :toolbar="false"
              size="small"
            >
              <template #column-Name="{ row }">
                <div class="sm-mount">
                  <span class="sm-mount__path" :title="row.Name">{{ row.Name }}</span>
                  <span class="sm-mount__device">{{ row.Device }}</span>
                </div>
              </template>
              <template #column-total="{ row }">
                <span class="sm-bytes">{{ formatBytes(row.TotalBytes) }}</span>
              </template>
              <template #column-used="{ row }">
                <div class="sm-used">
                  <span class="sm-bytes">{{ formatBytes(row.UsedBytes) }}</span>
                  <span class="sm-bytes sm-bytes--muted">可用 {{ formatBytes(row.AvailableBytes) }}</span>
                </div>
              </template>
              <template #column-pct="{ row }">
                <div class="sm-disk-pct">
                  <el-progress
                    :percentage="row.UsagePercent"
                    :color="progressColor(row.UsagePercent)"
                    :stroke-width="8"
                    :show-text="false"
                    class="sm-disk-pct__bar"
                  />
                  <span class="sm-disk-pct__num">{{ row.UsagePercent }}%</span>
                  <span :class="['sm-level', `sm-level--${levelOf(row.UsagePercent)}`]">
                    {{ row.UsagePercent < 60 ? '正常' : row.UsagePercent < 85 ? '偏高' : '危险' }}
                  </span>
                </div>
              </template>
            </YzhTable>
          </div>
          <p class="sm-note">
            APFS 容器内多个卷共享同一块空间，单行「可用」不会等于「总量 − 已用」；使用率取自 df 的容器用量。
          </p>
        </div>
      </section>
    </div>
  </div>
</template>

<style scoped>
/* ════════════════════════════════════════════
   页面根容器
   ════════════════════════════════════════════ */
.sm-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: var(--yzh-color-bg-page, #f8fafc);
}

/* ════════════════════════════════════════════
   顶栏
   ════════════════════════════════════════════ */
.sm-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: var(--yzh-space-4, 16px);
  padding: var(--yzh-space-3, 12px) var(--yzh-space-5, 20px);
  background: var(--yzh-color-bg-container, #fff);
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9);
  flex-shrink: 0;
}

.sm-header__left {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
  min-width: 0;
}

.sm-header__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border-radius: var(--yzh-radius-md, 8px);
  background: var(--yzh-color-primary-light-9, #eff6ff);
  color: var(--yzh-color-primary, #1e3a8a);
  font-size: var(--yzh-font-size-xl, 18px);
  flex-shrink: 0;
}

.sm-header__titles {
  min-width: 0;
}

.sm-header__title-row {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.sm-header__title {
  margin: 0;
  font-size: var(--yzh-font-size-lg, 16px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
  line-height: 1.3;
}

.sm-header__subtitle {
  margin: 0;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sm-header__actions {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-4, 16px);
  flex-shrink: 0;
}

.sm-header__updated {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-secondary, #606266);
  font-variant-numeric: tabular-nums;
}

.sm-toggle {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  cursor: pointer;
}

.sm-toggle__text {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
}

/* 综合健康状态（S08：状态徽章，不用 el-tag） */
.sm-status {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
  padding: 0 var(--yzh-space-2, 8px);
  height: 22px;
  border-radius: var(--yzh-radius-sm, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: var(--yzh-font-weight-medium, 500);
  white-space: nowrap;
}

.sm-status__dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
}

.sm-status--success {
  color: var(--yzh-color-success, #16a34a);
  background: var(--yzh-color-success-light-9, #f0f9eb);
}

.sm-status--warning {
  color: var(--yzh-color-warning, #d97706);
  background: var(--yzh-color-warning-light-9, #fdf6ec);
}

.sm-status--danger {
  color: var(--yzh-color-danger, #dc2626);
  background: var(--yzh-color-danger-light-9, #fef0f0);
}

/* ════════════════════════════════════════════
   主体
   ════════════════════════════════════════════ */
.sm-body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  /* 左右不留白（用户指定）：内容与顶栏同宽 */
  padding: var(--yzh-space-4, 16px) 0;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-4, 16px);
}

/* ⚠️ 必须禁止收缩：body 是定高 flex 容器，面板默认 flex-shrink:1，
   内容超高时会先把「资源走势」压到 0（曾表现为面板只剩标题条、图整体消失） */
.sm-body > * {
  flex-shrink: 0;
}

/* 断流提示条 */
.sm-alert {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-4, 16px);
  border: 1px solid var(--yzh-color-warning-light-7, #f5dab1);
  border-radius: var(--yzh-radius-md, 8px);
  background: var(--yzh-color-warning-light-9, #fdf6ec);
  color: var(--yzh-color-warning, #d97706);
  font-size: var(--yzh-font-size-sm, 13px);
}

.sm-alert__icon {
  font-size: var(--yzh-font-size-md, 14px);
  flex-shrink: 0;
}

.sm-alert__text {
  flex: 1;
  min-width: 0;
  word-break: break-all;
}

/* ════════════════════════════════════════════
   指标卡
   ════════════════════════════════════════════ */
.sm-metrics {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: var(--yzh-space-4, 16px);
}

@media (max-width: 1200px) {
  .sm-metrics {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 640px) {
  .sm-metrics {
    grid-template-columns: minmax(0, 1fr);
  }
}

.sm-metric {
  display: flex;
  gap: var(--yzh-space-3, 12px);
  padding: var(--yzh-space-4, 16px);
  border-radius: var(--yzh-radius-lg, 12px);
  background: var(--yzh-color-bg-container, #fff);
  border: 1px solid var(--yzh-color-border-light, #f1f5f9);
  box-shadow: var(--yzh-shadow-sm, 0 1px 2px rgba(0, 0, 0, 0.05));
  min-width: 0;
}

.sm-metric__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border-radius: var(--yzh-radius-md, 8px);
  font-size: var(--yzh-font-size-lg, 16px);
  flex-shrink: 0;
}

.sm-metric--cpu .sm-metric__icon {
  background: var(--yzh-color-primary-light-9, #eff6ff);
  color: var(--yzh-color-primary, #1e3a8a);
}

.sm-metric--memory .sm-metric__icon {
  background: var(--yzh-color-success-light-9, #f0f9eb);
  color: var(--yzh-color-success, #16a34a);
}

.sm-metric--disk .sm-metric__icon {
  background: var(--yzh-color-warning-light-9, #fdf6ec);
  color: var(--yzh-color-warning, #d97706);
}

.sm-metric--network .sm-metric__icon {
  background: var(--yzh-color-info-light-9, #f4f4f5);
  color: var(--yzh-color-info, #64748b);
}

.sm-metric__info {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
}

.sm-metric__label {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
  font-weight: var(--yzh-font-weight-medium, 500);
}

.sm-metric__value-row {
  display: flex;
  align-items: baseline;
  min-width: 0;
}

.sm-metric__value {
  font-size: var(--yzh-font-size-2xl, 20px);
  font-weight: var(--yzh-font-weight-bold, 700);
  color: var(--yzh-color-text-primary, #303133);
  line-height: 1.25;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/* 网络卡的数值是「1.23 GB/s」这类文本，降一档字号避免挤压 */
.sm-metric__value--text {
  font-size: var(--yzh-font-size-lg, 16px);
}

.sm-metric__unit {
  margin-left: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-normal, 400);
  color: var(--yzh-color-text-secondary, #606266);
}

.sm-metric__rates {
  display: flex;
  align-items: center;
  min-height: var(--yzh-space-3, 12px);
}

.sm-metric__rate {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-secondary, #606266);
  font-variant-numeric: tabular-nums;
}

.sm-metric__rate-key {
  font-style: normal;
  color: var(--yzh-color-text-tertiary, #909399);
}

.sm-metric__footer {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
  margin-top: auto;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ════════════════════════════════════════════
   面板通用
   ════════════════════════════════════════════ */
.sm-panel {
  background: var(--yzh-color-bg-container, #fff);
  border: 1px solid var(--yzh-color-border-light, #f1f5f9);
  border-radius: var(--yzh-radius-lg, 12px);
  box-shadow: var(--yzh-shadow-sm, 0 1px 2px rgba(0, 0, 0, 0.05));
  overflow: hidden;
}

.sm-panel--flex {
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.sm-panel__header {
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9);
  flex-shrink: 0;
}

.sm-panel__header--row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-3, 12px);
}

.sm-panel__heading {
  display: flex;
  align-items: baseline;
  gap: var(--yzh-space-2, 8px);
  min-width: 0;
}

.sm-panel__title {
  margin: 0;
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
  white-space: nowrap;
}

.sm-panel__meta {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sm-panel__body {
  padding: var(--yzh-space-4, 16px);
}

.sm-panel__body--scroll {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 0;
}

/* ════════════════════════════════════════════
   走势图
   ════════════════════════════════════════════ */
.sm-legend {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-4, 16px);
  margin: 0;
  padding: 0;
  list-style: none;
  flex-shrink: 0;
}

.sm-legend__item {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
}

.sm-legend__dot {
  width: 8px;
  height: 2px;
  border-radius: 1px;
}

.sm-legend__dot--cpu {
  background: var(--yzh-color-primary, #1e3a8a);
}

.sm-legend__dot--memory {
  background: var(--yzh-color-success, #16a34a);
}

.sm-legend__dot--disk {
  background: var(--yzh-color-warning, #d97706);
}

.sm-legend__value {
  font-variant-numeric: tabular-nums;
  color: var(--yzh-color-text-primary, #303133);
  font-weight: var(--yzh-font-weight-medium, 500);
}

.sm-chart {
  display: flex;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-4, 16px) var(--yzh-space-4, 16px) 0;
}

.sm-chart__axis {
  position: relative;
  width: 24px;
  height: 120px;
  flex-shrink: 0;
}

.sm-chart__tick {
  position: absolute;
  right: 0;
  transform: translateY(-50%);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
  font-variant-numeric: tabular-nums;
}

.sm-chart__plot {
  position: relative;
  flex: 1;
  height: 120px;
  min-width: 0;
}

.sm-chart__gridline {
  position: absolute;
  left: 0;
  right: 0;
  height: 1px;
  background: var(--yzh-color-border-light, #f1f5f9);
}

.sm-chart__svg {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  overflow: visible;
}

.sm-chart__line {
  fill: none;
  stroke-width: 2;
  stroke-linejoin: round;
  stroke-linecap: round;
}

.sm-chart__line--cpu {
  stroke: var(--yzh-color-primary, #1e3a8a);
}

.sm-chart__line--memory {
  stroke: var(--yzh-color-success, #16a34a);
}

.sm-chart__line--disk {
  stroke: var(--yzh-color-warning, #d97706);
}

.sm-chart__empty {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 0;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.sm-chart__xaxis {
  display: flex;
  justify-content: space-between;
  padding: var(--yzh-space-1, 4px) var(--yzh-space-4, 16px) var(--yzh-space-3, 12px)
    var(--yzh-space-8, 32px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

/* ════════════════════════════════════════════
   详情区
   ════════════════════════════════════════════ */
.sm-details {
  display: grid;
  grid-template-columns: minmax(320px, 1fr) 1.6fr;
  gap: var(--yzh-space-4, 16px);
  align-items: start;
}

.sm-side {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-4, 16px);
  min-width: 0;
}

@media (max-width: 1024px) {
  .sm-details {
    grid-template-columns: minmax(0, 1fr);
  }
}

/* 运行信息：键值行 */
.sm-kv {
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-3, 12px);
}

.sm-kv__row {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--yzh-space-4, 16px);
  min-width: 0;
}

.sm-kv__key {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
  flex-shrink: 0;
}

.sm-kv__row--stack {
  flex-direction: column;
  align-items: flex-start;
  gap: var(--yzh-space-1, 4px);
}

.sm-kv__row--stack .sm-kv__val {
  text-align: left;
  white-space: normal;
  line-height: 1.5;
}

.sm-kv__val {
  margin: 0;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-primary, #303133);
  font-weight: var(--yzh-font-weight-medium, 500);
  text-align: right;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-variant-numeric: tabular-nums;
}

/* 容量概览 */
.sm-cap {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}

.sm-cap__head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--yzh-space-2, 8px);
  margin-bottom: var(--yzh-space-2, 8px);
}

.sm-cap__title {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.sm-cap__value {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-primary, #303133);
  font-weight: var(--yzh-font-weight-medium, 500);
  font-variant-numeric: tabular-nums;
}

.sm-cap__bar {
  height: 8px;
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--yzh-color-bg-muted, #f1f5f9);
  overflow: hidden;
}

.sm-cap__bar-used {
  display: block;
  height: 100%;
  border-radius: var(--yzh-radius-sm, 4px);
  transition: width 0.4s ease;
}

.sm-cap__legend {
  display: flex;
  justify-content: space-between;
  margin-top: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
  font-variant-numeric: tabular-nums;
}

/* 表下脚注（解释 APFS 共享容器导致的数字「不平」） */
.sm-note {
  margin: 0;
  padding: var(--yzh-space-2, 8px) var(--yzh-space-4, 16px) var(--yzh-space-3, 12px);
  border-top: 1px solid var(--yzh-color-border-light, #f1f5f9);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
  line-height: 1.5;
  flex-shrink: 0;
}

/* 表格自带 16px 内边距：面板已给内边距，这里去掉横向留白，把宽度全留给列 */
.sm-panel__body--scroll :deep(.yzh-table__wrapper) {
  padding-left: 0;
  padding-right: 0;
}

/* 磁盘分区：挂载点 + 设备双行 */
.sm-mount {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
  min-width: 0;
}

.sm-mount__path {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-primary, #303133);
  font-weight: var(--yzh-font-weight-medium, 500);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sm-mount__device {
  font-family: var(--yzh-font-family-mono, monospace);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.sm-bytes {
  font-variant-numeric: tabular-nums;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
}

.sm-bytes--muted {
  color: var(--yzh-color-text-tertiary, #909399);
  font-size: var(--yzh-font-size-xs, 12px);
}

.sm-used {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: var(--yzh-space-1, 4px);
  line-height: 1.3;
}

.sm-disk-pct {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}

.sm-disk-pct__bar {
  flex: 1;
  min-width: var(--yzh-space-12, 48px);
}

.sm-disk-pct__num {
  width: 34px;
  text-align: right;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
  font-variant-numeric: tabular-nums;
  flex-shrink: 0;
}

/* 级别标签（S08：状态徽章，不用 el-tag） */
.sm-level {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  padding: 0 var(--yzh-space-2, 8px);
  height: 22px;
  border-radius: var(--yzh-radius-sm, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: var(--yzh-font-weight-medium, 500);
  flex-shrink: 0;
  min-width: 44px;
}

.sm-level--success {
  color: var(--yzh-color-success, #16a34a);
  background: var(--yzh-color-success-light-9, #f0f9eb);
}

.sm-level--warning {
  color: var(--yzh-color-warning, #d97706);
  background: var(--yzh-color-warning-light-9, #fdf6ec);
}

.sm-level--danger {
  color: var(--yzh-color-danger, #dc2626);
  background: var(--yzh-color-danger-light-9, #fef0f0);
}

/* Element Plus 覆盖（只 :deep() + 局部变量） */
:deep(.el-progress-bar__outer) {
  background-color: var(--yzh-color-bg-muted, #f1f5f9);
}

:deep(.el-progress-bar__inner) {
  border-radius: var(--yzh-radius-sm, 4px);
  transition: width 0.4s ease;
}
</style>
