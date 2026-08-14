using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// FilePreCheckResult 文件预检结果
    /// <para>表名：ent_file_pre_check_result</para>
    /// <para>注意：此表仅有 create_date 审计字段，不继承完整审计字段</para>
    /// </summary>
    [Table("ent_file_pre_check_result")]
    public class FilePreCheckResult : YZHBaseEntity
    {
        [Required, StringLength(36)]
        [Column("file_code")]
        public string FileCode { get; set; }

        [Required]
        [Column("version_number")]
        public int VersionNumber { get; set; }

        [Required, StringLength(20)]
        [Column("check_type")]
        public string CheckType { get; set; }

        [Required, StringLength(20)]
        [Column("check_result")]
        public string CheckResult { get; set; }

        [Column("message")]
        public string Message { get; set; }

        [Column("detail")]
        public string Detail { get; set; }

        [Required]
        [Column("checked_at")]
        public DateTime CheckedAt { get; set; }
    }
}
