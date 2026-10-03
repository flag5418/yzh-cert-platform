using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;
using YZH.Entity.Admin.Platform;

namespace CertPlatform.Shared.Entities.Doc
{
    /// <summary>标签字典（受控词表）</summary>
    /// <para>表名：cert_tag_dict</para>
    /// <para>
    /// ★ 业务定位（34 号 §6.4）：04 号五路召回中 <b>R2 类别路 / R3 关键词倒排路的原料供给侧</b>。
    /// 它是「文档语义规则」的核心实体，与 <see cref="StandardDocContract"/>（标准侧落点）、
    /// 企业画像（企业侧落点）构成同一条语义坐标系。
    /// </para>
    /// <para>
    /// ⛔ 三套「字典」是正交概念，勿混用（34 号 §五）：
    /// ① <c>cert_fill_param_def/value</c> = 参数（层 1 全局替换）；
    /// ② 字典 <c>DOC_CATEGORY</c>/<c>DOC_PURPOSE</c> = 语义分析的输出受控值（校验用）；
    /// ③ 本表 = 文档标签实体，<b>唯一召回键</b>。
    /// </para>
    /// <para>ORM：SqlSugar（铁律：DB 列名 == C# 属性名，PascalCase 逐字一致）</para>
    /// <para>设计依据：21 号 §4.3 表 3（基础列）· 33 号 §五（扩展列）· 14 号 D14（双侧同规则）</para>
    [SugarTable("cert_tag_dict")]
    public class TagDict : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / CreateTime / CreateBy / UpdateTime / UpdateBy 由 BaseEntity 提供 ────

        /// <summary>标签编码（业务键，PascalCase，如 RecordInternalAudit）</summary>
        [Required]
        [StringLength(50)]
        [UniqueField("标签编码")]
        public string TagCode { get; set; } = string.Empty;

        /// <summary>标签显示名（如 内审记录）</summary>
        [Required]
        [StringLength(100)]
        public string TagName { get; set; } = string.Empty;

        /// <summary>分组：文档类型 / 业务域 / 标准条款</summary>
        [StringLength(50)]
        public string? TagGroup { get; set; }

        /// <summary>
        ///     作用侧：standard / enterprise / both
        ///     <para>14 号 D14：双侧同规则，同字典同规则 ⇒ 两侧可比。缺一侧则过滤做不了交集。</para>
        /// </summary>
        [StringLength(20)]
        public string ApplicableSide { get; set; } = "both";

        /// <summary>
        ///     适用标准清单 JSON：["iso9001","iso13485"]；NULL = 全部标准。
        ///     <para>标签集合是「标准 × 文档」的函数（34 号 §6.2）—— 13485 有检验作业指导书，9001 没有。</para>
        /// </summary>
        public string? StandardCodes { get; set; }

        /// <summary>
        ///     编号前缀 / 文件名特征（如 <c>XASL-QR-</c>）—— 04 号 S1 指纹的 L0 规则落点，零 LLM 命中。
        ///     <para>与 <see cref="StandardDocContract.FingerprintJson"/> 是同一份能力的两个粒度。</para>
        /// </summary>
        [StringLength(200)]
        public string? MatchFeature { get; set; }

        /// <summary>样例文档名（逗号分隔），供提示词 few-shot 与人工核对</summary>
        [StringLength(500)]
        public string? SampleDocNames { get; set; }

        /// <summary>该标签下的文档通常起什么作用（给 doc_content 提示词的先验）</summary>
        [StringLength(500)]
        public string? TagPurposeHint { get; set; }

        /// <summary>来源：ai=元提示词生成 / manual=人工新建 / mixed=AI 生成后人工修正</summary>
        [StringLength(10)]
        public string GenSource { get; set; } = "manual";

        /// <summary>是否被人工修正过（1 = 人工改过，AI 批量重跑 ⛔ 不得覆盖）</summary>
        public bool IsManualCorrected { get; set; }

        /// <summary>排序号</summary>
        public int Sort { get; set; }

        /// <summary>状态：active=启用 / archived=归档</summary>
        [StringLength(20)]
        public string Status { get; set; } = "active";

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
