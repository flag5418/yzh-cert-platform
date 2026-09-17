using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Entity.Admin.Platform.Cert
{
    /// <summary>
    /// 认证阶段（全局基础资料）
    /// <para>表名：cert_cert_stage</para>
    /// <para>基于 ISO/IEC 17021-1:2015，9 个标准认证阶段</para>
    /// <para>ORM：SqlSugar（§16 铁律：DB列名 == C#属性名，PascalCase）</para>
    /// </summary>
    [SugarTable("cert_cert_stage")]
    public class CertStage : BaseEntity
    {
        /// <summary>主键</summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public new long Id { get; set; }

        /// <summary>业务编码</summary>
        public new string Code { get; set; } = Guid.NewGuid().ToString("N");

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

        /// <summary>有效标志</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>状态（active/inactive）</summary>
        [StringLength(50)]
        public string Status { get; set; } = "active";

        /// <summary>备注</summary>
        [StringLength(500)]
        public string? Remark { get; set; }

        /// <summary>创建人</summary>
        public new string? CreateBy { get; set; }

        /// <summary>创建时间</summary>
        public new DateTime CreateTime { get; set; } = DateTime.UtcNow;

        /// <summary>更新人</summary>
        public new string? UpdateBy { get; set; }

        /// <summary>更新时间</summary>
        public new DateTime? UpdateTime { get; set; }

        /// <summary>删除人</summary>
        public new string? DeleteBy { get; set; }

        /// <summary>删除时间</summary>
        public new DateTime? DeleteTime { get; set; }

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }
    }
}
