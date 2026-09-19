using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Ent
{
    /// <summary>
    /// EnterpriseFile 企业文件
    /// <para>表名：ent_enterprise_file</para>
    /// </summary>
    [SugarTable("ent_enterprise_file")]
    public class EnterpriseFile : BaseEntity
    {
        // ──── Id / 审计字段由 BaseEntity 基类统一提供 ────

        // ──── 业务字段 ────
        /// <summary>机构编码（所属认证机构，多租户隔离）</summary>
        [StringLength(50)]
        public string? OrgCode { get; set; }

        /// <summary>关联企业 code</summary>
        [Required, StringLength(36)]
        public string EnterpriseCode { get; set; }

        /// <summary>关联企业文档目录 code</summary>
        [Required, StringLength(36)]
        public string FolderCode { get; set; }

        /// <summary>文件名(中文原样)</summary>
        [Required, StringLength(500)]
        public string FileName { get; set; }

        /// <summary>文件类型(pdf/docx/xlsx等)</summary>
        [Required, StringLength(50)]
        public string FileType { get; set; }

        /// <summary>文件大小(bytes)</summary>
        [Required]
        public long FileSize { get; set; }

        /// <summary>MinIO存储路径</summary>
        [Required, StringLength(500)]
        public string StoragePath { get; set; }

        /// <summary>转换后文件路径(.docx/.xlsx)</summary>
        [StringLength(500)]
        public string? ConvertedStoragePath { get; set; }

        /// <summary>转换状态：null/pending/converting/converted/failed</summary>
        [StringLength(20)]
        public string? ConvertStatus { get; set; }

        /// <summary>转换失败原因</summary>
        [StringLength(1024)]
        public string? ConvertMessage { get; set; }

        /// <summary>转换完成时间</summary>
        public DateTime? ConvertDate { get; set; }

        /// <summary>SHA256哈希(增量审核依据)</summary>
        [StringLength(64)]
        public string? FileHash { get; set; }

        /// <summary>当前版本号</summary>
        public int CurrentVersion { get; set; } = 1;

        /// <summary>上传状态：pending/uploading/active/failed</summary>
        [StringLength(20)]
        public string UploadStatus { get; set; } = "active";

        /// <summary>标准文件编码（关联 cert_file_requirement.code）</summary>
        [StringLength(36)]
        public string? StandardFileCode { get; set; }
    }
}
