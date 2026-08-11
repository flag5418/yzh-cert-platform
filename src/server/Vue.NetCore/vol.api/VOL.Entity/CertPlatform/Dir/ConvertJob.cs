/*
 * 文件转换任务模型
 * 用于后台服务处理 Office 文档转换
 */
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.Dir
{
    /// <summary>
    /// 文件转换任务
    /// </summary>
    [Entity(TableCnName = "文件转换任务", TableName = "cert_file_convert_job", DBServer = "VOLContext")]
    [Table("cert_file_convert_job")]
    public class ConvertJob : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }
        
        /// <summary>
        /// 关联的文件编码（业务关联，非数据库ID）
        /// </summary>
        [MaxLength(200)]
        [Column("file_code")]
        public string FileCode { get; set; }
        
        /// <summary>
        /// 原始文件存储路径
        /// </summary>
        [MaxLength(500)]
        [Column("source_path")]
        public string SourcePath { get; set; }
        
        /// <summary>
        /// 转换后文件存储路径
        /// </summary>
        [MaxLength(500)]
        [Column("target_path")]
        public string TargetPath { get; set; }
        
        /// <summary>
        /// 转换类型：xls2xlsx, doc2docx
        /// </summary>
        [MaxLength(20)]
        [Column("convert_type")]
        public string ConvertType { get; set; }
        
        /// <summary>
        /// 任务状态：pending, processing, completed, failed
        /// </summary>
        [MaxLength(20)]
        [Column("status")]
        public string Status { get; set; } = "pending";
        
        /// <summary>
        /// 错误信息
        /// </summary>
        [MaxLength(2000)]
        [Column("error_message")]
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// 重试次数
        /// </summary>
        [Column("retry_count")]
        public int RetryCount { get; set; } = 0;
        
        /// <summary>
        /// 最大重试次数
        /// </summary>
        [Column("max_retry_count")]
        public int MaxRetryCount { get; set; } = 3;
        
        /// <summary>
        /// 创建时间
        /// </summary>
        [Column("create_time")]
        public DateTime CreateTime { get; set; } = DateTime.Now;
        
        /// <summary>
        /// 开始处理时间
        /// </summary>
        [Column("process_time")]
        public DateTime? ProcessTime { get; set; }
        
        /// <summary>
        /// 完成时间
        /// </summary>
        [Column("complete_time")]
        public DateTime? CompleteTime { get; set; }
    }
}
