using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Admin.Entities.Cert
{
    /// <summary>
    /// L1 条款属性配置 —— 「某条款用哪些属性 + 值 + 这个值固定还是参考」。
    ///
    /// <para><b>业务定位</b>（见 <c>docs/70-V2设计/24-条款标准化-属性池与条款配置-V2.md</c> §四）：
    /// 一条 = 一个条款上的一个属性。没配 = 没行（稀疏），⛔ 不需要"适用条款"列。</para>
    ///
    /// <para><b>★ <see cref="ValueSource"/> 只在 L1、只两档</b>（24 号 §五）：
    /// <c>fixed</c> = 条款已定，存值本身（如 <c>Obligation=shall</c>）；
    /// <c>reference</c> = 条款只给期望，存期望（如 <c>Artifacts</c> 的提取提示词）。
    /// ⛔ 没有第三档"求值"—— 求值是程序逻辑，不是属性值。</para>
    ///
    /// <para>⛔ <b>本表不存</b>企业实际值 / <c>Confidence</c> / <c>ValueState</c>（那是企业侧，暂缓）。
    /// 一张表同时描述"标准要求"和"企业实际"，判断「要求 vs 实际」时必然分不清 —— V1 已犯过一次。</para>
    ///
    /// <para>ORM：SqlSugar（铁律：DB 列名 == C# 属性名，PascalCase 逐字一致）</para>
    /// </summary>
    [SugarTable("cert_clause_attr")]
    public class ClauseAttr : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code(GUID) / CreateTime / CreateBy / UpdateTime / UpdateBy 由 BaseEntity 提供 ────

        /// <summary>
        /// 标准短码：<c>iso9001</c> / <c>iso14001</c> / <c>iso45001</c> / <c>iso13485</c>。
        /// <para>⚠️ 用**短码**不用 <c>cert_iso_standard.Code</c>（GUID）—— 配置页左树、过滤条件、
        /// 人工排查都要人可读；GUID 只出现在 <see cref="ClauseCode"/>。</para>
        /// </summary>
        [Required]
        [StringLength(36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>条款 Code（<c>cert_iso_clause.Code</c>，GUID）</summary>
        [Required]
        [StringLength(36)]
        public string ClauseCode { get; set; } = string.Empty;

        /// <summary>属性码（<c>cert_clause_attr_def.Code</c>）</summary>
        [Required]
        [StringLength(50)]
        public string AttrCode { get; set; } = string.Empty;

        /// <summary>
        /// ★ 值来源，<b>NOT NULL</b>，只两档：
        /// <list type="bullet">
        ///   <item><c>fixed</c> —— 条款已定，<see cref="AttrValue"/> 存值本身</item>
        ///   <item><c>reference</c> —— 条款只给期望，<see cref="AttrValue"/> 存期望</item>
        /// </list>
        /// ⛔ 空值提交必须被拒（否则下游无法判断"该不该向企业索取"）。
        /// </summary>
        [Required]
        [StringLength(20)]
        public string ValueSource { get; set; } = string.Empty;

        /// <summary>
        /// 值。<c>json</c> 存 JSON 原文；<c>prompt</c> 存**提示词文本**（可含 <c>{{skill:xxx}}</c> 占位）。
        /// <para>⛔ 只有一个值列 —— 不铺 <c>ValueBool</c>/<c>ValueJson</c>（同一事实写两遍，改类型时两边打架且不报错）。</para>
        /// </summary>
        public string? AttrValue { get; set; }

        /// <summary>同条款内的展示顺序</summary>
        public int Sort { get; set; }

        // ⚠️ 表里还有 FilterCode（GENERATED ALWAYS ... STORED，只给 3 个枚举型属性做过滤索引）
        //    —— 生成列**不在实体声明**：声明了 SqlSugar 会尝试 INSERT 它，直接报错。

        // ──── 接口字段（BaseEntity 不含，必须声明在实体自身，否则全库过滤静默失效）────

        /// <summary>有效标志（1=有效，0=停用）</summary>
        public int IsValid { get; set; } = 1;

        /// <summary>软删除标记</summary>
        public bool IsDeleted { get; set; }

        /// <summary>删除人 Code（仅软删除时赋值）</summary>
        public string? DeleteBy { get; set; }

        /// <summary>删除时间（仅软删除时赋值）</summary>
        public DateTime? DeleteTime { get; set; }

        /// <summary>业务状态（保留列）</summary>
        [StringLength(50)]
        public string? Status { get; set; }

        /// <summary>备注</summary>
        [StringLength(500)]
        public string? Remark { get; set; }
    }
}
