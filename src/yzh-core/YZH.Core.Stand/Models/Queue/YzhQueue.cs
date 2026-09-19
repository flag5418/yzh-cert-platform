using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Stand.Models.Queue;

/// <summary>
/// yzh 队列主表（通用队列中心，跨项目复用）
/// <para>一次业务动作 = 一个队列实例（如"上传批次 #abc 的 12 个 doc/xls 转换"）</para>
/// <para>表名：yzh_queue</para>
/// <para>架构铁律：继承 BaseEntity，DB 列名 = C# 属性名 = PascalCase</para>
/// </summary>
[SugarTable("yzh_queue")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class YzhQueue : BaseEntity, ISoftDelete
{
    /// <summary>主键（DB: bigint AUTO_INCREMENT）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public new long Id { get; set; }

    /// <summary>队列业务编码：Q-{yyyyMMdd}-{6位随机}</summary>
    public string QueueCode { get; set; } = string.Empty;

    /// <summary>队列类型：file_convert/auto_verify/report_generate</summary>
    public string QueueType { get; set; } = string.Empty;

    /// <summary>队列名称（人话）</summary>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string? QueueName { get; set; }

    /// <summary>范围键（按类型约定格式）</summary>
    [SugarColumn(Length = 200, IsNullable = true)]
    public string? ScopeKey { get; set; }

    /// <summary>冗余展示数据 JSON</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? ScopeInfo { get; set; }

    /// <summary>来源类型：upload_task/verify_req/report_req</summary>
    [SugarColumn(Length = 30, IsNullable = true)]
    public string? SourceType { get; set; }

    /// <summary>来源ID</summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? SourceId { get; set; }

    /// <summary>状态：pending/running/completed/failed/cancelled</summary>
    public string Status { get; set; } = "pending";

    public int TotalCount { get; set; }
    public int PendingCount { get; set; }
    public int ProcessingCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int CancelledCount { get; set; }
    public int Progress { get; set; }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    [SugarColumn(Length = 500, IsNullable = true)]
    public string? Remark { get; set; }

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
