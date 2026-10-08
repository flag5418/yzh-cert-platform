using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Rpt
{
    /// <summary>
    /// ★ 体系认证报告章节定义（配置层 · 扁平结构）
    /// <para>表名：cert_report_section（★2026-09-29 由 rpt_report_section 改名，N1 铁律）</para>
    /// <para><b>★ 去主表化（D34）</b>：原「cert_report_template（主表）→ rpt_report_section（章节）」两层
    /// 已扁平化。本表自带 <c>OrgCode</c>（认证机构）+ <c>StandardCode</c> + <c>PhaseCode</c> 三元组，
    /// <b>与 <see cref="Cert.Entities.Cert.ValidationRule"/> 完全对称</b>（同为配置层、同为三元组定位）。</para>
    /// <para><b>★ 相比旧版消除的 3 个问题</b>：
    ///   ① 旧表无 StandardCode/PhaseCode，每次查询都要 JOIN 主表 → 现在直接读
    ///   ② 旧 <c>ReportCode</c> 语义错位（DDL 声明 FK→rpt_audit_report，实际存 Template.Code）→ 列已删除
    ///   ③ 旧表用 <c>IsActive</c> 违反铁律九 → 现统一 <see cref="IIsValid.IsValid"/></para>
    /// <para>命名规范（YZH 铁律七）：DB 列名 = C# 属性名 = TS 字段名，PascalCase 逐字一致</para>
    /// </summary>
    [SugarTable("cert_report_section")]
    public class ReportSection : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 统一提供 ────

        /// <summary>
        /// 认证机构编码（★配置层归属，不是专家工作区 OrgCode）
        /// <para>⛔ 与 <c>cert_expert_task.OrgCode</c>（专家工作区）语义不同，详见 14 号 N8</para>
        /// </summary>
        [SugarColumn(Length = 50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>标准编码（cert_iso_standard.Code）</summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>
        /// 阶段编码（★<c>cert_cert_stage.Code</c>，GUID，<b>不是</b> StageCode 业务码）
        /// </summary>
        [SugarColumn(Length = 36)]
        public string PhaseCode { get; set; } = string.Empty;

        [SugarColumn(Length = 200)]
        public string SectionName { get; set; } = string.Empty;

        [SugarColumn(Length = 200, IsNullable = true)]
        public string? SectionNameEn { get; set; }

        /// <summary>章节内容（模板示例正文，纯文本）</summary>
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true, ColumnName = "SectionContent")]
        public string? Content { get; set; }

        /// <summary>章节排序（★唯一键组成：OrgCode + StandardCode + PhaseCode + SortOrder）</summary>
        public int SortOrder { get; set; }

        /// <summary>★ 有效标志（1=有效，0=无效）—— 铁律九唯一启用字段</summary>
        public int IsValid { get; set; } = 1;

        [SugarColumn(Length = 36, IsNullable = true)]
        public string? WorkflowCode { get; set; }

        /// <summary>★ 工作流 DAG（★注意：规则的 DAG 在 <c>RuleJson</c>，本表是 <c>WorkflowConfig</c>，字段名不同）</summary>
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
        public string? WorkflowConfig { get; set; }

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? LayoutJson { get; set; }

        /// <summary>
        /// 判定方式（auto=AI 自动判定 / manual=人工判定 / semi=半自动）
        /// <para>业务背景：部分章节必须由人工判断（如现场作业一致性、员工访谈），
        /// AI 无法从企业上传资料自动分析 → 执行引擎据此跳过不可自动化的章节。</para>
        /// <para>★ 与 <see cref="Cert.Entities.Cert.ValidationRule.JudgeMode"/> 完全对齐。</para>
        /// </summary>
        [SugarColumn(Length = 20)]
        public string JudgeMode { get; set; } = "auto";

        /// <summary>对应条款编码（★可空：概述/结论类章节不映射条款）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? ClauseCode { get; set; }

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? SectionJson { get; set; }

        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        [SugarColumn(Length = 50, IsNullable = true, ColumnName = "Status")]
        public string? Status { get; set; }

        public int Sort { get; set; }

        // ──── ISoftDelete 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
    }
}
