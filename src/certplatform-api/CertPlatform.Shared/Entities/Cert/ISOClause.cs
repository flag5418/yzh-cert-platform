using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// 标准条款
    /// <para>表名：cert_iso_clause</para>
    /// <para>列名规范：PascalCase（与 BaseEntity 标准属性名完全一致，无需映射）</para>
    /// <para>域：A - 认证体系配置</para>
    /// </summary>
    [SugarTable("cert_iso_clause")]
    public class ISOClause : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id 已由 BaseEntity 基类统一提供 ────

        // ──── 接口字段（BaseEntity 不包含，由接口继承提供） ────

        /// <summary>有效标志（1=有效，0=无效）</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记（false=正常，true=已删除）</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（仅软删除时赋值）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（仅软删除时赋值）</summary>
        public DateTime? DeleteTime { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [MaxLength(500)]
        public string? Remark { get; set; }

        // ──── 条款特有字段 ────

        /// <summary>所属标准编码（关联 ISOStandard.Code）</summary>
        [StringLength(36)]
        public string? StandardCode { get; set; }

        /// <summary>父条款编码（树形结构，关联同表的 Code）</summary>
        [StringLength(36)]
        public string? ParentCode { get; set; }

        /// <summary>条款编号（如 7.1、7.1.1）</summary>
        [Required]
        [StringLength(20)]
        [UniqueField("条款编号", WithFields = new[] { "StandardCode" })]
        public string ClauseNumber { get; set; } = string.Empty;

        /// <summary>条款标题</summary>
        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>条款原文或摘要</summary>
        public string? Description { get; set; }

        /// <summary>条款排序</summary>
        public int SortOrder { get; set; }
    }
}
