using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Cert
{
    /// <summary>
    /// 认证阶段（全局基础资料）
    /// <para>表名：cert_cert_stage</para>
    /// <para>基于 ISO/IEC 17021-1:2015，9 个标准认证阶段</para>
    /// <para>ORM：SqlSugar（§16 铁律：DB列名 == C#属性名，PascalCase）</para>
    /// </summary>
    [SugarTable("cert_cert_stage")]
    public class CertStage : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id 已由 BaseEntity 基类统一提供 ────

        /// <summary>阶段编码（如 AP/CR/SP/S1/S2/CD/CE/SV/RC）</summary>
        [Required]
        [StringLength(50)]
        [UniqueField("阶段编码")]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>阶段名称</summary>
        [Required]
        [StringLength(200)]
        public string StageName { get; set; } = string.Empty;

        /// <summary>分类（process/audit/post-cert）</summary>
        [StringLength(50)]
        public string Category { get; set; } = "process";

        /// <summary>排序号（1~9）</summary>
        public int SortOrder { get; set; }

        /// <summary>阶段说明</summary>
        public string? Description { get; set; }

        /// <summary>状态（active/inactive）</summary>
        [StringLength(50)]
        public string Status { get; set; } = "active";

        /// <summary>备注</summary>
        [StringLength(500)]
        public string? Remark { get; set; }

        // ──── 接口字段（BaseEntity 不包含，由接口继承提供） ────

        /// <summary>有效标志（1=有效，0=无效）</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记（false=正常，true=已删除）</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（仅软删除时赋值）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（仅软删除时赋值）</summary>
        public DateTime? DeleteTime { get; set; }
    }
}
