using SqlSugar;

namespace CertPlatform.Shared.Entities.Rpt
{
    /// <summary>
    /// 报告章节
    /// <para>表名：rpt_report_section</para>
    /// <para>不继承 BaseEntity，因为 Id 是 bigint 自增且 DB 审计字段命名不同</para>
    /// </summary>
    [SugarTable("rpt_report_section")]
    public class ReportSection
    {
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "Code", Length = 36)]
        public string? Code { get; set; }

        [SugarColumn(ColumnName = "OrgCode", Length = 50, IsNullable = true)]
        public string? OrgCode { get; set; }

        [SugarColumn(ColumnName = "ReportCode", Length = 36)]
        public string? ReportCode { get; set; }

        [SugarColumn(ColumnName = "SectionName", Length = 200)]
        public string? SectionName { get; set; }

        [SugarColumn(ColumnName = "SectionNameEn", Length = 200, IsNullable = true)]
        public string? SectionNameEn { get; set; }

        [SugarColumn(ColumnName = "SectionContent", ColumnDataType = "text", IsNullable = true)]
        public string? Content { get; set; }

        [SugarColumn(ColumnName = "SortOrder")]
        public int SortOrder { get; set; } = 0;

        [SugarColumn(ColumnName = "IsActive")]
        public int IsActive { get; set; } = 1;

        [SugarColumn(ColumnName = "WorkflowCode", Length = 36, IsNullable = true)]
        public string? WorkflowCode { get; set; }

        [SugarColumn(ColumnName = "WorkflowConfig", ColumnDataType = "text", IsNullable = true)]
        public string? WorkflowConfig { get; set; }

        [SugarColumn(ColumnName = "LayoutJson", ColumnDataType = "text", IsNullable = true)]
        public string? LayoutJson { get; set; }

        [SugarColumn(ColumnName = "ClauseCode", Length = 36, IsNullable = true)]
        public string? ClauseCode { get; set; }

        [SugarColumn(ColumnName = "SectionJson", ColumnDataType = "text", IsNullable = true)]
        public string? SectionJson { get; set; }

        [SugarColumn(ColumnName = "Remark", Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        [SugarColumn(ColumnName = "creator", Length = 50, IsNullable = true)]
        public string? Creator { get; set; }

        [SugarColumn(ColumnName = "create_by", Length = 50, IsNullable = true)]
        public string? CreateBy { get; set; }

        [SugarColumn(ColumnName = "create_date", IsNullable = true)]
        public DateTime? CreateDate { get; set; }

        [SugarColumn(ColumnName = "modifier", Length = 50, IsNullable = true)]
        public string? Modifier { get; set; }

        [SugarColumn(ColumnName = "update_by", Length = 50, IsNullable = true)]
        public string? UpdateBy { get; set; }

        [SugarColumn(ColumnName = "modify_date", IsNullable = true)]
        public DateTime? ModifyDate { get; set; }

        [SugarColumn(ColumnName = "deleter", Length = 50, IsNullable = true)]
        public string? Deleter { get; set; }

        [SugarColumn(ColumnName = "delete_by", Length = 50, IsNullable = true)]
        public string? DeleteBy { get; set; }

        [SugarColumn(ColumnName = "delete_time", IsNullable = true)]
        public DateTime? DeleteTime { get; set; }

        [SugarColumn(ColumnName = "IsDeleted")]
        public bool IsDeleted { get; set; } = false;

        [SugarColumn(ColumnName = "status", Length = 50, IsNullable = true)]
        public string? Status { get; set; }

        [SugarColumn(ColumnName = "enable", IsNullable = true)]
        public bool? Enable { get; set; }

        [SugarColumn(ColumnName = "Sort")]
        public int Sort { get; set; } = 0;
    }
}
