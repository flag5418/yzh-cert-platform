using System.Collections.Generic;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Dir
{
    /// <summary>标准目录文件夹实体</summary>
    /// <para>命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase</para>
    [SugarTable("cert_standard_directory_folder")]
    public class StandardDirectoryFolder : BaseEntity, ISoftDelete, IIsValid, ITreeEntity
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────
        // ──── ITreeEntity 接口字段由接口提供 ────

        [SugarColumn(IsIgnore = true)]
        public List<StandardDirectoryFolder>? Children { get; set; }

        [SugarColumn(Length = 150, IsNullable = true)]
        public string? FolderCode { get; set; }

        [SugarColumn(Length = 100, IsNullable = true)]
        public string? DirectoryCode { get; set; }

        [SugarColumn(Length = 150, IsNullable = true)]
        public string? ParentCode { get; set; }

        [SugarColumn(Length = 200, IsNullable = true)]
        public string? FolderName { get; set; }

        public int Depth { get; set; } = 1;

        public int SortOrder { get; set; } = 0;

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? Status { get; set; } = "draft";

        [SugarColumn(Length = 20, IsNullable = true)]
        public string? StatusField { get; set; } = "active";

        public int Sort { get; set; } = 0;

        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Remark { get; set; }

        [SugarColumn(Length = 64, IsNullable = true)]
        public string? TaskId { get; set; }

        [SugarColumn(Length = 1024, IsNullable = true)]
        public string? FullPath { get; set; }

        [SugarColumn(IsIgnore = true)]
        public bool Force { get; set; } = false;

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;

        // ──── ITreeEntity 接口显式实现 ────
        [SugarColumn(IsIgnore = true)]
        public bool? IsLeaf { get; set; }
    }
}
