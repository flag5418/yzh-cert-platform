using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// FileRequirement 文件要求 / 标准文件模板
    /// <para>表名：cert_file_requirement</para>
    /// <para>此表既存储文件要求，也存储标准目录的模板文件信息</para>
    /// </summary>
    [SugarTable("cert_file_requirement")]
    public class FileRequirement : BaseEntity
    {
        // ──── Id / 审计字段由 BaseEntity 基类统一提供 ────

        // ──── 业务字段 ────
        /// <summary>所属文件夹编码</summary>
        [Required, StringLength(36)]
        [SugarColumn(Length = 36)]
        public string FolderCode { get; set; }

        /// <summary>文件名称模板（如「质量手册」）</summary>
        [Required, StringLength(200)]
        [UniqueField("文件名称", WithFields = new[] { "FolderCode" })]
        [SugarColumn(Length = 200)]
        public string FileNameTemplate { get; set; }

        /// <summary>文件类型（doc/docx/xlsx/pdf …）</summary>
        [Required, StringLength(50)]
        [SugarColumn(Length = 50)]
        public string FileType { get; set; }

        /// <summary>是否必需</summary>
        public bool IsRequired { get; set; } = true;

        /// <summary>单文件大小上限（MB）</summary>
        public int MaxSizeMB { get; set; } = 10;

        /// <summary>说明</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Description { get; set; }

        /// <summary>排序</summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>模板文件 OSS 存储路径</summary>
        [StringLength(500)]
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? TemplateStoragePath { get; set; }

        /// <summary>模板文件原始名（上传时的文件名）</summary>
        [StringLength(500)]
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? TemplateFileName { get; set; }

        /// <summary>标准编码（关联 cert_iso_standard.Code）</summary>
        [StringLength(36)]
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? StandardCode { get; set; }
    }
}
