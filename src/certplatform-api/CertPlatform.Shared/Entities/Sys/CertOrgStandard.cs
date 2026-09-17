using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Models.Entity;

namespace YZH.Entity.Admin.Platform.Sys
{
    /// <summary>
    /// 机构-标准关联表（多对多）
    /// <para>表名：cert_org_standard</para>
    /// <para>ORM：SqlSugar。关联 cert_certification_body.Code ↔ cert_iso_standard.Code</para>
    /// </summary>
    [SugarTable("cert_org_standard")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class CertOrgStandard : BaseEntity
    {
        /// <summary>主键（DB: bigint AUTO_INCREMENT）</summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public new long Id { get; set; }

        /// <summary>业务编码（GUID）</summary>
        [SugarColumn(Length = 36)]
        public new string Code { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>机构编码（关联 cert_certification_body.Code）</summary>
        [SugarColumn(ColumnName = "OrgCode", Length = 50, IsNullable = false)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>标准编码（关联 cert_iso_standard.Code）</summary>
        [SugarColumn(ColumnName = "standard_code", Length = 50, IsNullable = false)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>备注</summary>
        [SugarColumn(ColumnName = "Remark", Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        // ──── 审计字段重声明（BaseEntity IsIgnore=true → 这里改为 false） ────
        public int IsValid { get; set; } = 1;
        [StringLength(50)]
        public new string? CreateBy { get; set; }
        public new DateTime CreateTime { get; set; } = DateTime.UtcNow;
        [StringLength(50)]
        public new string? UpdateBy { get; set; }
        public new DateTime? UpdateTime { get; set; }
        [StringLength(50)]
        public new string? DeleteBy { get; set; }
        public new DateTime? DeleteTime { get; set; }
        public bool IsDeleted { get; set; }
    }
}
