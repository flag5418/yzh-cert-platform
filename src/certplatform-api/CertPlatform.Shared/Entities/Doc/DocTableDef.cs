using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Doc
{
    /// <summary>
    /// DocTableDef 文档表格定义
    /// <para>表名：cert_doc_table_def（唯一键 uk_rule_table: RuleCode + TableCode）</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("cert_doc_table_def")]
    public class DocTableDef : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        // ──── 业务字段 ────
        /// <summary>规则编码（关联 cert_doc_extraction_rule.Code）</summary>
        [SugarColumn(Length = 100)]
        public string RuleCode { get; set; } = "";

        [SugarColumn(Length = 100)]
        public string TableName { get; set; } = "";

        /// <summary>表格编码（用于工作流引用，英文驼峰）</summary>
        [SugarColumn(Length = 100)]
        public string TableCode { get; set; } = "";

        /// <summary>表格描述（AI 提取依据）</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Description { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        /// <summary>状态</summary>
        public string Status { get; set; } = "active";

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
