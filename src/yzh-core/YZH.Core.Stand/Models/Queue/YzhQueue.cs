using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Stand.Models.Queue;

/// <summary>
/// yzh 队列主表（通用队列中心，跨项目复用）
/// <para>一次业务动作 = 一个队列实例（如"上传批次 #abc 的 12 个 doc/xls 转换"）</para>
/// <para>表名：yzh_queue</para>
/// </summary>
[SugarTable("yzh_queue")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class YzhQueue : BaseEntity
{
    /// <summary>主键（DB: bigint AUTO_INCREMENT）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public new long Id { get; set; }

    /// <summary>全局唯一编码(GUID)，表间关联用</summary>
    [SugarColumn(ColumnName = "code", Length = 36)]
    public new string Code { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>队列业务编码：Q-{yyyyMMdd}-{6位随机}</summary>
    [SugarColumn(ColumnName = "queue_code", Length = 64)]
    public string QueueCode { get; set; } = string.Empty;

    /// <summary>队列类型：file_convert/auto_verify/report_generate</summary>
    [SugarColumn(ColumnName = "queue_type", Length = 30)]
    public string QueueType { get; set; } = string.Empty;

    /// <summary>队列名称（人话）</summary>
    [SugarColumn(ColumnName = "queue_name", Length = 200, IsNullable = true)]
    public string? QueueName { get; set; }

    /// <summary>范围键（按类型约定格式）</summary>
    [SugarColumn(ColumnName = "scope_key", Length = 200, IsNullable = true)]
    public string? ScopeKey { get; set; }

    /// <summary>冗余展示数据 JSON</summary>
    [SugarColumn(ColumnName = "scope_info", ColumnDataType = "text", IsNullable = true)]
    public string? ScopeInfo { get; set; }

    /// <summary>来源类型：upload_task/verify_req/report_req</summary>
    [SugarColumn(ColumnName = "source_type", Length = 30, IsNullable = true)]
    public string? SourceType { get; set; }

    /// <summary>来源ID</summary>
    [SugarColumn(ColumnName = "source_id", Length = 64, IsNullable = true)]
    public string? SourceId { get; set; }

    /// <summary>状态：pending/running/completed/failed/cancelled</summary>
    [SugarColumn(ColumnName = "status", Length = 20)]
    public string Status { get; set; } = "pending";

    [SugarColumn(ColumnName = "total_count")]
    public int TotalCount { get; set; }

    [SugarColumn(ColumnName = "pending_count")]
    public int PendingCount { get; set; }

    [SugarColumn(ColumnName = "processing_count")]
    public int ProcessingCount { get; set; }

    [SugarColumn(ColumnName = "completed_count")]
    public int CompletedCount { get; set; }

    [SugarColumn(ColumnName = "failed_count")]
    public int FailedCount { get; set; }

    [SugarColumn(ColumnName = "cancelled_count")]
    public int CancelledCount { get; set; }

    [SugarColumn(ColumnName = "progress")]
    public int Progress { get; set; }

    [SugarColumn(ColumnName = "start_time", IsNullable = true)]
    public DateTime? StartTime { get; set; }

    [SugarColumn(ColumnName = "end_time", IsNullable = true)]
    public DateTime? EndTime { get; set; }

    [SugarColumn(ColumnName = "remark", Length = 500, IsNullable = true)]
    public string? Remark { get; set; }

    [SugarColumn(ColumnName = "org_code", Length = 50, IsNullable = true)]
    public string? OrgCode { get; set; }

    // ──── 审计字段重声明（BaseEntity IsIgnore=true → 这里映射到 DB 列） ────
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
