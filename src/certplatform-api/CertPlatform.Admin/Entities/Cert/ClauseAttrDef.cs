using System;
using YZH.Entity.Admin.Platform;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Cert
{
    /// <summary>
    /// L0 条款属性池 —— 「所有人的所有可能属性」。
    ///
    /// <para><b>业务定位</b>（见 <c>docs/70-V2设计/24-条款标准化-属性池与条款配置-V2.md</c> §三）：
    /// 属性全集，跨标准跨条款复用。<b>池子大、配置小</b> —— 池子 30–50 条不嫌多，
    /// 每个条款只挑自己那几条（挑哪几条 = <see cref="ClauseAttr"/> 有没有行）。</para>
    ///
    /// <para>⛔ <b>本表不设</b>：<c>ValueSource</c>（同属性不同条款来源不同，只能在 L1 定，见 24 号 §六）、
    /// <c>PoolLayer</c>（分层是假想的）、<c>AppliesTo</c>（挑哪几条 = 有没有行）、必填集。</para>
    ///
    /// <para><b>值类型只有 5 种</b>：<c>text</c> / <c>dict</c> / <c>bool</c> / <c>json</c> / <c>prompt</c>。
    /// ⛔ 没有 <c>int</c>（认证无数值比较）、<c>date</c>（无日期比较）、<c>*_multi</c>（多值即 JSON 数组）。</para>
    ///
    /// <para>ORM：SqlSugar（铁律：DB 列名 == C# 属性名，PascalCase 逐字一致）</para>
    /// </summary>
    [SugarTable("cert_clause_attr_def")]
    public class ClauseAttrDef : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / CreateTime / CreateBy / UpdateTime / UpdateBy 由 BaseEntity 提供 ────
        // ★ Code 就是「属性码」业务键（如 Artifacts / Obligation），不是 GUID —— 与其它实体不同：
        //   属性池是**受控枚举**，Code 必须人可读，否则提示词和配置里全是乱码 GUID。
        //   ⚠️ 它是 L1 ClauseAttr.AttrCode 的取值来源，改它必须同步迁 L1。

        /// <summary>属性名（中文展示名），如「企业应提供的资料」</summary>
        [Required]
        [StringLength(100)]
        public string AttrName { get; set; } = string.Empty;

        /// <summary>
        /// 值类型：<c>text</c> | <c>dict</c> | <c>bool</c> | <c>json</c> | <c>prompt</c>
        /// <para><c>prompt</c> = 值本身就是可执行提示词（<c>Artifacts</c>/<c>EvidenceChain</c>），
        /// 内部仍要求 LLM 输出结构化 JSON；只产"读到了什么"，不产结论。</para>
        /// </summary>
        [Required]
        [StringLength(20)]
        public string ValueType { get; set; } = "text";

        /// <summary>
        /// 字典编码（<c>Sys_Dictionary.DicNo</c>）—— <c>dict</c> 与 <c>json</c>(受控多选) 时必填。
        /// <para>如 <c>Obligation</c> → <c>clause_obligation</c>；<c>Process</c> → <c>process_domain</c>。</para>
        /// </summary>
        [StringLength(50)]
        public string? DictCode { get; set; }

        /// <summary>作用（给配置者看的一句话：这条属性参与哪一步）</summary>
        [StringLength(500)]
        public string? Remark { get; set; }

        /// <summary>
        /// <b>预生成提示词里的取值要求</b>（属性级生成契约 —— 给<b>模型</b>看，不是给人看）。
        ///
        /// <para>由 <c>ClauseAttrPromptService</c> 渲染进提示词的「属性清单」段。空白时按
        /// <see cref="ValueType"/> 给默认话术；<c>Artifacts</c>/<c>EvidenceChain</c> 这类
        /// 需要整段结构化契约的，正文就写在这里（<see cref="Remark"/> 装不下）。</para>
        ///
        /// <para><b>★ 为什么要有这一列</b>：属性池会长（8 → 30~50 条）。把判据写死在提示词正文里
        /// ⇒ 池子一变模型就不产新属性，<b>挂接静默缺失且不报错</b>。有了它，新增属性只填属性池，
        /// 提示词自动跟上。</para>
        /// </summary>
        public string? PromptHint { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        // ──── 接口字段（BaseEntity 不含，必须声明在实体自身，否则全库过滤静默失效）────

        /// <summary>有效标志（1=有效，0=停用）</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（仅软删除时赋值）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（仅软删除时赋值）</summary>
        public DateTime? DeleteTime { get; set; }

        /// <summary>业务状态（保留列，DB 有、本模块不用）</summary>
        [StringLength(50)]
        public string? Status { get; set; }
    }
}
