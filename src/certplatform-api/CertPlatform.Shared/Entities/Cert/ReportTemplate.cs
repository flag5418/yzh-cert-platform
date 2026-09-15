using SqlSugar;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// 报告模板
    /// <para>表名：cert_report_template</para>
    /// <para>不继承 BaseEntity，因为 Id 是 bigint 自增而非 string</para>
    /// </summary>
    [SugarTable("cert_report_template")]
    public class ReportTemplate
    {
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "Code", Length = 36)]
        public string? Code { get; set; }

        [SugarColumn(ColumnName = "OrgCode", Length = 50, IsNullable = true)]
        public string? OrgCode { get; set; }

        [SugarColumn(ColumnName = "CbCode", Length = 36)]
        public string? CbCode { get; set; }

        [SugarColumn(ColumnName = "StandardCode", Length = 36)]
        public string? StandardCode { get; set; }

        [SugarColumn(ColumnName = "PhaseCode", Length = 36)]
        public string? PhaseCode { get; set; }

        [SugarColumn(ColumnName = "TemplateName", Length = 200)]
        public string? TemplateName { get; set; }

        [SugarColumn(ColumnName = "TemplateFilePath", Length = 500, IsNullable = true)]
        public string? TemplateFilePath { get; set; }

        [SugarColumn(ColumnName = "IsDefault")]
        public bool IsDefault { get; set; } = false;

        [SugarColumn(ColumnName = "Remark", Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        [SugarColumn(ColumnName = "IsValid")]
        public int IsValid { get; set; } = 1;

        [SugarColumn(ColumnName = "CreateBy", Length = 50, IsNullable = true)]
        public string? CreateBy { get; set; }

        [SugarColumn(ColumnName = "CreateDate")]
        public DateTime CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName = "UpdateBy", Length = 50, IsNullable = true)]
        public string? UpdateBy { get; set; }

        [SugarColumn(ColumnName = "ModifyDate", IsNullable = true)]
        public DateTime? ModifyDate { get; set; }

        [SugarColumn(ColumnName = "DeleteBy", Length = 50, IsNullable = true)]
        public string? DeleteBy { get; set; }

        [SugarColumn(ColumnName = "DeleteTime", IsNullable = true)]
        public DateTime? DeleteTime { get; set; }

        [SugarColumn(ColumnName = "IsDeleted")]
        public bool IsDeleted { get; set; } = false;

        [SugarColumn(ColumnName = "status", Length = 50, IsNullable = true)]
        public string? Status { get; set; }

        [SugarColumn(ColumnName = "Sort")]
        public int Sort { get; set; } = 0;

        [SugarColumn(ColumnName = "SectionConfig", ColumnDataType = "json", IsNullable = true)]
        public string? SectionConfig { get; set; }
    }
}
