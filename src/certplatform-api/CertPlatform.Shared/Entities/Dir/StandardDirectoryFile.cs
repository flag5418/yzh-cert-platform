using System;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Entity.SystemModels;

namespace YZH.Entity.Admin.Platform.Dir
{
    [SugarTable("cert_standard_directory_file")]
    public class StandardDirectoryFile : BaseEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "Code", Length = 36)]
        public string Code { get; set; }

        [SugarColumn(ColumnName = "FileCode", Length = 150)]
        public string FileCode { get; set; }

        [SugarColumn(ColumnName = "FolderCode", Length = 150)]
        public string FolderCode { get; set; }

        [SugarColumn(ColumnName = "DirectoryCode", Length = 100)]
        public string DirectoryCode { get; set; }

        [SugarColumn(ColumnName = "FileName", Length = 500)]
        public string FileName { get; set; }

        [SugarColumn(ColumnName = "FileType", Length = 50)]
        public string FileType { get; set; }

        [SugarColumn(ColumnName = "FilePattern", Length = 200, IsNullable = true)]
        public string FilePattern { get; set; }

        [SugarColumn(ColumnName = "file_size", IsNullable = true)]
        public long? FileSize { get; set; }

        [SugarColumn(ColumnName = "IsRequired")]
        public bool IsRequired { get; set; } = true;

        [SugarColumn(ColumnName = "MaxFileSizeMB")]
        public int MaxFileSizeMB { get; set; } = 10;

        [SugarColumn(ColumnName = "Description", ColumnDataType = "text", IsNullable = true)]
        public string Description { get; set; }

        [SugarColumn(ColumnName = "SortOrder")]
        public int SortOrder { get; set; } = 0;

        [SugarColumn(ColumnName = "ExtractionEnabled")]
        public bool ExtractionEnabled { get; set; } = false;

        [SugarColumn(ColumnName = "ExtractionRules", ColumnDataType = "text", IsNullable = true)]
        public string ExtractionRules { get; set; }

        [SugarColumn(ColumnName = "PreCheckRequired")]
        public bool PreCheckRequired { get; set; } = true;

        [SugarColumn(ColumnName = "ComplianceRequired")]
        public bool ComplianceRequired { get; set; } = false;

        [SugarColumn(ColumnName = "status", Length = 20, IsNullable = true)]
        public string Status { get; set; } = "draft";

        [SugarColumn(ColumnName = "Enable")]
        public bool Enable { get; set; } = true;

        [SugarColumn(ColumnName = "TaskId", Length = 64, IsNullable = true)]
        public string TaskId { get; set; }

        [SugarColumn(ColumnName = "IsValid")]
        public int IsValid { get; set; } = 1;

        [SugarColumn(ColumnName = "UploadStatus", Length = 20, IsNullable = true)]
        public string UploadStatus { get; set; } = "active";

        [SugarColumn(ColumnName = "StoragePath", Length = 512, IsNullable = true)]
        public string StoragePath { get; set; }

        [SugarColumn(ColumnName = "FullPath", Length = 1024, IsNullable = true)]
        public string FullPath { get; set; }

        [SugarColumn(ColumnName = "converted_storage_path", Length = 512, IsNullable = true)]
        public string ConvertedStoragePath { get; set; }

        [SugarColumn(ColumnName = "convert_status", Length = 20, IsNullable = true)]
        public string ConvertStatus { get; set; }

        [SugarColumn(ColumnName = "convert_message", Length = 1024, IsNullable = true)]
        public string ConvertMessage { get; set; }

        [SugarColumn(ColumnName = "convert_date", IsNullable = true)]
        public DateTime? ConvertDate { get; set; }

        // ===== 2026-09-16 新增：预览/提取双产物列（决策 D-6，脚本 20260916_doc_extraction_alter.sql）=====

        /// <summary>预览 PDF 产物路径（LibreOffice 转换；PDF 文件原样透传 = StoragePath）</summary>
        [SugarColumn(ColumnName = "preview_pdf_path", Length = 512, IsNullable = true)]
        public string? PreviewPdfPath { get; set; }

        /// <summary>提取 Markdown 产物路径（anydoc 转换）</summary>
        [SugarColumn(ColumnName = "markdown_path", Length = 512, IsNullable = true)]
        public string? MarkdownPath { get; set; }

        /// <summary>Markdown 转换状态：none/pending/converting/completed/failed</summary>
        [SugarColumn(ColumnName = "markdown_status", Length = 20, IsNullable = true)]
        public string? MarkdownStatus { get; set; } = "none";

        /// <summary>Markdown 转换失败原因</summary>
        [SugarColumn(ColumnName = "markdown_message", Length = 1024, IsNullable = true)]
        public string? MarkdownMessage { get; set; }

        /// <summary>Markdown 转换完成时间</summary>
        [SugarColumn(ColumnName = "markdown_date", IsNullable = true)]
        public DateTime? MarkdownDate { get; set; }

        [SugarColumn(ColumnName = "CreateID", IsNullable = true)]
        public int? CreateID { get; set; }

        [SugarColumn(ColumnName = "CreateBy", Length = 50, IsNullable = true)]
        public string Creator { get; set; }

        [SugarColumn(ColumnName = "CreateDate")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName = "ModifyID", IsNullable = true)]
        public int? ModifyID { get; set; }

        [SugarColumn(ColumnName = "UpdateBy", Length = 50, IsNullable = true)]
        public string Modifier { get; set; }

        [SugarColumn(ColumnName = "ModifyDate", IsNullable = true)]
        public DateTime? ModifyDate { get; set; }

        [SugarColumn(ColumnName = "DeleteID", IsNullable = true)]
        public int? DeleteID { get; set; }

        [SugarColumn(ColumnName = "DeleteBy", Length = 50, IsNullable = true)]
        public string Deleter { get; set; }

        [SugarColumn(ColumnName = "DeleteTime", IsNullable = true)]
        public DateTime? DeleteTime { get; set; }

        [SugarColumn(ColumnName = "Status_field", Length = 50, IsNullable = true)]
        public string Status_field { get; set; } = "active";

        [SugarColumn(ColumnName = "Enable_field")]
        public bool Enable_field { get; set; } = true;

        [SugarColumn(ColumnName = "Sort")]
        public int Sort { get; set; } = 0;

        [SugarColumn(ColumnName = "Remark", ColumnDataType = "text", IsNullable = true)]
        public string Remark { get; set; }
    }
}
