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
    public class SysLog : YZHBaseEntity
    {

    
    public long? UserId { get; set; }
    [Required][StringLength(50)]
    public string Module { get; set; }
    [Required][StringLength(100)]
    public string Action { get; set; }
    [StringLength(50)]
    public string TargetType { get; set; }
    
    public long? TargetId { get; set; }
    
    public string Detail { get; set; }
    [StringLength(50)]
    public string IpAddress { get; set; }
    [StringLength(500)]
    public string UserAgent { get; set; }

    }
}
