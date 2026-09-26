using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// 企业（体系认证基础资料 · 单表结构）
    /// <para>表名：cert_enterprise</para>
    /// <para>归属：专家平台「企业管理」——工作区（虚拟机构）下的企业档案</para>
    /// <para>唯一性：同一企业可存在于不同虚拟机构；约束只作用于单个工作区
    /// → uk_org_ent_no / uk_org_ent_name / uk_org_ent_credit 均以 (OrgCode, …) 为键</para>
    /// <para>ORM：SqlSugar（铁律七：DB列名 == C#属性名，PascalCase 逐字一致）</para>
    /// </summary>
    [SugarTable("cert_enterprise")]
    public class Enterprise : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段（CreateTime/CreateBy/UpdateTime/UpdateBy）由 BaseEntity 提供 ────

        /// <summary>
        ///     所属机构编码（工作区/虚拟机构隔离键，多租户）
        ///     ⛔ 不加 [Required]：本字段由服务端（EnterpriseController.OnBeforeAdd）按当前工作区填充，
        ///        客户端不传；加了 [Required] 会被 [ApiController] 的自动模型校验拦成 400，
        ///        OnBeforeAdd 根本没机会执行。
        /// </summary>
        [StringLength(50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>
        ///     企业编号（工作区内唯一，如 ENT-0001，用于 OSS 路径）
        ///     ⛔ 不加 [Required]：留空即由服务端自动生成（NextEnterpriseNoAsync）。
        /// </summary>
        [StringLength(20)]
        [UniqueField("企业编号")]
        public string EnterpriseNo { get; set; } = string.Empty;

        /// <summary>
        ///     企业全称（工作区内唯一）
        ///     ⛔ 不加 [Required]：本实体是**表单 POST 目标**，[ApiController] 的自动模型校验会在
        ///        动作执行前短路并返回 ProblemDetails（非 ApiResponse 信封）。
        ///        必填由两处保证：EntityConfig 的 BcFlag/Yxk + EnterpriseController.OnBeforeAdd 显式校验。
        /// </summary>
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>企业简称</summary>
        [StringLength(100)]
        public string? ShortName { get; set; }

        /// <summary>统一社会信用代码（工作区内唯一）</summary>
        [StringLength(50)]
        [UniqueField("统一社会信用代码")]
        public string? CreditCode { get; set; }

        /// <summary>法人代表</summary>
        [StringLength(100)]
        public string? LegalPerson { get; set; }

        /// <summary>省份</summary>
        [StringLength(50)]
        public string? Province { get; set; }

        /// <summary>城市</summary>
        [StringLength(50)]
        public string? City { get; set; }

        /// <summary>企业地址</summary>
        [StringLength(500)]
        public string? Address { get; set; }

        /// <summary>行业类型</summary>
        [StringLength(100)]
        public string? IndustryType { get; set; }

        /// <summary>员工人数</summary>
        public int? EmployeeCount { get; set; }

        /// <summary>认证范围描述</summary>
        public string? CertScope { get; set; }

        /// <summary>对接人姓名</summary>
        [StringLength(100)]
        public string? ContactName { get; set; }

        /// <summary>对接人电话</summary>
        [StringLength(20)]
        public string? ContactPhone { get; set; }

        /// <summary>对接人邮箱</summary>
        [StringLength(200)]
        public string? ContactEmail { get; set; }

        /// <summary>归档日期</summary>
        public DateTime? ArchiveDate { get; set; }

        /// <summary>备注</summary>
        public string? Remark { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        // ──── 接口字段（BaseEntity 不包含，由接口继承提供） ────

        /// <summary>有效标志（1=有效，0=无效）——铁律九：启禁唯一契约</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记（false=正常，true=已删除）</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（仅软删除时赋值）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（仅软删除时赋值）</summary>
        public DateTime? DeleteTime { get; set; }
    }
}
