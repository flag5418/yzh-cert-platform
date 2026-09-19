using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Stand.Models.Queue;

/// <summary>
/// yzh 队列资源锁定表（通用，跨项目复用）
/// <para>队列创建时对涉及资源加锁；子任务完成释放对应锁；队列终态批量释放</para>
/// <para>resource_table + resource_code 可锁定任意业务表资源，实现跨队列类型冲突检测</para>
/// <para>表名：yzh_queue_resource_lock</para>
/// <para>架构铁律：继承 BaseEntity，DB 列名 = C# 属性名 = PascalCase</para>
/// </summary>
[SugarTable("yzh_queue_resource_lock")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class YzhQueueResourceLock : BaseEntity, ISoftDelete
{
    /// <summary>主键（DB: bigint AUTO_INCREMENT）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public new long Id { get; set; }

    /// <summary>所属队列编码</summary>
    public string QueueCode { get; set; } = string.Empty;

    /// <summary>资源表名（任意业务表，如 cert_standard_directory_file / cert_report）</summary>
    public string ResourceTable { get; set; } = string.Empty;

    /// <summary>资源唯一编码</summary>
    public string ResourceCode { get; set; } = string.Empty;

    /// <summary>资源名称快照</summary>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string? ResourceName { get; set; }

    /// <summary>占用该资源的子任务序号（NULL=队列级锁）</summary>
    public int? TaskNo { get; set; }

    /// <summary>状态：locked/released</summary>
    public string Status { get; set; } = "locked";

    /// <summary>活跃锁键（释放时置 NULL）</summary>
    [SugarColumn(Length = 260, IsNullable = true)]
    public string? ActiveKey { get; set; }

    /// <summary>加锁时间</summary>
    public DateTime? LockTime { get; set; } = DateTime.UtcNow;

    /// <summary>释放时间</summary>
    public DateTime? ReleaseTime { get; set; }

    /// <summary>锁租约安全网：回收任务扫描超时锁强制释放</summary>
    public DateTime? ExpireAt { get; set; }

    /// <summary>机构编码</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? OrgCode { get; set; }

    // ──── ISoftDelete 接口实现 ────

    /// <summary>软删除标记（false=正常，true=已删除）</summary>
    public bool IsDeleted { get; set; }

    /// <summary>删除人 Code</summary>
    public new string? DeleteBy { get; set; }

    /// <summary>删除时间</summary>
    public new DateTime? DeleteTime { get; set; }
}
