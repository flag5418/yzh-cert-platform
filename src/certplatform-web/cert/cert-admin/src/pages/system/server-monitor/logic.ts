/**
 * 服务监控页纯逻辑（与 index.vue 同目录，页面专属）
 *
 * 抽出来的理由：这些都是**有过真实 bug 的解析/换算**（
 * 例：TimeSpan 不满一天时没有天数段，"01:12:33.4041660" 曾被读成「1 天 00:00:00」），
 * 放在 SFC 里只能靠肉眼看页面，搬到这里才能上单元测试。
 */

/** 单个采样点（网络存累计字节，速率由相邻两点做差得出） */
export interface MonitorSample {
  t: number
  cpu: number
  mem: number
  disk: number
  netIn: number
  netOut: number
}

export type Level = 'success' | 'warning' | 'danger'

/** 字节 → 可读体积 */
export function formatBytes(bytes: number): string {
  if (!bytes || bytes <= 0) return '0 B'
  const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB']
  const i = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1)
  const value = bytes / Math.pow(1024, i)
  return `${value.toFixed(i === 0 ? 0 : 2)} ${units[i]}`
}

/** 字节/秒 → 可读吞吐 */
export function formatRate(bytesPerSecond: number, ready: boolean): string {
  if (!ready) return '--'
  return `${formatBytes(bytesPerSecond)}/s`
}

/** 水位分档：<60 正常、<85 偏高、其余告警 */
export function levelOf(percent: number): Level {
  if (percent < 60) return 'success'
  if (percent < 85) return 'warning'
  return 'danger'
}

export function levelText(percent: number): string {
  if (percent < 30) return '空闲'
  if (percent < 60) return '正常'
  if (percent < 85) return '偏高'
  return '告警'
}

/** 多档取最差 */
export function worstLevel(levels: Level[]): Level {
  if (levels.includes('danger')) return 'danger'
  if (levels.includes('warning')) return 'warning'
  return 'success'
}

/**
 * 解析 .NET `TimeSpan` 常量格式（"c"）。
 *
 * ⚠️ 两种形态都要支持：
 *   - 不满一天：`hh:mm:ss.fffffff`            → "01:12:33.4041660"
 *   - 满一天  ：`d.hh:mm:ss.fffffff`          → "1.01:12:33.4041660"
 * 旧实现按 `天.时:分:秒` 硬切，把第一种读成「1 天 00:00:00」（页面曾真实如此）。
 */
export function parseTimeSpan(text: string): { days: number; hours: number; minutes: number; seconds: number } | null {
  const m = /^(?:(\d+)\.)?(\d{1,3}):(\d{2}):(\d{2})(?:\.\d+)?$/.exec(text.trim())
  if (!m) return null
  return {
    days: Number(m[1] ?? 0),
    hours: Number(m[2]),
    minutes: Number(m[3]),
    seconds: Number(m[4]),
  }
}

/** 运行时长人性化：1 天 3 小时 / 1 小时 12 分 / 42 秒 */
export function humanizeUptime(text?: string): string {
  if (!text) return '--'
  const p = parseTimeSpan(text)
  if (!p) return text
  const parts: string[] = []
  if (p.days > 0) parts.push(`${p.days} 天`)
  if (p.hours > 0) parts.push(`${p.hours} 小时`)
  if (p.days === 0 && p.minutes > 0) parts.push(`${p.minutes} 分`)
  if (p.days === 0 && p.hours === 0 && p.minutes === 0) parts.push(`${p.seconds} 秒`)
  return parts.join(' ') || '不足 1 秒'
}

const pad2 = (n: number) => String(n).padStart(2, '0')

/**
 * 后端时间串 → 显示串。
 * 后端序列化为「2026-10-19 10:52:40」（本地时间、无时区标记），
 * 把空格换成 T 交给浏览器按本地解析；解析失败原样回显。
 */
export function formatDateTime(value?: string): string {
  if (!value) return '--'
  const d = new Date(value.replace(' ', 'T'))
  if (Number.isNaN(d.getTime())) return value
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())} ${pad2(d.getHours())}:${pad2(d.getMinutes())}:${pad2(d.getSeconds())}`
}

/** 整机磁盘使用率（已用 / 各物理卷总量合计） */
export function diskPercentOf(input: { DiskTotal: number; DiskUsed: number } | null | undefined): number {
  if (!input || !input.DiskTotal) return 0
  return Math.round((input.DiskUsed / input.DiskTotal) * 100)
}

/**
 * 相邻两点的网络吞吐（字节/秒）。
 * 计数器回绕/为零时归零 —— 宁可显示 0 也不显示负速率。
 */
export function rateFromSamples(samples: MonitorSample[]): { in: number; out: number; ready: boolean } {
  if (samples.length < 2) return { in: 0, out: 0, ready: false }
  const prev = samples[samples.length - 2]
  const last = samples[samples.length - 1]
  const seconds = (last.t - prev.t) / 1000
  if (seconds <= 0) return { in: 0, out: 0, ready: false }
  const dIn = last.netIn - prev.netIn
  const dOut = last.netOut - prev.netOut
  return {
    in: dIn > 0 ? dIn / seconds : 0,
    out: dOut > 0 ? dOut / seconds : 0,
    ready: true,
  }
}

/**
 * 走势折线的 SVG 路径（viewBox 0..100，x/y 都是百分比坐标，y 需翻转）。
 *
 * `historyMax` 是横轴槽位数：不足时**右对齐**（最新点贴右边），
 * 符合「时间向右流动」的实时监控直觉；左对齐会像图表坏了。
 */
export function buildSeriesPath(
  samples: MonitorSample[],
  pick: (s: MonitorSample) => number,
  historyMax: number,
): string {
  if (samples.length < 2 || historyMax < 2) return ''
  const offset = Math.max(historyMax - samples.length, 0)
  const step = 100 / (historyMax - 1)
  return samples
    .map((s, i) => {
      const v = Math.min(Math.max(pick(s), 0), 100)
      return `${i === 0 ? 'M' : 'L'}${((offset + i) * step).toFixed(2)},${(100 - v).toFixed(2)}`
    })
    .join(' ')
}
