using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    [Table("cert_standard_phase_config")]
    public class StandardPhaseConfig : YZHBaseEntity
    {
        [Required][StringLength(36)][Column("standard_code")]
        public string StandardCode { get; set; }
        [Required][StringLength(36)][Column("phase_code")]
        public string PhaseCode { get; set; }
        [Column("required_clauses")]
        public string RequiredClauses { get; set; }
        [Column("required_files")]
        public string RequiredFiles { get; set; }
        [Column("notes")]
        public string Notes { get; set; }
    }
}
