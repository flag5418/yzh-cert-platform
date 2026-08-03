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

    [Required][StringLength(36)]
    public string ConfigCode { get; set; }
    [StringLength(36)]
    public string ParentCode { get; set; }
    [Required][StringLength(200)]
    public string FolderName { get; set; }
    
    public int SortOrder { get; set; } = 0;

    }
}
