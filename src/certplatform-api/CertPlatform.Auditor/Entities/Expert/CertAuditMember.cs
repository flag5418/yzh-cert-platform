using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 审核人员（专家端 —— 审核人员管理 · 单表结构）
    /// <para>表名：cert_audit_member</para>
    /// <para>用途：维护认证审核人员基本信息（姓名/职称/专业/资质/联系方式）。
    /// 入场计划指派审核员时从本池子挑选，不关联登录账号，不绑定角色。</para>
    /// <para>工作区隔离：不同工作区各自维护自己的审核人员库</para>
    /// <para>ORM：SqlSugar（铁律七：DB列名 == C#属性名，PascalCase 逐字一致）</para>
    /// </summary>
    [SugarTable("cert_audit_member")]
    public class CertAuditMember : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 提供 ────

        /// <summary>所属工作区/虚拟机构编码（专家平台的租户隔离键）</summary>
        [StringLength(50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>姓名</summary>
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>性别：0=未知 1=男 2=女</summary>
        public int Gender { get; set; }

        /// <summary>职称（高级工程师/审核员/高级审核员等）</summary>
        [StringLength(100)]
        public string? Title { get; set; }

        /// <summary>专业领域</summary>
        [StringLength(100)]
        public string? Profession { get; set; }

        /// <summary>从业年限</summary>
        public int? WorkYears { get; set; }

        /// <summary>联系电话</summary>
        [StringLength(50)]
        public string? Phone { get; set; }

        /// <summary>邮箱</summary>
        [StringLength(100)]
        public string? Email { get; set; }

        /// <summary>资质证书编号（工作区内唯一）</summary>
        [StringLength(100)]
        public string? QualificationNo { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        public string? Remark { get; set; }

        // ──── ISoftDelete / IIsValid 接口字段 ────

        /// <summary>有效标志（1=有效，0=无效）——铁律九：唯一启用字段</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记（false=正常，true=已删除）</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }
    }
}
