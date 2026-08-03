using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// EnterpriseFile
    /// <para>表名：ent_enterprise_file</para>
    /// </summary>
    [Table("ent_enterprise_file")]
    public class EnterpriseFile : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string FolderCode { get; set; }
    [Required][StringLength(500)]
    public string FileName { get; set; }
    [Required][StringLength(50)]
    public string FileType { get; set; }
    [Required]
    public long FileSize { get; set; }
    [Required][StringLength(500)]
    public string StoragePath { get; set; }
    [StringLength(64)]
    public string FileHash { get; set; }
    
    public int CurrentVersion { get; set; } = 1;
    
    public string Notes { get; set; }

    }
}
