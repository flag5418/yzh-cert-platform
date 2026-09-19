using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Shared.Entities.Sys
{
    /// <summary>
    /// 机构-标准关联表（多对多）
    /// <para>表名：cert_org_standard</para>
    /// <para>ORM：SqlSugar。关联 cert_certification_body.Code ↔ cert_iso_standard.Code</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("cert_org_standard")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class CertOrgStandard : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        /// <summary>机构编码（关联 cert_certification_body.Code）</summary>
        [SugarColumn(Length = 50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>标准编码（关联 cert_iso_standard.Code）</summary>
        [SugarColumn(Length = 50)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>备注</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}
