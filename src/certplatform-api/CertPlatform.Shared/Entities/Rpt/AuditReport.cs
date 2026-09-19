using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Rpt
{
    /// <summary>
    /// AuditReport 审核报告
    /// <para>表名：rpt_audit_report</para>
    /// </summary>
    [SugarTable("rpt_audit_report")]
    public class AuditReport : BaseEntity
    {
        // ──── Id / 审计字段由 BaseEntity 基类统一提供 ────

        // ──── 业务字段 ────
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        public string? OrgCode { get; set; }

        [Required, StringLength(36)]
        public string ReportTaskCode { get; set; }

        [Required, StringLength(50)]
        [UniqueField("报告编号")]
        public string ReportNumber { get; set; }

        [StringLength(500)]
        public string? FilePath { get; set; }

        [Required]
        public int CreatedBy { get; set; }
    }
}
