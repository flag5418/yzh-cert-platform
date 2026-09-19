using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Ent
{
    /// <summary>
    /// FileVersion 文件版本
    /// <para>表名：ent_file_version</para>
    /// </summary>
    [SugarTable("ent_file_version")]
    public class FileVersion : BaseEntity
    {
        // ──── Id / 审计字段由 BaseEntity 基类统一提供 ────

        // ──── 业务字段 ────
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        public string? OrgCode { get; set; }

        [Required, StringLength(36)]
        [UniqueField("文件编码", WithFields = new[] { "VersionNumber" })]
        public string FileCode { get; set; }

        [Required]
        public int VersionNumber { get; set; }

        [Required]
        public long FileSize { get; set; }

        [Required, StringLength(500)]
        public string StoragePath { get; set; }

        [Required, StringLength(64)]
        public string FileHash { get; set; }

        public string? ChangeNotes { get; set; }

        [Required]
        public int UploadBy { get; set; }
    }
}
