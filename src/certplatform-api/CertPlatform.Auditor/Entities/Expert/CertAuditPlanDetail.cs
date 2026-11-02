using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 认证计划明细（专家端 —— 每条组合 = 条款 × 部门）
    /// <para>表名：<c>cert_audit_plan_detail</c></para>
    /// <para>存储口径（用户 2026-10-29 指定，去规范化）：</para>
    /// <list type="bullet">
    ///   <item><c>Clauses</c> 条款（多个逗号隔开，存 ISOClause.Code，GUID）</item>
    ///   <item><c>Departments</c> 部门（多个逗号隔开，存 CertEnterpriseDept.Code，GUID）</item>
    ///   <item><c>DeptNames</c> 部门名称（逗号隔开，冗余存储便于展示）</item>
    ///   <item><c>ClauseNumbers</c> 条款序号（逗号隔开，冗余存储便于展示）</item>
    /// </list>
    /// <para>合并/拆分天然体现：一行可含多条款（合并）；同条款+不同部门拆成多行（拆分）。</para>
    /// </summary>
    [SugarTable("cert_audit_plan_detail")]
    public class CertAuditPlanDetail : BaseEntity, ISoftDelete, IIsValid
    {
        /// <summary>所属计划（关联 cert_audit_plan.Code）</summary>
        [StringLength(50)]
        public string PlanCode { get; set; } = string.Empty;

        /// <summary>条款（多个逗号隔开，存 ISOClause.Code，GUID）</summary>
        [StringLength(1000)]
        public string? Clauses { get; set; }

        /// <summary>部门（多个逗号隔开，存 CertEnterpriseDept.Code，GUID）</summary>
        [StringLength(500)]
        public string? Departments { get; set; }

        /// <summary>部门名称（多个逗号隔开，冗余存储便于展示）</summary>
        [StringLength(500)]
        public string? DeptNames { get; set; }

        /// <summary>条款序号（多个逗号隔开，冗余存储便于展示）</summary>
        [StringLength(1000)]
        public string? ClauseNumbers { get; set; }

        /// <summary>排序号（计划内顺序）</summary>
        public int Sort { get; set; }

        /// <summary>
        /// 检查项负责审核员 Code（关联专家组成员）。
        /// <para>★ 口径（用户 2026-11-01）：<b>只在认证准备阶段可定义</b>；认证检查阶段只读，
        /// 因为审核员分工是入场计划的关键信息，必须由审核组长在准备期定死。
        /// 一个企业审核通常 2–3 人，不同人负责不同部门（A 查 A 部门、B 查 B 部门）。</para>
        /// </summary>
        [StringLength(64)]
        public string? OwnerCode { get; set; }

        /// <summary>检查项负责审核员姓名（冗余存储，便于列表直接展示）</summary>
        [StringLength(64)]
        public string? OwnerName { get; set; }

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
