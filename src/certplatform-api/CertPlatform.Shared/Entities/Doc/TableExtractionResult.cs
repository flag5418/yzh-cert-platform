using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Doc
{
    /// <summary>
    /// 文档表格提取结果（表格级，每表格一条）
    /// <para>表名：cert_table_extraction_result（原 ent_table_extraction_result，按命名约定 ent→cert）</para>
    /// <para>归属：管理端「文档提取规则」+ 工作流 get_table 节点</para>
    /// </summary>
    [SugarTable("cert_table_extraction_result")]
    public class TableExtractionResult : BaseEntity
    {
        // ──── Id / 审计字段由 BaseEntity 基类统一提供 ────

        // ──── 业务字段 ────
        /// <summary>标准企业标识（多租户隔离，映射到 DB OrgCode 列）</summary>
        [Required, StringLength(36)]
        [SugarColumn(ColumnName = "OrgCode")]
        public string EnterpriseCode { get; set; }

        /// <summary>标准文件编码（规则键：实际文件 FileCode 或文件要求模板 Code，最长 200）</summary>
        [StringLength(200)]
        public string? StandardFileCode { get; set; }

        /// <summary>标准编码（冗余，关联 cert_iso_standard.code）</summary>
        [StringLength(36)]
        public string? StandardCode { get; set; }

        /// <summary>阶段编码（冗余，方便过滤）</summary>
        [StringLength(36)]
        public string? PhaseCode { get; set; }

        [Required, StringLength(200)]
        [UniqueField("文件编码", WithFields = new[] { "EnterpriseCode", "TableIndex" })]
        public string FileCode { get; set; }

        [Required]
        public int VersionNumber { get; set; }

        [Required, StringLength(200)]
        public string RuleCode { get; set; }

        /// <summary>定义表编码（cert_doc_table_def.code；工作流 get_table 节点查询键）</summary>
        [StringLength(200)]
        public string? TableCode { get; set; }

        public int TableIndex { get; set; } = 1;

        [Required]
        public string ExtractedJson { get; set; }

        public decimal? Confidence { get; set; }

        public string? PositionInfo { get; set; }

        [Required]
        public DateTime ExtractedAt { get; set; }
    }
}
