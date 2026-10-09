using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Sys
{
    /// <summary>
    /// 机构-阶段关联表（多对多）
    /// <para>表名：cert_org_stage</para>
    /// <para>ORM：SqlSugar。关联 cert_certification_body.Code ↔ cert_cert_stage.Code（★ Q2 统一口径 2026-10-08：
    /// 阶段键一律存 Code/GUID，⛔ 不存业务码 jd01/03）</para>
    /// <para><b>★ 2026-10-09 删除策略改硬删除</b>：uk_org_std_stage(OrgCode,StandardCode,StageCode)
    /// 设计上对全表行唯一（含已删行）。StandardCode 现恒为 NULL（MySQL 唯一键不约束 NULL）
    /// 暂未撞键，但回填 StandardCode 或跨多个勾选/取消周期后，「取消勾选再勾选」必撞 1062
    /// ⇒ 关联表物理删除（先例：<c>CertOrgStandard</c> uk_org_std、<c>CertEnterpriseStage</c>）。</para>
    ///
    /// 命名规范（YZH 铁律）：DB 列名 = C# 属性名 = PascalCase
    /// </summary>
    [SugarTable("cert_org_stage")]
    [YZHDeleteStrategy(Mode = DeleteMode.Hard)]
    public class CertOrgStage : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity 基类统一提供 ────
        // ──── ISoftDelete / IIsValid 接口字段由接口提供 ────

        /// <summary>机构编码（关联 cert_certification_body.Code）</summary>
        [SugarColumn(Length = 50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>标准编码（可空，暂未使用）</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? StandardCode { get; set; }

        /// <summary>阶段键（★存 cert_cert_stage.Code，GUID —— 不是业务码 StageCode slug）</summary>
        public string StageCode { get; set; } = string.Empty;

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
