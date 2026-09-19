using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;
using YZH.Core.Stand.Models;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// ISO 标准
    /// <para>表名：cert_iso_standard</para>
    /// <para>ORM：SqlSugar（§16 铁律：DB列名 == C#属性名，PascalCase）</para>
    /// </summary>
    [SugarTable("cert_iso_standard")]
    public class ISOStandard : BaseEntity, ISoftDelete, IIsValid, ITreeEntity
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
