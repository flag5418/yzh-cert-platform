using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SqlSugar;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// FileRequirement 文件要求 / 标准文件模板
    /// <para>表名：cert_file_requirement</para>
    /// <para>此表既存储文件要求，也存储标准目录的模板文件信息</para>
    /// <para>模板文件 OSS 路径：/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}</para>
    /// </summary>
    [Table("cert_file_requirement")]
    [SugarTable("cert_file_requirement")]
    public class FileRequirement : EntityBase
    {
        [Required, StringLength(36)]
        [Column("folder_code")]
        [SugarColumn(ColumnName = "folder_code", Length = 36)]
        public string FolderCode { get; set; }

        [Required, StringLength(200)]
        [UniqueField("文件名称", WithFields = new[] { "FolderCode" })]
        [Column("file_name_template")]
        [SugarColumn(ColumnName = "file_name_template", Length = 200)]
        public string FileNameTemplate { get; set; }

        [Required, StringLength(50)]
        [Column("file_type")]
        [SugarColumn(ColumnName = "file_type", Length = 50)]
        public string FileType { get; set; }

        [Column("is_required")]
        [SugarColumn(ColumnName = "is_required")]
        public bool IsRequired { get; set; } = true;

        [Column("max_size_mb")]
        [SugarColumn(ColumnName = "max_size_mb")]
        public int MaxSizeMB { get; set; } = 10;

        [Column("description")]
        [SugarColumn(ColumnName = "description", ColumnDataType = "text", IsNullable = true)]
        public string Description { get; set; }

        [Column("sort_order")]
        [SugarColumn(ColumnName = "sort_order")]
        public int SortOrder { get; set; } = 0;

        [StringLength(500)]
        [Column("template_storage_path")]
        [SugarColumn(ColumnName = "template_storage_path", Length = 500, IsNullable = true)]
        public string TemplateStoragePath { get; set; }

        [StringLength(500)]
        [Column("template_file_name")]
        [SugarColumn(ColumnName = "template_file_name", Length = 500, IsNullable = true)]
        public string TemplateFileName { get; set; }

        [StringLength(36)]
        [Column("standard_code")]
        [SugarColumn(ColumnName = "standard_code", Length = 36, IsNullable = true)]
        public string StandardCode { get; set; }
    }
}
