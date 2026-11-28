using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Auditor.Entities.Cert;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// <b>文档提取提示词服务</b> —— 读 <c>cert_prompt_template</c>（旧 <c>doc_clause_extract</c> +
    /// 画像 <c>doc_profile</c> + 5 条按模式提取提示词）+ 占位符渲染 + 代码内兜底正文。
    ///
    /// <para><b>★★ 为什么提示词必须落库、且必须能被覆盖</b>：提取质量 = 提示词质量。
    /// 换一份标准（iso9001 → iso13485）时条款语法差异很大（见 24 号 §十二 208 条叶子语法实测），
    /// 靠改代码发版调提示词不可行。库里有行 ⇒ 以库为准；旧 <c>doc_clause_extract</c> 没有 ⇒ 用
    /// <see cref="FallbackTemplate"/>（与 <c>scripts/db/20261029_doc_clause_extract_prompt_V1.sql</c> 逐字一致）。
    /// 新 5 条提示词没有代码兜底：缺行直接失败，避免“以为用了新提示词、实际跑旧正文”。</para>
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
        /// <summary>提示词代码（<c>cert_prompt_template.PromptCode</c>）—— 旧主链</summary>
        public const string PromptCode = "doc_clause_extract";

        /// <summary>5 条按模式提取提示词代码（与 <c>20261113_doc_prompt_templates_V1.sql</c> 对齐）</summary>
        public const string PromptCodeProfile = "doc_profile";
        public const string PromptCodeClauseFull = "doc_extract_clause_full";

        /// <summary>手册章节模式提示词代码（手册/方针整篇一次按章节组织）。</summary>
        public const string PromptCodeManualChapter = "doc_extract_manual_chapter";
        public const string PromptCodeProcessProfile = "doc_extract_process_profile";
        public const string PromptCodeRecordProfile = "doc_extract_record_profile";
        public const string PromptCodeIndex = "doc_extract_index";
        public const string PromptCodeAudit = "doc_extract_audit";

        /// <summary>文档「大章节归属」语义档提示词代码（批3-4：确定性档 + 关键词档均判不出时才调用）。</summary>
        public const string PromptCodeChapterDomain = "doc_chapter_domain";

        /// <summary>提示词版本（留痕到 <c>cert_document_extraction.PromptVersion</c>）</summary>
        public const string PromptVersion = "v2.1";

        public const string PhStandardName = "{{standard_name}}";
        public const string PhStandardCode = "{{standard_code}}";
        public const string PhClauseList = "{{clause_list}}";
        public const string PhDocumentName = "{{document_name}}";
        public const string PhDocumentContent = "{{document_content}}";
        public const string PhDocProfile = "{{doc_profile}}";
        public const string PhDocumentText = "{{document_text}}";

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
        public Task<(string Template, string Scope, string? StandardScopeKey)> ResolveAsync(
            string? standardShortCode, CancellationToken ct = default)
            => ResolveByCodeAsync(PromptCode, standardShortCode, ct);

        /// <summary>
        /// 按 <paramref name="promptCode"/> 读提示词正文：标准级 → 平台级 两级回退。
        /// <para>⚠️ 新 5 条提示词没有代码内兜底：库中缺行时返回空 Template + <c>missing</c>，
        /// 调用方必须显式失败，⛔ 不许静默退回旧 <c>doc_clause_extract</c>。</para>
        /// </summary>
        public async Task<(string Template, string Scope, string? StandardScopeKey)> ResolveByCodeAsync(
            string promptCode, string? standardShortCode, CancellationToken ct = default)
        {
            var code = (promptCode ?? string.Empty).Trim();
            if (code.Length == 0) return (string.Empty, "missing", null);

            var all = await _db.Client.Queryable<CertPromptTemplateRow>()
                .Where(x => x.PromptCode == code && !x.IsDeleted && x.IsValid == 1
                            // ★ 2026-11-11：平台级行的 OrgCode 存的是空串（列 NOT NULL 无默认）。
                            //   只写 == null 会永不命中 ⇒ 页面保存好的正文被忽略，静默走代码兜底。
                            && (x.OrgCode == null || x.OrgCode == ""))
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

            // ③ 旧主链允许代码兜底；新提示词缺行必须失败，避免“以为用了新提示词、实际跑旧正文”。
            if (code == PromptCode)
                return (FallbackTemplate, "fallback", null);
            // 章域判定是通用分类（与标准无关、无提取副作用），允许代码兜底，缺库行也能跑。
            if (code == PromptCodeChapterDomain)
                return (FallbackChapterDomainTemplate, "fallback", null);
            return (string.Empty, "missing", null);
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
        /// 渲染过程/记录/索引/审核 4 条模式提示词：只有 <c>{{doc_profile}}</c> 与 <c>{{document_text}}</c>。
        /// </summary>
        public static string RenderMode(string template, string docProfileJson, string documentText)
        {
            return (template ?? string.Empty)
                .Replace(PhDocProfile, docProfileJson ?? string.Empty, StringComparison.Ordinal)
                .Replace(PhDocumentText, documentText ?? string.Empty, StringComparison.Ordinal);
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
        /// 库中缺行时的兜底正文（<b>与 <c>scripts/db/fix/20261125_doc_clause_extract_slim_V1.sql</c> 逐字一致</b>）。
        /// <para>⚠️ 兜底<b>不是</b>第二份可编辑副本：只在库里缺行时使用，保存后即以库为准。</para>
        /// <para>★ 2026-11-25 精简：去掉画像判闸段与 doubts 段（<see cref="ExtractedDoc"/> 只消费
        /// <c>clauses[]</c>），本链现仅为 other / 画像退化时的兜底。</para>
        /// </summary>
        public const string FallbackTemplate = @"你是 ISO 管理体系认证的资深审核技术专家。任务：在指定标准下，把这份企业文档里**实际写到了**的条款逐条摘出来。

> 本提示词仅作**兜底**：文档画像（doc_profile）判为 other / 无法归类时才走本链；手册、程序、记录、清单、制度、作业指导书等已有各自的「按类型提取」提示词，不走这里。

## 输入

- 标准：{{standard_name}}（{{standard_code}}）
- 文档：{{document_name}}
- 可命中的条款清单（编号 + 标题，只能从这里选）：
{{clause_list}}

- 文档全文（Markdown）：
<<<DOC
{{document_content}}
DOC

## 输出（只输出一个 JSON 对象；第一个字符 {，最后一个字符 }；不要代码块、不要解释）

{
  ""clauses"": [
    {
      ""ClauseNumber"": ""8.4"",
      ""Title"": ""采购"",
      ""Anchor"": ""原文里逐字存在的连续片段，10~40 字"",
      ""Excerpts"": [""原文摘录第一段"", ""原文摘录第二段""],
      ""Confidence"": 0.9
    }
  ]
}

## 提取条款的四条硬规则（违反任何一条，本次输出作废）

① **只输出有实质内容的条款**。文档里没写、只在目录里列了标题、或只写""见某某程序""而没有实际内容 ⇒ 不出现。⛔ 不许为凑数编造条款。

② **每条必须带 Anchor**：必须是文档原文里**逐字存在**的连续片段（10~40 字），用于定位回原文。⛔ 不许改写、不许概括、不许自编一句当锚点；找不到 ⇒ 这条不要输出。

③ **⛔ 绝对不判符合性**：不写""符合/不符合/充分/缺失/满足要求/需要改进""。只回答""文档里读到了什么""，判断是审核员的事。

④ **ClauseNumber 必须逐字取自上面的条款清单**：⛔ 不许自造编号、⛔ 不许改写格式（不写""第8.4条""；清单里没有 8.4.1 就不写 8.4.1）。

## 提取粒度

- 按条款摘""规定了什么""，Excerpts 每条 20~200 字，按原文顺序，同段不重复。
- 记录/台账类：按**行**摘，一行一条 Excerpt，保留日期、记录编号、对象、结论词、签批；⛔ 不许把整张表拼成一句话，⛔ 不许抽掉日期/编号。
- 同一条款最多 6 段摘录；超过则只留最能说明问题的 6 段。
- Confidence 0~1：原文明确对应条款要求 ⇒ 0.8 以上；相关但不完整 ⇒ 0.4~0.7；勉强沾边 ⇒ 0.4 以下。⛔ 不许一律高分。

## 其它

- 文档与条款清单无关 ⇒ 返回 { ""clauses"": [] }，这是合法输出。
- 全中文（原文英文术语照抄），字段名保持英文。";

        /// <summary>
        /// 章域语义档提示词占位符 —— 由 <c>EnterpriseDocService</c> 替换后送模型。
        /// <c>{{abstract_text}}</c> = 画像语义骨架（简介 + 目的 + 范围 + 表头/抽样行）。
        /// </summary>
        public const string PhDocType = "{{doc_type}}";
        public const string PhAbstractText = "{{abstract_text}}";

        /// <summary>
        /// 章域判定兜底正文（库中 <c>doc_chapter_domain</c> 缺行时使用）。
        /// <para>记录/表单类文档无条款号、无正文，只靠「表头字段 + 抽样行 + 部门 + 文件名」语义归章；
        /// ⛔ 判不出必须落 <c>CD_UNKNOWN</c>，不许硬猜（见 27 号 §3.3 / §十 风险）。</para>
        /// </summary>
        public const string FallbackChapterDomainTemplate = @"你是 ISO 管理体系文档的「大章节归属」判定器。只判断这份文档主要属于哪几个大章节，不做符合性判断、不提取条款事实。

## 可选章域（只能从这里选，可多选，最多 3 个）
CD_ENV 组织环境（第4章）：理解组织及其环境、相关方需求与期望、管理体系范围
CD_LEAD 领导作用（第5章）：领导承诺、方针、岗位职责与权限、沟通协商
CD_DECIDE 策划（第6章）：风险和机遇、目标及其实现策划、变更策划
CD_SUPPORT 支持（第7章）：资源、能力与意识、培训、基础设施、监视和测量资源、组织知识、成文信息（文件控制/记录控制）
CD_RUN 运行（第8章）：运行策划与控制、设计开发、采购与外部提供、生产与服务提供、检验与放行、不合格输出、标识追溯防护、交付
CD_ASSESS 绩效评价（第9章）：监视测量分析评价、内部审核、管理评审、顾客满意、合规性评价
CD_IMPROVE 改进（第10章）：不符合与纠正措施、持续改进

## 判定规则（记录/表单类重点）
- 记录/表单常无条款号、无正文，只能靠「表头字段 + 抽样行 + 部门 + 文件名」的语义判断归属。
- 「培训计划 / 培训签到 / 岗位能力评价 / 检验人员任命 / 质量意识」→ CD_SUPPORT（7.2 能力、7.5 成文信息）。
- 「进料检验 / 出厂检验 / 制程巡检 / 首件 / 质检日报 / 生产日报 / 发货单 / 装箱单 / 生产通知单」→ CD_RUN（8.5 生产与服务提供、8.6 放行）。
- 「文件借阅 / 文件更改 / 文件销毁 / 外来文件清单」→ CD_SUPPORT（7.5.3 成文信息控制）。
- 与 ISO 管理体系无关的行政制度（考勤、劳动纪律、请销假、宿舍、报销等）⇒ Domains=[""CD_UNKNOWN""]。
- 证据不足、判断不了 ⇒ Domains=[""CD_UNKNOWN""]，⛔ 不许硬猜、⛔ 不许默认归 CD_RUN。

## 输入
文档类型：{{doc_type}}
语义骨架（简介 / 目的 / 范围 / 目录标题 / 表头抽样行）：
<<<DOC
{{abstract_text}}
DOC

## 输出（只输出一个 JSON 对象；第一个字符 {，最后一个字符 }；不要代码块、不要解释）
{""Domains"":[""CD_XXX""],""Confidence"":0.0,""Reason"":""一句话依据""}
Confidence 0~1：语义明确对应某章 ⇒ 0.8 以上；相关但不充分 ⇒ 0.4~0.7；只能靠猜测 ⇒ 0.4 以下并改判 CD_UNKNOWN。";
    }
}
