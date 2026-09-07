using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity;
using YZH.Entity.Admin.Platform;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// ISO 标准
    /// <para>表名：cert_iso_standard（用于增删改）</para>
    /// <para>视图名：v_iso_standard（用于查询，CategoryName 从视图填充）</para>
    /// <para>域：A - 认证体系配置</para>
    /// </summary>
    [Entity(TableCnName = "ISO标准管理", TableName = "cert_iso_standard", ViewName = "v_iso_standard", DBServer = "VOLContext")]
    [Table("cert_iso_standard")]
    public class ISOStandard : EntityBase
    {
        #region snake_case 列名覆盖（cert_iso_standard 表使用 snake_case）

        /// <summary>
        /// 业务编码（覆盖基类 PascalCase → snake_case）
        /// </summary>
        [MaxLength(100)]
        [Column("code")]
        public new string Code { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 创建人 ID（覆盖基类 PascalCase → snake_case）
        /// </summary>
        [Column("create_by")]
        public new string CreateBy { get; set; }

        /// <summary>
        /// 创建时间（覆盖基类 PascalCase → snake_case）
        /// </summary>
        [Column("create_date")]
        public new DateTime? CreateDate { get; set; } = DateTime.Now;

        /// <summary>
        /// 修改人 ID（覆盖基类 PascalCase → snake_case）
        /// </summary>
        [Column("update_by")]
        public new string UpdateBy { get; set; }

        /// <summary>
        /// 修改时间（覆盖基类 PascalCase → snake_case）
        /// </summary>
        [Column("modify_date")]
        public new DateTime? ModifyDate { get; set; } = DateTime.Now;

        /// <summary>
        /// 删除人 ID（覆盖基类 PascalCase → snake_case）
        /// </summary>
        [Column("delete_by")]
        public new string DeleteBy { get; set; }

        /// <summary>
        /// 删除时间（覆盖基类 PascalCase → snake_case）
        /// </summary>
        [Column("delete_time")]
        public new DateTime? DeleteTime { get; set; }

        /// <summary>
        /// 备注（覆盖基类 PascalCase → snake_case）
        /// </summary>
        [MaxLength(500)]
        [Column("remark")]
        public new string Remark { get; set; }

        #endregion

        #region 业务字段

        /// <summary>
        /// 标准编号（如 ISO 9001:2015, ISO 13485:2016）
        /// </summary>
        [Required]
        [StringLength(50)]
        [Editable(true)]
        [UniqueField("标准编号", WithFields = new[] { "VersionYear" })]
        [Column("standard_code")]
        public string StandardCode { get; set; }

        /// <summary>
        /// 标准中文名称
        /// </summary>
        [Required]
        [StringLength(200)]
        [Editable(true)]
        [UniqueField("标准名称")]
        [Column("standard_name")]
        public string StandardName { get; set; }

        /// <summary>
        /// 版本年份
        /// </summary>
        [Editable(true)]
        [Column("version_year")]
        public int VersionYear { get; set; }

        /// <summary>
        /// 类别（quality/environment/medical 等）
        /// </summary>
        [StringLength(50)]
        [Editable(true)]
        [Column("category")]
        public string Category { get; set; } = "quality";

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        #endregion

        #region 视图扩展字段（仅查询填充，不参与 SaveChanges）

        /// <summary>
        /// 分类中文名（质量管理/环境管理/医疗器械等）
        /// 来源：v_iso_standard 视图的 LEFT JOIN
        /// </summary>
        [NotMapped]
        public string CategoryName { get; set; }

        #endregion
    }
}
