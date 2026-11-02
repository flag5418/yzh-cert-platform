using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 疑点（V2 — 多条 + Origin 方向 + Fingerprint 幂等）
    /// <para>表名：<c>cert_point</c></para>
    /// <para>一个检查项可关联多条疑点（N:1）。</para>
    /// <para>按方向分 prep/onsite/pq/manual，重跑时仅清 Origin 相同的旧数据，不跨方向覆盖。</para>
    /// </summary>
    [SugarTable("cert_point")]
    public class CertPoint : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>检查项 Code（关联 cert_audit_plan_detail.Code）</summary>
        [StringLength(50)]
        public string CheckItemCode { get; set; } = string.Empty;

        /// <summary>条款 Code（可空）</summary>
        [StringLength(50)]
        public string? ClauseCode { get; set; }

        /// <summary>部门 Code（可空）</summary>
        [StringLength(50)]
        public string? DeptCode { get; set; }

        /// <summary>来源方向：prep / onsite / pq / manual</summary>
        [StringLength(20)]
        public string Origin { get; set; } = "onsite";

        /// <summary>疑点类型：communicate / observe / interview / verify</summary>
        [StringLength(20)]
        public string? PointKind { get; set; }

        /// <summary>remote / onsite（远程验证 or 必须到场）</summary>
        [StringLength(10)]
        public string? Mode { get; set; }

        /// <summary>疑点原因（为什么列这条疑点）</summary>
        public string? Reason { get; set; }

        /// <summary>建议动作（审核员应该做什么）</summary>
        public string? Action { get; set; }

        /// <summary>拟提问内容</summary>
        public string? Question { get; set; }

        /// <summary>目标角色（如"质量负责人"）</summary>
        [StringLength(100)]
        public string? TargetRole { get; set; }

        /// <summary>预计耗时（分钟）</summary>
        public int? EstMinutes { get; set; }

        /// <summary>优先级（越大越优先）</summary>
        public int Priority { get; set; }

        /// <summary>重跑幂等键（同指纹不重复插入）</summary>
        [StringLength(64)]
        public string? Fingerprint { get; set; }

        // ═══ 状态 ═══

        /// <summary>open / resolved / dropped + 理由 + 留痕</summary>
        [StringLength(20)]
        public string Status { get; set; } = "open";

        /// <summary>销帐原因</summary>
        public string? ResolveReason { get; set; }

        /// <summary>销帐人</summary>
        [StringLength(64)]
        public string? ResolvedBy { get; set; }

        /// <summary>销帐时间</summary>
        public DateTime? ResolvedAt { get; set; }

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
