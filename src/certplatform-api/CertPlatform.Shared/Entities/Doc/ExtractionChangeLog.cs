using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Doc
{
    /// <summary>
    /// 提取值补录日志（<c>cert_extraction_change_log</c>）—— ★ <b>不可变表</b>
    /// </summary>
    ///
    /// <para><b>铁律</b>：⛔ 只允许 <c>INSERT</c> / <c>SELECT</c>，代码中禁止 <c>UPDATE</c> / <c>DELETE</c>。
    /// 需要「作废某条日志」时追加一条 <see cref="ActionRevoke"/> 反向记录，不改原记录（05 号 §八）。</para>
    ///
    /// <para><b>记什么</b>：人工改了提取结果的值。<b>与 <c>cert_expert_task_log</c> 是两类，不可合并</b> ——
    /// 本表对象是<b>数据源层</b>（字段值 / 表格值）、生命周期<b>跨任务</b>（值改了所有任务都受益）；
    /// <c>cert_expert_task_log</c> 对象是<b>结果层</b>（NC 结论 / 报告正文）、生命周期<b>任务内</b>
    /// （任务批准后冻结）。</para>
    ///
    /// <para><b>裁决</b>：2026-09-30 用户裁决 J2「补录直接写提取结果表 + <c>ValueSource</c> 区分来源」；
    /// J4「1 文件 = 1 规则」⇒ <see cref="RuleCode"/> 是取数与补录的主键成分，本表同步冗余以便按规则查时间线。</para>
    /// </summary>
    [SugarTable("cert_extraction_change_log")]
    public class ExtractionChangeLog : BaseEntity
    {
        // ──── Id / Code / 审计字段由 BaseEntity 统一提供 ────

        /// <summary>★ 专家工作区编码（租户隔离键 · 操作人所属）</summary>
        [Required, StringLength(50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>结果类型：<c>field</c> | <c>table</c></summary>
        [Required, StringLength(20)]
        public string ResultType { get; set; } = string.Empty;

        /// <summary>提取结果行编码（<c>cert_extraction_result.Code</c> 或 <c>cert_table_extraction_result.Code</c>）</summary>
        [Required, StringLength(36)]
        public string ResultCode { get; set; } = string.Empty;

        /// <summary>企业编码（⚠️ 提取结果表的 <c>OrgCode</c> 列存的也是这个值 —— 历史遗留列名）</summary>
        [Required, StringLength(36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>★ 规则编码（裁决 J4：1 文件 = 1 规则）</summary>
        [StringLength(36)]
        public string? RuleCode { get; set; }

        [StringLength(36)]
        public string? StandardCode { get; set; }

        /// <summary>阶段编码（GUID，关联 <c>cert_cert_stage.Code</c>）</summary>
        [StringLength(36)]
        public string? StageCode { get; set; }

        /// <summary>标准文件行 Code（只读提示；裁决 J1「不分文档」⇒ 不作分组键）</summary>
        [StringLength(200)]
        public string? StandardFileCode { get; set; }

        /// <summary>提取的源文件编码（★ 补录行填的是规则的 <c>StandardFileCode</c>，非文件槽位 Code）</summary>
        [StringLength(36)]
        public string? FileCode { get; set; }

        [StringLength(200)]
        public string? FieldCode { get; set; }

        [StringLength(200)]
        public string? TableCode { get; set; }

        /// <summary>展示名快照（专家改名后仍能还原当时看到什么）</summary>
        [StringLength(200)]
        public string? FieldLabel { get; set; }

        /// <summary>变更动作：<see cref="ActionManualEdit"/> | <see cref="ActionArchive"/> |
        /// <see cref="ActionRestoreAuto"/> | <see cref="ActionRevoke"/></summary>
        [Required, StringLength(20)]
        public string ChangeAction { get; set; } = string.Empty;

        /// <summary>旧值</summary>
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
        public string? OldValue { get; set; }

        /// <summary>新值</summary>
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
        public string? NewValue { get; set; }

        /// <summary>旧来源：<c>auto</c> | <c>manual</c> | <c>null</c>（原先不存在该行）</summary>
        [StringLength(20)]
        public string? OldValueSource { get; set; }

        /// <summary>新来源：<c>manual</c> | <c>auto</c></summary>
        [StringLength(20)]
        public string? NewValueSource { get; set; }

        /// <summary>哪个任务触发的补录（手动改提取结果时为空）</summary>
        [StringLength(36)]
        public string? TaskCode { get; set; }

        /// <summary>对应的缺口编码（<c>cert_expert_task_data_gap.Code</c>）</summary>
        [StringLength(36)]
        public string? GapCode { get; set; }

        [StringLength(50)]
        public string? OperatorCode { get; set; }

        /// <summary>操作人姓名（冗余，防用户改名后追溯断裂）</summary>
        [StringLength(100)]
        public string? OperatorName { get; set; }

        public DateTime? OperateTime { get; set; }

        [StringLength(500)]
        public string? Remark { get; set; }

        /// <summary>有效标志（1=有效）。★ 表本身不可变，此列恒为 1，不用于软删</summary>
        public int IsValid { get; set; } = 1;

        // ──── 动作常量（⛔ 禁止各处写字面量） ────

        /// <summary>人工补录</summary>
        public const string ActionManualEdit = "manual_edit";

        /// <summary>★ 新提取 / 按 RuleCode 归档，覆盖了人工值（裁决 J4 的连带后果，05 号 §5.3）</summary>
        public const string ActionArchive = "archive";

        /// <summary>恢复为自动提取值</summary>
        public const string ActionRestoreAuto = "restore_auto";

        /// <summary>作废前一条（⛔ 不改原记录，追加反向记录）</summary>
        public const string ActionRevoke = "revoke";
    }
}
