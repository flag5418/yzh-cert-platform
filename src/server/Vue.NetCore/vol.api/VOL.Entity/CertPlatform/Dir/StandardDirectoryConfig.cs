using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.Dir
{
    /// <summary>
    /// 标准目录配置实体
    /// 
    /// 职责：定义标准目录结构（机构无关）
    /// 编码规则：SDC-{StandardCode}|{PhaseCode}
    /// 示例：SDC-ISO9001|PH01
    /// </summary>
    [Entity(TableCnName = "标准目录配置", TableName = "cert_standard_directory_config", DBServer = "VOLContext")]
    [Table("cert_standard_directory_config")]
    public class StandardDirectoryConfig : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        #region 编码字段

        /// <summary>
        /// 全局唯一编码（GUID）
        /// </summary>
        [MaxLength(36)]
        [Column("Code")]
        public string Code { get; set; }

        /// <summary>
        /// 目录编码（SDC-{标准}|{阶段}）
        /// </summary>
        [MaxLength(100)]
        [Column("DirectoryCode")]
        public string DirectoryCode { get; set; }

        #endregion

        #region 关联字段

        /// <summary>
        /// 标准编码
        /// </summary>
        [MaxLength(50)]
        [Column("StandardCode")]
        public string StandardCode { get; set; }

        /// <summary>
        /// 阶段编码
        /// </summary>
        [MaxLength(50)]
        [Column("PhaseCode")]
        public string PhaseCode { get; set; }

        #endregion

        #region 目录配置

        /// <summary>
        /// 根文件夹名称
        /// </summary>
        [MaxLength(200)]
        [Column("RootFolderName")]
        public string RootFolderName { get; set; }

        #endregion

        #region 状态字段

        /// <summary>
        /// 状态（draft/active/archived）
        /// </summary>
        [MaxLength(20)]
        [Column("Status")]
        public string Status { get; set; } = "draft";

        /// <summary>
        /// 是否启用
        /// </summary>
        [Column("Enable")]
        public bool Enable { get; set; } = true;

        #endregion

        #region 审计字段

        /// <summary>
        /// 创建人ID
        /// </summary>
        [Column("CreateID")]
        public int? CreateID { get; set; }

        /// <summary>
        /// 创建人姓名
        /// </summary>
        [MaxLength(50)]
        [Column("Creator")]
        public string Creator { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [Column("CreateDate")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        /// <summary>
        /// 修改人ID
        /// </summary>
        [Column("ModifyID")]
        public int? ModifyID { get; set; }

        /// <summary>
        /// 修改人姓名
        /// </summary>
        [MaxLength(50)]
        [Column("Modifier")]
        public string Modifier { get; set; }

        /// <summary>
        /// 修改时间
        /// </summary>
        [Column("ModifyDate")]
        public DateTime? ModifyDate { get; set; }

        /// <summary>
        /// 删除人ID
        /// </summary>
        [Column("DeleteID")]
        public int? DeleteID { get; set; }

        /// <summary>
        /// 删除人姓名
        /// </summary>
        [MaxLength(50)]
        [Column("Deleter")]
        public string Deleter { get; set; }

        /// <summary>
        /// 删除时间
        /// </summary>
        [Column("DeleteTime")]
        public DateTime? DeleteTime { get; set; }

        /// <summary>
        /// 业务状态
        /// </summary>
        [MaxLength(50)]
        [Column("Status_field")]
        public string Status_field { get; set; } = "active";

        /// <summary>
        /// 启用状态
        /// </summary>
        [Column("Enable_field")]
        public bool Enable_field { get; set; } = true;

        /// <summary>
        /// 排序
        /// </summary>
        [Column("Sort")]
        public int Sort { get; set; } = 0;

        /// <summary>
        /// 备注
        /// </summary>
        [Column("Remark")]
        public string Remark { get; set; }

        #endregion
    }
}
