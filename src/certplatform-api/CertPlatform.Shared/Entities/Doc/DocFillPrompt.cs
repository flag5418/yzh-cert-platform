using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;
using YZH.Entity.Admin.Platform;

namespace CertPlatform.Shared.Entities.Doc
{
    /// <summary>全文填写提示词（「AI 按模板通篇写」的规则）</summary>
    /// <para>表名：cert_doc_fill_prompt</para>
    /// <para>
    /// ★ 业务定位：与 <c>cert_doc_template_anchor</c>（逐锚点规则）<b>并列的第二条填写通路</b>。
    /// 锚点规则管「这个格子填什么」；本表管「整篇文档怎么组织、口吻怎么写」。
    /// <c>cert_doc_template.FillPromptCode</c> 引用本表 <see cref="PromptCode"/>（空 = 不走全文规则）。
    /// </para>
    /// <para>
    /// ★ 两级提示词：<see cref="SystemPrompt"/>（角色设定）+ <see cref="UserTemplate"/>
    /// （含 <c>{{__FILL__.xxx}}</c> 占位符的用户模板，渲染后交给模型）。
    /// </para>
    /// <para>
    /// ★ <see cref="Version"/> 参与唯一键 <c>uk_org_prompt_ver(OrgCode, PromptCode, Version)</c>
    /// —— Prompt 变更<b>不覆盖旧行，而是 +1 出新版本</b>（留痕，支撑「为什么当时这么写」的追溯）。
    /// </para>
    /// <para>
    /// ★ <see cref="IsDefault"/>：同一 <see cref="PromptCode"/> 下只能有一条为 1（DB 有 <c>idx_default</c>，
    /// 但 ⛔ 不是唯一索引 ⇒ <b>由服务层保证</b>：置 1 前先把同 Code 的其它行清零）。
    /// </para>
    /// <para>ORM：SqlSugar（铁律：DB 列名 == C# 属性名，PascalCase 逐字一致）</para>
    /// <para>设计依据：37 号 §四（表结构）· 39 号 §十（全文规则）</para>
    [SugarTable("cert_doc_fill_prompt")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class DocFillPrompt : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / CreateTime / CreateBy / UpdateTime / UpdateBy 由 BaseEntity 提供 ────

        /// <summary>认证机构编码（配置层归属，默认空串 = 全局）</summary>
        [StringLength(50)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>
        ///     Prompt 编码，如 <c>doc_fill</c>。
        ///     <para>★ <c>cert_doc_template.FillPromptCode</c> 引用此值（⛔ 不是本表 Code）。</para>
        /// </summary>
        [Required]
        [StringLength(100)]
        [UniqueField("全文填写提示词", WithFields = new[] { "OrgCode", "Version" })]
        public string PromptCode { get; set; } = string.Empty;

        /// <summary>名称，如「标准文档通用填写」</summary>
        [Required]
        [StringLength(200)]
        public string PromptName { get; set; } = string.Empty;

        /// <summary>系统提示词（角色设定）</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? SystemPrompt { get; set; }

        /// <summary>★ 用户提示词模板（含 <c>{{__FILL__.xxx}}</c> 占位符）</summary>
        [SugarColumn(ColumnDataType = "longtext", IsNullable = true)]
        public string? UserTemplate { get; set; }

        /// <summary>★ 期望输出结构（字段清单 + 表格清单），用于校验 AI 返回</summary>
        public string? OutputSchema { get; set; }

        /// <summary>模型（空 = 六键兜底）</summary>
        [StringLength(100)]
        public string? Model { get; set; }

        /// <summary>★ 低温保证可复现</summary>
        public decimal Temperature { get; set; }

        /// <summary>最大输出 token</summary>
        public int MaxTokens { get; set; } = 4000;

        /// <summary>
        ///     ★ 版本（Prompt 变更留痕，参与唯一键）。
        ///     <para><b>0 = 未指定</b> —— 由服务层取 <c>max(Version) + 1</c> 自动生成
        ///     （见 <c>DocFillPromptController.AddCore</c>）。</para>
        ///     <para>⛔ <b>CLR 默认值必须是 0，不能是 1</b>：JSON 未传 <c>version</c> 时反序列化会落到
        ///     属性初始值，若初始值是 1 则「自动自增」分支永不触发，第二次新增会撞
        ///     <c>uk_org_prompt_ver</c> 报「已存在版本 1」（实测踩过）。</para>
        ///     <para>DB 列 NOT NULL 默认 1；经服务层写入时恒 &gt; 0。</para>
        /// </summary>
        public int Version { get; set; }

        /// <summary>是否默认（★ 同一 PromptCode 只能一条为 1，由服务层保证）</summary>
        public bool IsDefault { get; set; }

        /// <summary>状态（draft / active / archived）</summary>
        [StringLength(50)]
        public string? Status { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        [StringLength(500)]
        public string? Remark { get; set; }

        // ──── 接口字段（BaseEntity 不含，必须声明在实体自身，否则全库过滤静默失效）────

        /// <summary>有效标志（1=有效，0=无效）。⛔ 禁 Enable</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（仅软删除时赋值）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（仅软删除时赋值）</summary>
        public DateTime? DeleteTime { get; set; }
    }
}
