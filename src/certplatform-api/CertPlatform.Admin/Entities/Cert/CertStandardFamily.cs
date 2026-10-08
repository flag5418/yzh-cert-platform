using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Entity.Admin.Platform;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Cert
{
    /// <summary>
    ///     ISO 标准族（体系 → 族 → 版本 三层中的「族」层）
    /// <para>表名：cert_standard_family</para>
    /// <para>ORM：SqlSugar（§16 铁律：DB列名 == C#属性名，PascalCase）</para>
    /// <para>
    ///     ★ 三层语义（2026-10-08 裁决「统一，都支持」）：
    ///     <list type="bullet">
    ///         <item>体系 = <c>iso_category</c> 字典（已有，不建表）</item>
    ///         <item>族   = 本表 <c>cert_standard_family</c>（新）</item>
    ///         <item>版本 = 现有 <c>cert_iso_standard</c>（一行 = 一个版本，不建版本实体表）</item>
    ///     </list>
    /// </para>
    /// <para>
    ///     ⚠️ 人读编号用 <c>FamilyNo</c>（如 <c>iso9000</c>）——
    ///     <b>禁叫 FamilyCode</b>：<c>cert_iso_standard.FamilyCode</c> 存的是本表 <c>Code</c>(GUID)，
    ///     同名不同义会让洗码（A→B）后的关联静默断裂（V4「发现 A」同类坑）。
    /// </para>
    /// <para>
    ///     唯一性由<b>应用层</b>保证（<c>OnBeforeAdd/OnBeforeUpdate</c> 校验），
    ///     ⛔ 不建 DB 唯一索引 —— 唯一索引会把 <c>IsDeleted=1</c> 的行也算进去，
    ///     软删后重建同名族必撞（与 <c>cert_iso_standard.UniqueField</c> 惯例一致）。
    /// </para>
    /// <para>
    ///     全局表，<b>无 OrgCode</b>（照 <c>cert_phase_definition</c> 惯例 —— 标准与族都是基础资料，
    ///     实测 <c>cert_iso_standard.OrgCode</c> 全为 NULL）。
    /// </para>
    /// </summary>
    [SugarTable("cert_standard_family")]
    public class CertStandardFamily : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────

        /// <summary>族人读编号（如 iso9000、iso14001 系）。人读 slug，⚠️ 非关联键</summary>
        [Required]
        [StringLength(50)]
        [UniqueField("族编号")]
        public string FamilyNo { get; set; } = string.Empty;

        /// <summary>族中文名（如「ISO 9000 质量管理体系族」）。唯一性 = Category + FamilyName</summary>
        [Required]
        [StringLength(200)]
        [UniqueField("族名称", WithFields = new[] { "Category" })]
        public string FamilyName { get; set; } = string.Empty;

        /// <summary>所属体系（存 iso_category 字典 DicValue，如 quality）</summary>
        [Required]
        [StringLength(50)]
        public string Category { get; set; } = "quality";

        /// <summary>族说明</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? Description { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        // ──── ISoftDelete 接口实现 ────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }

        // ──── IIsValid 接口实现 ────
        /// <summary>启用标志（1=启用 0=停用 · 铁律九：全库禁用 Enable）</summary>
        public int IsValid { get; set; } = 1;
    }
}
