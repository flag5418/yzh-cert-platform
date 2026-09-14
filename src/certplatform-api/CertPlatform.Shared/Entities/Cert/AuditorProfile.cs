using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// 审核员资质档案
    /// <para>表名：cert_auditor_profile</para>
    /// <para>ORM：SqlSugar（§16 铁律：DB列名 == C#属性名，PascalCase）</para>
    /// </summary>
    [SugarTable("cert_auditor_profile")]
    public class AuditorProfile : BaseEntity
    {
        /// <summary>机构编码（所属认证机构，多租户隔离）</summary>
        [Required, StringLength(50)]
        public string OrgCode { get; set; }

        /// <summary>关联 Sys_User 的用户编码</summary>
        [Required, StringLength(50)]
        [UniqueField("用户ID")]
        public string UserCode { get; set; }

        /// <summary>审核员资格证号</summary>
        [Required, StringLength(50)]
        [UniqueField("审核员资格证号")]
        public string AuditorNo { get; set; }

        /// <summary>审核员姓名</summary>
        [Required, StringLength(100)]
        public string AuditorName { get; set; }

        /// <summary>手机号</summary>
        [Required, StringLength(20)]
        public string Phone { get; set; }

        /// <summary>邮箱</summary>
        [StringLength(200)]
        public string Email { get; set; }

        /// <summary>审核资质(标准类型+级别) JSON</summary>
        public string Qualification { get; set; }

        /// <summary>专业领域(行业分类) JSON</summary>
        public string ExpertiseAreas { get; set; }

        /// <summary>状态（active/inactive）</summary>
        [StringLength(20)]
        public string Status { get; set; } = "active";
    }
}
