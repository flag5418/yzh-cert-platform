using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Stand.Models.Queue;

/// <summary>
/// yzh 队列子任务表（通用任务，跨项目复用）
/// <para>业务数据统一进 payload JSON，按 task_type 由对应 IYzhTaskExecutor 解析执行</para>
/// <para>表名：yzh_queue_task</para>
/// </summary>
[SugarTable("yzh_queue_task")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class YzhQueueTask : BaseEntity
{
    /// <summary>主键（DB: bigint AUTO_INCREMENT）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public new long Id { get; set; }

    /// <summary>全局唯一编码(GUID)</summary>
    [SugarColumn(ColumnName = "code", Length = 36)]
    public new string Code { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>所属队列编码(yzh_queue.queue_code)</summary>
    [SugarColumn(ColumnName = "queue_code", Length = 64)]
    public string QueueCode { get; set; } = string.Empty;

    /// <summary>任务类型：file_convert/auto_verify/report_generate</summary>
    [SugarColumn(ColumnName = "task_type", Length = 30)]
    public string TaskType { get; set; } = string.Empty;

    /// <summary>业务数据 JSON</summary>
    [SugarColumn(ColumnName = "payload", ColumnDataType = "text", IsNullable = true)]
    public string? Payload { get; set; }

    /// <summary>状态：pending/processing/completed/failed/cancelled</summary>
    [SugarColumn(ColumnName = "status", Length = 20)]
    public string Status { get; set; } = "pending";

    /// <summary>错误分类：retryable(可重试)/permanent(永久)</summary>
    [SugarColumn(ColumnName = "error_type", Length = 20, IsNullable = true)]
    public string? ErrorType { get; set; }

    /// <summary>错误信息</summary>
    [SugarColumn(ColumnName = "error_message", Length = 2000, IsNullable = true)]
    public string? ErrorMessage { get; set; }

    /// <summary>重试次数</summary>
    [SugarColumn(ColumnName = "retry_count")]
    public int RetryCount { get; set; }

    /// <summary>最大重试次数</summary>
    [SugarColumn(ColumnName = "max_retry_count")]
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>下次重试时间（指数退避+抖动）</summary>
    [SugarColumn(ColumnName = "next_retry_at", IsNullable = true)]
    public DateTime? NextRetryAt { get; set; }

    /// <summary>领取租约到期时间</summary>
    [SugarColumn(ColumnName = "locked_until", IsNullable = true)]
    public DateTime? LockedUntil { get; set; }

    /// <summary>领取时间</summary>
    [SugarColumn(ColumnName = "locked_at", IsNullable = true)]
    public DateTime? LockedAt { get; set; }

    /// <summary>领取 Worker 标识</summary>
    [SugarColumn(ColumnName = "locked_by", Length = 100, IsNullable = true)]
    public string? LockedBy { get; set; }

    /// <summary>开始处理时间</summary>
    [SugarColumn(ColumnName = "process_time", IsNullable = true)]
    public DateTime? ProcessTime { get; set; }

    /// <summary>完成/失败/取消时间</summary>
    [SugarColumn(ColumnName = "complete_time", IsNullable = true)]
    public DateTime? CompleteTime { get; set; }

    /// <summary>入队时间</summary>
    [SugarColumn(ColumnName = "create_time")]
    public DateTime CreateTime { get; set; } = DateTime.Now;

    /// <summary>来源批次ID（如上传任务taskId）</summary>
    [SugarColumn(ColumnName = "task_id", Length = 64, IsNullable = true)]
    public string? TaskId { get; set; }

    /// <summary>发起用户ID</summary>
    [SugarColumn(ColumnName = "user_id", IsNullable = true)]
    public int? UserId { get; set; }

    /// <summary>发起用户名</summary>
    [SugarColumn(ColumnName = "user_name", Length = 100, IsNullable = true)]
    public string? UserName { get; set; }

    /// <summary>机构编码</summary>
    [SugarColumn(ColumnName = "org_code", Length = 50, IsNullable = true)]
    public string? OrgCode { get; set; }

    /// <summary>优先级（0=普通，10=高优先）</summary>
    [SugarColumn(ColumnName = "priority")]
    public int Priority { get; set; }

    /// <summary>本任务持有的资源锁编码（逗号分隔）</summary>
    [SugarColumn(ColumnName = "lock_codes", Length = 500, IsNullable = true)]
    public string? LockCodes { get; set; }

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
