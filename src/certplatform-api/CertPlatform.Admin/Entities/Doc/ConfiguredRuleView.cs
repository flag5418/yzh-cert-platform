using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Doc
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

        /// <summary>
        /// 文件完整路径（<c>cert_standard_directory_file.FullPath</c>，如「质量/一级/质量手册.doc」）。
        /// <para>★ 2026-10-15 新增：NC 规则设计器文档下拉改「文件夹 → 文件」树形选择
        /// （buildFolderFileTree）需要文件夹路径；消费侧由本列去 FileName 后缀得 folderPath。</para>
        /// <para>文件行缺失（LEFT JOIN 未命中）时为空串 —— 下拉中该文件归根级，不报错。</para>
        /// </summary>
        [SugarColumn(ColumnName = "FullPath")]
        public string FullPath { get; set; } = "";

        /// <summary>标准编码</summary>
        [SugarColumn(ColumnName = "StandardCode")]
        public string StandardCode { get; set; } = "";

        /// <summary>阶段编码（决策 ⑩ 统一为 StageCode）</summary>
        [SugarColumn(ColumnName = "StageCode")]
        public string StageCode { get; set; } = "";

        /// <summary>技能类型</summary>
        [SugarColumn(ColumnName = "Skill")]
        public string Skill { get; set; } = "";

        /// <summary>是否有效</summary>
        [SugarColumn(ColumnName = "DocIsValid")]
        public bool DocIsValid { get; set; }

        /// <summary>状态</summary>
        [SugarColumn(ColumnName = "Status")]
        public string Status { get; set; } = "";

        /// <summary>
        /// 规则启用状态（来自 cert_doc_extraction_rule.IsValid）
        /// <para>★ 2026-10-13 按 26-数据库视图设计规范-V1 §三 改造：视图不再把 IsValid 固化进 WHERE，
        /// 改为以本列暴露，由消费侧（DocExtractionRuleService.GetConfiguredRulesAsync）显式判 == 1。</para>
        /// <para>⚠️ 列名刻意不叫 IsValid：本类继承 BaseEntity，用 IsValid 会被基类语义/ORM 判据混淆。</para>
        /// </summary>
        [SugarColumn(ColumnName = "RuleIsValid")]
        public int RuleIsValid { get; set; } = 1;

        // 视图只读，禁用增删改相关字段
        [SugarColumn(IsIgnore = true)]
        public bool IsDeleted { get; set; }

        [SugarColumn(IsIgnore = true)]
        public int IsValid { get; set; } = 1;
    }
}
