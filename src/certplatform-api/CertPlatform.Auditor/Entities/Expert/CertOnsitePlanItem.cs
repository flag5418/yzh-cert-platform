using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 入场检查内容（认证准备阶段定义「查什么 / 怎么查」，可多条）
    /// <para>表名：<c>cert_onsite_plan_item</c></para>
    /// <para>一条 = 一个检查点。<see cref="CheckItemCode"/> = 计划明细 Code（cert_audit_plan_detail.Code）。</para>
    /// <para>★ 设计来源：docs/70-V2设计/48-认证准备与认证检查-业务设计-V3.md §9</para>
    /// </summary>
    [SugarTable("cert_onsite_plan_item")]
    public class CertOnsitePlanItem : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>所属计划（cert_audit_plan.Code）</summary>
        [StringLength(50)]
        public string PlanCode { get; set; } = string.Empty;

        /// <summary>所属检查项（cert_audit_plan_detail.Code）</summary>
        [StringLength(50)]
        public string CheckItemCode { get; set; } = string.Empty;

        /// <summary>检查项内序号</summary>
        public int SeqNo { get; set; }

        /// <summary>检查内容（查什么）</summary>
        [StringLength(500)]
        public string CheckWhat { get; set; } = string.Empty;

        /// <summary>检查方式（文件查阅 / 现场观察 / 人员访谈 / 抽样验证 / 记录核对）</summary>
        [StringLength(50)]
        public string? CheckMethod { get; set; }

        /// <summary>受检部门 / 岗位</summary>
        [StringLength(200)]
        public string? TargetDept { get; set; }

        /// <summary>受检联系人 Code</summary>
        [StringLength(50)]
        public string? ContactCode { get; set; }

        /// <summary>受检联系人姓名（冗余，便于展示）</summary>
        [StringLength(100)]
        public string? ContactName { get; set; }

        /// <summary>执行审核员 Code</summary>
        [StringLength(64)]
        public string? AuditorCode { get; set; }

        /// <summary>执行审核员姓名（冗余）</summary>
        [StringLength(100)]
        public string? AuditorName { get; set; }

        /// <summary>抽样要求</summary>
        [StringLength(200)]
        public string? SampleRequirement { get; set; }

        /// <summary>期望发现的合规证据</summary>
        [StringLength(500)]
        public string? ExpectedFinding { get; set; }

        /// <summary>对应条款 Code（逗号分隔）</summary>
        [StringLength(500)]
        public string? ClauseCodes { get; set; }

        /// <summary>来源：manual 人工 / ai 系统生成</summary>
        [StringLength(20)]
        public string Source { get; set; } = "manual";

        /// <summary>状态：planned / done / skipped</summary>
        [StringLength(20)]
        public string Status { get; set; } = "planned";

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [MaxLength(500)]
        public string? Remark { get; set; }

        // ──── 接口字段（BaseEntity 不包含，由接口继承提供） ────

        /// <summary>有效标志（1=启用，0=停用）</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }
    }
}
