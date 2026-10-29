using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Shared.Entities.Cert;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// <b>「按标准提取条款」提示词</b> —— 读 <c>cert_prompt_template</c>（<c>PromptCode = doc_clause_extract</c>）
    /// + 占位符渲染 + 代码内兜底正文。
    ///
    /// <para><b>★★ 为什么提示词必须落库、且必须能被覆盖</b>：提取质量 = 提示词质量。
    /// 换一份标准（iso9001 → iso13485）时条款语法差异很大（见 24 号 §十二 208 条叶子语法实测），
    /// 靠改代码发版调提示词不可行。库里有行 ⇒ 以库为准；没有 ⇒ 用
    /// <see cref="FallbackTemplate"/>（与 <c>scripts/db/20261029_doc_clause_extract_prompt_V1.sql</c> 逐字一致）。</para>
    ///
    /// <para><b>★ 四条硬规则（写进提示词正文，缺一条产物就不可用）</b>：
    /// <list type="number">
    ///   <item><b>只输出有实质内容的条款</b> —— 文档里没写就<b>不出现</b>，⛔ 不许为凑数编。</item>
    ///   <item><b>必带锚点</b> —— 每段摘录必须能在原文里逐字定位（否则审核员无法复核）。</item>
    ///   <item><b>⛔ 不判符合性</b> —— 只产「读到什么」，不写「符合/不符合/充分/缺失」。</item>
    ///   <item><b>编号逐字取自清单</b> —— ⛔ 不许模型自己造编号或改写格式。</item>
    /// </list></para>
    ///
    /// <para><b>⚠️ 条款清单只给 <c>DocCheckMode ≠ none</c> 的条款</b>（见
    /// <see cref="ClauseExtractExecutor"/>）：把不需要资料核对的条款也塞进来，模型会为它们硬凑内容，
    /// 直接拉低锚点率。<b>清单的组装在本类之外</b>（本类只做渲染），因为「哪些条款」是业务判据不是文案。</para>
    /// </summary>
    public sealed class DocExtractPromptService
    {
        /// <summary>提示词代码（<c>cert_prompt_template.PromptCode</c>）</summary>
        public const string PromptCode = "doc_clause_extract";

        /// <summary>提示词版本（留痕到 <c>cert_document_extraction.PromptVersion</c>）</summary>
        public const string PromptVersion = "v1";

        public const string PhStandardName = "{{standard_name}}";
        public const string PhStandardCode = "{{standard_code}}";
        public const string PhClauseList = "{{clause_list}}";
        public const string PhDocumentName = "{{document_name}}";
        public const string PhDocumentContent = "{{document_content}}";

        private readonly IDbOrm _db;

        public DocExtractPromptService(IDbOrm db)
        {
            _db = db;
        }

        /// <summary>
        /// 读提示词正文：<b>标准级 → 平台级</b> 两级回退。
        ///
        /// <para>⚠️ 返回的 <c>Scope</c> 必须被调用方记录下来（<c>stored</c>/<c>fallback</c>）——
        /// 「用的是兜底正文」等于「库里改的正文没生效」，用户会以为模型不行。</para>
        /// </summary>
        public async Task<(string Template, string Scope, string? StandardScopeKey)> ResolveAsync(
            string? standardShortCode, CancellationToken ct = default)
        {
            var all = await _db.Client.Queryable<CertPromptTemplateRow>()
                .Where(x => x.PromptCode == PromptCode && !x.IsDeleted && x.IsValid == 1
                            && x.OrgCode == null)
                .ToListAsync();

            var rows = all ?? new List<CertPromptTemplateRow>();

            // ① 标准级（ScopeKey = 标准短码，如 iso13485）
            if (!string.IsNullOrWhiteSpace(standardShortCode))
            {
                var hit = rows.FirstOrDefault(x =>
                    string.Equals(x.ScopeKey, standardShortCode, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(x.Template));
                if (hit != null) return (hit.Template!, "stored", hit.ScopeKey);
            }

            // ② 平台级（ScopeKey 为空）
            var platform = rows.FirstOrDefault(x =>
                string.IsNullOrWhiteSpace(x.ScopeKey) && !string.IsNullOrWhiteSpace(x.Template));
            if (platform != null) return (platform.Template!, "stored", null);

            // ③ 库中缺行 ⇒ 代码兜底（⛔ 不抛异常：兜底正文是可用正文，不是错误）
            return (FallbackTemplate, "fallback", null);
        }

        /// <summary>
        /// 渲染最终提示词（纯函数，可单测）。
        /// <para>⚠️ 用 <see cref="StringComparison.Ordinal"/> 替换：提示词里的占位符是 ASCII，
        /// 走文化相关比较在某些区域设置下会漏替换且不报错。</para>
        /// </summary>
        public static string Render(
            string template, string standardName, string standardShortCode,
            string documentName, string clauseList, string documentContent)
        {
            return (template ?? string.Empty)
                .Replace(PhStandardName, standardName ?? string.Empty, StringComparison.Ordinal)
                .Replace(PhStandardCode, standardShortCode ?? string.Empty, StringComparison.Ordinal)
                .Replace(PhClauseList, clauseList ?? string.Empty, StringComparison.Ordinal)
                .Replace(PhDocumentName, documentName ?? string.Empty, StringComparison.Ordinal)
                .Replace(PhDocumentContent, documentContent ?? string.Empty, StringComparison.Ordinal);
        }

        /// <summary>
        /// 条款清单 → 提示词里的编号表（<b>纯函数</b>）。
        ///
        /// <para>格式：<c>- 4.2.4 医疗器械文件控制</c>，一行一条，按条款号自然序。
        /// ⛔ 不加序号、不加分类、不加任何「该条款要不要查」的提示 —— 清单只提供<b>可选编号</b>，
        /// 加判据会让模型把清单当成答案去抄。</para>
        /// </summary>
        public static string BuildClauseList(IEnumerable<(string Number, string Title)> clauses)
        {
            var sb = new StringBuilder();
            foreach (var c in clauses ?? Enumerable.Empty<(string, string)>())
            {
                var num = (c.Number ?? string.Empty).Trim();
                if (num.Length == 0) continue;
                var title = (c.Title ?? string.Empty).Trim();
                sb.Append("- ").Append(num);
                if (title.Length > 0) sb.Append(' ').Append(title);
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 库中缺行时的兜底正文（<b>与 <c>scripts/db/20261029_doc_clause_extract_prompt_V1.sql</c> 逐字一致</b>）。
        /// <para>⚠️ 兜底<b>不是</b>第二份可编辑副本：只在库里缺行时使用，保存后即以库为准。</para>
        /// </summary>
        public const string FallbackTemplate = @"你是 ISO 管理体系认证的资深审核技术专家。你的任务：读一份企业提供的文档，判断这份文档里**实际写到了**下列哪些条款，并逐条摘录原文。

## 输入

- 标准：{{standard_name}}（{{standard_code}}）
- 文档：{{document_name}}
- 可命中的条款清单（编号 + 标题，共若干条）：
{{clause_list}}

- 文档全文（Markdown）：
<<<DOC
{{document_content}}
DOC

## 输出

只输出一个 JSON 对象，不要 markdown 代码块，不要任何解释文字：

{
  ""clauses"": [
    {
      ""ClauseNumber"": ""4.2.4"",
      ""Title"": ""医疗器械文件控制"",
      ""Anchor"": ""原文中能定位这段的连续片段，10~40 字，逐字摘录"",
      ""Excerpts"": [""原文摘录第一段"", ""原文摘录第二段""],
      ""Confidence"": 0.9
    }
  ]
}

## 硬规则（违反任何一条，本次输出作废）

① **只输出有实质内容的条款**。文档里没写、只是目录里列了个标题、或只有一句「见某某程序」而没有实际内容 ⇒ **不要出现在结果里**。⛔ 不许为了凑数编造条款。

② **每一条都必须带 `Anchor`**。`Anchor` 必须是文档原文里**逐字存在**的连续片段（10~40 字），用于把结果定位回原文。⛔ 不许改写、不许概括、不许自己编一句话当锚点。找不到这样的原文片段 ⇒ 这条不要输出。

③ **⛔ 绝对不许判断符合性**。不写「符合」「不符合」「充分」「缺失」「满足要求」「需要改进」。你只负责回答「这份文档里**读到了什么**」。判断是审核员的事，你写了就是污染结论。

④ **`ClauseNumber` 必须逐字取自上面的条款清单**。⛔ 不许自造编号、⛔ 不许改写格式（不要写「第4.2.4条」、不要写「4.2.4.1」除非清单里就有它）。

## 其它要求

- `Excerpts` 每条 20~200 字，按文档中出现的顺序；同一段不要重复摘录。
- `Confidence` 取 0~1：原文明确对应条款要求 ⇒ 0.8 以上；只是相关但不完整 ⇒ 0.4~0.7；勉强沾边 ⇒ 0.4 以下。
- 文档是别的体系（如财务制度、人事花名册）与条款清单无关 ⇒ 返回 `{ ""clauses"": [] }`，这是合法输出。
- 所有文本用简体中文（原文是英文就照抄英文，不要翻译）。
- 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。";
    }
}
