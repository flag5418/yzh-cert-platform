using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Ent
{
    /// <summary>
    /// ExtractionResult
    /// <para>表名：ent_extraction_result</para>
    /// </summary>
    [Table("ent_extraction_result")]
    public class ExtractionResult : YZHBaseEntity
    {

    [Required][StringLength(36)]
    public string FileCode { get; set; }
    [Required]
    public int VersionNumber { get; set; }
    [Required][StringLength(36)]
    public string RuleCode { get; set; }
    [Required][StringLength(36)]
    public string FieldCode { get; set; }
    [StringLength(500)]
    public string LabelTag { get; set; }
    
    public string ExtractedValue { get; set; }
    
    public decimal? Confidence { get; set; }
    
    public string PositionInfo { get; set; }
    
    public bool IsManualEdited { get; set; } = false;
    [Required]
    public DateTime ExtractedAt { get; set; }

    }
}
