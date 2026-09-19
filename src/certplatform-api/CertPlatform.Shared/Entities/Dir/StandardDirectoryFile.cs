using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Dir
{
    /// <summary>标准目录文件实体</summary>
    /// <para>命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase</para>
    [SugarTable("cert_standard_directory_file")]
    public class StandardDirectoryFile : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────

        [SugarColumn(Length = 150)]
        public string FileCode { get; set; } = string.Empty;

        [SugarColumn(Length = 150)]
        public string FolderCode { get; set; } = string.Empty;

        [SugarColumn(Length = 100)]
        public string DirectoryCode { get; set; } = string.Empty;

        [SugarColumn(Length = 500)]
        public string FileName { get; set; } = string.Empty;

        [SugarColumn(Length = 50)]
        public string FileType { get; set; } = string.Empty;

        [SugarColumn(Length = 200, IsNullable = true)]
        public string? FilePattern { get; set; }

        public long? FileSize { get; set; }

        public bool IsRequired { get; set; } = true;

        public int MaxFileSizeMB { get; set; } = 10;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Description { get; set; }

        public int SortOrder { get; set; } = 0;

        public bool ExtractionEnabled { get; set; } = false;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? ExtractionRules { get; set; }

        public bool PreCheckRequired { get; set; } = true;

        public bool ComplianceRequired { get; set; } = false;

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? Status { get; set; } = "draft";

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? StatusField { get; set; } = "active";

        public bool Enable { get; set; } = true;

        public bool EnableField { get; set; } = true;

        public int Sort { get; set; } = 0;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Remark { get; set; }

        [SugarColumn(Length = 64, IsNullable = true)]
        public string? TaskId { get; set; }

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? UploadStatus { get; set; } = "active";

        [SugarColumn(Length = 512, IsNullable = true)]
        public string? StoragePath { get; set; }

        [SugarColumn(Length = 1024, IsNullable = true)]
        public string? FullPath { get; set; }

        [SugarColumn(Length = 512, IsNullable = true)]
        public string? ConvertedStoragePath { get; set; }

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? ConvertStatus { get; set; }

        [SugarColumn(Length = 1024, IsNullable = true)]
        public string? ConvertMessage { get; set; }

        public DateTime? ConvertDate { get; set; }

        [SugarColumn(Length = 512, IsNullable = true)]
        public string? PreviewPdfPath { get; set; }

        [SugarColumn(Length = 512, IsNullable = true)]
        public string? MarkdownPath { get; set; }

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? MarkdownStatus { get; set; } = "none";

        [SugarColumn(Length = 1024, IsNullable = true)]
        public string? MarkdownMessage { get; set; }

        public DateTime? MarkdownDate { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
