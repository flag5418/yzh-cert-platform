using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Shared.Entities.Cert;
using SqlSugar;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.Cert;

/// <summary>
/// <b>条款属性预生成提示词</b> —— 存取（委托通用 PromptTemplateService）+ 属性池注入段渲染。
///
/// <para><b>存储迁移</b>（2026-10-29）：从 <c>cert_sys_config</c>（ConfigKey = clause_attr_prompt）
/// 迁移至 <c>cert_prompt_template</c>（PromptCode = clause_attr_gen）。
/// 读写全部委托 <see cref="PromptTemplateService"/> —— 按作用域解析最匹配行。</para>
///
/// <para><b>★★ 为什么属性不进提示词正文</b>：属性池是会长的（现在 8 条，规划 30–50 条）。
/// 把属性名与判据写在正文里 ⇒ 池子一变，模型就不产新属性，<b>挂接静默缺失且不报错</b>
/// （这正是本页此前的问题）。现在：正文只写「通用规则」+ <c>{{attr_pool}}</c> 占位符，
/// 占位符由本类按 <c>cert_clause_attr_def</c> <b>实时渲染</b>
/// ⇒ 属性池增删改 ⇒ 提示词自动跟上，⛔ 不需要再改正文。</para>
///
/// <para><b>★ 无占位符也要注入</b>：老提示词里没有 <c>{{attr_pool}}</c> 时**追加到末尾**，
/// ⛔ 不能因为"用户没写占位符"就让属性整段丢掉（那等于静默退回旧行为）。</para>
/// </summary>
public sealed class ClauseAttrPromptService
{
    /// <summary>提示词代码（cert_prompt_template.PromptCode）</summary>
    public const string PromptCode = "clause_attr_gen";

    /// <summary>属性池注入占位符</summary>
    public const string AttrPoolPlaceholder = "{{attr_pool}}";

    /// <summary>条款级占位符</summary>
    private const string PhStandardName = "{{standard_name}}";
    private const string PhClauseNumber = "{{clause_number}}";
    private const string PhClauseTitle = "{{clause_title}}";
    private const string PhClauseContent = "{{clause_content}}";

    private readonly IDbOrm _db;
    private readonly PromptTemplateService _promptTemplate;

    public ClauseAttrPromptService(IDbOrm db, PromptTemplateService promptTemplate)
    {
        _db = db;
        _promptTemplate = promptTemplate;
    }

    // ════════════════════════════════════════════════════════════════════
    // 一、读取 / 保存
    // ════════════════════════════════════════════════════════════════════

    /// <summary>读提示词正文（库中缺失时回落到 <see cref="FallbackTemplate"/>，不抛异常）</summary>
    public async Task<(string Template, PromptTemplate? Row)> GetTemplateAsync(CancellationToken ct = default)
    {
        var row = await _promptTemplate.GetAsync(PromptCode, null, null);
        var template = string.IsNullOrWhiteSpace(row?.Template) ? FallbackTemplate : row!.Template!;
        return (template, row);
    }

    /// <summary>
    /// 读取提示词完整 DTO（给前端编辑抽屉用）。
    /// <para>⚠️ 读取时实时渲染属性池注入段（与真正喂给模型的 <see cref="BuildPoolSectionAsync"/> 同一口径），
    /// 让用户未保存就能看到「当前属性池下会长什么样」。</para>
    /// </summary>
    public async Task<ClauseAttrPromptDto> GetPromptAsync(CancellationToken ct = default)
    {
        var (template, row) = await GetTemplateAsync(ct);
        var pool = await _db.Client.Queryable<ClauseAttrDef>()
            .Where(p => !p.IsDeleted && p.IsValid == 1)
            .OrderBy(p => p.Sort)
            .ToListAsync();

        return new ClauseAttrPromptDto
        {
            ConfigKey = PromptCode,
            DisplayName = "条款属性预生成提示词",
            Template = template,
            AttrPoolSection = await BuildPoolSectionAsync(ct),
            AttrCount = pool.Count,
            Stored = row != null && row.Status != "fallback",
            UpdateBy = row?.UpdateBy,
            UpdateTime = row?.UpdateTime,
        };
    }

    /// <summary>
    /// 保存提示词正文（幂等 upsert —— 委托 PromptTemplateService）。
    /// </summary>
    public async Task<(bool Ok, string Message)> SaveAsync(
        string template, string? userCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(template))
            return (false, "提示词正文不能为空");

