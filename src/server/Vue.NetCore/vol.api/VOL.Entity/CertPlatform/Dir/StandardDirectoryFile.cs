using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.Dir
{
    /// <summary>
    /// 标准目录文件实体
    /// 
    /// 职责：定义标准目录中每个文件夹要求的文件规格
    /// 编码规则：FL-{FolderCode}|{FileName}|{Type}
    /// 示例：FL-FD-SDC-ISO9001|PH01|L01|S001|营业执照|pdf
    /// </summary>
    [Entity(TableCnName = "标准目录文件", TableName = "cert_standard_directory_file", DBServer = "VOLContext")]
    [Table("cert_standard_directory_file")]
    public class StandardDirectoryFile : BaseEntity
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
        /// 文件编码（FL-{FolderCode}|{FileName}|{Type}）
        /// </summary>
        [MaxLength(150)]
        [Column("FileCode")]
        public string FileCode { get; set; }

        #endregion

        #region 关联字段

        /// <summary>
        /// 所属文件夹编码
        /// </summary>
        [MaxLength(150)]
        [Column("FolderCode")]
        public string FolderCode { get; set; }

        /// <summary>
        /// 目录编码
        /// </summary>
        [MaxLength(100)]
        [Column("DirectoryCode")]
        public string DirectoryCode { get; set; }

        #endregion

        #region 文件信息

        /// <summary>
        /// 文件名称模板
        /// </summary>
        [MaxLength(500)]
        [Column("FileName")]
        public string FileName { get; set; }

        /// <summary>
        /// 文件类型（pdf/docx/xlsx/png等）
        /// </summary>
        [MaxLength(50)]
        [Column("FileType")]
        public string FileType { get; set; }

        /// <summary>
        /// 文件名正则匹配规则
        /// </summary>
        [MaxLength(200)]
        [Column("FilePattern")]
        public string FilePattern { get; set; }

        /// <summary>
        /// 文件大小（字节），上传成功后由后端从 IFormFile.Length 记录（权威值，不依赖前端）
        /// </summary>
        [Column("file_size")]
        public long? FileSize { get; set; }

        #endregion

        #region 文件要求

        /// <summary>
        /// 是否必须提供
        /// </summary>
        [Column("IsRequired")]
        public bool IsRequired { get; set; } = true;

        /// <summary>
        /// 最大文件大小（MB）
        /// </summary>
        [Column("MaxFileSizeMB")]
        public int MaxFileSizeMB { get; set; } = 10;

        /// <summary>
        /// 文件说明/要求描述
        /// </summary>
        [Column("Description")]
        public string Description { get; set; }

        /// <summary>
        /// 排序
        /// </summary>
        [Column("SortOrder")]
        public int SortOrder { get; set; } = 0;

        #endregion

        #region 提取规则

        /// <summary>
        /// 是否启用自动提取
        /// </summary>
        [Column("ExtractionEnabled")]
        public bool ExtractionEnabled { get; set; } = false;

        /// <summary>
        /// 提取规则配置（JSON）
        /// </summary>
        [Column("ExtractionRules")]
        public string ExtractionRules { get; set; }

        #endregion

        #region 校验规则

        /// <summary>
        /// 是否要求预审
        /// </summary>
        [Column("PreCheckRequired")]
        public bool PreCheckRequired { get; set; } = true;

        /// <summary>
        /// 是否要求合规检查
        /// </summary>
        [Column("ComplianceRequired")]
        public bool ComplianceRequired { get; set; } = false;

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

        /// <summary>
        /// 上传任务ID
        /// </summary>
        [MaxLength(64)]
        [Column("TaskId")]
        public string TaskId { get; set; }

        /// <summary>
        /// 有效标志: 0=无效(预创建), 1=有效(已确认)
        /// </summary>
        [Column("IsValid")]
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// 上传状态: pending/uploading/active/failed
        /// </summary>
        [MaxLength(20)]
        [Column("UploadStatus")]
        public string UploadStatus { get; set; } = "active";

        /// <summary>
        /// MinIO存储路径
        /// </summary>
        [MaxLength(512)]
        [Column("StoragePath")]
        public string StoragePath { get; set; }

        /// <summary>
        /// 完整路径（从根到当前文件），用于路径判定
        /// 示例：4记录文件/内审记录/陪审人员.doc
        /// </summary>
        [MaxLength(1024)]
        [Column("FullPath")]
        public string FullPath { get; set; }

        #endregion

        #region 文件转换（旧版 Office → OOXML）

        /// <summary>
        /// 转换后文件在 MinIO 的存储路径（.docx/.xlsx）
        /// </summary>
        [MaxLength(512)]
        [Column("converted_storage_path")]
        public string ConvertedStoragePath { get; set; }

        /// <summary>
        /// 转换状态：null/pending/converting/converted/failed
        /// </summary>
        [MaxLength(20)]
        [Column("convert_status")]
        public string ConvertStatus { get; set; }

        /// <summary>
        /// 转换失败原因或丢失的样式信息
        /// </summary>
        [MaxLength(1024)]
        [Column("convert_message")]
        public string ConvertMessage { get; set; }

        /// <summary>
        /// 转换完成时间
        /// </summary>
        [Column("convert_date")]
        public DateTime? ConvertDate { get; set; }

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
