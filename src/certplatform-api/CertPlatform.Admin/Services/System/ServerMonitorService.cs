using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CertPlatform.Admin.Services.Monitoring;

/// <summary>
/// 服务监控数据采集服务（macOS 宿主机环境）
/// <para>读取当前运行机器的 CPU / 内存 / 磁盘 / 网络 / OS 信息。</para>
/// <para>数据来源：通过 Process 调用 macOS 原生命令（sysctl、vm_stat、df、netstat），
/// 无额外 NuGet 依赖、无特权要求。</para>
/// </summary>
public class ServerMonitorService
{
    private readonly ILogger<ServerMonitorService> _logger;

    public ServerMonitorService(ILogger<ServerMonitorService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 采集一次完整的系统指标快照
    /// </summary>
    public async Task<ServerMonitorDto> GetMetricsAsync()
    {
        var cpu = await GetCpuUsageAsync();
        var memory = await GetMemoryInfoAsync();
        var disks = await GetDiskInfoAsync();
        var network = await GetNetworkInfoAsync();
        var osInfo = GetOsInfo();

        return new ServerMonitorDto
        {
            CpuUsage = cpu,
            CpuCores = Environment.ProcessorCount,
            MemoryUsed = memory.UsedBytes,
            MemoryTotal = memory.TotalBytes,
            MemoryUsagePercent = memory.TotalBytes > 0
                ? (int)Math.Round((double)memory.UsedBytes / memory.TotalBytes * 100) : 0,
            Disks = disks,
            DiskTotal = disks.Sum(d => d.TotalBytes),
            DiskUsed = disks.Sum(d => d.UsedBytes),
            NetworkIn = network.InBytes,
            NetworkOut = network.OutBytes,
            OsName = osInfo.OsName,
            MachineName = Environment.MachineName,
            UpTime = GetUpTime(),
            // 铁律十：上屏时间用本地时间（DateTime.Now）。
            // 本 DTO 的 DateTime 由 Newtonsoft 序列化成「2026-10-19 10:52:40」无时区标记，
            // 浏览器按本地解析 —— 这里写 UtcNow 会让页面整体早 8 小时且不报错。
            Timestamp = DateTime.Now
        };
    }

    /// <summary>
    /// CPU 使用率（%）— 通过 top 命令取瞬时值
    /// </summary>
    private async Task<int> GetCpuUsageAsync()
    {
        try
        {
            // macOS: 用 top -l 1 -n 0 -s 0 取瞬时 CPU
            var output = await RunProcessAsync("top", "-l 1 -n 0 -s 0");
            foreach (var line in output.Split('\n'))
            {
                // "CPU usage: 5.23% user, 3.12% sys, 91.65% idle"
                if (line.StartsWith("CPU usage:", StringComparison.OrdinalIgnoreCase))
                {
                    var idleIdx = line.IndexOf("idle", StringComparison.OrdinalIgnoreCase);
                    if (idleIdx > 0)
                    {
                        var segment = line.Substring(0, idleIdx);
                        var pctIdx = segment.LastIndexOf('%');
                        if (pctIdx > 0)
                        {
                            var startIdx = segment.LastIndexOf(' ', pctIdx) + 1;
                            if (double.TryParse(segment[startIdx..pctIdx], out var idle))
                                return Math.Clamp((int)Math.Round(100.0 - idle), 0, 100);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CPU 采集失败");
        }
        return 0;
    }

    /// <summary>
    /// 内存信息 — 通过 sysctl hw.memsize + vm_stat 计算
    /// </summary>
    private async Task<(long TotalBytes, long UsedBytes)> GetMemoryInfoAsync()
    {
        long total = 0, used = 0;
        try
        {
            // 总物理内存
            var memsizeOutput = await RunProcessAsync("sysctl", "-n hw.memsize");
            memsizeOutput = memsizeOutput.Trim();
            long.TryParse(memsizeOutput, out total);

            // 解析 vm_stat 获取活跃 + 已使用页
            var vmstat = await RunProcessAsync("vm_stat", "");
            var pages = ParseVmStat(vmstat);
            var pageSize = 4096L; // macOS 默认页大小（有 Apple Silicon 16384 情况）

            // 准确页大小
            var pagesizeOutput = await RunProcessAsync("sysctl", "-n hw.pagesize");
            long.TryParse(pagesizeOutput.Trim(), out pageSize);

            // Used = active + wired + compressed（近似）
            var active = pages.GetValueOrDefault("Pages active", 0);
            var wired = pages.GetValueOrDefault("Pages wired down", 0);
            var occupied = pages.GetValueOrDefault("Pages occupied by compressor", 0);
            used = (active + wired + occupied) * pageSize;

            if (used > total) used = total;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "内存采集失败");
        }
        return (total, used);
    }

    /// <summary>
    /// 磁盘信息 — df -k 获取所有挂载点
    /// <para>macOS APFS 容器下 /、/System/Volumes/Data、/VM、/Preboot 等共享同一物理盘，
    /// TotalBytes 完全相同。过滤虚拟 + 子系统卷后按设备名去重，避免 DiskTotal 虚高。</para>
    /// </summary>
    private async Task<List<DiskInfoDto>> GetDiskInfoAsync()
    {
        var disks = new List<DiskInfoDto>();
        // 已见过物理设备 → 去重，避免同一块物理盘被多个逻辑卷重复求和
        var seenDevices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var output = await RunProcessAsync("df", "-k -l");
            var lines = output.Split('\n').Skip(1); // 跳过表头
            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 6 &&
                    long.TryParse(parts[1], out var totalKb) &&
                    long.TryParse(parts[2], out var usedKb) &&
                    long.TryParse(parts[3], out var availKb))
                {
                    var mountPoint = parts[^1];
                    var name = parts[0];

                    // 过滤虚拟/内存/网络文件系统
                    if (name.StartsWith("devfs") || name.Contains("synthesized") || name.StartsWith("map ")
                        || name.Contains("autofs") || name.Contains("//")) continue;

                    // 过滤 macOS APFS 子系统卷（Preboot / VM / Update / xart / Hardware）—— 与 Data 卷同容量
                    if (mountPoint.StartsWith("/System/Volumes/Preboot") ||
                        mountPoint.StartsWith("/System/Volumes/VM") ||
                        mountPoint.StartsWith("/System/Volumes/Update") ||
                        mountPoint.StartsWith("/System/Volumes/xart") ||
                        mountPoint.StartsWith("/System/Volumes/Hardware") ||
                        mountPoint.StartsWith("/System/Volumes/Recovery")) continue;

                    // ★ 关键：APFS 容器内 /、/Data 等共享同一物理盘（设备名相同如 disk1s5s1），仅保留一次
                    if (!seenDevices.Add(name)) continue;

                    disks.Add(new DiskInfoDto
                    {
                        Name = mountPoint,
                        Device = name,
                        TotalBytes = totalKb * 1024,
                        UsedBytes = usedKb * 1024,
                        AvailableBytes = availKb * 1024,
                        UsagePercent = totalKb > 0 ? (int)Math.Round((double)usedKb / totalKb * 100) : 0
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "磁盘采集失败");
        }
        return disks;
    }

    /// <summary>
    /// 网络流量 — netstat -ib 获取 en0 接口累计字节
    /// </summary>
    private async Task<(long InBytes, long OutBytes)> GetNetworkInfoAsync()
    {
        long inBytes = 0, outBytes = 0;
        try
        {
            var output = await RunProcessAsync("netstat", "-ib");
            var lines = output.Split('\n').Skip(1);
            foreach (var line in lines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 10 && parts[0] != "Name")
                {
                    var iface = parts[0];
                    // 只统计 en0（主网卡），避免把 lo0 等虚拟接口也算进来
                    if (iface != "en0") continue;

                    if (long.TryParse(parts[6], out var inB)) inBytes = inB;
                    if (long.TryParse(parts[9], out var outB)) outBytes = outB;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "网络采集失败");
        }
        return (inBytes, outBytes);
    }

    /// <summary>
    /// OS 名称 + 版本
    /// </summary>
    private (string OsName, string OsVersion) GetOsInfo()
    {
        try
        {
            var version = RuntimeInformation.OSDescription;
            var arch = RuntimeInformation.OSArchitecture;
            return ($"{version} ({arch})", Environment.OSVersion.VersionString);
        }
        catch
        {
            return ("Unknown", "Unknown");
        }
    }

    /// <summary>
    /// 运行时长 — 进程启动至今
    /// </summary>
    private static TimeSpan GetUpTime()
    {
        try
        {
            var proc = Process.GetCurrentProcess();
            // 同一时钟源相减，避免 UtcNow 与 StartTime(本地) 混算（铁律十）
            return DateTime.Now - proc.StartTime;
        }
        catch
        {
            return TimeSpan.Zero;
        }
    }

    /// <summary>
    /// 解析 vm_stat 输出为字典
    /// </summary>
    private static Dictionary<string, long> ParseVmStat(string vmstat)
    {
        var dict = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in vmstat.Split('\n'))
        {
            var colonIdx = line.IndexOf(':');
            if (colonIdx < 0) continue;
            var key = line[..colonIdx].Trim();
            var valuePart = line[(colonIdx + 1)..].Trim().TrimEnd('.');
            if (long.TryParse(valuePart, out var value))
                dict[key] = value;
        }
        return dict;
    }

    /// <summary>
    /// 执行外部命令并返回 stdout
    /// </summary>
    private static async Task<string> RunProcessAsync(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi };
        proc.Start();
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return stdout ?? "";
    }
}

// ════════════════════════════════════════
// DTO
// ════════════════════════════════════════

/// <summary>服务监控完整快照</summary>
public class ServerMonitorDto
{
    /// <summary>CPU 使用率 (0-100)</summary>
    public int CpuUsage { get; set; }
    /// <summary>CPU 逻辑核心数</summary>
    public int CpuCores { get; set; }
    /// <summary>已用内存字节数</summary>
    public long MemoryUsed { get; set; }
    /// <summary>总内存字节数</summary>
    public long MemoryTotal { get; set; }
    /// <summary>内存使用率 (0-100)</summary>
    public int MemoryUsagePercent { get; set; }
    /// <summary>磁盘分区信息</summary>
    public List<DiskInfoDto> Disks { get; set; } = new();
    /// <summary>磁盘总字节数（各分区合计）</summary>
    public long DiskTotal { get; set; }
    /// <summary>磁盘已用字节数</summary>
    public long DiskUsed { get; set; }
    /// <summary>网络累计入站字节</summary>
    public long NetworkIn { get; set; }
    /// <summary>网络累计出站字节</summary>
    public long NetworkOut { get; set; }
    /// <summary>操作系统名称</summary>
    public string OsName { get; set; } = "";
    /// <summary>机器名</summary>
    public string MachineName { get; set; } = "";
    /// <summary>服务运行时长</summary>
    public TimeSpan UpTime { get; set; }
    /// <summary>采集时间</summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>磁盘分区信息</summary>
public class DiskInfoDto
{
    public string Name { get; set; } = "";       // 挂载点
    public string Device { get; set; } = "";     // 设备名
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }
    public long AvailableBytes { get; set; }
    public int UsagePercent { get; set; }
}
