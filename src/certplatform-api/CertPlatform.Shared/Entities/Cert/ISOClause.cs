using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SqlSugar;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// 标准条款
    /// <para>表名：cert_iso_clause</para>
    /// <para>列名规范：业务字段 PascalCase，审计字段 snake_case</para>
    /// <para>域：A - 认证体系配置</para>
    /// 
    /// 注意：cert_iso_clause 表业务字段（StandardCode, ParentCode, ClauseNumber 等）为 PascalCase，
    /// 与 C# 属性名一致，无需显式映射。审计字段（create_by, create_date 等）为 snake_case，
    /// 需用 [SugarColumn] 显式映射。
    /// </summary>
    [Table("cert_iso_clause")]
    [SugarTable("cert_iso_clause")]
    public class ISOClause : EntityBase
    {
        // ──── 覆盖基类审计字段（适配 snake_case 列名） ────
        
        /// <summary>主键</summary>
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true, IsIdentity = true)]
        [Column(TypeName = "bigint")]
        public new long Id { get; set; }

        /// <summary>业务编码</summary>
        [SugarColumn(ColumnName = "Code")]
        [StringLength(100)]
        public new string? Code { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>创建人姓名</summary>
        [SugarColumn(ColumnName = "creator")]
        [MaxLength(50)]
        public new string? Creator { get; set; }

        /// <summary>创建人 Code</summary>
        [SugarColumn(ColumnName = "create_by")]
        [MaxLength(50)]
        public new string? CreateBy { get; set; }

        /// <summary>创建时间</summary>
        [SugarColumn(ColumnName = "create_date")]
        public new DateTime? CreateDate { get; set; } = DateTime.Now;

        /// <summary>修改人姓名</summary>
        [SugarColumn(ColumnName = "modifier")]
        [MaxLength(50)]
        public new string? Modifier { get; set; }

        /// <summary>修改人 Code</summary>
        [SugarColumn(ColumnName = "update_by")]
        [MaxLength(50)]
        public new string? UpdateBy { get; set; }

        /// <summary>修改时间</summary>
        [SugarColumn(ColumnName = "modify_date")]
        public new DateTime? ModifyDate { get; set; }

        /// <summary>删除人姓名</summary>
        [SugarColumn(ColumnName = "deleter")]
        [MaxLength(50)]
        public new string? Deleter { get; set; }

        /// <summary>删除人 Code</summary>
        [SugarColumn(ColumnName = "delete_by")]
        [MaxLength(50)]
        public new string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        [SugarColumn(ColumnName = "delete_time")]
        public new DateTime? DeleteTime { get; set; }

        /// <summary>启用状态</summary>
        [SugarColumn(ColumnName = "enable")]
        public new bool Enable { get; set; } = true;

        /// <summary>排序</summary>
        [SugarColumn(ColumnName = "Sort")]
        public new int Sort { get; set; }

        /// <summary>备注</summary>
        [SugarColumn(ColumnName = "Remark")]
        [MaxLength(500)]
        public new string? Remark { get; set; }

        // ──── 条款特有字段（PascalCase，与数据库列名一致，无需映射） ────

        /// <summary>
        /// 所属标准编码（关联 ISOStandard.Code）
        /// </summary>
        [Required]
        [StringLength(36)]
        public string StandardCode { get; set; }

        /// <summary>
        /// 父条款编码（树形结构，关联同表的 Code）
        /// </summary>
        [StringLength(36)]
        public string? ParentCode { get; set; }

        /// <summary>
        /// 条款编号（如 7.1、7.1.1）
        /// </summary>
        [Required]
        [StringLength(20)]
        [UniqueField("条款编号", WithFields = new[] { "StandardCode" })]
        public string ClauseNumber { get; set; }

        /// <summary>
        /// 条款标题
        /// </summary>
        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        /// <summary>
        /// 条款原文或摘要
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 排序
        /// </summary>
        public int SortOrder { get; set; } = 0;
    }
}
