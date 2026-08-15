using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// FileVersion 文件版本
    /// <para>表名：ent_file_version</para>
    /// <para>注意：此表仅有 create_date 审计字段，不继承完整审计字段</para>
    /// </summary>
    [Table("ent_file_version")]
    public class FileVersion : YZHBaseEntity
    {
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        [Column("org_code")]
        public string OrgCode { get; set; }

        [Required, StringLength(36)]
        [Column("file_code")]
        public string FileCode { get; set; }

        [Required]
        [Column("version_number")]
        public int VersionNumber { get; set; }

        [Required]
        [Column("file_size")]
        public long FileSize { get; set; }

        [Required, StringLength(500)]
        [Column("storage_path")]
        public string StoragePath { get; set; }

        [Required, StringLength(64)]
        [Column("file_hash")]
        public string FileHash { get; set; }

        [Column("change_notes")]
        public string ChangeNotes { get; set; }

        [Required]
        [Column("upload_by")]
        public int UploadBy { get; set; }
    }
}
