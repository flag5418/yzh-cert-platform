using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// FileVersion
    /// <para>表名：ent_file_version</para>
    /// </summary>
    [Table("ent_file_version")]
    public class FileVersion : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string FileCode { get; set; }
    [Required]
    public int VersionNumber { get; set; }
    [Required]
    public long FileSize { get; set; }
    [Required][StringLength(500)]
    public string StoragePath { get; set; }
    [Required][StringLength(64)]
    public string FileHash { get; set; }
    
    public string ChangeNotes { get; set; }

    }
}
