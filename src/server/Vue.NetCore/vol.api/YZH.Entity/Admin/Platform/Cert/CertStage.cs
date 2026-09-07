using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity;
using YZH.Entity.Admin.Platform;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// 认证阶段（全局基础资料）
    /// <para>表名：cert_cert_stage（用于增删改）</para>
    /// <para>视图名：v_cert_stage（用于查询，CategoryName/StatusName 从视图填充）</para>
    /// </summary>
    [Entity(TableCnName = "认证阶段", TableName = "cert_cert_stage", ViewName = "v_cert_stage", DBServer = "VOLContext")]
    [Table("cert_cert_stage")]
    public class CertStage : EntityBase
    {
        #region snake_case 审计字段覆盖

        [Column("create_by")]
        public new string CreateBy { get; set; }

        [Column("create_date")]
        public new DateTime? CreateDate { get; set; } = DateTime.Now;

        [Column("update_by")]
        public new string UpdateBy { get; set; }

        [Column("modify_date")]
        public new DateTime? ModifyDate { get; set; } = DateTime.Now;

        [Column("delete_by")]
        public new string DeleteBy { get; set; }

        [Column("delete_time")]
        public new DateTime? DeleteTime { get; set; }

        #endregion

        #region 业务字段

        /// <summary>
        /// 阶段编码
        /// </summary>
        [Required]
        [StringLength(50)]
        [Editable(true)]
        [UniqueField("阶段编码")]
        [Column("phase_code")]
        public string StageCode { get; set; }

        /// <summary>
        /// 阶段名称
        /// </summary>
        [Required]
        [StringLength(200)]
        [Editable(true)]
        [Column("phase_name")]
        public string StageName { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 排序号
        /// </summary>
        [Editable(true)]
        [Column("sort_order")]
        public int SortOrder { get; set; }

        /// <summary>
        /// 分类
        /// </summary>
        [StringLength(50)]
        [Editable(true)]
        [Column("category")]
        public string Category { get; set; } = "process";

        #endregion

        #region 视图扩展字段

        /// <summary>
        /// 分类中文名
        /// </summary>
        [NotMapped]
        public string CategoryName { get; set; }

        /// <summary>
        /// 状态中文名
        /// </summary>
        [NotMapped]
        public string StatusName { get; set; }

        #endregion
    }
}
