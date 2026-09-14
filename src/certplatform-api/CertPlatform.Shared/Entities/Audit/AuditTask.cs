using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Entity.Admin.Platform.Audit
{
    /// <summary>
    /// 审核任务
    /// <para>表名：audit_task</para>
    /// <para>ORM：SqlSugar（§16 铁律：DB列名 == C#属性名，PascalCase）</para>
    /// </summary>
    [SugarTable("audit_task")]
    public class AuditTask : BaseEntity
    {
        /// <summary>机构编码（多租户隔离）</summary>
        [StringLength(50)]
        public string OrgCode { get; set; }

        /// <summary>阶段编码</summary>
        [Required, StringLength(36)]
        public string PhaseCode { get; set; }

        /// <summary>任务编号</summary>
        [Required, StringLength(50)]
        [UniqueField("任务编号")]
        public string TaskNumber { get; set; }

        /// <summary>审核员ID</summary>
        [Required]
        public long AuditorId { get; set; }

        /// <summary>计划日期</summary>
        public DateTime? PlannedDate { get; set; }

        /// <summary>实际开始日期</summary>
        public DateTime? ActualStartDate { get; set; }

        /// <summary>实际完成日期</summary>
        public DateTime? ActualCompleteDate { get; set; }

        /// <summary>审核范围</summary>
        public string AuditScope { get; set; }
    }
}
