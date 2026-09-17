using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// 标准条款
    /// <para>表名：cert_iso_clause</para>
    /// <para>列名规范：PascalCase（与 BaseEntity 标准属性名完全一致，无需映射）</para>
    /// <para>域：A - 认证体系配置</para>
    /// 
    /// ORM 映射说明：
    /// - 数据库审计列（CreateBy/CreateTime/UpdateBy/UpdateTime/DeleteBy/DeleteTime/IsDeleted/IsValid）
    ///   与 BaseEntity 标准属性名完全一致，SqlSugar 自动映射
    /// - 子类使用 new 关键字覆盖基类属性，移除基类 IsIgnore 标记
    /// </summary>
    [SugarTable("cert_iso_clause")]
    public class ISOClause : BaseEntity
    {
        // ──── 覆盖基类审计字段（DB列名 == 属性名，自动映射） ────
        
        /// <summary>主键</summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public new long Id { get; set; }

        /// <summary>业务编码</summary>
        public new string Code { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>创建人 Code</summary>
        public new string? CreateBy { get; set; }

        /// <summary>创建时间</summary>
        public new DateTime CreateTime { get; set; } = DateTime.UtcNow;

        /// <summary>更新人 Code</summary>
        public new string? UpdateBy { get; set; }

        /// <summary>更新时间</summary>
        public new DateTime? UpdateTime { get; set; }

        /// <summary>删除人 Code</summary>
        public new string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public new DateTime? DeleteTime { get; set; }

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>有效标志（1=有效，0=无效）</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>备注</summary>
        [MaxLength(500)]
        public string? Remark { get; set; }

        // ──── 条款特有字段（PascalCase，自动映射） ────

        /// <summary>所属标准编码（关联 ISOStandard.Code）</summary>
        [Required]
        [StringLength(36)]
        public string StandardCode { get; set; } = string.Empty;

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
