using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// DirectoryTemplate 标准目录模板
    /// <para>表名：cert_directory_template</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("cert_directory_template")]
    public class DirectoryTemplate : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        // ──── 业务字段 ────
        [Required]
        [StringLength(36)]
        public string ConfigCode { get; set; } = string.Empty;

        [StringLength(36)]
        public string? ParentCode { get; set; }

        [Required]
        [StringLength(200)]
        [UniqueField("文件夹名称", WithFields = new[] { "ConfigCode", "ParentCode" })]
        public string FolderName { get; set; } = string.Empty;

        public int SortOrder { get; set; } = 0;

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
