using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Rpt
{
    /// <summary>
    /// 报告章节
    /// <para>表名：rpt_report_section</para>
    /// <para>命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase</para>
    /// </summary>
    [SugarTable("rpt_report_section")]
    public class ReportSection : BaseEntity, ISoftDelete
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────

        [SugarColumn(Length = 50, IsNullable = true)]
        public string? OrgCode { get; set; }

        [SugarColumn(Length = 36)]
        public string? ReportCode { get; set; }

        [SugarColumn(Length = 200)]
        public string? SectionName { get; set; }

        [SugarColumn(Length = 200, IsNullable = true)]
        public string? SectionNameEn { get; set; }

        [SugarColumn(ColumnDataType = "text", IsNullable = true, ColumnName = "SectionContent")]
        public string? Content { get; set; }

        public int SortOrder { get; set; } = 0;

        public int IsActive { get; set; } = 1;

        [SugarColumn(Length = 36, IsNullable = true)]
        public string? WorkflowCode { get; set; }

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? WorkflowConfig { get; set; }

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? LayoutJson { get; set; }

        [SugarColumn(Length = 36, IsNullable = true)]
        public string? ClauseCode { get; set; }

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? SectionJson { get; set; }

        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        [SugarColumn(Length = 50, IsNullable = true, ColumnName = "status")]
        public string? Status { get; set; }

        [SugarColumn(ColumnName = "enable")]
        public bool? Enable { get; set; }

        public int Sort { get; set; } = 0;

        // ──── ISoftDelete 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
    }
}
