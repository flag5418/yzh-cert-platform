using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// DirectoryTemplate 标准目录模板
    /// <para>表名：cert_directory_template</para>
    /// </summary>
    [Table("cert_directory_template")]
    public class DirectoryTemplate : EntityBase
    {
        [Required, StringLength(36)]
        [Column("config_code")]
        public string ConfigCode { get; set; }

        [StringLength(36)]
        [Column("parent_code")]
        public string ParentCode { get; set; }

        [Required, StringLength(200)]
        [UniqueField("文件夹名称", WithFields = new[] { "ConfigCode", "ParentCode" })]
        [Column("folder_name")]
        public string FolderName { get; set; }

        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;
    }
}