        return await _promptTemplate.SaveAsync(
            PromptCode, template, null, null,
            "条款属性预生成提示词",
            "「条款属性配置」页 AI 预生成的通用提示词（对全部标准、全部条款生效）",
            userCode, ct);
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、属性池注入段
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 按属性池实时渲染「属性清单」段。
    ///
    /// <para>每条属性给出：字段名（= <c>Code</c>）/ 名称 / 值类型 / <b>允许取值</b>（dict / json 自动取字典项，
    /// 防模型自造码）/ <b>取值要求</b>（优先 <c>PromptHint</c>，空则回落到 <c>Remark</c> 再回落到按类型的默认话术）。</para>
    /// </summary>
    public async Task<string> BuildPoolSectionAsync(CancellationToken ct = default)
    {
        var pool = await _db.Client.Queryable<ClauseAttrDef>()
            .Where(p => !p.IsDeleted && p.IsValid == 1)
            .OrderBy(p => p.Sort)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.Append("## 属性清单（由「条款属性池」自动生成 · 共 ").Append(pool.Count).AppendLine(" 条）");
        sb.AppendLine();
        sb.AppendLine("字段名严格按下面每个属性的 `Code` 输出，逐个都要有；⛔ 不许增、删、改名。");
        sb.AppendLine();

        // 字典项只为 dict / json 且有 DictCode 的属性加载（同一 dicNo 只查一次）
        var dictCache = new Dictionary<string, List<(string Value, string Label)>>(StringComparer.OrdinalIgnoreCase);

        var index = 0;
        foreach (var attr in pool)
        {
            index++;
            sb.Append("### ").Append(index).Append(". `").Append(attr.Code).Append("` ")
              .AppendLine(attr.AttrName);
            sb.Append("- 值类型：").AppendLine(DescribeValueType(attr));

            if (!string.IsNullOrWhiteSpace(attr.DictCode))
            {
                var allowed = await LoadDictOptionsAsync(attr.DictCode!, dictCache, ct);
                if (allowed.Count > 0)
                {
                    var list = string.Join("、", allowed.Select(x =>
                        string.IsNullOrWhiteSpace(x.Label) ? $"`{x.Value}`" : $"`{x.Value}`（{x.Label}）"));
                    sb.Append("- 允许取值（⛔ 只能用这些码，不许自造）：").AppendLine(list);
                }
                else
                {
                    // 字典配错/停用必须**说出来**：否则模型自由发挥，前端只看到"属性对不上"
                    sb.Append("- 允许取值：⚠️ 字典【").Append(attr.DictCode)
                      .AppendLine("】无可用项或已停用，请允许模型按条款原文给出合理中文值。");
                }
            }

            var hint = FirstNonEmpty(attr.PromptHint, attr.Remark, DefaultHint(attr));
            sb.Append("- 取值要求：").AppendLine(hint.Trim());
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 把注入段合进正文：有 <c>{{attr_pool}}</c> ⇒ 替换（可多处）；没有 ⇒ 追加到末尾。
    /// <para>段为空（属性池 0 条）时原样返回正文 —— 免得末尾挂一个空标题。</para>
    /// </summary>
    public static string ApplyPoolSection(string template, string poolSection)
    {
        if (string.IsNullOrEmpty(poolSection)) return template;
        if (template.Contains(AttrPoolPlaceholder, StringComparison.Ordinal))
            return template.Replace(AttrPoolPlaceholder, poolSection, StringComparison.Ordinal);

        return template.TrimEnd() + "\n\n" + poolSection;
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、渲染（给试运行 / 预览 / 真正调用）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>渲染某条款的最终提示词（条款占位符 + 属性池注入段）</summary>
    public async Task<string> RenderAsync(
        string template, ISOStandard standard, ISOClause? clause, CancellationToken ct = default)
    {
        var rendered = template
            .Replace(PhStandardName, $"{standard.StandardName}（{standard.StandardCode}）")
            .Replace(PhClauseNumber, clause?.ClauseNumber ?? string.Empty)
            .Replace(PhClauseTitle, clause?.Title ?? string.Empty)
            .Replace(PhClauseContent, clause?.Description ?? string.Empty);

        var section = await BuildPoolSectionAsync(ct);
        return ApplyPoolSection(rendered, section);
    }

    // ════════════════════════════════════════════════════════════════════
    // 四、工具
    // ════════════════════════════════════════════════════════════════════

    private async Task<List<(string Value, string Label)>> LoadDictOptionsAsync(
        string dicNo, Dictionary<string, List<(string, string)>> cache, CancellationToken ct)
    {
        if (cache.TryGetValue(dicNo, out var cached)) return cached;

        var list = new List<(string, string)>();
        var dict = await _db.Client.Queryable<Sys_Dictionary>()
            .Where(d => d.DicNo == dicNo && d.IsValid == 1 && !d.IsDeleted)
            .OrderBy(d => d.Id)
            .FirstAsync();

        if (dict != null)
        {
            var items = await _db.Client.Queryable<Sys_DictionaryList>()
                .Where(x => x.DicCode == dict.Code && x.IsValid == 1 && !x.IsDeleted)
                .OrderBy(x => x.OrderNo)
                .ToListAsync();

            list = items
                .Where(x => !string.IsNullOrWhiteSpace(x.DicValue))
                .Select(x => (x.DicValue!.Trim(), x.DicName ?? string.Empty))
                .ToList();
        }

        cache[dicNo] = list;
        return list;
    }

    private static string DescribeValueType(ClauseAttrDef attr)
    {
        var vt = (attr.ValueType ?? "text").Trim().ToLowerInvariant();
        return vt switch
        {
            "text" => "text（自由文本，一句话说清）",
            "dict" => "dict（单值枚举，只能填一个码；⛔ 不许自己写中文描述）",
            "json" => string.IsNullOrWhiteSpace(attr.DictCode)
                ? "json（JSON 值；数组或对象，按下面的取值要求给结构）"
                : "json（JSON 数组，元素只能取下面列出的码）",
            "bool" => "bool（只能是 true 或 false）",
            "prompt" => "prompt（产出一段**提示词正文** —— 一段可执行的提示词，不是结论、不是清单结果）",
            _ => vt,
        };
    }

    private static string DefaultHint(ClauseAttrDef attr)
    {
        var vt = (attr.ValueType ?? "text").Trim().ToLowerInvariant();
        return vt switch
        {
            "text" => "一句话说清，判不了就给空串（空=待人工确认）。",
            "dict" => "只能在允许取值里选一个码。",
            "json" => "按取值要求给 JSON；没有内容给空数组 []。",
            "bool" => "是 true，否 false。",
            "prompt" => "写一段可执行提示词正文；找不到证据就交空，⛔ 不许推测。",
            _ => "按条款原文给出。",
        };
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    /// <summary>
    /// 库中还没有这条参数时的兜底正文（与迁移脚本种子逐字一致）。
    /// <para>⚠️ 兜底**不是**第二份可编辑副本：只在库里缺行时用于渲染，保存后即以库为准。</para>
    /// </summary>
    public const string FallbackTemplate = @"你是 ISO 管理体系认证的资深审核技术专家。你的任务：读一条标准条款，为这条条款产出「属性配置」JSON。

## 输入

- 标准：{{standard_name}}
- 条款号：{{clause_number}}
- 条款标题：{{clause_title}}
- 条款正文：
{{clause_content}}

## 输出

只输出一个 JSON 对象，不要 markdown 代码块，不要任何解释文字。
字段名 = 文末「属性清单」里的字段名，**逐个都要输出**；⛔ 不许增、删、改名，⛔ 不许输出清单外的字段。

## 通用判据

① 先读条款正文的**主句**（第一句带义务动词的话），再看细节；正文为空或纯说明性 ⇒ 按「不产生检查要求」处理，各字段给空值。
② 出现「适用时 / 如有时 / 当…时」这类限定语 ⇒ 该条款是**条件适用**，必须把触发条件写出来，⛔ 不许笼统写「视情况而定」。
③ 判不了就留空串（或空数组），⛔ 不许编造、⛔ 不许用通用套话把字段填满。空值 = 待人工确认，是合法输出。
④ 所有文本用简体中文；字段名保持英文原样。

## 硬约束

- ⛔ 「企业该提供什么」与「审核员怎么取证」必须分开产出（各归各的属性），合并会导致企业照着取证剧本准备、审核失效。
- ⛔ 不许把条款原文抄一遍充当答案。
- 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。

{{attr_pool}}";
}

// ════════════════════════════════════════════════════════════════════════
// 契约 DTO（前端直接消费，字段 PascalCase）
// ════════════════════════════════════════════════════════════════════════

/// <summary>提示词读取结果（编辑抽屉用）</summary>
public sealed class ClauseAttrPromptDto
{
    public string ConfigKey { get; set; } = ClauseAttrPromptService.PromptCode;
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>提示词正文（含 <c>{{attr_pool}}</c> 占位符）</summary>
    public string Template { get; set; } = string.Empty;
    /// <summary>当前属性池渲染出的注入段（只读预览 —— 与真正注入的逐字一致）</summary>
    public string AttrPoolSection { get; set; } = string.Empty;
    /// <summary>属性池启用条数（0 = 提示词会要求模型输出 0 个字段，必须先配属性池）</summary>
    public int AttrCount { get; set; }
    /// <summary>库里是否已有该参数行（false = 用的是兜底正文，保存后才会落库）</summary>
    public bool Stored { get; set; }
    public string? UpdateBy { get; set; }
    public DateTime? UpdateTime { get; set; }
}

/// <summary>保存入参</summary>
public sealed class ClauseAttrPromptSaveRequest
{
    public string Template { get; set; } = string.Empty;
}

/// <summary>渲染预览入参</summary>
public sealed class ClauseAttrPromptPreviewRequest
{
    /// <summary>标准短码（iso9001）—— 用于渲染 <c>{{standard_name}}</c> 并定位条款</summary>
    public string StandardCode { get; set; } = string.Empty;
    /// <summary>条款 Code（可选；空 = 用占位示例值渲染）</summary>
    public string? ClauseCode { get; set; }
    /// <summary>未保存的正文（可选）—— 支持「边改边看」；空 = 用库里已存的</summary>
    public string? Template { get; set; }
}

/// <summary>渲染预览结果</summary>
public sealed class ClauseAttrPromptPreviewDto
{
    /// <summary>最终喂给模型的完整提示词</summary>
    public string Prompt { get; set; } = string.Empty;
    public string ClauseNumber { get; set; } = string.Empty;
    public string ClauseTitle { get; set; } = string.Empty;
    public int AttrCount { get; set; }
    /// <summary>正文里是否有 <c>{{attr_pool}}</c> 占位符（false ⇒ 注入段被追加在末尾）</summary>
    public bool HasPoolPlaceholder { get; set; }
    public int PromptChars { get; set; }
}
