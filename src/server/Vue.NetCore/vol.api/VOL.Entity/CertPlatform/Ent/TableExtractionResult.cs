using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// TableExtractionResult
    /// <para>表名：ent_table_extraction_result</para>
    /// </summary>
    [Table("ent_table_extraction_result")]
    public class TableExtractionResult : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string FileCode { get; set; }
    [Required]
    public int VersionNumber { get; set; }
    [Required][StringLength(36)]
    public string RuleCode { get; set; }
    
    public int TableIndex { get; set; } = 1;
    [Required]
    public string ExtractedJson { get; set; }
    
    public decimal? Confidence { get; set; }
    
    public string PositionInfo { get; set; }
    [Required]
    public DateTime ExtractedAt { get; set; }

    }
}
