using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// AuditorProfile 审核员资质档案
    /// <para>表名：cert_auditor_profile</para>
    /// </summary>
    [Table("cert_auditor_profile")]
    public class AuditorProfile : YZHBaseEntity
    {
        /// <summary>关联 Sys_User.User_Id</summary>
        [Required]
        [Column("user_id")]
        public long UserId { get; set; }

        /// <summary>审核员资格证号</summary>
        [Required, StringLength(50)]
        [Column("auditor_no")]
        public string AuditorNo { get; set; }

        /// <summary>审核员姓名</summary>
        [Required, StringLength(100)]
        [Column("auditor_name")]
        public string AuditorName { get; set; }

        /// <summary>手机号</summary>
        [Required, StringLength(20)]
        [Column("phone")]
        public string Phone { get; set; }

        /// <summary>邮箱</summary>
        [StringLength(200)]
        [Column("email")]
        public string Email { get; set; }

        /// <summary>审核资质(标准类型+级别) JSON</summary>
        [Column("qualification")]
        public string Qualification { get; set; }

        /// <summary>专业领域(行业分类) JSON</summary>
        [Column("expertise_areas")]
        public string ExpertiseAreas { get; set; }

        // Status, OrgCode, Code, CreateID, Creator, CreateDate, ModifyID, Modifier, ModifyDate,
        // DeleteID, Deleter, DeleteTime, Enable, Remark 继承自 YZHBaseEntity
    }
}
