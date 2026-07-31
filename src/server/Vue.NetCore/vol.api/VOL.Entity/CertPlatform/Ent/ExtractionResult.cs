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

    [Required][StringLength(36)][Column("file_code")]
    public string FileCode { get; set; }
    [Required][Column("version_number")]
    public int VersionNumber { get; set; }
    [Required][StringLength(36)][Column("rule_code")]
    public string RuleCode { get; set; }
    [Required][StringLength(36)][Column("field_code")]
    public string FieldCode { get; set; }
    [StringLength(500)][Column("label_tag")]
    public string LabelTag { get; set; }
    [Column("extracted_value")]
    public string ExtractedValue { get; set; }
    [Column("confidence")]
    public decimal? Confidence { get; set; }
    [Column("position_info")]
    public string PositionInfo { get; set; }
    [Column("is_manual_edited")]
    public bool IsManualEdited { get; set; } = false;
    [Required][Column("extracted_at")]
    public DateTime ExtractedAt { get; set; }

    }
}
