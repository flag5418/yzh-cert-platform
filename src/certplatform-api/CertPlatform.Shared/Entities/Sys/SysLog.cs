using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Sys
{
    /// <summary>
    /// SysLog
    /// <para>表名：sys_log</para>
    /// </summary>
    [SugarTable("sys_log")]
    public class SysLog : BaseEntity
    {
        // ──── Id / 审计字段由 BaseEntity 基类统一提供 ────

        // ──── 业务字段（与物理表 sys_log / V2 设计 E-06 对齐）────

        /// <summary>操作用户 ID（DB: UserId, bigint）</summary>
        public long? UserId { get; set; }

        [Required, StringLength(50)]
        public string Module { get; set; }

        [Required, StringLength(100)]
        public string Action { get; set; }

        [StringLength(50)]
        public string? TargetType { get; set; }

        /// <summary>操作对象 ID（DB: TargetId, bigint）</summary>
        public long? TargetId { get; set; }

        [SugarColumn(Length = 2000, IsNullable = true)]
        public string? Detail { get; set; }

        [StringLength(50)]
        public string? IpAddress { get; set; }

        [StringLength(500)]
        public string? UserAgent { get; set; }
    }
}
