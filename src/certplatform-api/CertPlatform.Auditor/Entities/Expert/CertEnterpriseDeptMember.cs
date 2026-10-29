using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 企业部门人员（专家端 —— 企业机构管理 · 右表）
    /// <para>表名：cert_enterprise_dept_member</para>
    /// <para>用途：选中左侧部门后维护该部门的人员（沟通对象／访谈对象／陪同人员等）。
    /// 入场计划派沟通任务、定访谈对象时引用。</para>
    /// <para>归属：DeptCode → cert_enterprise_dept.Code；EnterpriseCode 冗余（工作区隔离走本工作区企业集合）</para>
    /// <para>ORM：SqlSugar（铁律七：DB列名 == C#属性名，PascalCase 逐字一致）</para>
    /// </summary>
    [SugarTable("cert_enterprise_dept_member")]
    public class CertEnterpriseDeptMember : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 提供 ────

        /// <summary>所属企业编码（冗余，关联 cert_enterprise.Code）</summary>
        [StringLength(50)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>所属部门编码（关联 cert_enterprise_dept.Code）</summary>
        [StringLength(50)]
        public string DeptCode { get; set; } = string.Empty;

        /// <summary>姓名</summary>
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>职务</summary>
        [StringLength(100)]
        public string? Position { get; set; }

        /// <summary>角色（沟通对象／访谈对象／陪同人员等）</summary>
        [StringLength(100)]
        public string? RoleType { get; set; }

        /// <summary>联系电话</summary>
        [StringLength(50)]
        public string? Phone { get; set; }

        /// <summary>邮箱</summary>
        [StringLength(100)]
        public string? Email { get; set; }

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
