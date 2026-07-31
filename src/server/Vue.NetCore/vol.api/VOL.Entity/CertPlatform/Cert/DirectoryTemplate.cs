using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// DirectoryTemplate
    /// <para>表名：cert_directory_template</para>
    /// </summary>
    [Table("cert_directory_template")]
    public class DirectoryTemplate : YZHBaseEntity
    {

    [Required][StringLength(36)][Column("config_code")]
    public string ConfigCode { get; set; }
    [StringLength(36)][Column("parent_code")]
    public string ParentCode { get; set; }
    [Required][StringLength(200)][Column("folder_name")]
    public string FolderName { get; set; }
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    }
}
