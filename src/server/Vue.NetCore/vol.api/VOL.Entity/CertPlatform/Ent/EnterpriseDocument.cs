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

    [Required][StringLength(36)][Column("enterprise_code")]
    public string EnterpriseCode { get; set; }
    [StringLength(36)][Column("phase_code")]
    public string PhaseCode { get; set; }
    [Required][Column("scope")]
    public string Scope { get; set; }
    [StringLength(36)][Column("template_folder_code")]
    public string TemplateFolderCode { get; set; }
    [StringLength(36)][Column("parent_code")]
    public string ParentCode { get; set; }
    [Required][StringLength(200)][Column("folder_name")]
    public string FolderName { get; set; }
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    }
}
