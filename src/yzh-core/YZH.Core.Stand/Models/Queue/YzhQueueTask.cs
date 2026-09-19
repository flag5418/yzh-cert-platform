using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Core.Stand.Models.Queue;

/// <summary>
/// yzh 队列子任务表（通用任务，跨项目复用）
/// <para>业务数据统一进 payload JSON，按 task_type 由对应 IYzhTaskExecutor 解析执行</para>
/// <para>表名：yzh_queue_task</para>
/// <para>架构铁律：继承 BaseEntity，DB 列名 = C# 属性名 = PascalCase</para>
/// </summary>
[SugarTable("yzh_queue_task")]
[YZHDeleteStrategy(Mode = DeleteMode.Soft)]
public class YzhQueueTask : BaseEntity, ISoftDelete
{
    /// <summary>主键（DB: bigint AUTO_INCREMENT）</summary>
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public new long Id { get; set; }

    /// <summary>所属队列编码(yzh_queue.QueueCode)</summary>
    public string QueueCode { get; set; } = string.Empty;

    /// <summary>任务类型：file_convert/auto_verify/report_generate</summary>
    public string TaskType { get; set; } = string.Empty;

    /// <summary>业务数据 JSON</summary>
    [SugarColumn(ColumnDataType = "text", IsNullable = true)]
    public string? Payload { get; set; }

    /// <summary>状态：pending/processing/completed/failed/cancelled</summary>
    public string Status { get; set; } = "pending";

    /// <summary>错误分类：retryable(可重试)/permanent(永久)</summary>
    public string? ErrorType { get; set; }

    /// <summary>错误信息</summary>
    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? ErrorMessage { get; set; }

    /// <summary>重试次数</summary>
    public int RetryCount { get; set; }

    /// <summary>最大重试次数</summary>
    public int MaxRetryCount { get; set; } = 3;

    /// <summary>下次重试时间（指数退避+抖动）</summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>领取租约到期时间</summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>领取时间</summary>
    public DateTime? LockedAt { get; set; }

    /// <summary>领取 Worker 标识</summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? LockedBy { get; set; }

    /// <summary>开始处理时间</summary>
    public DateTime? ProcessTime { get; set; }

    /// <summary>完成/失败/取消时间</summary>
    public DateTime? CompleteTime { get; set; }

    /// <summary>来源批次ID（如上传任务taskId）</summary>
    [SugarColumn(Length = 64, IsNullable = true)]
    public string? TaskId { get; set; }

    /// <summary>发起用户ID</summary>
    public int? UserId { get; set; }

    /// <summary>发起用户名</summary>
    [SugarColumn(Length = 100, IsNullable = true)]
    public string? UserName { get; set; }

    /// <summary>机构编码</summary>
    [SugarColumn(Length = 50, IsNullable = true)]
    public string? OrgCode { get; set; }

    /// <summary>优先级（0=普通，10=高优先）</summary>
    public int Priority { get; set; }

    /// <summary>本任务持有的资源锁编码（逗号分隔）</summary>
    [SugarColumn(Length = 500, IsNullable = true)]
    public string? LockCodes { get; set; }

    // ──── ISoftDelete 接口实现 ────

    /// <summary>软删除标记（false=正常，true=已删除）</summary>
    public bool IsDeleted { get; set; }

    /// <summary>删除人 Code</summary>
    public new string? DeleteBy { get; set; }

    /// <summary>删除时间</summary>
    public new DateTime? DeleteTime { get; set; }
}
