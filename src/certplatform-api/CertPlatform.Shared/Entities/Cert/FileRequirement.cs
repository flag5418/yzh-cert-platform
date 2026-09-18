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
    /// <para>
    /// ⚠️ 列映射遵循新架构惯例：数据库列为 PascalCase（与 C# 属性名一致，见 cert_sys_config /
    /// cert_standard_directory_file）。旧架构的 snake_case 列名（folder_code / file_name_template …）
    /// 在该表中并不存在，会让 ORM 查询抛 Unknown column。
    /// </para>
    /// </summary>
    [Table("cert_file_requirement")]
    [SugarTable("cert_file_requirement")]
    public class FileRequirement : EntityBase
    {
        /// <summary>所属文件夹编码</summary>
        [Required, StringLength(36)]
        [Column("FolderCode")]
        [SugarColumn(ColumnName = "FolderCode", Length = 36)]
        public string FolderCode { get; set; }

        /// <summary>文件名称模板（如「质量手册」）</summary>
        [Required, StringLength(200)]
        [UniqueField("文件名称", WithFields = new[] { "FolderCode" })]
        [Column("FileNameTemplate")]
        [SugarColumn(ColumnName = "FileNameTemplate", Length = 200)]
        public string FileNameTemplate { get; set; }

        /// <summary>文件类型（doc/docx/xlsx/pdf …）</summary>
        [Required, StringLength(50)]
        [Column("FileType")]
        [SugarColumn(ColumnName = "FileType", Length = 50)]
        public string FileType { get; set; }

        /// <summary>是否必需</summary>
        [Column("IsRequired")]
        [SugarColumn(ColumnName = "IsRequired")]
        public bool IsRequired { get; set; } = true;

        /// <summary>单文件大小上限（MB）</summary>
        [Column("MaxSizeMB")]
        [SugarColumn(ColumnName = "MaxSizeMB")]
        public int MaxSizeMB { get; set; } = 10;

        /// <summary>说明</summary>
        [Column("Description")]
        [SugarColumn(ColumnName = "Description", ColumnDataType = "text", IsNullable = true)]
        public string Description { get; set; }

        /// <summary>排序</summary>
        [Column("SortOrder")]
        [SugarColumn(ColumnName = "SortOrder")]
        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// 模板文件 OSS 存储路径
        /// 格式：/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
        /// </summary>
        [StringLength(500)]
        [Column("TemplateStoragePath")]
        [SugarColumn(ColumnName = "TemplateStoragePath", Length = 500, IsNullable = true)]
        public string TemplateStoragePath { get; set; }

        /// <summary>模板文件原始名（上传时的文件名）</summary>
        [StringLength(500)]
        [Column("TemplateFileName")]
        [SugarColumn(ColumnName = "TemplateFileName", Length = 500, IsNullable = true)]
        public string TemplateFileName { get; set; }

        /// <summary>标准编码（关联 cert_iso_standard.Code）</summary>
        [StringLength(36)]
        [Column("StandardCode")]
        [SugarColumn(ColumnName = "StandardCode", Length = 36, IsNullable = true)]
        public string StandardCode { get; set; }
    }
}
