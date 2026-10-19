/**
 * 服务监控 API
 * 路由前缀：GET /api/Admin/System/ServerMonitor/overview
 */
import { yzhApi } from '@yzh-core/api/client'

// ──── 类型 ────

/** 磁盘分区信息 */
export interface DiskInfo {
  Name: string
  Device: string
  TotalBytes: number
  UsedBytes: number
  AvailableBytes: number
  UsagePercent: number
}

/** 服务监控完整快照 */
export interface ServerMonitorData {
  CpuUsage: number
  CpuCores: number
  MemoryUsed: number
  MemoryTotal: number
  MemoryUsagePercent: number
  Disks: DiskInfo[]
  DiskTotal: number
  DiskUsed: number
  NetworkIn: number
  NetworkOut: number
  OsName: string
  MachineName: string
  /** 服务运行时长（ISO 8601 TimeSpan 格式） */
  UpTime: string
  /** 采集时间（ISO 8601） */
  Timestamp: string
}

// ──── API 调用 ────

/**
 * 获取系统资源监控快照
 */
export async function getServerMetrics(): Promise<ServerMonitorData> {
  const res = await yzhApi.get<{ data: ServerMonitorData }>(
    '/api/System/ServerMonitor/overview'
  )
  return res.data
}
