using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Expert
{
    /// <summary>
    /// 企业部门（专家端 —— 企业部门管理 · 树形结构）
    /// <para>表名：cert_enterprise_dept</para>
    /// <para>用途：按企业建立部门树（不限层级），每部门带负责人信息。
    /// 入场计划选择部门、定访谈对象时引用。暂不负责过程(Process)。</para>
    /// <para>归属：按 EnterpriseCode 关联 cert_enterprise</para>
    /// <para>ORM：SqlSugar（铁律七：DB列名 == C#属性名，PascalCase 逐字一致）</para>
    /// </summary>
    [SugarTable("cert_enterprise_dept")]
    public class CertEnterpriseDept : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 提供 ────

        /// <summary>所属企业编码（关联 cert_enterprise.Code）</summary>
        [StringLength(50)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>部门名称</summary>
        [StringLength(200)]
        public string DeptName { get; set; } = string.Empty;

        /// <summary>上级部门编码（NULL = 顶层部门，不限层级）</summary>
        [StringLength(50)]
        public string? ParentCode { get; set; }

        /// <summary>负责人姓名</summary>
        [StringLength(50)]
        public string? LeaderName { get; set; }

        /// <summary>负责人联系电话</summary>
        [StringLength(50)]
        public string? LeaderPhone { get; set; }

        /// <summary>负责人职务</summary>
        [StringLength(100)]
        public string? LeaderTitle { get; set; }

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
