using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// SysLog
    /// <para>表名：sys_log</para>
    /// </summary>
    [Table("sys_log")]
    public class SysLog : BaseEntity
    {

    [Column("user_id")]
    public long? UserId { get; set; }
    [Required][StringLength(50)][Column("module")]
    public string Module { get; set; }
    [Required][StringLength(100)][Column("action")]
    public string Action { get; set; }
    [StringLength(50)][Column("target_type")]
    public string TargetType { get; set; }
    [Column("target_id")]
    public long? TargetId { get; set; }
    [Column("detail")]
    public string Detail { get; set; }
    [StringLength(50)][Column("ip_address")]
    public string IpAddress { get; set; }
    [StringLength(500)][Column("user_agent")]
    public string UserAgent { get; set; }

    }
}
