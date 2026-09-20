using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Wf
{
    /// <summary>
    /// 队列资源锁
    /// <para>表名：yzh_queue_resource_lock</para>
    /// </summary>
    [SugarTable("yzh_queue_resource_lock")]
    public class YzhQueueResourceLock : BaseEntity, ISoftDelete
    {
        [SugarColumn(Length = 64)]
        public string QueueCode { get; set; } = string.Empty;

        [SugarColumn(Length = 50)]
        public string ResourceTable { get; set; } = string.Empty;

        [SugarColumn(Length = 200)]
        public string ResourceCode { get; set; } = string.Empty;

        [SugarColumn(Length = 200, IsNullable = true)]
        public string? ResourceName { get; set; }

        public int? TaskNo { get; set; }

        [SugarColumn(Length = 20)]
        public string Status { get; set; } = "locked";

        [SugarColumn(Length = 260, IsNullable = true)]
        public string? ActiveKey { get; set; }

        public DateTime? CreateTime { get; set; }

        public DateTime? ReleaseTime { get; set; }

        public DateTime? ExpireAt { get; set; }

        [SugarColumn(Length = 50, IsNullable = true)]
        public string? OrgCode { get; set; }

        // ──── ISoftDelete 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
    }
}
