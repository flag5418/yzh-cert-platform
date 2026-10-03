using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Dir
{
    /// <summary>
    /// 标准目录根级文件视图实体
    /// <para>映射视图：v_standard_directory_root_files</para>
    /// <para>用途：查询根级别文件（FolderCode为空或不存在的文件），含中间状态</para>
    /// </summary>
    [SugarTable("v_standard_directory_root_files")]
    public class StandardDirectoryRootFileView : BaseEntity
    {
        // 视图无基础审计字段，标记为忽略
        [SugarColumn(IsIgnore = true)]
        public new long Id { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new string? Code { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new DateTime CreateTime { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new string? CreateBy { get; set; }

        [SugarColumn(IsIgnore = true)]
        public new DateTime? UpdateTime { get; set; }

        [SugarColumn(ColumnName = "FileName")]
        public string FileName { get; set; } = "";

        [SugarColumn(ColumnName = "FileType")]
        public string FileType { get; set; } = "";

        [SugarColumn(ColumnName = "StoragePath")]
        public string? StoragePath { get; set; }

        [SugarColumn(ColumnName = "ConvertedStoragePath")]
        public string? ConvertedStoragePath { get; set; }

        [SugarColumn(ColumnName = "ConvertStatus")]
        public string? ConvertStatus { get; set; }

        [SugarColumn(ColumnName = "ConvertMessage")]
        public string? ConvertMessage { get; set; }

        // ★ 2026-09-26 双产物链：视图已同步加列（scripts/db/views/v_standard_directory_root_files.sql）
        // 缺列时根级文件拿不到产物路径与提取链状态 → 前端静默显示空状态
        [SugarColumn(ColumnName = "PreviewPdfPath")]
        public string? PreviewPdfPath { get; set; }

        [SugarColumn(ColumnName = "MarkdownPath")]
        public string? MarkdownPath { get; set; }

        [SugarColumn(ColumnName = "MarkdownStatus")]
        public string? MarkdownStatus { get; set; }

        [SugarColumn(ColumnName = "MarkdownMessage")]
        public string? MarkdownMessage { get; set; }

        [SugarColumn(ColumnName = "UploadStatus")]
        public string? UploadStatus { get; set; }

        [SugarColumn(ColumnName = "TaskId")]
        public string? TaskId { get; set; }

        [SugarColumn(ColumnName = "ConfigCode")]
        public string ConfigCode { get; set; } = "";

        [SugarColumn(ColumnName = "FolderCode")]
        public string? FolderCode { get; set; }

        [SugarColumn(ColumnName = "IsValid")]
        public int IsValid { get; set; }

        [SugarColumn(ColumnName = "IsDeleted")]
        public bool IsDeleted { get; set; }

        [SugarColumn(ColumnName = "FileSize")]
        public long? FileSize { get; set; }

        // 视图只读
        [SugarColumn(IsIgnore = true)]
        public bool IsSoftDeleted { get; set; }

        [SugarColumn(IsIgnore = true)]
        public int IsValidFlag { get; set; } = 1;
    }
}
