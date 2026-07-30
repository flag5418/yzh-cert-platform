using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// EnterprisePhase
    /// <para>表名：ent_enterprise_phase</para>
    /// </summary>
    [Table("ent_enterprise_phase")]
    public class EnterprisePhase : BaseEntity
    {

    [Required][StringLength(36)][Column("enterprise_code")]
    public string EnterpriseCode { get; set; }
    [Required][StringLength(36)][Column("phase_code")]
    public string PhaseCode { get; set; }
    [Required][StringLength(36)][Column("standard_code")]
    public string StandardCode { get; set; }
    [Column("status")]
    public string Status { get; set; } = "pending";
    [Column("started_at")]
    public DateTime? StartedAt { get; set; }
    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    }
}
