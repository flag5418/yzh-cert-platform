using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VOL.Entity.CertPlatform.Cert
{
    /// <summary>
    /// 标准条款
    /// <para>表名：cert_iso_clause</para>
    /// <para>域：A - 认证体系配置</para>
    /// </summary>
    [Table("cert_iso_clause")]
    public class ISOClause : BaseEntity
    {
        /// <summary>
        /// 所属标准编码（关联 ISOStandard.Code）
        /// </summary>
        [Required]
        [StringLength(36)]
        [Column("standard_code")]
        public string StandardCode { get; set; }

        /// <summary>
        /// 父条款编码（树形结构，关联同表的 Code）
        /// </summary>
        [StringLength(36)]
        [Column("parent_code")]
        public string ParentCode { get; set; }

        /// <summary>
        /// 条款编号（如 7.1、7.1.1）
        /// </summary>
        [Required]
        [StringLength(20)]
        [Column("clause_number")]
        public string ClauseNumber { get; set; }

        /// <summary>
        /// 条款标题
        /// </summary>
        [Required]
        [StringLength(200)]
        [Column("title")]
        public string Title { get; set; }

        /// <summary>
        /// 条款原文或摘要
        /// </summary>
        [Column("description")]
        public string Description { get; set; }

        /// <summary>
        /// 排序
        /// </summary>
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;
    }
}
