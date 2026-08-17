using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// TableExtractionResult 表格提取结果
    /// <para>表名：ent_table_extraction_result</para>
    /// <para>核心关联：standard_file_code → cert_file_requirement.code</para>
    /// <para>冗余字段：org_code / standard_code / phase_code 方便过滤和数据提取</para>
    /// </summary>
    [Table("ent_table_extraction_result")]
    public class TableExtractionResult : YZHBaseEntity
    {
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        [Column("enterprise_code")]
        public string EnterpriseCode { get; set; }

        /// <summary>标准文件编码（规则键：实际文件 FileCode 或文件要求模板 Code，最长 200）</summary>
        [StringLength(200)]
        [Column("standard_file_code")]
        public string StandardFileCode { get; set; }

        /// <summary>标准编码（冗余，关联 cert_iso_standard.code）</summary>
        [StringLength(36)]
        [Column("standard_code")]
        public string StandardCode { get; set; }

        /// <summary>阶段编码（冗余，方便过滤）</summary>
        [StringLength(36)]
        [Column("phase_code")]
        public string PhaseCode { get; set; }

        [Required, StringLength(200)]
        [Column("file_code")]
        public string FileCode { get; set; }

        [Required]
        [Column("version_number")]
        public int VersionNumber { get; set; }

        [Required, StringLength(200)]
        [Column("rule_code")]
        public string RuleCode { get; set; }

        /// <summary>定义表编码（cert_doc_table_def.code；工作流 get_table 节点查询键，评审 §3.3 修复）</summary>
        [StringLength(200)]
        [Column("table_code")]
        public string TableCode { get; set; }

        [Column("table_index")]
        public int TableIndex { get; set; } = 1;

        [Required]
        [Column("extracted_json")]
        public string ExtractedJson { get; set; }

        [Column("confidence")]
        public decimal? Confidence { get; set; }

        [Column("position_info")]
        public string PositionInfo { get; set; }

        [Required]
        [Column("extracted_at")]
        public DateTime ExtractedAt { get; set; }

        // org_code 继承自 YZHBaseEntity
    }
}
