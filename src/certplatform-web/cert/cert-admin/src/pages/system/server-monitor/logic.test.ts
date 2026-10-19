import { describe, expect, it } from 'vitest'
import {
  buildSeriesPath,
  diskPercentOf,
  formatBytes,
  formatDateTime,
  formatRate,
  humanizeUptime,
  levelOf,
  parseTimeSpan,
  rateFromSamples,
  worstLevel,
  type MonitorSample,
} from './logic'

const sample = (over: Partial<MonitorSample> = {}): MonitorSample => ({
  t: 0,
  cpu: 10,
  mem: 20,
  disk: 30,
  netIn: 0,
  netOut: 0,
  ...over,
})

describe('parseTimeSpan / humanizeUptime', () => {
  it('不满一天时没有天数段（曾把 01:12:33 读成 1 天 00:00:00）', () => {
    expect(parseTimeSpan('01:12:33.4041660')).toEqual({ days: 0, hours: 1, minutes: 12, seconds: 33 })
    expect(humanizeUptime('01:12:33.4041660')).toBe('1 小时 12 分')
  })

  it('满一天时才有 `d.` 前缀', () => {
    expect(parseTimeSpan('1.01:12:33.4041660')).toEqual({ days: 1, hours: 1, minutes: 12, seconds: 33 })
    expect(humanizeUptime('1.01:12:33.4041660')).toBe('1 天 1 小时')
  })

  it('不足一分钟显示秒，别显示「0 分」', () => {
    expect(humanizeUptime('00:00:28.6324600')).toBe('28 秒')
  })

  it('无法解析时原样回显，空值占位', () => {
    expect(humanizeUptime('n/a')).toBe('n/a')
    expect(humanizeUptime(undefined)).toBe('--')
  })
})

describe('formatDateTime', () => {
  it('无时区标记的后端时间按本地解析，不做 UTC 偏移', () => {
    const d = new Date('2026-10-19T10:52:40')
    expect(formatDateTime('2026-10-19 10:52:40')).toBe(
      `${d.getFullYear()}-10-19 10:52:40`,
    )
  })

  it('非法值原样回显；空值占位', () => {
    expect(formatDateTime('not-a-date')).toBe('not-a-date')
    expect(formatDateTime()).toBe('--')
  })
})

describe('网络速率', () => {
  it('按相邻两点做差并除以秒数', () => {
    const r = rateFromSamples([
      sample({ t: 1000, netIn: 0, netOut: 0 }),
      sample({ t: 6000, netIn: 5000, netOut: 2500 }),
    ])
    expect(r.ready).toBe(true)
    expect(r.in).toBe(1000)
    expect(r.out).toBe(500)
  })

  it('计数器回绕（变小）时归零，不出负数', () => {
    const r = rateFromSamples([
      sample({ t: 0, netIn: 9000, netOut: 9000 }),
      sample({ t: 5000, netIn: 1000, netOut: 1000 }),
    ])
    expect(r.in).toBe(0)
    expect(r.out).toBe(0)
  })

  it('不足两点时标记未就绪', () => {
    expect(rateFromSamples([sample()])).toEqual({ in: 0, out: 0, ready: false })
    expect(formatRate(1024, false)).toBe('--')
    expect(formatRate(1024 * 1024, true)).toBe('1.00 MB/s')
  })
})

describe('折线路径', () => {
  it('不足两点不出线', () => {
    expect(buildSeriesPath([sample()], (s) => s.cpu, 60)).toBe('')
  })

  it('槽位未满时右对齐：最新点落在 x=100', () => {
    const path = buildSeriesPath(
      [sample({ cpu: 0 }), sample({ cpu: 50 }), sample({ cpu: 100 })],
      (s) => s.cpu,
      60,
    )
    const coords = path.split(' ').map((seg) => seg.slice(1).split(',').map(Number))
    expect(coords).toHaveLength(3)
    expect(coords[2][0]).toBeCloseTo(100, 5)
    // y 轴翻转：100% 在顶部（y=0），0% 在底部（y=100）
    expect(coords[0][1]).toBeCloseTo(100, 5)
    expect(coords[2][1]).toBeCloseTo(0, 5)
  })

  it('超出 0–100 的值被夹紧', () => {
    const path = buildSeriesPath([sample({ cpu: -20 }), sample({ cpu: 160 })], (s) => s.cpu, 60)
    const ys = path.split(' ').map((seg) => Number(seg.slice(1).split(',')[1]))
    expect(ys).toEqual([100, 0])
  })
})

describe('分档与汇总', () => {
  it('三档阈值', () => {
    expect(levelOf(59)).toBe('success')
    expect(levelOf(60)).toBe('warning')
    expect(levelOf(85)).toBe('danger')
    expect(worstLevel(['success', 'danger', 'warning'])).toBe('danger')
    expect(worstLevel(['success'])).toBe('success')
  })

  it('整机磁盘使用率；总量为 0 时不出 NaN', () => {
    expect(diskPercentOf({ DiskTotal: 200, DiskUsed: 50 })).toBe(25)
    expect(diskPercentOf({ DiskTotal: 0, DiskUsed: 50 })).toBe(0)
    expect(diskPercentOf(null)).toBe(0)
  })

  it('体积格式化', () => {
    expect(formatBytes(0)).toBe('0 B')
    expect(formatBytes(1024)).toBe('1.00 KB')
  })
})
