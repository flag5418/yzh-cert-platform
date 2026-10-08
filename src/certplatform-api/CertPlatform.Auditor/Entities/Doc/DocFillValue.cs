using SqlSugar;
using YZH.Core.Stand.Attributes;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Entity;

namespace CertPlatform.Auditor.Entities.Doc
{
    /// <summary>
    ///     文档填充取值账本（表 <c>cert_doc_fill_value</c>）—— <b>单元格级</b>
    ///     <para>
    ///         一次填充中，<b>每一个锚点</b>（含表格区域的每一个单元格）的
    ///         「<b>值 + 来源 + 证据 + 可信度 + 人工覆盖</b>」五元组。
    ///     </para>
    ///     <para>
    ///         <b>端归属</b>：专家端（<c>CertPlatform.Auditor</c>）——
    ///         写入方是编排器 <c>Services/Ent/Normalize/DocumentFillOrchestrator</c>，
    ///         读取方是规范化页面与审计抽屉，消费者<b>全部在 Auditor</b>（55 §1.9 表 A）。
    ///     </para>
    ///
    ///     <para><b>DDL 权威</b>：<c>scripts/db/20261006_doc_normalize_V1.sql</c> 与
    ///     [<c>参考/41-01-数据模型与两个口径-V1.md</c> §一·1] —— <b>两处必须同步维护，⛔ 不得各改各的</b>。</para>
    ///
    ///     <para><b>★ 与两张邻近表的关系（⛔ 极易混，先读这段）</b>：</para>
    ///     <list type="bullet">
    ///         <item><c>cert_doc_fill_log</c> = <b>文件级汇总</b>（1 行/文件）。它的每一个计数都能由本表聚合出来
    ///         ⇒ <b>本表是唯一真相源，log 是物化的汇总</b>（列表页读 log，⛔ 不做实时聚合）。</item>
    ///         <item><c>cert_doc_ai_suggestion</c> = <b>AI 候选池</b>（多候选 + 人工选）。
    ///         建议值从候选池<b>选中后</b>才写进本表。</item>
    ///     </list>
    ///
    ///     <para><b>★★ 为什么必须有（41-00 §二·缺口 2）</b>：25 号实测 <b>98.6%</b> 的锚点是
    ///     确定性来源（编号 444 / 日期 472 / 人名 315 / 公司名 22），而
    ///     <c>cert_doc_ai_suggestion</c> 只管 AI ⇒ <b>98.6% 的取值原本没有账本</b>，
    ///     「查看数据来源 / 改数据来源 / 全部重写」三个动作一个都做不了。</para>
    ///
    ///     <para><b>★ 覆盖全来源</b>：⛔ 不是只有 AI —— <c>global</c>/<c>self</c>/<c>profile</c>/
    ///     <c>compute</c>/<c>ai</c>/<c>manual</c> 六类都要有账。</para>
    /// </summary>
    [SugarTable("cert_doc_fill_value")]
    [YZHDeleteStrategy(Mode = DeleteMode.Soft)]
    public class DocFillValue : BaseEntity, ISoftDelete, IIsValid
    {
        // ──── Id / Code / CreateTime / CreateBy / UpdateTime / UpdateBy 由 BaseEntity 提供 ────

        // ════════ 身份段（P13：NOT NULL DEFAULT ''，⛔ 禁 NULL）════════

        /// <summary>认证机构 Code（冗余，便于按机构查）</summary>
        [SugarColumn(Length = 36)]
        public string OrgCode { get; set; } = string.Empty;

        /// <summary>★ 企业 Code → <c>cert_enterprise.Code</c></summary>
        [SugarColumn(Length = 36)]
        public string EnterpriseCode { get; set; } = string.Empty;

        /// <summary>★ 阶段 Code → <c>cert_cert_stage.Code</c></summary>
        [SugarColumn(Length = 36)]
        public string StageCode { get; set; } = string.Empty;

        /// <summary>★ 标准 Code → <c>cert_iso_standard.Code</c></summary>
        [SugarColumn(Length = 36)]
        public string StandardCode { get; set; } = string.Empty;

        // ════════ 宿主（三个维度定位一行）════════

        /// <summary>★ 目标标准文档 → <c>cert_standard_directory_file.Code</c></summary>
        [SugarColumn(Length = 36)]
        public string StandardFileCode { get; set; } = string.Empty;

        /// <summary>模板 → <c>cert_doc_template.Code</c>（<b>固定文档</b>时留空）</summary>
        [SugarColumn(Length = 36)]
        public string TemplateCode { get; set; } = string.Empty;

