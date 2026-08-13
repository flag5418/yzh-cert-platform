using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;
using VOL.Entity.SystemModels;

namespace YZH.Core.Queue
{
    /// <summary>
    /// yzh 队列资源锁定表（通用，跨项目复用）
    /// <para>队列创建时对涉及资源加锁；子任务完成释放对应锁；队列终态批量释放</para>
    /// <para>resource_table + resource_code 可锁定任意业务表资源，实现跨队列类型冲突检测</para>
    /// </summary>
    [Entity(TableCnName = "yzh队列资源锁定表", TableName = "yzh_queue_resource_lock", DBServer = "VOLContext")]
    [Table("yzh_queue_resource_lock")]
    public class YzhQueueResourceLock : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        /// <summary>全局唯一编码(GUID)</summary>
        [MaxLength(36)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        /// <summary>所属队列编码</summary>
        [MaxLength(64)]
        [Column("queue_code")]
        public string QueueCode { get; set; } = string.Empty;

        /// <summary>资源表名（任意业务表，如 cert_standard_directory_file / cert_report）</summary>
        [MaxLength(50)]
        [Column("resource_table")]
        public string ResourceTable { get; set; } = string.Empty;

        /// <summary>资源唯一编码</summary>
        [MaxLength(200)]
        [Column("resource_code")]
        public string ResourceCode { get; set; } = string.Empty;

        /// <summary>资源名称快照（可空）</summary>
        [MaxLength(200)]
        [Column("resource_name")]
        public string? ResourceName { get; set; }

        /// <summary>占用该资源的子任务序号（NULL=队列级锁）</summary>
        [Column("task_no")]
        public int? TaskNo { get; set; }

        /// <summary>状态：locked/released</summary>
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "locked";

        /// <summary>活跃锁键（可空，释放时置 NULL）</summary>
        [MaxLength(260)]
        [Column("active_key")]
        public string? ActiveKey { get; set; }

        /// <summary>加锁时间</summary>
        [Column("create_time")]
        public DateTime? CreateTime { get; set; } = DateTime.Now;

        /// <summary>释放时间</summary>
        [Column("release_time")]
        public DateTime? ReleaseTime { get; set; }

        /// <summary>锁租约安全网：回收任务扫描超时锁强制释放</summary>
        [Column("expire_at")]
        public DateTime? ExpireAt { get; set; }

        [MaxLength(50)]
        [Column("org_code")]
        public string? OrgCode { get; set; }

        [Column("create_id")]
        public int? CreateID { get; set; }

        [MaxLength(50)]
        [Column("creator")]
        public string? Creator { get; set; }

        [Column("create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        [Column("modify_id")]
        public int? ModifyID { get; set; }

        [MaxLength(50)]
        [Column("modifier")]
        public string? Modifier { get; set; }

        [Column("modify_date")]
        public DateTime? ModifyDate { get; set; }

        [Column("delete_id")]
        public int? DeleteID { get; set; }

        [MaxLength(50)]
        [Column("deleter")]
        public string? Deleter { get; set; }

        [Column("delete_time")]
        public DateTime? DeleteTime { get; set; }
    }
}
