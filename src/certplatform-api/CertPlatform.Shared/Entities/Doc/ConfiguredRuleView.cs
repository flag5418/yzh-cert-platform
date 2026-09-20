using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Doc
{
    /// <summary>
    /// 已配置规则列表视图实体
    /// <para>映射视图：v_cert_configured_rules</para>
    /// <para>用途：查询已配置的文档提取规则列表（含文件名关联）</para>
    /// </summary>
    [SugarTable("v_cert_configured_rules")]
    public class ConfiguredRuleView : BaseEntity
    {
        // 视图无自增主键和基础审计字段，标记为忽略
        [SugarColumn(IsIgnore = true)]
        public new long Id { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new string? Code { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new DateTime CreateTime { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new string? CreateBy { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new DateTime? UpdateTime { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new string? UpdateBy { get; set; }

        /// <summary>规则编码（来自 cert_doc_extraction_rule.Code）</summary>
        [SugarColumn(ColumnName = "RuleCode")]
        public string RuleCode { get; set; } = "";

        /// <summary>标准文件编码</summary>
        [SugarColumn(ColumnName = "StandardFileCode")]
        public string StandardFileCode { get; set; } = "";

        /// <summary>文件名称（优先取文件名，否则取规则编码）</summary>
        [SugarColumn(ColumnName = "FileName")]
        public string FileName { get; set; } = "";

        /// <summary>标准编码</summary>
        [SugarColumn(ColumnName = "StandardCode")]
        public string StandardCode { get; set; } = "";

        /// <summary>阶段编码</summary>
        [SugarColumn(ColumnName = "PhaseCode")]
        public string PhaseCode { get; set; } = "";

        /// <summary>技能类型</summary>
        [SugarColumn(ColumnName = "Skill")]
        public string Skill { get; set; } = "";

        /// <summary>是否有效</summary>
        [SugarColumn(ColumnName = "DocIsValid")]
        public bool DocIsValid { get; set; }

        /// <summary>状态</summary>
        [SugarColumn(ColumnName = "Status")]
        public string Status { get; set; } = "";

        // 视图只读，禁用增删改相关字段
        [SugarColumn(IsIgnore = true)]
        public bool IsDeleted { get; set; }

        [SugarColumn(IsIgnore = true)]
        public int IsValid { get; set; } = 1;
    }
}
