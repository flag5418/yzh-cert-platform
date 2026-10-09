using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Sys
{
    /// <summary>
    /// 机构-标准关联表（多对多）
    /// <para>表名：cert_org_standard</para>
    /// <para>ORM：SqlSugar。关联 cert_certification_body.Code ↔ cert_iso_standard.Code</para>
    /// <para><b>★ 2026-10-09 删除策略改硬删除</b>：uk_org_std(OrgCode,StandardCode) 对全表行唯一
    /// （含已删行），软删除后「取消勾选再勾选」必撞唯一键 ⇒ 关联表物理删除
    /// （先例：<c>cert_enterprise_stage</c>）。历史软删除行见
    /// <c>scripts/db/fix/20261009_org_standard_slug_to_code_V1.sql</c> 清理。</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("cert_org_standard")]
    [YZHDeleteStrategy(Mode = DeleteMode.Hard)]
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
