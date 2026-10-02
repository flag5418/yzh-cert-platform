using System;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Expert
{
    /// <summary>
    /// NC 检查结果 —— <b>多轮</b>（专家任务系统 B 组 · 结果域）
    /// <para>表名：<c>cert_expert_nc_result</c></para>
    ///
    /// <para>唯一键 <c>uk_item_round(ItemCode, RoundNo)</c> ⇒ 同一检查项同一轮次只有一条结果，
    /// 重跑是<b>追加新轮次</b>而不是覆盖旧轮次。</para>
    ///
    /// <para><b>自动结果 vs 人工结论（两组字段刻意分开）</b>：</para>
    /// <list type="bullet">
    ///   <item><c>Auto*</c> 组 = <b>机器产出</b>，机器每次重跑可覆盖；</item>
    ///   <item><c>Conformity / Severity / ContentText / EvidenceRef</c> = <b>专家结论</b>，
    ///         机器<b>永不写</b>。导出时 <see cref="IsModified"/> 决定「结论来源」列显示「自动 / 人工修改」。</item>
    /// </list>
    /// <para>这条分离是「系统是辅助系统、不替代正式报告」在数据层的落点 —— 任何时刻都能回答
    /// 「这条结论是机器给的还是人给的」。</para>
    ///
    /// <para><b>★ <see cref="SkipCategory"/> 与 <see cref="SkipReason"/></b>：
    /// 「跳过」不是失败。原因必须<b>完整落库并完整展示</b>，否则专家会以为系统漏检。</para>
    /// </summary>
    [SugarTable("cert_expert_nc_result")]
    public class CertExpertNcResult : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 提供 ────

        /// <summary>★ 专家工作区编码（租户隔离键；★ 禁止为 NULL）</summary>
        [SugarColumn(Length = 50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>创建人姓名（冗余）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? CreateName { get; set; }

        /// <summary>业务状态（保留）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? Status { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        // ──── 归属 ────

        /// <summary>★所属检查项（<c>cert_expert_nc_item.Code</c>）</summary>
        [SugarColumn(Length = 36)]
        public string ItemCode { get; set; } = string.Empty;

        /// <summary>企业编码（冗余，免 JOIN）</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段编码</summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>标准编码</summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>★业务轮次（与引擎 Attempt 独立编号）</summary>
        public int RoundNo { get; set; } = 1;

        /// <summary>★产生本轮的任务；<b>沿用轮次为空</b></summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? TaskCode { get; set; }

        // ──── 机器产出（Auto* 组） ────

        /// <summary>自动结果：<c>none</c> | <c>ok</c> | <c>ng</c> | <c>skipped</c> | <c>failed</c></summary>
        [SugarColumn(Length = 20)]
        public string AutoStatus { get; set; } = "none";

        /// <summary>★业务侧结论快照（原始 prompt/输出在 <c>wf_node_execution</c>）</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? AutoResult { get; set; }

        /// <summary>自动判定的严重度</summary>
        [SugarColumn(Length = 20, IsNullable = true)]
        public string? AutoSeverity { get; set; }

        /// <summary>自动判定的说明原文</summary>
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
        public string? AutoDescription { get; set; }

        /// <summary>AI 置信度（0.00-1.00）</summary>
        [SugarColumn(DecimalDigits = 2, ColumnDataType = "decimal(3,2)", IsNullable = true)]
        public decimal? AutoConfidence { get; set; }

        /// <summary>★引擎执行任务（<c>wf_execution_task.Code</c>），可下钻溯源</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? ExecutionTaskCode { get; set; }

        /// <summary>自动判定时间</summary>
        public DateTime? AutoEvaluatedAt { get; set; }

        // ──── 跳过 ────

        /// <summary>跳过分类：<c>data_gap</c> | <c>data_gap_skipped</c> | <c>no_rule</c> | <c>rule_disabled</c> | <c>manual_mode</c> | <c>exec_failed</c></summary>
        [SugarColumn(Length = 30, IsNullable = true)]
        public string? SkipCategory { get; set; }

        /// <summary>★跳过原因（界面必须完整展示）</summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? SkipReason { get; set; }

        // ──── 专家结论（机器永不写） ────

        /// <summary>★判定：<c>conform</c> | <c>nonconform</c> | <c>observation</c> | <c>na</c></summary>
        [SugarColumn(Length = 20, IsNullable = true)]
        public string? Conformity { get; set; }

        /// <summary>★严重度：<c>major</c> | <c>minor</c> | <c>observation</c></summary>
        [SugarColumn(Length = 20, IsNullable = true)]
        public string? Severity { get; set; }

        /// <summary>不符合描述</summary>
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
        public string? ContentText { get; set; }

        /// <summary>★客观证据引用</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? EvidenceRef { get; set; }

        // ──── 复核 ────

        /// <summary>★not_started | pending_review | reviewed | modified | skipped | frozen</summary>
        [SugarColumn(Length = 20)]
        public string ReviewStatus { get; set; } = "not_started";

        /// <summary>★是否被专家改过（导出「结论来源」列用）</summary>
        public bool IsModified { get; set; }

        /// <summary>复核人 Code</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? ReviewBy { get; set; }

        /// <summary>复核人姓名（冗余）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? ReviewName { get; set; }

        /// <summary>复核时间</summary>
        public DateTime? ReviewTime { get; set; }

        /// <summary>复核备注</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? ReviewRemark { get; set; }

        // ──── 生成的 NC ────

        /// <summary>★生成的 NC（<c>cert_nc.Code</c>）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? NcCode { get; set; }

        /// <summary>NC 条数</summary>
        public int NcCount { get; set; }

        // ──── ISoftDelete / IIsValid ────

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }

        /// <summary>有效标志（1=有效，0=无效）</summary>
        public int IsValid { get; set; } = 1;
    }
}