        /// <summary>
        ///     ★ 锚点 → <c>cert_doc_template_anchor.Code</c>。
        ///     <para>⚠️ <b>固定文档时 = 该标准文档的「标准侧 Code」</b>
        ///     （固定文档无空白模板 ⇒ 无锚点行，见 41-01 §1.1.1）。</para>
        /// </summary>
        [SugarColumn(Length = 36)]
        public string AnchorCode { get; set; } = string.Empty;

        /// <summary>本次填充留痕 → <c>cert_doc_fill_log.Code</c></summary>
        [SugarColumn(Length = 36)]
        public string FillLogCode { get; set; } = string.Empty;

        // ════════ ① 值 ════════

        /// <summary>值类型 <c>text</c>/<c>number</c>/<c>date</c>/<c>bool</c>/<c>enum</c>（与锚点 <c>ValueType</c> 同口径）</summary>
        [SugarColumn(Length = 20)]
        public string ValueType { get; set; } = "text";

        /// <summary>文本值（<c>ValueType=text</c> 时用）</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? ValueText { get; set; }

        /// <summary>数值（<c>ValueType=number</c> 时用）</summary>
        [SugarColumn(IsNullable = true)]
        public decimal? ValueNumber { get; set; }

        /// <summary>日期（<c>ValueType=date</c> 时用）</summary>
        public DateTime? ValueDate { get; set; }

        /// <summary>
        ///     ★ 按类型格式化后的展示值（120 字符截断）。
        ///     <para>界面<b>直接用它</b>，⛔ <b>不在前端二次格式化</b> —— 否则同一份数据会出现两套显示口径。</para>
        /// </summary>
        [SugarColumn(Length = 500)]
        public string ValueDisplay { get; set; } = string.Empty;

        // ════════ ② 来源（D1 取值来源，见 22 号 §四）════════

        /// <summary>
        ///     ★ <c>global</c>/<c>self</c>/<c>profile</c>/<c>compute</c>/<c>ai</c>/<c>manual</c>
        ///     —— D1 取值来源（<c>sibling</c> 一期不用）。
        /// </summary>
        [SugarColumn(Length = 20)]
        public string SourceKind { get; set; } = string.Empty;

        /// <summary>★ 人话来源标签（如「企业基础信息 · 企业全称」），列表页直接显示</summary>
        [SugarColumn(Length = 200)]
        public string SourceLabel { get; set; } = string.Empty;

        /// <summary>
        /// ★ <b>AI 匹配来源的企业文件 Code</b>（§3 全量留痕，2026-10-07）。
        /// <para>当 <c>SourceKind='ai'</c> 且值由「两层过滤 + 读 Markdown 抽取」自动填时，
        /// 记录匹配到的是<b>哪个企业文件</b>（<c>cert_enterprise_original_file.Code</c>）；
        /// 非 AI 来源（global / manual / profile）本列留空串。⛔ 100% 可信度也记（审核可追溯 / 可改源）。</para>
        /// </summary>
        [SugarColumn(Length = 36, IsNullable = false)]
        public string SourceFileCode { get; set; } = string.Empty;

        /// <summary>
        ///     ★ 完整来源链 <c>{paramCode, profileCode, originalFileCode, fieldPath, pageHint, tableHint}</c>
        ///     —— 供「查看来源」下钻（证据链 5 级）。
        /// </summary>
        [SugarColumn(ColumnDataType = "json", IsNullable = true)]
        public string? SourceDetailJson { get; set; }

        // ════════ ③ 证据 ════════

        /// <summary>证据原文片段（≤500 字），一键可看</summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? EvidenceText { get; set; }

        /// <summary>证据位置提示（如 <c>P3</c> / 第2段 / 表2行3），⛔ 不做跨文档双向定位</summary>
        [SugarColumn(Length = 50, IsNullable = true)]
        public string? EvidencePageHint { get; set; }

        // ════════ ④ 可信度（★ 单元格级，口径见 54 §4.5）════════

        /// <summary>
        ///     ★ 单元格级可信度 0.00~1.00。
        ///     <para><b>R-C1：恒 NOT NULL</b>（与 <c>cert_extraction_result.Confidence</c> 的可空口径<b>相反</b>）
        ///     —— 可空 = 「不知道」，但「从全局参数取值」这件事本身就是确定的，
        ///     留空会让 <b>72%</b> 的行显示「未知」，界面没法用。</para>
        ///     <para>确定性来源（<c>global</c>/<c>self</c>/<c>compute</c>/<c>manual</c>）恒 <b>1.00</b>；
        ///     <c>profile</c> 取画像分；<c>ai</c> 取模型分；<b>未知来源兜底 0.50</b>（⛔ 不是 1.00）。</para>
        /// </summary>
        public decimal Confidence { get; set; } = 1.00m;

