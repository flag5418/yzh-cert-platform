using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// FileRequirement
    /// <para>表名：cert_file_requirement</para>
    /// </summary>
    [Table("cert_file_requirement")]
    public class FileRequirement : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string FolderCode { get; set; }
    [Required][StringLength(200)]
    public string FileNameTemplate { get; set; }
    [Required][StringLength(50)]
    public string FileType { get; set; }
    
    public bool IsRequired { get; set; } = true;
    
    public int MaxSizeMB { get; set; } = 10;
    
    public string Description { get; set; }
    
    public int SortOrder { get; set; } = 0;

    }
}
