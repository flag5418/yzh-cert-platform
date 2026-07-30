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
    public class FileRequirement : BaseEntity
    {

    [Required][StringLength(36)][Column("folder_code")]
    public string FolderCode { get; set; }
    [Required][StringLength(200)][Column("file_name_template")]
    public string FileNameTemplate { get; set; }
    [Required][StringLength(50)][Column("file_type")]
    public string FileType { get; set; }
    [Column("is_required")]
    public bool IsRequired { get; set; } = true;
    [Column("max_size_mb")]
    public int MaxSizeMB { get; set; } = 10;
    [Column("description")]
    public string Description { get; set; }
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    }
}
