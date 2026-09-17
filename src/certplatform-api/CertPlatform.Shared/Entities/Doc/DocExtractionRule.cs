using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SqlSugar;
using YZH.Entity.Admin.Platform;

namespace YZH.Entity.Admin.Platform.Doc
{
    /// <summary>
    /// DocExtractionRule 文档提取规则主表
    /// 一个标准文件对应一个规则
    /// <para>规则键：standard_file_code — 存实际标准目录文件的 FileCode（FL-xxx，前端目录树流程）</para>
    /// <para>或文件要求的模板 Code（FR-xxx，模板流程）</para>
    /// <para>冗余字段：standard_code / phase_code 方便过滤</para>
    /// <para>表名：cert_doc_extraction_rule（列名以 2026-09-16 实测 DDL 为准：DocIsValid / CreateBy+CreateTime 体系 / IsDeleted+IsValid）</para>
    /// </summary>
    [Table("cert_doc_extraction_rule")]
    [SugarTable("cert_doc_extraction_rule")]
    public class DocExtractionRule : EntityBase
    {
        [Column("id")]
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "id")]
        public long Id { get; set; }

        [Column("code")]
        [SugarColumn(ColumnName = "code", Length = 100)]
        public new string Code { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>文件编码（历史字段，保留兼容；当前流程规则键统一用 standard_file_code）</summary>
        [Column("file_code")]
        [Display(Name = "文件编码")]
        [StringLength(100)]
        [SugarColumn(ColumnName = "file_code", Length = 100, IsNullable = true)]
        public string? FileCode { get; set; }

        /// <summary>
        /// 规则键：实际文件 FileCode（FL-FD-...|文件名，前端目录树 AI 分析流程）
        /// 或文件要求模板 Code（FR-xxx，模板流程）
        /// GetFileInfoAsync 按此值两级查询（模板优先、实际文件兜底）
        /// </summary>
        [Column("standard_file_code")]
        [Display(Name = "规则文件编码")]
        [StringLength(200)]
        [SugarColumn(ColumnName = "standard_file_code", Length = 200, IsNullable = true)]
        public string? StandardFileCode { get; set; }

        /// <summary>标准编码（冗余，关联 cert_iso_standard.code）</summary>
        [Column("standard_code")]
        [Display(Name = "标准编码")]
        [StringLength(36)]
        [SugarColumn(ColumnName = "standard_code", Length = 36, IsNullable = true)]
        public string? StandardCode { get; set; }

        /// <summary>阶段编码（冗余，方便过滤）</summary>
        [Column("phase_code")]
        [Display(Name = "阶段编码")]
        [StringLength(36)]
        [SugarColumn(ColumnName = "phase_code", Length = 36, IsNullable = true)]
        public string? PhaseCode { get; set; }

        /// <summary>技能类型（word/excel/pdf，按文件扩展名权威推导）</summary>
        [Column("skill")]
        [Display(Name = "技能类型")]
        [Required(ErrorMessage = "技能类型不能为空")]
        [StringLength(50)]
        [SugarColumn(ColumnName = "skill", Length = 50)]
        public string Skill { get; set; } = "word";

        /// <summary>提取 Prompt</summary>
        [Display(Name = "提取Prompt")]
        [SugarColumn(ColumnName = "prompt", ColumnDataType = "text", IsNullable = true)]
        public string? Prompt { get; set; }

        /// <summary>是否验证通过（列名 DocIsValid，注意不是 is_valid）</summary>
        [Column("DocIsValid")]
        [Display(Name = "是否验证通过")]
        [SugarColumn(ColumnName = "DocIsValid")]
        public bool DocIsValid { get; set; } = false;

        /// <summary>验证结果信息</summary>
        [Column("verify_message")]
        [Display(Name = "验证信息")]
        [StringLength(500)]
        [SugarColumn(ColumnName = "verify_message", Length = 500, IsNullable = true)]
        public string? VerifyMessage { get; set; }

        /// <summary>验证时提取的样本数据（JSON 格式）</summary>
        [Column("sample_data")]
        [Display(Name = "样本数据")]
        [SugarColumn(ColumnName = "sample_data", ColumnDataType = "json", IsNullable = true)]
        public string? SampleData { get; set; }

        /// <summary>提取的文档内容缓存（Markdown，避免每次验证都重新转换文档）</summary>
        [Column("doc_content")]
        [Display(Name = "文档内容缓存")]
        [SugarColumn(ColumnName = "doc_content", ColumnDataType = "longtext", IsNullable = true)]
        public string? DocContent { get; set; }

        /// <summary>规则状态：none/configured/failed（与字典 rule_status、目录树徽标对齐，禁止 0/1/2）</summary>
        [Column("status")]
        [SugarColumn(ColumnName = "status", Length = 20)]
        public new string Status { get; set; } = "none";

        [Column("Remark")]
        [StringLength(500)]
        [SugarColumn(ColumnName = "Remark", Length = 500, IsNullable = true)]
        public new string? Remark { get; set; }

        [Column("CreateBy")]
        [StringLength(50)]
        [SugarColumn(ColumnName = "CreateBy", Length = 50, IsNullable = true)]
        public new string? CreateBy { get; set; }

        [Column("CreateTime")]
        [SugarColumn(ColumnName = "CreateTime")]
        public new DateTime? CreateTime { get; set; } = DateTime.Now;

        [Column("UpdateBy")]
        [StringLength(50)]
        [SugarColumn(ColumnName = "UpdateBy", Length = 50, IsNullable = true)]
        public new string? UpdateBy { get; set; }

        [Column("UpdateTime")]
        [SugarColumn(ColumnName = "UpdateTime")]
        public new DateTime? UpdateTime { get; set; }

        [Column("DeleteBy")]
        [StringLength(50)]
        [SugarColumn(ColumnName = "DeleteBy", Length = 50, IsNullable = true)]
        public new string? DeleteBy { get; set; }

        [Column("DeleteTime")]
        [SugarColumn(ColumnName = "DeleteTime")]
        public new DateTime? DeleteTime { get; set; }

        [SugarColumn(ColumnName = "IsDeleted")]
        public bool IsDeleted { get; set; }

        [SugarColumn(ColumnName = "IsValid")]
        public int IsValid { get; set; } = 1;
    }
}
