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

        /// <summary>
        ///     ★ <b>机构归属</b>（归属模型 V2 —— 见
        ///     <c>docs/30-项目规则/跨端共享与核心资源检索规则-V1.md</c> §二）。
        ///     <para>本列 = <b>机构 Code</b>（该行归属的认证机构）；<b>空 = 平台标准级</b>
        ///     （本表无 <c>EnterpriseCode</c> 列 ⇒ 只有「机构专属 / 平台级」两态）。</para>
        ///     <para>⛔ 旧常量 <c>yzh-std-env</c> / <c>YZH-STD-ENT</c> <b>已废除</b>（守卫 <b>A-R10</b>）。</para>
        ///     <para>⚠️ 本列此前<b>未在实体声明</b>（DB 有列、实体无字段）⇒ 铁律无法生效；
        ///     2026-10-12 补声明。</para>
        /// </summary>
        [StringLength(50)]
        public string? OrgCode { get; set; }

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
        ///     标准简称（如 iso9001→e、iso14001→n），用于「条款序号」前缀显示（如 <c>e 4.3</c>）。
        ///     <para>★ 可空：未配置时由后端按 <see cref="StandardCode"/> slug 推导兜底；
        ///     可在标准管理页维护。仅在条款序号上作为前缀，不替换标准名称。</para>
        /// </summary>
        [StringLength(20)]
        public string? Abbrev { get; set; }

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

        // ──── 标准原文 Markdown（2026-11-10 V3 知识层） ────

        /// <summary>
        /// <b>标准原文 Markdown 的存储路径</b>（MinIO key，形如
        /// <c>/standard-source/{标准Code}/{文件名}.md</c>）。
        ///
        /// <para><b>为什么挂在标准行上</b>：这份 Markdown 是「自动生成条款」与
        /// 「企业文档内容提取（给模型看标准原文）」的<b>同一个输入</b>；
        /// 挂在标准行 ⇒ 全系统只有一个事实源，⛔ 不允许第二个表再存一份。</para>
        /// <para>空 = 还没上传过标准原文。</para>
        /// </summary>
        [StringLength(500)]
        public string? MarkdownPath { get; set; }

        /// <summary>Markdown 字符数（0 = 未生成；&gt;0 即已可用）</summary>
        public int MarkdownChars { get; set; }

        /// <summary>生成该 Markdown 的原始文件名（含扩展名）</summary>
        [StringLength(255)]
        public string? MarkdownSourceFile { get; set; }

        /// <summary>Markdown 最近生成时间（本地时间，铁律十）</summary>
        public DateTime? MarkdownUpdatedTime { get; set; }

        // ──── 标准条款 Markdown（2026-11-11 V3 知识层，派生视图） ────

        /// <summary>
        /// <b>条款 Markdown 的存储路径</b>（MinIO key，形如
        /// <c>/standard-source/{标准Code}/clauses/clauses.md</c>）。
        ///
        /// <para><b>它不是第二事实源</b>：由 <c>cert_iso_clause</c> 渲染，条款表是事实源；
        /// 原始标准 Markdown 仍在 <see cref="MarkdownPath"/>，供人工核对与溯源。</para>
        /// <para><b>用途</b>：构造提示词里的条款清单、按条款取「要求原文」、页面预览；
        /// ⛔ 不要把它当成标准原文来判符合性。</para>
        /// <para>空 = 还没执行过「写入条款」或补建。</para>
        /// </summary>
        [StringLength(500)]
        public string? ClauseMarkdownPath { get; set; }

        /// <summary>条款 Markdown 字符数（0 = 未生成）</summary>
        public int ClauseMarkdownChars { get; set; }

        /// <summary>条款 Markdown 最近生成时间（本地时间，铁律十）</summary>
        public DateTime? ClauseMarkdownUpdatedTime { get; set; }
    }
}
