using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// 报告模板
    /// <para>表名：cert_report_template</para>
    /// <para>命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase</para>
    /// </summary>
    [SugarTable("cert_report_template")]
    public class ReportTemplate : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────

        [SugarColumn(Length = 50, IsNullable = true)]
        public string? OrgCode { get; set; }

        [SugarColumn(Length = 36)]
        public string? CbCode { get; set; }

        [SugarColumn(Length = 36)]
        public string? StandardCode { get; set; }

        [SugarColumn(Length = 36)]
        public string? PhaseCode { get; set; }

        [SugarColumn(Length = 200)]
        public string? TemplateName { get; set; }

        [SugarColumn(Length = 500, IsNullable = true)]
        public string? TemplateFilePath { get; set; }

        public bool IsDefault { get; set; } = false;

        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        public int Sort { get; set; } = 0;

        [SugarColumn(Length = 50, IsNullable = true)]
        public string? Status { get; set; }

        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? SectionConfig { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
