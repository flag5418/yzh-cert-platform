using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;
using YZH.Core.Stand.Models;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// ISO 标准
    /// <para>表名：cert_iso_standard</para>
    /// <para>ORM：SqlSugar（§16 铁律：DB列名 == C#属性名，PascalCase）</para>
    /// </summary>
    [SugarTable("cert_iso_standard")]
    public class ISOStandard : BaseEntity, ITreeEntity
    {
        /// <summary>主键（物理自增 bigint）</summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public new long Id { get; set; }

        /// <summary>业务编码（GUID 32位，关联键）</summary>
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

        // ──── ITreeEntity 成员 ────

        /// <summary>父节点编码（ISO标准无层级，始终 null = 根节点）</summary>
        public string? ParentCode { get; set; } = null;

        /// <summary>是否叶子节点（ISO标准无子节点，始终 true）</summary>
        public bool? IsLeaf { get; set; } = true;

        // ──── ISO 标准特有字段 ────

        /// <summary>认证机构编码（关联 cert_certification_body.Code）</summary>
        [StringLength(50)]
        public string? CbCode { get; set; }

        /// <summary>标准编号（如 ISO 9001:2015, ISO 13485:2016）</summary>
        [Required]
        [StringLength(50)]
        [UniqueField("标准编号", WithFields = new[] { "VersionYear" })]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>标准中文名称</summary>
        [Required]
        [StringLength(200)]
        [UniqueField("标准名称")]
        public string StandardName { get; set; } = string.Empty;

        /// <summary>版本年份</summary>
        public int VersionYear { get; set; }

        /// <summary>类别（quality/environment/medical 等）</summary>
        [StringLength(50)]
        public string Category { get; set; } = "quality";

        /// <summary>描述</summary>
        public string? Description { get; set; }
    }
}
