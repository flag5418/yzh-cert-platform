using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// EnterpriseDocument
    /// <para>表名：ent_enterprise_document</para>
    /// </summary>
    [Table("ent_enterprise_document")]
    public class EnterpriseDocument : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string EnterpriseCode { get; set; }
    [StringLength(36)]
    public string PhaseCode { get; set; }
    [Required]
    public string Scope { get; set; }
    [StringLength(36)]
    public string TemplateFolderCode { get; set; }
    [StringLength(36)]
    public string ParentCode { get; set; }
    [Required][StringLength(200)]
    public string FolderName { get; set; }
    
    public int SortOrder { get; set; } = 0;

    }
}
