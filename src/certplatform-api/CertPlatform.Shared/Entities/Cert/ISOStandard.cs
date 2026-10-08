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

        /// <summary>
        ///     ⛔ <b>已废止</b>（2026-10-08 三层体系裁决）—— 本字段恒为 <c>null</c>。
        /// <para>ISO 标准无树层级：树形由 <c>ISOStandardTreeTableController.TreeConfig</c>
        ///     （<c>ParentCodeField=ParentCode</c> + <c>MaxLevel=1</c>）决定，
        ///     <c>TreeTableControllerBase.GetRootNodes</c> 只返回 <c>ParentCode=Root</c> 的行。</para>
        /// <para>⛔ <b>严禁改为指向标准族</b> —— 会让现有 <c>/cert/iso-standard</c> 的
        ///     <c>/tree/root</c> 返回 0 行白屏。族关联一律走 <see cref="FamilyCode"/> 列。</para>
        /// <para>ITreeEntity 接口要求本属性存在，故保留声明但不落业务值。</para>
        /// </summary>
        public string? ParentCode { get; set; } = null;

        /// <summary>
        ///     ⛔ <b>已废止</b>（同 <see cref="ParentCode"/>）—— 恒为 <c>true</c>（标准全是叶子）。
        ///     ITreeEntity 接口要求本属性存在，故保留声明但不落业务值。
        /// </summary>
        public bool? IsLeaf { get; set; } = true;

        // ──── ISO 标准特有字段 ────

        /// <summary>认证机构编码（关联 cert_certification_body.Code）</summary>
        [StringLength(50)]
        public string? CbCode { get; set; }

        /// <summary>
        ///     所属标准族（体系 → <b>族</b> → 版本 三层中的族层）
        /// <para>值 = <c>cert_standard_family.Code</c>（<b>GUID</b>，关联键）。</para>
        /// <para>⚠️ 族的<b>人读</b>编号在族表叫 <c>FamilyNo</c>（如 iso9000）——
        ///     本列叫 <c>FamilyCode</c> 存 GUID，两者同名不同义，<b>勿混用</b>。</para>
        /// <para>可空：允许版本尚未归族（新页面左树会落到「未归族」分支）。</para>
        /// </summary>
        [StringLength(36)]
        public string? FamilyCode { get; set; }

        /// <summary>
        ///     标准编号（人读 slug，**不含年份**）—— 如 <c>iso9001</c>、<c>iso13485</c>。
        /// <para>★ 与 <see cref="VersionYear"/> 分离（2026-10-08「去岁数化」）：年份只进 <c>VersionYear</c>，
        ///     ⛔ 不得再写成 <c>iso9001-2015</c>（老写法会与「同编号可多版本」冲突）。</para>
        /// <para>⚠️ <b>非关联键</b>：所有关联（条款/目录/阶段/提示词/机构标准…）一律用 <c>Code</c>(GUID)；
        ///     本字段仅供人读与 slug 匹配（如 <c>cert_tag_dict.StandardCodes</c> 存的就是本字段）。</para>
        /// </summary>
        [Required]
        [StringLength(50)]
        [UniqueField("标准编号", WithFields = new[] { "VersionYear" })]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>
        ///     标准中文名称
        /// <para>唯一约束 = <b>名称 + 版本年份</b>（同名标准可并存多个版本）；
        ///     运行时校验在 <c>ISOStandardTreeTableController.OnBeforeAdd/UpdateTree</c>。</para>
        /// </summary>
        [Required]
        [StringLength(200)]
        [UniqueField("标准名称", WithFields = new[] { "VersionYear" })]
        public string StandardName { get; set; } = string.Empty;

        /// <summary>版本年份</summary>
        public int VersionYear { get; set; }

        /// <summary>
        ///     所属体系（存 <c>iso_category</c> 字典 <b>DicValue</b>：quality / environment / safety …）
        /// <para>权威字典 = <c>Sys_Dictionary.DicNo='iso_category'</c>（GUID 集），读取接口
        ///     <c>GET /api/System/Dictionary/items/by-no/iso_category</c>。</para>
        /// <para>⛔ legacy 字面量集（<c>DicCode='iso_category'</c> 10 项）已于
        ///     2026-10-08 软删统一，勿再引用。</para>
        /// </summary>
        [StringLength(50)]
        public string Category { get; set; } = "quality";

        /// <summary>描述</summary>
        public string? Description { get; set; }
    }
}
