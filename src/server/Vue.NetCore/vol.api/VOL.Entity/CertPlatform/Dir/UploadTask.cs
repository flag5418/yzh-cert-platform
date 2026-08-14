using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.Dir
{
    /// <summary>
    /// 上传任务追踪实体
    /// 
    /// 职责：追踪批量上传任务的状态，支持回滚和过期清理
    /// </summary>
    [Entity(TableCnName = "上传任务", TableName = "cert_upload_task", DBServer = "VOLContext")]
    [Table("cert_upload_task")]
    public class UploadTask : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>
        /// 任务唯一ID（UUID）
        /// </summary>
        [MaxLength(64)]
        [Column("TaskId")]
        public string TaskId { get; set; }

        /// <summary>
        /// 目标目录编码
        /// </summary>
        [MaxLength(128)]
        [Column("DirectoryCode")]
        public string DirectoryCode { get; set; }

        /// <summary>
        /// 总文件数
        /// </summary>
        [Column("TotalFiles")]
        public int TotalFiles { get; set; } = 0;

        /// <summary>
        /// 总文件大小（字节）
        /// </summary>
        [Column("TotalSize")]
        public long TotalSize { get; set; } = 0;

        /// <summary>
        /// 已成功上传数
        /// </summary>
        [Column("SuccessCount")]
        public int SuccessCount { get; set; } = 0;

        /// <summary>
        /// 任务状态: initialized/uploading/completed/cancelled/expired
        /// </summary>
        [MaxLength(20)]
        [Column("Status")]
        public string Status { get; set; } = "initialized";

        /// <summary>
        /// 创建人
        /// </summary>
        [MaxLength(64)]
        [Column("Creator")]
        public string Creator { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [Column("CreateDate")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        /// <summary>
        /// 修改时间
        /// </summary>
        [Column("ModifyDate")]
        public DateTime? ModifyDate { get; set; }

        /// <summary>
        /// 过期时间（用于自动清理）
        /// </summary>
        [Column("ExpireTime")]
        public DateTime? ExpireTime { get; set; }
    }
}
