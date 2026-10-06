using System.Collections.Generic;

namespace CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai
{
    /// <summary>
    /// <b>待填锚点</b> —— 批量装配（<c>AiFillPromptBuilder.BuildBatchAsync</c>）时的一条。
    ///
    /// <para><b>★ 为什么「清单」必须由锚点表给，⛔ 不由提示词手写</b>（42 号 §3.3 第 3 条）：
    /// 若靠提示词手写锚点，<b>漏一个锚点不会报错</b> —— AI 不返回它，它就永远空着，
    /// 而「一键看证据摘要」里也看不出漏了。<see cref="AnchorCode"/> 与
    /// <c>fields.{anchor_code}</c> 逐字对应，是值能落到正确格子的<b>唯一凭据</b>。</para>
    ///
    /// <para><b>★ <see cref="Instruction"/> 的来源（D11 尚未裁决，故此处刻意留回退链）</b>：
    /// 39 号 §7.1 定义它是「<b>模板里这一项的填写说明</b>」⇒ 最自然的来源是模板正文上下文。
    /// 但 <c>cert_doc_template_anchor</c> 目前<b>没有这一列</b>（33 列里只有 <c>Remark</c> 算半个）。
    /// ⇒ 装配器按 <b>显式传入 → 锚点 Remark → FieldCode 去前缀</b> 回退，
    /// 使 D11 无论怎么裁，<b>本类与 3 个 Skill 都不需要改</b>。</para>
    /// </summary>
    public sealed class AiFillAnchorSpec
    {
        /// <summary>锚点编码（与文档 <c>{{ }}</c> 内文本去前缀后<b>逐字一致</b>）</summary>
        public string AnchorCode { get; set; } = string.Empty;

        /// <summary>这一格要填什么（人话说明，进「待填锚点」清单给模型看）</summary>
        public string Instruction { get; set; } = string.Empty;

        /// <summary>
        /// 值类型：<c>text</c> / <c>number</c> / <c>date</c> / <c>bool</c> / <c>enum</c>。
        /// <para>★ 39 号 §7.3：<b>类型由锚点属性给，⛔ 不由 AI 猜</b> —— Excel 传错
        /// <c>SetCellValue</c> 重载<b>不报错但结果错</b>。故此处是<b>输入</b>，模型只负责「值是什么」。</para>
        /// </summary>
        public string ValueKind { get; set; } = "text";

        /// <summary>格式串（<b>.NET 方言</b>，见 39 号 §十六）；空 = 不格式化</summary>
        public string? NumberFormat { get; set; }

        /// <summary>
        /// 归属段：<c>semantic</c> / <c>field</c> / <c>table</c>。
        /// <para>对应输出 JSON 的 <c>semantic</c> / <c>fields</c> / <c>tables</c> 三段（39 号 §12.5），
        /// 编排器按段分派给 3 个 Skill 做校验。</para>
        /// </summary>
        public string Section { get; set; } = "field";

        /// <summary>
        /// ★ 提示词组（41 号原型 V6）：支持按自定义组进行批量 AI 提取。
        /// <para>如果为空，则默认按 <see cref="Section"/> 分组。</para>
        /// </summary>
        public string? PromptGroup { get; set; }

        /// <summary>表格列定义（仅 <c>Section=table</c> 时有值；列顺序 = 写入列序）</summary>
        public List<AiFillColumnSpec>? Columns { get; set; }

        /// <summary>
        /// ★ 待改写的<b>标准原文</b>（仅 <c>Section=semantic</c> 用）。
        ///
        /// <para>39 号 §6.1：<c>src_semantic</c> 的职责是「把<b>模板里的标准原文</b>按企业实际情况改写」
        /// ⇒ 原文是它的<b>核心输入</b>，没有它这个 Skill 无从下手。</para>
        ///
        /// <para>⚠️ 原文<b>不来自锚点表</b>（锚点表只有 token 位置，没有段落正文）——
        /// 由编排器在扫描模板时<b>连同上下文一起带出</b>，或由调用方显式传入。</para>
        /// </summary>
        public string? SourceText { get; set; }
    }

    /// <summary>表格的一列定义（仅 <c>src_ai_table</c> 用）</summary>
    public sealed class AiFillColumnSpec
    {
        /// <summary>字段编码（与表格列一一对应）</summary>
        public string FieldCode { get; set; } = string.Empty;

        /// <summary>列标题（给模型看的语义名）</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>值类型：text / number / date / bool / enum</summary>
        public string ValueKind { get; set; } = "text";

        /// <summary>格式串（.NET 方言）</summary>
        public string? NumberFormat { get; set; }
    }

    /// <summary>
    /// <b>一次 AI 填充调用</b>的请求 —— 与 <c>LlmInvokeRequest</c> 的分工：
    /// 本类只管「提示词 + 期望结构」，<b>不碰</b>端点 / 密钥 / 模型（那些由 <c>AiFillInvoker</c> 从
    /// <c>cert_sys_config</c> 六键注入）。
    /// </summary>
    public sealed class AiFillInvokeRequest
    {
        /// <summary>系统提示词（角色设定）</summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>用户提示词（模板已渲染）</summary>
        public string UserPrompt { get; set; } = string.Empty;

        /// <summary>期望输出结构（JSON Schema 文本，用于校验 AI 返回）</summary>
        public string? OutputSchema { get; set; }

        /// <summary>
        /// ★ 低温保证可复现（39 号 §12.1）。默认 <c>0</c> —— 填充是「取事实」不是「创作」，
        /// 同一份资料跑两次应得同样的值。
        /// </summary>
        public decimal Temperature { get; set; }

        /// <summary>★ 默认 8192 —— 4096 会在字符串中间截断 ⇒ JSON 解析失败（LlmExtractSkill 的教训）</summary>
        public int MaxTokens { get; set; } = 8192;
    }

    /// <summary><b>一次 AI 填充调用</b>的结果</summary>
    public sealed class AiFillInvokeResult
    {
        /// <summary>是否成功（含 JSON 解析成功）</summary>
        public bool Success { get; set; }

        /// <summary>失败原因（<c>Success=false</c> 时有值）</summary>
        public string? Error { get; set; }

        /// <summary>★ 深转换后的根对象（<c>JsonElement</c> 已转成 <c>Dictionary</c> / <c>List</c> / 基元）</summary>
        public Dictionary<string, object>? Root { get; set; }

        /// <summary>模型原始文本输出（排查用）</summary>
        public string RawText { get; set; } = string.Empty;

        /// <summary>实际使用的模型名（快照）</summary>
        public string Model { get; set; } = string.Empty;

        public int? PromptTokens { get; set; }

        public int? CompletionTokens { get; set; }

        public long DurationMs { get; set; }
    }
}
