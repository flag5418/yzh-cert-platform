using SqlSugar;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Doc
{
    /// <summary>
    /// 企业资料画像（表 <c>cert_enterprise_doc_profile</c>）—— 36 号 §3.4 表④，L3 层
    /// <para><b>端归属</b>：专家端独占（企业原始资料分析的产物）。</para>
    ///
    /// <para><b>DDL 权威</b>：[<c>33-文档语义规则设计-V1.md</c> §5.4] 与
    /// <c>scripts/db/20261003_enterprise_original_V1.sql</c> —— <b>两处必须同步维护，⛔ 不得各改各的</b>。
    /// 本实体 2026-10-03 随该表首次落库而建（此前 DDL 只存在于 md）。</para>
    ///
    /// <para><b>只追加哲学</b>：<c>uk_file_std_ver(OriginalFileCode, StandardCode, ProfileVersion)</c> + <c>IsLatest</c> ——
    /// 换文件 ⇒ <c>ProfileVersion+1</c>、旧版 <c>IsLatest=0</c> 保留可审计（26 号 A-4）。</para>
    ///
    /// <para><b>★★★ M6（2026-10-06）唯一键扩标准</b>：一个文件 <b>× 每个标准 = 一行</b>
    /// ⇒ 版本号 <c>ProfileVersion</c> <b>按 (文件, 标准) 各自递增</b>，<c>IsLatest</c> 也只置同一标准内的旧行。
    /// 迁移脚本：<c>scripts/db/20261006_m6_profile_uk_standard_V1.sql</c>（旧 <c>uk_file_ver</c> 已删）。
    /// ⚠️ 依据：原 <c>uk_file_ver(OriginalFileCode, ProfileVersion)</c> 会让标准 B 顶掉标准 A 的画像行。</para>
    ///
    /// <para><b>与标准侧对称</b>：<see cref="TagsJson"/> / <see cref="TagsSource"/> / <see cref="TagsReason"/> /
    /// <see cref="TagsConfidence"/> / <see cref="DocPurpose"/> / <see cref="InfoItemsJson"/> /
    /// <see cref="DocPurposeSource"/> / <see cref="DocPurposeConfidence"/> 与
    /// <c>cert_standard_doc_contract</c> <b>逐字对称</b> ⇒ 两侧共用一个 DTO、一份 Service。</para>
    /// </summary>
    [SugarTable("cert_enterprise_doc_profile")]
    public class EnterpriseDocProfile : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / 审计字段由 BaseEntity + 接口统一提供 ────

        /// <summary>★ 宿主 → <c>EnterpriseOriginalFile.Code</c>（一文件一画像版本）</summary>
        [SugarColumn(Length = 36)]
        public string OriginalFileCode { get; set; } = string.Empty;

        /// <summary>企业 Code。⛔ 禁 NULL（P13）</summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>认证机构 Code</summary>
        [SugarColumn(Length = 36)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>阶段 Code（冗余）</summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>
        /// 标准 Code（<b>GUID</b>，冗余）；空 = 平台级 / 标准无关的原始资料。
        /// ⚠️ 与 <c>cert_tag_dict.StandardCodes</c>（存 <b>slug</b>）口径不同，
        /// 标签裁剪前必须显式做一次 <c>Code → cert_iso_standard.standard_code</c> 转换（36 号 §5.3）。
        /// </summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        /// <summary>文件原名（冗余）</summary>
        [SugarColumn(Length = 300)]
        public string FileName { get; set; } = string.Empty;

        // ──── 与标准侧完全对称的语义字段（两侧共用同一 DTO）───

        /// <summary>★ 受控标签多值（JSON 数组），值 ∈ <c>cert_tag_dict.TagCode</c></summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? TagsJson { get; set; }

        /// <summary><c>ai / manual / carried</c></summary>
        [SugarColumn(Length = 10, IsNullable = true)]
        public string? TagsSource { get; set; }

        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? TagsReason { get; set; }

        [SugarColumn(ColumnDataType = "decimal(3,2)", IsNullable = true)]
        public decimal? TagsConfidence { get; set; }

        /// <summary>★ 作用四段式：【是什么】【审核关注点】【来源口径】【包含信息】</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? DocPurpose { get; set; }

        /// <summary>★ 第四段结构化 <c>[{itemName,itemDesc,valueType,isKey}]</c></summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? InfoItemsJson { get; set; }

        /// <summary><c>ai / manual / carried</c></summary>
        [SugarColumn(Length = 10, IsNullable = true)]
        public string? DocPurposeSource { get; set; }

        [SugarColumn(ColumnDataType = "decimal(3,2)", IsNullable = true)]
        public decimal? DocPurposeConfidence { get; set; }

        // ──── 画像结论 ────

        /// <summary><c>fixed / editable / hybrid</c>（画像结论，与标签/受控字典是三套正交概念）</summary>
        [SugarColumn(Length = 20, IsNullable = true)]
        public string? DocCategory { get; set; }

        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? Summary { get; set; }

        /// <summary>★ 语义精要（长文档 AI 提取结果快照）</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? EssentialSummary { get; set; }

        /// <summary>关键词（逗号分隔，召回倒排）</summary>
        [SugarColumn(Length = 1000, IsNullable = true)]
        public string? Keywords { get; set; }

        /// <summary>AI 建议可服务的标准 Code 数组（JSON）</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? SuggestedStandardCodes { get; set; }

        /// <summary>画像整体置信度</summary>
        [SugarColumn(ColumnDataType = "decimal(3,2)", IsNullable = true)]
        public decimal? Confidence { get; set; }

        /// <summary>★ 画像版本（重跑递增，只追加）</summary>
        public int ProfileVersion { get; set; } = 1;

        /// <summary>★ 是否最新版本（查询过滤位）</summary>
        public bool IsLatest { get; set; } = true;

        /// <summary>AI 猜测类别（供召回排序）</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? TypeGuess { get; set; }

        /// <summary>字段数组（≤100 条）</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? FieldsJson { get; set; }

        /// <summary>表格数组（超 50 行截断）</summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? TablesJson { get; set; }

        /// <summary><c>pending / processing / completed / failed / skipped</c></summary>
        [SugarColumn(Length = 20)]
        public string ProfileStatus { get; set; } = "pending";

        [SugarColumn(Length = 1024, IsNullable = true)]
        public string? ProfileMessage { get; set; }

        /// <summary><c>ai / manual / fingerprint / rule / mixed</c></summary>
        [SugarColumn(Length = 20)]
        public string DetectSource { get; set; } = "ai";

        /// <summary>实际调用模型快照</summary>
        [SugarColumn(Length = 100, IsNullable = true)]
        public string? ModelName { get; set; }

        /// <summary>所用提示词 Code → <c>wf_prompt_template.Code</c></summary>
        [SugarColumn(Length = 36, IsNullable = true)]
        public string? PromptCode { get; set; }

        public int PromptVersion { get; set; } = 1;
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int DurationMs { get; set; }

        /// <summary>★ 分析输入 markdown 路径快照（③ 的产物）</summary>
        [SugarColumn(Length = 512, IsNullable = true)]
        public string? SourceMarkdownPath { get; set; }

        /// <summary>最近分析时间</summary>
        public DateTime? AnalyzeTime { get; set; }

        // ──── 人工修正留痕（D6 全量编辑）───

        /// <summary>人工干预标记（D6：标签 / 作用四段 / InfoItems / 策略 均可人工改）</summary>
        public bool IsManualCorrected { get; set; }

        [SugarColumn(Length = 64, IsNullable = true)]
        public string? CorrectedBy { get; set; }

        public DateTime? CorrectedTime { get; set; }

        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        // ──── ISoftDelete + IIsValid ────

        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }
        public int IsValid { get; set; } = 1;
    }
}