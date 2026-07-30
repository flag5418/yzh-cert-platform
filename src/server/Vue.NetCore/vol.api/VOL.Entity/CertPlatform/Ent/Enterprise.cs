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
    public class Enterprise : BaseEntity
    {

    [Required][StringLength(200)][Column("name")]
    public string Name { get; set; }
    [StringLength(100)][Column("short_name")]
    public string ShortName { get; set; }
    [StringLength(50)][Column("credit_code")]
    public string CreditCode { get; set; }
    [StringLength(50)][Column("legal_person")]
    public string LegalPerson { get; set; }
    [Column("address")]
    public string Address { get; set; }
    [Column("cert_scope")]
    public string CertScope { get; set; }
    [StringLength(50)][Column("contact_name")]
    public string ContactName { get; set; }
    [StringLength(20)][Column("contact_phone")]
    public string ContactPhone { get; set; }
    [StringLength(200)][Column("contact_email")]
    public string ContactEmail { get; set; }
    [Column("status")]
    public string Status { get; set; } = "active";
    [Column("archive_date")]
    public DateTime? ArchiveDate { get; set; }
    [Column("notes")]
    public string Notes { get; set; }

    }
}
