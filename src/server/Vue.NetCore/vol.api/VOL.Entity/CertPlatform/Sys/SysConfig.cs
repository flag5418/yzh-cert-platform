using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// SysConfig
    /// <para>表名：sys_config</para>
    /// </summary>
    [Table("sys_config")]
    public class SysConfig : YZHBaseEntity
    {

    [Required][StringLength(100)]
    public string ConfigKey { get; set; }
    [Required]
    public string ConfigValue { get; set; }
    
    public string ValueType { get; set; } = "string";
    
    public string Description { get; set; }
    
    public bool IsSystem { get; set; } = false;

    }
}
