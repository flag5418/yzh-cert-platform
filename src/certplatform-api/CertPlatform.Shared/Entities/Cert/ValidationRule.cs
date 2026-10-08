using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// NC 检查规则（审核规则）
    /// <para>表名：cert_validation_rule</para>
    /// <para>ORM：SqlSugar（列名 == 属性名，PascalCase）</para>
    /// <para><b>★ 与孪生表 <see cref="CertPlatform.Shared.Entities.Rpt.ReportSection"/> 对称</b>：
    /// 同为三元组定位的配置层实体，同样实现 <see cref="ISoftDelete"/>
    /// （2026-10-08 修正：原仅继承 BaseEntity，导致①删除回落到
    /// <c>UpdateAsync(entity, ["IsDeleted"])</c> 找不到属性而失败 ②软删行不过滤）。</para>
    /// <para><b>★ 启停字段 = <see cref="IsValid"/></b>（铁律九）：原 <c>IsActive</c> 与其余 8 张配置表
    /// 判据不统一，已统一为 <c>IsValid</c>（1=有效 / 0=无效）。</para>
    /// </summary>
    [SugarTable("cert_validation_rule")]
    public class ValidationRule : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段已由 BaseEntity 基类统一提供 ────
        // ──── DB cert_validation_rule.Code 为 PascalCase，无需 new 覆盖 ────

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

        // ★ 已删除 WorkflowCode 僵尸列（2026-10-08）：
        //   DAG 已由本表 RuleJson 承载；该列 FK 指向 0 行的 wf_workflow_definition（死表），
        //   且前端从未有任何采集入口。DB 侧同步 DROP FOREIGN KEY fk_valrule_workflow + DROP COLUMN。

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

        /// <summary>
        /// 判定方式（auto=AI 自动判定 / manual=人工判定 / semi=半自动）
        /// <para>存在意义：部分检查项是**人为调研**发现的（如「某个该有的设备是否存在」），
        /// AI 不可能知道 → 必须由人工判定。术语是「判定方式」，不是「复核」。</para>
        /// </summary>
        [StringLength(20)]
        public string JudgeMode { get; set; } = "auto";

        /// <summary>
        /// 工作流 DAG JSON。
        /// <para><b>NULL = 未配置工作流</b>（唯一判据，见 <c>ValidationRuleRules.HasWorkflow</c>）。</para>
        /// <para>⛔ 禁止用 <c>"{}"</c> / <c>"null"</c> / <c>"[]"</c> 字符串表达"未配置" —— 控制器
        /// <c>NormalizeDag</c> 会在落库前把三者归一为 NULL。</para>
        /// </summary>
        public string? RuleJson { get; set; }

        /// <summary>画布布局 JSON（节点坐标/缩放/平移）</summary>
        public string? LayoutJson { get; set; }

        /// <summary>NC 描述模板</summary>
        public string? NcDescriptionTemplate { get; set; }

        /// <summary>有效标志（1=有效，0=无效）—— 铁律九唯一启用字段（原 IsActive 已废）</summary>
        public int IsValid { get; set; } = 1;

        // ──── ISoftDelete 接口显式实现 ────

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }

        // ──── 视图字段（OnQueried 填充，不入库） ────

        /// <summary>条款编号（JOIN cert_iso_clause 填充）</summary>
        [SugarColumn(IsIgnore = true)]
        public string? ClauseNumber { get; set; }

        /// <summary>条款标题（JOIN cert_iso_clause 填充）</summary>
        [SugarColumn(IsIgnore = true)]
        public string? ClauseTitle { get; set; }
    }
}
