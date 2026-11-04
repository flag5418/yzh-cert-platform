using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 结论（分析 × 条款 · 一行只填一段）
    /// <para>表名：<c>cert_check_result</c></para>
    ///
    /// <para><b>2026-11-01 结构性修订</b>：唯一键从 <c>(CheckItemCode, ClauseCode)</c>
    /// 改为 <c>(AnalysisCode, ClauseCode)</c>。原因：旧键让"一个检查项在一个条款上只能有一行"，
    /// 准备分支与检查分支只能挤同一行（"一行两段"），谁后跑谁覆盖（46 号 F13）。</para>
    ///
    /// <para><b>现在一行 = 某次分析在某条款上的判定</b>，<see cref="AnalysisCode"/> 指向
    /// <see cref="CertCheckAnalysis"/>。同一检查项同一分支重跑 N 次 ⇒ N 组结果行，互不覆盖。</para>
    ///
    /// <para><b>字段归属不变（仍是"两段"），但现在是"一行只填一段"</b>：</para>
    /// <list type="bullet">
    ///   <item><c>prep</c> 分支只写 Prep 组（PrepState / MissingKinds / Conflicts / IsMandatory / VerifyLevel / PrepNcHint）
    ///         ⇒ Conclusion / NcSuggested / Severity <b>必须为 NULL</b></item>
    ///   <item><c>onsite</c> 分支只写 Check 组（Conclusion / NcSuggested / Severity / SeveritySource）
    ///         ⇒ PrepState 等 <b>必须为 NULL</b></item>
    /// </list>
    /// <para>写入断言在 <c>CertCheckEngineService</c> 里强制（46 号 §4.5.4）。</para>
    ///
    /// <para><b>锁定不在这张表</b>：锁是检查项级的，见 <see cref="CertCheckLock"/>
    /// （结果行是"分析 × 条款"，一个检查项 3 条款就有 3 行，锁在这要锁 3 次，且重跑后锁跟着旧行变历史）。</para>
    /// </summary>
    [SugarTable("cert_check_result")]
    public class CertCheckResult : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>所属分析（关联 cert_check_analysis.Code）</summary>
        [StringLength(50)]
        public string AnalysisCode { get; set; } = string.Empty;

        /// <summary>分支冗余：prep / onsite（便于按分支过滤，不用每次 join 分析表）</summary>
        [StringLength(20)]
        public string? StageKind { get; set; }

        /// <summary>检查项 Code（冗余，免 join；关联 cert_audit_plan_detail.Code）</summary>
        [StringLength(50)]
        public string CheckItemCode { get; set; } = string.Empty;

        /// <summary>条款 Code（关联 cert_clause_attr_def.Code 或 ISOClause.Code）</summary>
        [StringLength(50)]
        public string ClauseCode { get; set; } = string.Empty;

        /// <summary>标准 Code（冗余便于展示）</summary>
        [StringLength(50)]
        public string? StandardCode { get; set; }

        /// <summary>条款序号（如 "8.3"；冗余便于展示）</summary>
        [StringLength(50)]
        public string? ClauseNumber { get; set; }

        // ═══ 准备组（prep 方向写）═══

        /// <summary>found / partial / missing / unknown / na</summary>
        [StringLength(20)]
        public string? PrepState { get; set; }

        /// <summary>缺的材料（JSON 数组）</summary>
        public string? MissingKindsJson { get; set; }

        /// <summary>跨文档矛盾（JSON 数组）</summary>
        public string? ConflictsJson { get; set; }

        /// <summary>是否必须执行（检查可覆盖，写 ConclusionBy）</summary>
        public bool? IsMandatory { get; set; }

        /// <summary>doc / remote / onsite / test（验到什么程度）</summary>
        [StringLength(20)]
        public string? VerifyLevel { get; set; }

        /// <summary>准备期"建议开 NC 吗" + 理由（只是给现场的建议）</summary>
        public string? PrepNcHint { get; set; }

        // ═══ 检查组（check 方向写）═══

        /// <summary>pass / ofi / nc / na（**只由审核员写**；AI 只给 <see cref="AiConclusion"/> 建议）</summary>
        [StringLength(20)]
        public string? Conclusion { get; set; }

        /// <summary>
        /// 系统建议结论：pass / ofi / nc / na（AI 产出，⛔ 仅供审核员参考）。
        /// <para>2026-11-03：系统是辅助系统，分析只给建议 —— 最终 <see cref="Conclusion"/>
        /// 必须由审核员判定（<see cref="ConclusionBy"/> 非空即已判定）。</para>
        /// </summary>
        [StringLength(20)]
        public string? AiConclusion { get; set; }

        /// <summary>系统建议不符合程度：major / minor（AI 产出，⛔ 仅供参考）</summary>
        [StringLength(20)]
        public string? AiSeverity { get; set; }

        /// <summary>检查期是否建议开 NC（≠ 已开 NC，见 cert_nc）</summary>
        public bool? NcSuggested { get; set; }

        /// <summary>实际严重程度：major / minor</summary>
        [StringLength(20)]
        public string? Severity { get; set; }

        /// <summary>attr / ai / manual</summary>
        [StringLength(10)]
        public string? SeveritySource { get; set; }

        /// <summary>条款基线严重程度（prep 可校正）</summary>
        [StringLength(20)]
        public string? SeverityBaseline { get; set; }

        /// <summary>判定依据摘要（必填）</summary>
        public string? Reason { get; set; }

        /// <summary>
        /// 疑点分析（JSON 数组 <c>[{point, detail}]</c>，可多条；空数组 = 无疑点）。
        /// <para>AI 产出的疑点只作参考：无疑点时系统建议该检查项合格；
        /// 有疑点也不等于不合格，仍由审核员判定。</para>
        /// </summary>
        public string? DoubtsJson { get; set; }

        /// <summary>判定人 Code</summary>
        [StringLength(64)]
        public string? ConclusionBy { get; set; }

        /// <summary>判定时间</summary>
        public DateTime? ConclusionAt { get; set; }

        // ═══ 锁定保护已迁出（2026-11-01）═══
        // IsLocked / LockedBy / LockedAt / LockReason 四列已从本表移除，迁到 cert_check_lock。
        // 原因：锁是「检查项级」状态，不是「结果行级」—— 一个检查项 3 条款锁 1 次就够，
        //       且重跑产生新分析后锁不能跟着旧行变成历史（46 号 §4.5.9 / F15）。
        // 判断是否锁定 ⇒ CertCheckLockService.IsLockedAsync(checkItemCode, stageKind)

        // ═══ 其他 ═══

        /// <summary>该行是否参与检查项汇总（0=na 不参与）</summary>
        public bool RollupTo { get; set; } = true;

        /// <summary>计划被编辑后置 1（条款/部门变了 → 结论须重跑）</summary>
        public bool ResultStale { get; set; }

        /// <summary>导致 stale 的原因：clause_change / dept_change</summary>
        [StringLength(200)]
        public string? StaleReason { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [MaxLength(500)]
        public string? Remark { get; set; }

        // ──── 接口字段（BaseEntity 不包含，由接口继承提供） ────

        /// <summary>有效标志（1=启用，0=停用）—— 铁律九统一用 IsValid</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }
    }
}
