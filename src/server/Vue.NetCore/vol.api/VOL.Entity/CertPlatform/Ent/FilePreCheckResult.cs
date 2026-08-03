using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// FilePreCheckResult
    /// <para>表名：ent_file_pre_check_result</para>
    /// </summary>
    [Table("ent_file_pre_check_result")]
    public class FilePreCheckResult : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string FileCode { get; set; }
    [Required]
    public int VersionNumber { get; set; }
    [Required]
    public string CheckType { get; set; }
    [Required]
    public string CheckResult { get; set; }
    
    public string Message { get; set; }
    
    public string Detail { get; set; }
    [Required]
    public DateTime CheckedAt { get; set; }

    }
}
