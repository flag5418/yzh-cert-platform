using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// 全局系统参数配置实体
    /// </summary>
    [Entity(TableCnName = "系统参数配置", TableName = "cert_sys_config", DBServer = "VOLContext")]
    [Table("cert_sys_config")]
    public class SysConfig : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Column("config_key")]
        public string ConfigKey { get; set; }

        [Column("config_value")]
        public string ConfigValue { get; set; }

        [Column("config_type")]
        public string ConfigType { get; set; } = "string";

        [Column("category")]
        public string Category { get; set; }

        [Column("display_name")]
        public string DisplayName { get; set; }

        [Column("description")]
        public string Description { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; }

        [Column("is_readonly")]
        public int IsReadonly { get; set; }

        [Column("create_date")]
        public DateTime CreateDate { get; set; } = DateTime.Now;

        [Column("modify_date")]
        public DateTime? ModifyDate { get; set; }
    }
}
