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
    public class EnterprisePhase : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string EnterpriseCode { get; set; }
    [Required][StringLength(36)]
    public string PhaseCode { get; set; }
    [Required][StringLength(36)]
    public string StandardCode { get; set; }
    
    public DateTime? StartedAt { get; set; }
    
    public DateTime? CompletedAt { get; set; }

    }
}
