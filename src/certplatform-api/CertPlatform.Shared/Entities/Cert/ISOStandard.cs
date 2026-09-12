using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YZH.Entity;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Models;
using SqlSugar;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// ISO 标准
    /// <para>表名：cert_iso_standard</para>
    /// <para>列名规范：snake_case，通过 [SugarColumn] 特性映射</para>
    /// <para>域：A - 认证体系配置</para>
    /// 
    /// ORM 映射说明：
    /// - DbContextFactory 的自定义 EntityService 不读取 EF Core [Column]，必须使用 SqlSugar 的 [SugarColumn]
    /// - 基类 EntityBase 属性需用 new + [SugarColumn] 显式覆盖，确保 snake_case 列正确映射
    /// - 参考 PromptTemplate.cs 的映射模式（PromptTemplate 是唯一全量 override 的成功案例）
    /// </summary>
    [Entity(TableCnName = "ISO标准管理", TableName = "cert_iso_standard", DBServer = "VOLContext")]
    [Table("cert_iso_standard")]
    [SugarTable("cert_iso_standard")]
    public class ISOStandard : EntityBase, ITreeEntity
    {
        // ──── 覆盖基类审计字段，适配 cert_iso_standard 的 snake_case 列名 ────
        
        /// <summary>主键（物理自增 bigint）</summary>
        [SugarColumn(ColumnName = "Id", IsPrimaryKey = true, IsIdentity = true)]
        [Column(TypeName = "bigint")]
        public new long Id { get; set; }

        /// <summary>业务编码（GUID 32位，关联键）</summary>
        [SugarColumn(ColumnName = "Code")]
        [StringLength(100)]
        public new string? Code { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>创建人姓名</summary>
        [SugarColumn(ColumnName = "creator")]
        [MaxLength(50)]
        public new string? Creator { get; set; }

        /// <summary>创建人 Code（USER_000001）</summary>
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

    // ──── ITreeEntity 成员（ISO 标准扁平结构，TreeTable 架构要求必须为真实列） ────

    /// <summary>父节点编码（ISO标准无层级，始终 null = 根节点）</summary>
    [SugarColumn(ColumnName = "parent_code")]
    [StringLength(100)]
    public new string? ParentCode { get; set; } = null;

    /// <summary>是否叶子节点（ISO标准无子节点，始终 true）</summary>
    [SugarColumn(ColumnName = "is_leaf")]
    public new bool? IsLeaf { get; set; } = true;

    // ──── ISO 标准特有字段 ────

    /// <summary>
    /// 标准编号（如 ISO 9001:2015, ISO 13485:2016）
    /// </summary>
        [Required]
        [StringLength(50)]
        [Editable(true)]
        [UniqueField("标准编号", WithFields = new[] { "VersionYear" })]
        [Column("standard_code")]
        [SugarColumn(ColumnName = "standard_code")]
        public string StandardCode { get; set; }

        /// <summary>
        /// 标准中文名称
        /// </summary>
        [Required]
        [StringLength(200)]
        [Editable(true)]
        [UniqueField("标准名称")]
        [Column("standard_name")]
        [SugarColumn(ColumnName = "standard_name")]
        public string StandardName { get; set; }

        /// <summary>
        /// 版本年份
        /// </summary>
        [Editable(true)]
        [Column("version_year")]
        [SugarColumn(ColumnName = "version_year")]
        public int VersionYear { get; set; }

        /// <summary>
        /// 类别（quality/environment/medical 等）
        /// </summary>
        [StringLength(50)]
        [Editable(true)]
        [Column("category")]
        [SugarColumn(ColumnName = "category")]
        public string Category { get; set; } = "quality";

        /// <summary>
        /// 描述
        /// </summary>
        [Column("description")]
        [SugarColumn(ColumnName = "description")]
        public string? Description { get; set; }

        /// <summary>
        /// 认证机构编码（关联 cert_certification_body.code）
        /// </summary>
        [StringLength(50)]
        [Column("cb_code")]
        [SugarColumn(ColumnName = "cb_code")]
        public string? CbCode { get; set; }
    }
}
