using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Stand.Models.Queue;

/// <summary>
/// yzh 队列资源锁定表（通用，跨项目复用）
/// <para>队列创建时对涉及资源加锁；子任务完成释放对应锁；队列终态批量释放</para>
/// <para>resource_table + resource_code 可锁定任意业务表资源，实现跨队列类型冲突检测</para>
/// <para>表名：yzh_queue_resource_lock</para>
/// </summary>
[SugarTable("yzh_queue_resource_lock")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class YzhQueueResourceLock : BaseEntity
{
    /// <summary>主键（DB: bigint AUTO_INCREMENT）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public new long Id { get; set; }

    /// <summary>全局唯一编码(GUID)</summary>
    [SugarColumn(ColumnName = "code", Length = 36)]
    public new string Code { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>所属队列编码</summary>
    [SugarColumn(ColumnName = "queue_code", Length = 64)]
    public string QueueCode { get; set; } = string.Empty;

    /// <summary>资源表名（任意业务表，如 cert_standard_directory_file / cert_report）</summary>
    [SugarColumn(ColumnName = "resource_table", Length = 50)]
    public string ResourceTable { get; set; } = string.Empty;

    /// <summary>资源唯一编码</summary>
    [SugarColumn(ColumnName = "resource_code", Length = 200)]
    public string ResourceCode { get; set; } = string.Empty;

    /// <summary>资源名称快照</summary>
    [SugarColumn(ColumnName = "resource_name", Length = 200, IsNullable = true)]
    public string? ResourceName { get; set; }

    /// <summary>占用该资源的子任务序号（NULL=队列级锁）</summary>
    [SugarColumn(ColumnName = "task_no", IsNullable = true)]
    public int? TaskNo { get; set; }

    /// <summary>状态：locked/released</summary>
    [SugarColumn(ColumnName = "status", Length = 20)]
    public string Status { get; set; } = "locked";

    /// <summary>活跃锁键（释放时置 NULL）</summary>
    [SugarColumn(ColumnName = "active_key", Length = 260, IsNullable = true)]
    public string? ActiveKey { get; set; }

    /// <summary>加锁时间</summary>
    [SugarColumn(ColumnName = "create_time", IsNullable = true)]
    public DateTime? CreateTime { get; set; } = DateTime.Now;

    /// <summary>释放时间</summary>
    [SugarColumn(ColumnName = "release_time", IsNullable = true)]
    public DateTime? ReleaseTime { get; set; }

    /// <summary>锁租约安全网：回收任务扫描超时锁强制释放</summary>
    [SugarColumn(ColumnName = "expire_at", IsNullable = true)]
    public DateTime? ExpireAt { get; set; }

    [SugarColumn(ColumnName = "org_code", Length = 50, IsNullable = true)]
    public string? OrgCode { get; set; }

    // ──── 审计字段重声明 ────
    [SugarColumn(ColumnName = "create_id", IsNullable = true)]
    public new int? CreateId { get; set; }

    [SugarColumn(ColumnName = "creator", Length = 50, IsNullable = true)]
    public new string? Creator { get; set; }

    [SugarColumn(ColumnName = "create_date", IsNullable = true)]
    public new DateTime? CreateDate { get; set; }

    [SugarColumn(ColumnName = "modify_id", IsNullable = true)]
    public new int? ModifyId { get; set; }

    [SugarColumn(ColumnName = "modifier", Length = 50, IsNullable = true)]
    public new string? Modifier { get; set; }

    [SugarColumn(ColumnName = "modify_date", IsNullable = true)]
    public new DateTime? ModifyDate { get; set; }

    [SugarColumn(ColumnName = "delete_id", IsNullable = true)]
    public new int? DeleteId { get; set; }

    [SugarColumn(ColumnName = "deleter", Length = 50, IsNullable = true)]
    public new string? Deleter { get; set; }

    [SugarColumn(ColumnName = "delete_time", IsNullable = true)]
    public new DateTime? DeleteTime { get; set; }
}
