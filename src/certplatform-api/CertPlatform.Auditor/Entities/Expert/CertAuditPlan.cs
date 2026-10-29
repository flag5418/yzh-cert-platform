using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 认证计划主表（专家端 —— 按条款为核心的体系认证入场计划）
    /// <para>表名：<c>cert_audit_plan</c></para>
    /// <para>存储口径（用户 2026-10-29 指定，去规范化）：企业 / 阶段 / 标准（多个逗号隔开，存标准 Code）。</para>
    /// <para>★ 关联键一律用 Code（GUID，铁律九/双关键字准则）：
    /// <c>EnterpriseCode</c>=cert_enterprise.Code、<c>StageCode</c>=cert_cert_stage.Code、
    /// <c>Standards</c>=ISOStandard.Code（逗号分隔）。</para>
    /// <para>明细见 <see cref="CertAuditPlanDetail"/>（每条组合 = 条款×部门）。</para>
    /// </summary>
    [SugarTable("cert_audit_plan")]
    public class CertAuditPlan : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>计划名称（默认=企业·阶段·认证计划，可改）</summary>
        [StringLength(200)]
        public string? Name { get; set; }

        /// <summary>企业编码（关联 cert_enterprise.Code）</summary>
        [StringLength(50)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>阶段编码（关联 cert_cert_stage.Code，GUID）</summary>
        [StringLength(50)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>认证标准（多个逗号隔开，存 ISOStandard.Code，GUID）</summary>
        [StringLength(500)]
        public string? Standards { get; set; }

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
