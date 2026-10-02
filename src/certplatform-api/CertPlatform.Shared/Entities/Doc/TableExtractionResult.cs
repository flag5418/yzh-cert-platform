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

        /// <summary>阶段编码（冗余，方便过滤；决策 ⑩ 统一为 StageCode → cert_cert_stage.Code）</summary>
        [StringLength(36)]
        public string? StageCode { get; set; }

        [Required, StringLength(200)]
        [UniqueField("文件编码", WithFields = new[] { "EnterpriseCode", "TableIndex" })]
        public string FileCode { get; set; }

        [Required]
        public int VersionNumber { get; set; }

        /// <summary>提取规则编码（<c>cert_doc_extraction_rule.Code</c>）
        /// <para>★ 2026-09-30 起是<b>取数与清理的主键成分</b>（裁决 J4），与
        /// <see cref="ExtractionResult.RuleCode"/> 同语义。</para>
        /// </summary>
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

        /// <summary>是否被人工修改（★ 2026-09-30 落库；本列原本不存在 ⇒ 表格补录不留痕）</summary>
        public bool IsManualEdited { get; set; } = false;

        /// <summary>值来源（★ 2026-09-30 落库，裁决 J2）
        /// <para><c>auto</c>=系统自动提取 ｜ <c>manual</c>=人工录入（补录）。
        /// 与 <see cref="ExtractionResult.ValueSource"/> 同语义、同取值域。</para>
        /// </summary>
        [Required, StringLength(20)]
        public string ValueSource { get; set; } = ExtractionValueSource.Auto;

        [Required]
        public DateTime ExtractedAt { get; set; }

        /// <summary>
        /// 有效标志（DB 列 IsValid 本就存在，此前实体未声明 → ORM 自动过滤失效）。
        /// <para>企业域版本链（02 号 V-P1）：1=当前版本行，0=归档旧版本（**不物理删**）。</para>
        /// </summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记（★ 2026-09-30 补声明）
        /// <para>⚠️ DB 列本就存在，但本实体此前未声明 ⇒ <c>NodeExecutor</c> 的注释
        /// 「实体无 IsDeleted 列」是错的 ⇒ 软删行可被取数读到（缺陷 B6）。</para>
        /// </summary>
        public bool IsDeleted { get; set; }
    }
}
