using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 检查项锁（检查项级 · 与分支正交）
    /// <para>表名：<c>cert_check_lock</c></para>
    ///
    /// <para><b>用户口径</b>：「一旦一个检查项被锁定了，即使选择了这个检查项，
    /// 但我们队列执行的时候，也要忽略该检查项」。</para>
    ///
    /// <para><b>三件事被这句话定死</b>（46 号 §10.1）：</para>
    /// <list type="number">
    ///   <item>锁在<b>检查项</b>上，不在结果行上 —— 一个检查项 3 条款只锁 1 次，不是锁 3 行；</item>
    ///   <item>锁是<b>硬跳过</b>，不是"提示" —— 勾选了也跑不了；</item>
    ///   <item>锁的唯一出口是<b>解锁</b> —— 没有任何 RunMode 能绕过锁（包括 full）。</item>
    /// </list>
    ///
    /// <para><b>为什么不在 cert_check_result 上锁</b>：结果行是"分析 × 条款"，一个检查项 3 条款就有 3 行，
    /// 锁要锁 3 次；而且重跑产生新分析后，锁就跟着旧行一起变成历史了 —— 锁必须独立于分析次数（F15）。</para>
    /// </summary>
    [SugarTable("cert_check_lock")]
    public class CertCheckLock : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>检查项 Code（= cert_audit_plan_detail.Code）</summary>
        [StringLength(50)]
        public string CheckItemCode { get; set; } = string.Empty;

        /// <summary>
        /// 锁的粒度：<c>all</c>=整项锁死（默认，最符合"一个检查项被锁定了"的口径）/
        /// <c>prep</c>=只锁准备分支 / <c>onsite</c>=只锁检查分支。
        /// <para>分分支锁是<b>能力</b>不是默认 —— 给"我确认了准备结论，但现场还没去"这种真实场景留口子。</para>
        /// </summary>
        [StringLength(20)]
        public string StageKind { get; set; } = "all";

        /// <summary>锁定人 Code</summary>
        [StringLength(64)]
        public string? LockedBy { get; set; }

        /// <summary>锁定时间</summary>
        public DateTime? LockedAt { get; set; }

        /// <summary>锁定原因：manual_confirm / auditor_fixed / other</summary>
        [StringLength(200)]
        public string? LockReason { get; set; }

        /// <summary>来源：manual=人工锁 / auto_nc=开 NC 后自动锁</summary>
        [StringLength(20)]
        public string LockSource { get; set; } = "manual";

        /// <summary>1=锁定生效；0=已解锁（留痕不删行）</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>解锁人 Code</summary>
        [StringLength(64)]
        public string? UnlockedBy { get; set; }

        /// <summary>解锁时间</summary>
        public DateTime? UnlockedAt { get; set; }

        /// <summary>备注</summary>
        [MaxLength(500)]
        public string? Remark { get; set; }

        // ──── 接口字段 ────

        /// <summary>有效标志</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }
    }
}
