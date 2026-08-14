using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// EnterpriseFile 企业文件
    /// <para>表名：ent_enterprise_file</para>
    /// <para>列名规范：snake_case</para>
    /// </summary>
    [Table("ent_enterprise_file")]
    public class EnterpriseFile : YZHBaseEntity
    {
        /// <summary>关联企业 code</summary>
        [Required, StringLength(36)]
        [Column("enterprise_code")]
        public string EnterpriseCode { get; set; }

        /// <summary>关联企业文档目录 code</summary>
        [Required, StringLength(36)]
        [Column("folder_code")]
        public string FolderCode { get; set; }

        /// <summary>文件名(中文原样)</summary>
        [Required, StringLength(500)]
        [Column("file_name")]
        public string FileName { get; set; }

        /// <summary>文件类型(pdf/docx/xlsx等)</summary>
        [Required, StringLength(50)]
        [Column("file_type")]
        public string FileType { get; set; }

        /// <summary>文件大小(bytes)</summary>
        [Required]
        [Column("file_size")]
        public long FileSize { get; set; }

        /// <summary>MinIO存储路径</summary>
        [Required, StringLength(500)]
        [Column("storage_path")]
        public string StoragePath { get; set; }

        /// <summary>转换后文件路径(.docx/.xlsx)</summary>
        [StringLength(500)]
        [Column("converted_storage_path")]
        public string ConvertedStoragePath { get; set; }

        /// <summary>转换状态：null/pending/converting/converted/failed</summary>
        [StringLength(20)]
        [Column("convert_status")]
        public string ConvertStatus { get; set; }

        /// <summary>转换失败原因</summary>
        [StringLength(1024)]
        [Column("convert_message")]
        public string ConvertMessage { get; set; }

        /// <summary>转换完成时间</summary>
        [Column("convert_date")]
        public DateTime? ConvertDate { get; set; }

        /// <summary>SHA256哈希(增量审核依据)</summary>
        [StringLength(64)]
        [Column("file_hash")]
        public string FileHash { get; set; }

        /// <summary>当前版本号</summary>
        [Column("current_version")]
        public int CurrentVersion { get; set; } = 1;

        /// <summary>上传状态：pending/uploading/active/failed</summary>
        [StringLength(20)]
        [Column("upload_status")]
        public string UploadStatus { get; set; } = "active";

        /// <summary>标准文件编码（关联 cert_file_requirement.code，标记企业文件对应的标准文件模板）</summary>
        [StringLength(36)]
        [Column("standard_file_code")]
        public string StandardFileCode { get; set; }

        // 注：Id, Code, OrgCode, CreateID, Creator, CreateDate, ModifyID, Modifier, ModifyDate,
        // DeleteID, Deleter, DeleteTime, Status, Enable, Remark 继承自 YZHBaseEntity
    }
}