        /// <summary>可信度依据（如「来源为 AI 建议，未人工确认」）；<c>1.00</c> 时留空</summary>
        [SugarColumn(Length = 200)]
        public string ConfidenceReason { get; set; } = string.Empty;

        // ════════ ⑤ 人工覆盖（★「更改数据来源」「全部重写」的落点）════════

        /// <summary>★ 是否被人工改过值或改过来源（<b>改来源不改值</b>时，本行值不变但本位置 1）</summary>
        public bool IsOverridden { get; set; }

        /// <summary>人工干预类型 <c>value</c>=只改值 / <c>source</c>=只改来源 / <c>both</c>=两者都改</summary>
        [SugarColumn(Length = 20)]
        public string OverrideKind { get; set; } = string.Empty;

        /// <summary>改的理由（审计要求：⛔ 改值必须填理由）</summary>
        [SugarColumn(Length = 500, IsNullable = true)]
        public string? OverrideReason { get; set; }

        /// <summary>干预人 Code</summary>
        [SugarColumn(Length = 64, IsNullable = true)]
        public string? OverriddenBy { get; set; }

        /// <summary>干预时间</summary>
        public DateTime? OverriddenTime { get; set; }

        /// <summary>
        ///     ★ 是否「<b>钉住</b>」—— 「全部重写」时 <b>⛔ 跳过本行</b>（人工确认过的值不被自动覆盖）。
        ///     <para><b>与 <see cref="IsOverridden"/> 的区别</b>：<c>IsOverridden</c> = 「值或来源被人工改过」
        ///     （<b>记录事实</b>）；<c>IsPinned</c> = 「重写时不要动它」（<b>表达指令</b>）。
        ///     二者<b>可同时为 1</b>（人工改了值，并要求以后保留）。</para>
        ///     <para><b>落库时机</b>：点「更新文档」时（⛔ 暂存阶段只写内存 —— 编辑三缓冲）。</para>
        ///     <para><b>动作留痕</b>：<c>cert_doc_normalize_action</c>（<c>ActionType='pin'</c>/<c>'unpin'</c>）。</para>
        /// </summary>
        public bool IsPinned { get; set; }

        // ════════ ⑥ 状态 ════════

        /// <summary>
        ///     <c>filled</c>=已写入 / <c>pending</c>=待办（无值） /
        ///     <c>kept_as_is</c>=未命中且保留原文 / <c>removed</c>=按 <c>remove</c> 清空。
        ///     <para>⚠️ <b>只有 <c>filled</c> 计入完成率分子</b>；<c>kept_as_is</c> 与 <c>pending</c> 都不算「已写入」。</para>
        /// </summary>
        [SugarColumn(Length = 20)]
        public string FillStatus { get; set; } = "filled";

        /// <summary>写入方式 <c>replace</c>/<c>overwrite</c>/<c>append</c>/<c>remove</c>（快照自锚点，便于审计回放）</summary>
        [SugarColumn(Length = 20)]
        public string WriteMode { get; set; } = "overwrite";

        /// <summary>
        ///     ★ <b>替换前的原文快照</b>（<c>WriteMode=replace</c> 时必落）
        ///     —— 支撑差异比对与回滚；⚠️ 这也是 25 号 Q-5「模板示例数据」的清理依据。
        /// </summary>
        [SugarColumn(ColumnDataType = "text", IsNullable = true)]
        public string? OriginalText { get; set; }

        // ════════ ⑦ 位置（界面定位用）════════

        /// <summary>
        ///     位置类别 <c>body</c>/<c>table_cell</c>/<c>header</c>/<c>excel_cell</c>/
        ///     <c>excel_region</c>/<c>word_table_region</c>（对齐 <c>FillLocationKind</c>）。
        /// </summary>
        [SugarColumn(Length = 20)]
        public string LocationKind { get; set; } = string.Empty;

        /// <summary>可读位置（如「正文·第 12 段」/「Sheet1!B7」）</summary>
        [SugarColumn(Length = 200)]
        public string LocationDesc { get; set; } = string.Empty;

        /// <summary>锚点序号（来自锚点表 <c>Sort</c>，保证展示顺序稳定）</summary>
        public int Sort { get; set; }

        // ════════ 审计段 ════════

        [SugarColumn(Length = 500, IsNullable = true)]
        public string? Remark { get; set; }

        // ──── ISoftDelete + IIsValid 接口显式实现（⛔ 必须声明在实体自身，否则全库过滤静默失效）────
        public bool IsDeleted { get; set; }
        public string? DeleteBy { get; set; }
        public DateTime? DeleteTime { get; set; }

        /// <summary>★ 1 有效 / 0 无效（铁律九，⛔ 禁 <c>Enable</c>）</summary>
        public int IsValid { get; set; } = 1;
    }
}
