using System;
using System.ComponentModel.DataAnnotations;
using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;
using YZH.Entity.Admin.Platform;

namespace CertPlatform.Admin.Entities.Doc
{
    /// <summary>标准文档契约（五要素 1 / 2 / 5）</summary>
    /// <para>表名：cert_standard_doc_contract</para>
    /// <para>
    /// ★ 业务定位（34 号 §6.3）：「文档语义规则」的<b>标准侧落点</b> —— 标签 <c>TagsJson</c>、
    /// 作用 <c>DocPurpose</c>、结构化要素 <c>InfoItemsJson</c> 都写入此表。
    /// 一文件一契约（<c>uk_standard_file_code</c>），是天然的业务幂等键。
    /// </para>
    /// <para>
    /// ⛔ 为什么落契约表而不是 <c>cert_doc_template</c>（33 号 Q3）：模板行只有「上传了模板的」才有行，
    /// 而契约表覆盖全部 167 个标准文档；且固定文档 <c>fixed</c> 也要契约（指纹 + 分类），未必有模板。
    /// </para>
    /// <para>
    /// ⛔ 画像链与定向链分离（01 号 D3）：本表产出<b>只落契约表 / 画像表</b>，
    /// 任何情况下不写 <c>cert_extraction_result</c> —— 否则会污染 NC 与报告的数据源。
    /// </para>
    /// <para>ORM：SqlSugar（铁律：DB 列名 == C# 属性名，PascalCase 逐字一致）</para>
    /// <para>设计依据：02 号 §3.1（基础列）· 33 号 §五（语义/分析列）· 05 号 §3（DocCategory 三分类）</para>
    [SugarTable("cert_standard_doc_contract")]
    public class StandardDocContract : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / CreateTime / CreateBy / UpdateTime / UpdateBy 由 BaseEntity 提供 ────

        // ──── 身份段（⛔ 禁 NULL，服务端填充，不加 [Required]）────

        /// <summary>★ 宿主标准文件 Code → <c>cert_standard_directory_file.Code</c>（一文件一契约）</summary>
        [StringLength(36)]
        [UniqueField("标准文件Code")]
        public string StandardFileCode { get; set; } = string.Empty;

        /// <summary>目录配置 Code → <c>cert_standard_directory_config.Code</c>（冗余，过滤用）</summary>
        [StringLength(36)]
        public string ConfigCode { get; set; } = string.Empty;

