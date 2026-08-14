using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// ClauseExtractionRule 条款提取规则
    /// <para>表名：cert_clause_extraction_rule</para>
    /// </summary>
    [Table("cert_clause_extraction_rule")]
    public class ClauseExtractionRule : YZHBaseEntity
    {
        [Required, StringLength(36)]
        [Column("clause_code")]
        public string ClauseCode { get; set; }

        [Required, StringLength(36)]
        [Column("workflow_code")]
        public string WorkflowCode { get; set; }

        [Column("description")]
        public string Description { get; set; }
    }
}
