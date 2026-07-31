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

    [Required][StringLength(100)][Column("config_key")]
    public string ConfigKey { get; set; }
    [Required][Column("config_value")]
    public string ConfigValue { get; set; }
    [Column("value_type")]
    public string ValueType { get; set; } = "string";
    [Column("description")]
    public string Description { get; set; }
    [Column("is_system")]
    public bool IsSystem { get; set; } = false;

    }
}
