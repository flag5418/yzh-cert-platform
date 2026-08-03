using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// Enterprise
    /// <para>表名：ent_enterprise</para>
    /// </summary>
    [Table("ent_enterprise")]
    public class Enterprise : YZHBaseEntity
    {

    [Required][StringLength(200)]
    public string Name { get; set; }
    [StringLength(100)]
    public string ShortName { get; set; }
    [StringLength(50)]
    public string CreditCode { get; set; }
    [StringLength(50)]
    public string LegalPerson { get; set; }
    
    public string Address { get; set; }
    
    public string CertScope { get; set; }
    [StringLength(50)]
    public string ContactName { get; set; }
    [StringLength(20)]
    public string ContactPhone { get; set; }
    [StringLength(200)]
    public string ContactEmail { get; set; }
    
    public DateTime? ArchiveDate { get; set; }

    }
}