        /// <summary>标准 Code → <c>cert_iso_standard.Code</c>（GUID，冗余）</summary>
        [StringLength(36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>阶段 Code → <c>cert_cert_stage.Code</c>（GUID，⛔ 不是业务短码 jd01/03）</summary>
        [StringLength(36)]
        public string StageCode { get; set; } = string.Empty;

        // ──── 五要素 1 / 2 / 5 ────

        /// <summary>【要素1】标准文档名称（= 标准文件名，冗余供匹配读取，改名时同步）</summary>
        [Required]
        [StringLength(200)]
        public string DocName { get; set; } = string.Empty;

        /// <summary>
        ///     【要素2】文档作用，<b>四段式</b>：
        ///     ① 是什么 / 核心内容 ② 审核关注点 ③ 来源口径 ④ 包含信息（100–400 字）。
        ///     <para>本列存<b>人读</b>的渲染文本；机器读的要素清单存 <see cref="InfoItemsJson"/>。</para>
        ///     <para>⛔ 不要把本列改成 json —— 05 号 §2.1 三段式是既有权威，第四段是其摘要层。</para>
        /// </summary>
        public string? DocPurpose { get; set; }

        /// <summary>
        ///     【分类】fixed=固定文档（匹配即终点）/ editable=可编写 / hybrid=混合。
        ///     <para>⛔ 决定 04 号 §5.2 用哪套加权公式，不得留空（05 号 §3）。</para>
        /// </summary>
        [StringLength(20)]
        public string DocCategory { get; set; } = "editable";

        /// <summary>
        ///     【固定文档 · 可替换性】<c>standard_provided</c> = 标准自带（不向企业索取）/
        ///     <c>enterprise_provided</c> = 企业提供（要匹配依据）。
        ///     <para>★ 仅当 <see cref="DocCategory"/> = <c>fixed</c> 时有意义；<b>人工判断，可覆盖</b>（不阻断）。</para>
        ///     <para>⛔ <b>该列 DDL 早已存在</b>（<c>varchar(20) NOT NULL DEFAULT 'enterprise_provided'</c>），
        ///     但实体此前未声明 ⇒ ORM 看不见 ⇒ 读写都被静默丢弃。本次补上（缺口 G1）。</para>
        /// </summary>
        [StringLength(20)]
        public string FixedDocSubtype { get; set; } = "enterprise_provided";

        /// <summary>required=必需 / optional=可选 / reference=参考 / attachment=附件</summary>
        [StringLength(20)]
        public string DocRole { get; set; } = "required";

        /// <summary>【要素5a】匹配提示词：告诉 LLM 如何判断企业文档是否对应本文档</summary>
        public string? MatchPrompt { get; set; }

        /// <summary>【要素5b】填充提示词：不可直接结构化的段落如何组织撰写</summary>
        public string? SynthesisPrompt { get; set; }

        /// <summary>
        ///     【固定文档】指纹规则集：
        ///     <c>{"fileNames":[],"regex":[],"keyFields":[{"name":"","pattern":""}],"minScore":0.8}</c>
        /// </summary>
        public string? FingerprintJson { get; set; }

        /// <summary>标准文件名正则/通配（匹配侧兜底，主用 <c>cert_standard_directory_file.FilePattern</c>）</summary>
        [StringLength(200)]
        public string? FileNamePattern { get; set; }

        /// <summary>召回关键词，逗号分隔（人工可维护，提升 R3 召回率）</summary>
        [StringLength(500)]
        public string? Keywords { get; set; }

        /// <summary>自动确认阈值（NULL = 用全局默认 <c>ent_norm_auto_threshold=0.85</c>）</summary>
        public decimal? AutoConfirmThreshold { get; set; }

        /// <summary>人工复核下限，低于此值直接判不匹配（NULL = 全局默认 <c>ent_norm_review_threshold=0.50</c>）</summary>
        public decimal? ReviewThreshold { get; set; }

        /// <summary>draft=草稿 / active=生效 / archived=归档</summary>
        [StringLength(20)]
        public string Status { get; set; } = "draft";

        /// <summary>关联提取规则 Code → <c>cert_doc_extraction_rule.Code</c>（有则契约 3/4 号要素存在）</summary>
        [StringLength(36)]
        public string? RuleCode { get; set; }

        // ──── 语义分析列（标签 + 作用的结构化伴生产出，33 号 §五）────

        /// <summary>
        ///     标签数组 JSON：<c>["RecordInternalAudit"]</c>
        ///     <para>每个值 ⛔ 必须 ∈ <c>cert_tag_dict.TagCode</c>，越界置 OTHER 并记 <see cref="AnalyzeMessage"/>（整单不失败）。</para>
        /// </summary>
        public string? TagsJson { get; set; }

        /// <summary>标签来源：ai / manual / carried</summary>
        [StringLength(10)]
        public string? TagsSource { get; set; }

        /// <summary>打标签的理由（LLM 返回，人工复核用）</summary>
        [StringLength(500)]
        public string? TagsReason { get; set; }

        /// <summary>标签置信度 0.00~1.00</summary>
        public decimal? TagsConfidence { get; set; }

        /// <summary>作用来源：ai / manual / carried</summary>
        [StringLength(10)]
        public string? DocPurposeSource { get; set; }

        /// <summary>作用置信度 0.00~1.00</summary>
        public decimal? DocPurposeConfidence { get; set; }

        /// <summary>
        ///     ★【要素2 第四段 · 机器读】包含信息结构化清单：
        ///     <c>[{"Name":"统一社会信用代码","Required":true,"Hint":"18位"}]</c>
        ///     <para>供 04 号 R3 倒排召回与覆盖度计算；与 <see cref="DocPurpose"/>（人读）成对存在。</para>
        /// </summary>
        public string? InfoItemsJson { get; set; }

        // ──── 分析元数据（可观测 + 可追溯 + 可重跑，33 号 §五）────

        /// <summary>
        ///     语义分析状态：<c>pending</c> / <c>running</c> / <c>completed</c> / <c>partial</c> /
        ///     <c>failed</c> / <c>manual</c>（人工直接写入）。
        ///     <para>★ <c>partial</c>（2026-10-05 新增）= 两跳提示词（<c>doc_group</c> / <c>doc_content</c>）
        ///     只成功了其中一跳，结论<b>部分可用</b>；失败的那一跳的原因记在 <see cref="AnalyzeMessage"/>。
        ///     ⛔ 不要把它归并进 <c>failed</c> —— 那会让「标签拿到了、作用没拿到」和「什么都没拿到」
        ///     变成同一种状态，人工无从判断该不该重跑。</para>
        /// </summary>
        [StringLength(20)]
        public string AnalyzeStatus { get; set; } = "pending";

        /// <summary>分析失败原因或越界告警</summary>
        [StringLength(1024)]
        public string? AnalyzeMessage { get; set; }

        /// <summary>本次分析实际使用的模型名（快照，不跟随 <c>cert_sys_config.ai_model_name</c> 变动）</summary>
        [StringLength(50)]
        public string? ModelName { get; set; }

        /// <summary>使用的提示词 PromptCode（<c>wf_prompt_template.PromptCode</c>）</summary>
        [StringLength(64)]
        public string? PromptCode { get; set; }

        /// <summary>提示词版本（<c>wf_prompt_template.Version</c>）</summary>
        public int? PromptVersion { get; set; }

        /// <summary>输入 Token 数</summary>
        public int? PromptTokens { get; set; }

        /// <summary>输出 Token 数</summary>
        public int? CompletionTokens { get; set; }

        /// <summary>耗时（毫秒）</summary>
        public int? DurationMs { get; set; }

        /// <summary>是否被人工修正过（1 = 人工改过，批量重跑 ⛔ 不得覆盖）</summary>
        public bool IsManualCorrected { get; set; }

        /// <summary>最近一次分析完成时间</summary>
        public DateTime? AnalyzeTime { get; set; }

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
