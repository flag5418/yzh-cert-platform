using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// FileRequirement 文件要求 / 标准文件模板
    /// <para>表名：cert_file_requirement</para>
    /// <para>此表既存储文件要求，也存储标准目录的模板文件信息</para>
    /// <para>模板文件 OSS 路径：/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}</para>
    /// </summary>
    [Table("cert_file_requirement")]
    public class FileRequirement : YZHBaseEntity
    {
        [Required, StringLength(36)]
        [Column("folder_code")]
        public string FolderCode { get; set; }

        [Required, StringLength(200)]
        [Column("file_name_template")]
        public string FileNameTemplate { get; set; }

        [Required, StringLength(50)]
        [Column("file_type")]
        public string FileType { get; set; }

        [Column("is_required")]
        public bool IsRequired { get; set; } = true;

        [Column("max_size_mb")]
        public int MaxSizeMB { get; set; } = 10;

        [Column("description")]
        public string Description { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// 模板文件 OSS 存储路径
        /// 格式：/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
        /// </summary>
        [StringLength(500)]
        [Column("template_storage_path")]
        public string TemplateStoragePath { get; set; }

        /// <summary>
        /// 模板文件原始名（上传时的文件名）
        /// </summary>
        [StringLength(500)]
        [Column("template_file_name")]
        public string TemplateFileName { get; set; }

        /// <summary>标准编码（关联 cert_iso_standard.code）</summary>
        [StringLength(36)]
        [Column("standard_code")]
        public string StandardCode { get; set; }
    }
}
