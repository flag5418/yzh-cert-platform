using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Ent
{
    /// <summary>
    /// EnterpriseDocument 企业文档目录
    /// <para>表名：ent_enterprise_document</para>
    /// </summary>
    [SugarTable("ent_enterprise_document")]
    public class EnterpriseDocument : BaseEntity, ISoftDelete
    {
        // ──── Id / 审计字段由 BaseEntity 基类统一提供 ────

        // ──── 接口字段（显式实现 ISoftDelete） ────
        /// <summary>软删除标记（false=正常，true=已删除）</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public DateTime? DeleteTime { get; set; }

        // ──── 业务字段 ────
        /// <summary>机构编码（多租户隔离，此表需要机构级数据隔离）</summary>
        [StringLength(50)]
        public string? OrgCode { get; set; }

        [Required, StringLength(36)]
        public string EnterpriseCode { get; set; }

        [StringLength(36)]
        public string? PhaseCode { get; set; }

        [Required, StringLength(20)]
        public string Scope { get; set; }

        [StringLength(36)]
        public string? TemplateFolderCode { get; set; }

        [StringLength(36)]
        public string? ParentCode { get; set; }

        [Required, StringLength(200)]
        public string FolderName { get; set; }

        public int SortOrder { get; set; } = 0;
    }
}
