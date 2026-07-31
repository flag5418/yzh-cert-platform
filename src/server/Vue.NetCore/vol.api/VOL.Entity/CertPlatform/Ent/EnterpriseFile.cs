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

    [Required][StringLength(36)][Column("folder_code")]
    public string FolderCode { get; set; }
    [Required][StringLength(500)][Column("file_name")]
    public string FileName { get; set; }
    [Required][StringLength(50)][Column("file_type")]
    public string FileType { get; set; }
    [Required][Column("file_size")]
    public long FileSize { get; set; }
    [Required][StringLength(500)][Column("storage_path")]
    public string StoragePath { get; set; }
    [StringLength(64)][Column("file_hash")]
    public string FileHash { get; set; }
    [Column("current_version")]
    public int CurrentVersion { get; set; } = 1;
    [Column("notes")]
    public string Notes { get; set; }

    }
}
