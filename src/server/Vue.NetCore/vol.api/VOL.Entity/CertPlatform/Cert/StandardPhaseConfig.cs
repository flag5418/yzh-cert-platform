using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    [Table("cert_standard_phase_config")]
    public class StandardPhaseConfig : YZHBaseEntity
    {
        [Required][StringLength(36)]
        public string StandardCode { get; set; }
        [Required][StringLength(36)]
        public string PhaseCode { get; set; }
        
        public string RequiredClauses { get; set; }
        
        public string RequiredFiles { get; set; }
    }
}
