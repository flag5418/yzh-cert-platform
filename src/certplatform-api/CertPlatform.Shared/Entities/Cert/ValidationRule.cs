using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// NC 检查规则（审核规则）
    /// <para>表名：cert_validation_rule</para>
    /// <para>ORM：SqlSugar（列名 == 属性名，PascalCase）</para>
    /// </summary>
    [SugarTable("cert_validation_rule")]
    public class ValidationRule : BaseEntity
    {
        // ──── 覆盖基类审计字段 ────

        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public new long Id { get; set; }

        /// <summary>
        /// 业务唯一码（覆盖基类，设默认值避免 INSERT 时报错）
        /// </summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public new string? Code { get; set; } = Guid.NewGuid().ToString("N");

        public int Sort { get; set; }

        [MaxLength(500)]
        public string? Remark { get; set; }

        // ──── 业务字段 ────

        /// <summary>认证机构编码</summary>
        [StringLength(50)]
        public string? OrgCode { get; set; }

        /// <summary>标准编码（关联 cert_iso_standard.Code）</summary>
        [Required]
        [StringLength(36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>阶段编码（关联 cert_cert_stage.Code）</summary>
        [Required]
        [StringLength(36)]
        public string PhaseCode { get; set; } = string.Empty;

        /// <summary>条款编码（关联 cert_iso_clause.Code）</summary>
        [Required]
        [StringLength(36)]
        public string ClauseCode { get; set; } = string.Empty;

        /// <summary>工作流编码（关联 wf_workflow_definition.Code，可空）</summary>
        [StringLength(36)]
        public string? WorkflowCode { get; set; }

        /// <summary>规则唯一编号（如 NC-ISO9001-001，后端自动生成）</summary>
        [StringLength(50)]
        [UniqueField("规则编码")]
        public string RuleCode { get; set; } = string.Empty;

        /// <summary>规则中文名称</summary>
        [Required]
        [StringLength(200)]
        public string RuleName { get; set; } = string.Empty;

        /// <summary>规则英文名称</summary>
        [StringLength(200)]
        public string? RuleNameEn { get; set; }

        /// <summary>违规严重级别（major/minor/observation）</summary>
        [StringLength(20)]
        public string? SeverityIfViolated { get; set; }

        /// <summary>工作流定义 JSON</summary>
        public string? RuleJson { get; set; }

        /// <summary>画布布局 JSON（节点坐标/缩放/平移）</summary>
        public string? LayoutJson { get; set; }

        /// <summary>NC 描述模板</summary>
        public string? NcDescriptionTemplate { get; set; }

        /// <summary>是否启用</summary>
        public bool IsActive { get; set; } = true;

        // ──── 视图字段（OnQueried 填充，不入库） ────

        /// <summary>条款编号（JOIN cert_iso_clause 填充）</summary>
        [SugarColumn(IsIgnore = true)]
        public string? ClauseNumber { get; set; }

        /// <summary>条款标题（JOIN cert_iso_clause 填充）</summary>
        [SugarColumn(IsIgnore = true)]
        public string? ClauseTitle { get; set; }
    }
}
