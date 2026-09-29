using System.Collections.Generic;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Dir
{
    /// <summary>标准目录文件夹实体</summary>
    /// <para>命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase</para>
    /// <para>⛔ 复合编码 <c>FolderCode</c>（FD-{目录}|L{层}|S{序}）已于 2026-09-26 删除，本行 Code 即业务键。</para>
    [SugarTable("cert_standard_directory_folder")]
    public class StandardDirectoryFolder : BaseEntity, ISoftDelete, IIsValid, ITreeEntity
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────
        // ──── ITreeEntity 接口字段由接口提供 ────

        [SugarColumn(IsIgnore = true)]
        public List<StandardDirectoryFolder>? Children { get; set; }

        /// <summary>配置 Code → <c>cert_standard_directory_config.Code</c>（原列名 <c>DirectoryCode</c>）</summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? ConfigCode { get; set; }

        /// <summary>
        /// 父文件夹 Code → <b>本表</b> <c>Code</c>；<b>根节点恒为 <c>""</c>，⛔ 不用 <c>NULL</c></b>
        /// （决策 ⑨：MySQL 唯一索引允许多个 NULL ⇒ 用 NULL 会导致同名根文件夹拦不住）。
        /// <para>⚠️ C# 侧因 <see cref="ITreeEntity"/> 约束仍为可空类型 —— <b>写库前必须 <c>?? ""</c></b>，
        /// DB 列是 <c>NOT NULL DEFAULT ''</c>，写 null 会报 1048（响的，非静默）。</para>
        /// </summary>
        [SugarColumn(Length = 36, IsNullable = true)]
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

        /// <summary>相对配置根的文件夹路径（如 <c>4记录文件/内审记录</c>）。★ 收窄到 500 —— 1024 会让唯一索引超出 InnoDB 3072 字节上限</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
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
