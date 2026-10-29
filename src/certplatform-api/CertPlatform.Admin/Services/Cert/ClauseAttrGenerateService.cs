using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Domain.Scope;
using CertPlatform.Shared.Services.Workflow;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;

namespace CertPlatform.Admin.Services.Cert;

/// <summary>
/// <b>条款属性 AI 预生成</b>（V2 条款主链 P4）。
///
/// <para><b>做什么</b>：给一条条款（号 + 标题 + 正文）→ 让 LLM 产出该条款 8 个属性的值
/// → 落 <c>cert_clause_attr</c>。省掉「273 条条款 × 8 属性 = 2184 次手工填写」。</para>
///
/// <para><b>★ 关键设计（照 24 号 §三/§四）</b>：
/// <list type="bullet">
///   <item><c>Artifacts</c> 与 <c>EvidenceChain</c> 产出的是<b>提示词正文</b>（不是结论）——
///         因为这两条属性在 L1 的值类型就是 <c>prompt</c>。真正"读到什么"要等跑证据时才产生。</item>
///   <item>两份要求 <b>⛔ 不合并</b>：前者企业侧可公开，后者是审核员取证剧本。
///         合并 ⇒ 企业照着剧本准备 ⇒ 审核失效。</item>
///   <item><c>ValueSource</c>：<c>Artifacts</c>/<c>EvidenceChain</c> = <c>reference</c>（要索取）；
///         其余 6 条 = <c>fixed</c>（条款已定，不需要企业提供）。</item>
/// </list>
///
/// <para><b>⚠️ 串行执行</b>：LLM 端点并发必 429（见「运行期排查坑与沙箱铁律」）。
/// 本服务逐条串行，靠 <c>MaxCount</c> 控制单次批量，⛔ 不做并行。</para>
///
/// <para><b>⚠️ 直插必须显式赋 <c>Code</c></b>：<c>YzhControllerBase.AddCore</c> 才自动生成 Code，
/// 这里走 <c>IDbOrm.InsertAsync</c> ⇒ 必须自己给 <c>Guid.NewGuid().ToString("N")</c>；
/// 且 <c>InsertAsync</c> 失败只返回 <c>Result.Fail</c> <b>不抛异常</b>（守卫 B-R5）⇒ 必须判 <c>Success</c>。</para>
/// </summary>
public sealed class ClauseAttrGenerateService
{
    private readonly IDbOrm _db;
    private readonly ICoreResourceQuery _core;
    private readonly IPromptWorkbench _workbench;
    private readonly LlmInvokeService _llm;
    private readonly IUserContext _user;

    /// <summary>提示词类型（唯一出处：<see cref="PromptTypes.ClauseAttr"/>）</summary>
    private const string PromptType = PromptTypes.ClauseAttr;

    /// <summary>单次请求最多跑多少条条款（防误点全量把 AI 额度烧光）</summary>
    public const int MaxBatchSize = 50;

    /// <summary>
    /// 「值来源」判定：<b>只有这两条是 reference</b>（条款只给期望，实际值要靠证据/企业材料）；
    /// 其余 6 条（条款意图/约束强度/适用性/适用条件/涉及过程/严重度）由标准或工程师**当场定死**。
    /// </summary>
    private static readonly HashSet<string> ReferenceAttrs =
        new(StringComparer.Ordinal) { "Artifacts", "EvidenceChain" };

    /// <summary>提示词里的占位符（与种子 SQL 的正文逐字对应）</summary>
    private const string PhStandardName = "{{standard_name}}";
    private const string PhClauseNumber = "{{clause_number}}";
    private const string PhClauseTitle = "{{clause_title}}";
    private const string PhClauseContent = "{{clause_content}}";

    public ClauseAttrGenerateService(
        IDbOrm db,
        ICoreResourceQuery core,
        IPromptWorkbench workbench,
        LlmInvokeService llm,
        IUserContext user)
    {
        _db = db;
        _core = core;
        _workbench = workbench;
        _llm = llm;
        _user = user;
    }

    // ════════════════════════════════════════════════════════════════════
    // 主流程
    // ════════════════════════════════════════════════════════════════════

    /// <summary>预生成（串行，逐条返回结果 —— 前端靠 Items 显示"第几条失败/为什么"）</summary>
    public async Task<ClauseAttrGenerateResult> GenerateAsync(
        ClauseAttrGenerateRequest request, CancellationToken ct = default)
    {
        var result = new ClauseAttrGenerateResult();

        // ── ① 参数与前置校验（每条都给"人话"，⛔ 不返回英文异常） ──
        var standardCode = (request.StandardCode ?? string.Empty).Trim();
        if (standardCode.Length == 0)
            return ClauseAttrGenerateResult.Fail("请先选择标准");

        // ★ 入参是**短码**（iso9001，与 cert_clause_attr.StandardCode 同口径），
        //   而 ICoreResourceQuery.GetStandardByCodeAsync 按 **Code（GUID）** 查
        //   ⇒ 必须先在有效标准里按 StandardCode 匹配，⛔ 不能直接拿短码去查 Code。
        var standard = (await _core.GetValidStandardsAsync())
            .FirstOrDefault(s => string.Equals(s.StandardCode, standardCode, StringComparison.Ordinal));
        if (standard == null)
            return ClauseAttrGenerateResult.Fail($"标准【{standardCode}】不存在或已停用");

        // ★★ 提示词按标准隔离（24 号 §零）：标准级 → 平台级 回退。
        //    ⚠️ 这里传的是 standard.Code（**GUID**）—— wf_prompt_template.StandardCode 存 GUID，
        //       与 cert_iso_standard.StandardCode（短码）**不是同一口径**。
        //       传短码不会报错，只会静默回退到平台级 ⇒ 多标准隔离悄悄失效，且没人发现。
        var prompt = await _workbench.ResolveActiveAsync(PromptType, standard.Code);
        if (prompt == null || string.IsNullOrWhiteSpace(prompt.Template))
            return ClauseAttrGenerateResult.Fail(
                $"未找到生效的「条款属性预生成」提示词（PromptType=clause_attr，标准【{standardCode}】或平台级）。" +
                "请先在「文档语义规则」页启用一条。");

        // ★★ 回退必须**可见**：标准级提示词缺失时用平台级兜底，前端要显示出来。
        //    ⛔ 不许静默降级 —— 否则用户以为跑的是本标准的提示词，质量差了也找不到原因。
        //    放在这里（而不是取到 todo 之后）是为了让「已全部配齐」这类提前返回也带得上。
        result.PromptCode = prompt.Code;
        result.PromptVersion = prompt.Version;
        result.PromptScope = string.IsNullOrWhiteSpace(prompt.StandardCode) ? "platform" : "standard";

        var settings = await AiFillSettings.ReadAsync(_db, ct);
        if (!settings.IsUsable)
            return ClauseAttrGenerateResult.Fail(
                "AI 参数未配置：请在「系统管理 → 系统参数」里填写 ai_api_key 与 ai_base_url");

        // 属性池（决定每条属性怎么落值：json 要序列化、prompt 存正文）
        var pool = await _db.Client.Queryable<ClauseAttrDef>()
            .Where(p => !p.IsDeleted && p.IsValid == 1)
            .ToListAsync();
        if (pool.Count == 0)
            return ClauseAttrGenerateResult.Fail("属性池为空，请先在「条款属性池」维护属性");

        // ── ② 取条款（只跑叶子：非叶子条款不产生检查要求） ──
        var all = await _core.GetClausesAsync(standard.Code);
        var parentCodes = all.Where(c => !string.IsNullOrEmpty(c.ParentCode))
                             .Select(c => c.ParentCode!)
                             .ToHashSet(StringComparer.Ordinal);

        var leaves = all.Where(c => !parentCodes.Contains(c.Code)).ToList();

        if (request.ClauseCodes is { Count: > 0 })
        {
            var wanted = request.ClauseCodes.Where(x => !string.IsNullOrWhiteSpace(x))
                                            .Select(x => x.Trim())
                                            .ToHashSet(StringComparer.Ordinal);
            leaves = leaves.Where(c => wanted.Contains(c.Code)).ToList();
        }

        leaves = leaves.OrderBy(c => c.Sort)
                       .ThenBy(c => c.ClauseNumber, StringComparer.Ordinal)
                       .ToList();

        if (leaves.Count == 0)
            return ClauseAttrGenerateResult.Fail("该标准下没有可生成属性的叶子条款");

        // ── ②-a 前置条款直接跳过（2026-10-28 用户裁定：生成阶段就跳，⛔ 不留到下游过滤）──
        // 判据两条，命中任一即跳：
        //   ① 条款号 ∈ {1,2,3} —— ISO 高层结构里固定是「范围 / 规范性引用文件 / 术语和定义」，
        //      这三条只做定义与引用，**不产生检查要求**（实测：给它们生成属性 ⇒ 下游会冒出检查项）
        //   ② 正文非空但**一个义务动词都没有**（应 / 必须 / 须 / 宜 / shall / should / may / must）
        //      ⇒ 纯说明性条款，同样不产生要求
        // ⚠️ 正文为空时**不判**：判不了就不猜，宁可多生成一条也不静默丢条款
        // ⚠️ 用户显式点名 `ClauseCodes` 时不跳（点名即意图，缺什么由 `Overwrite` 决定）
        var skippedPreamble = new List<string>();
        if (request.ClauseCodes is not { Count: > 0 })
        {
            var kept = new List<CertPlatform.Shared.Entities.Cert.ISOClause>(leaves.Count);
            foreach (var c in leaves)
            {
                if (IsPreambleClause(c)) skippedPreamble.Add(c.ClauseNumber ?? c.Code ?? string.Empty);
                else kept.Add(c);
            }
            leaves = kept;

            if (leaves.Count == 0)
            {
                result.Success = true;
                result.Message =
                    $"标准【{standardCode}】下的叶子条款全是前置条款（范围 / 规范性引用文件 / 术语和定义），" +
                    "它们不产生检查要求，无需生成属性";
                return result;
            }
        }

        // ── ②-b 未覆盖模式下先摘掉「已配齐」的条款 ──
        // ★ 不这么做的话 `Take(limit)` 永远取**前 N 条**：用户点 5 次「开始生成」
        //   每次都跑同一批（40 条全跳过），永远推不到第 51 条 —— 分批等于没有。
        //   显式指定 ClauseCodes 时不摘（用户点名要跑这几条，缺哪条补哪条由 overwrite 决定）。
        if (!request.Overwrite && request.ClauseCodes is not { Count: > 0 })
        {
            var poolCodes = pool.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
            var filled = (await _db.Client.Queryable<ClauseAttr>()
                    .Where(p => p.StandardCode == standard.StandardCode && !p.IsDeleted)
                    .Select(p => new { p.ClauseCode, p.AttrCode })
                    .ToListAsync())
                .GroupBy(x => x.ClauseCode ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(g => g.Key,
                              g => g.Select(x => x.AttrCode ?? string.Empty).ToHashSet(StringComparer.Ordinal),
                              StringComparer.Ordinal);

            leaves = leaves.Where(c =>
            {
                if (!filled.TryGetValue(c.Code ?? string.Empty, out var have)) return true;
                return !poolCodes.IsSubsetOf(have); // 还缺属性 ⇒ 这一条要做
            }).ToList();

            if (leaves.Count == 0)
            {
                // ⚠️ 这不是错误（用户点「生成」而库里已经齐了），走 success 信封，
                //    ⛔ 不返回 Fail —— 否则前端弹一条红色报错，用户以为坏了
                result.Success = true;
                result.Message =
                    $"标准【{standardCode}】的叶子条款属性已全部配齐，无需再生成（如需重跑请打开「覆盖已有」）";
                return result;
            }
        }

        var limit = request.MaxCount <= 0 ? MaxBatchSize : Math.Min(request.MaxCount, MaxBatchSize);
        var todo = leaves.Take(limit).ToList();

        result.Total = todo.Count;
        result.Model = string.IsNullOrWhiteSpace(prompt.ModelName) ? settings.Model : prompt.ModelName!;

        // ★ 跳过的前置条款必须**说出来**：⛔ 不能静默少跑几条（用户会以为漏了/坏了）
        var preambleNote = skippedPreamble.Count == 0
            ? string.Empty
            : $"；跳过 {skippedPreamble.Count} 条前置条款（{string.Join("、", skippedPreamble.Take(8))}"
              + (skippedPreamble.Count > 8 ? "…" : string.Empty)
              + "：范围/引用/术语类，不产生检查要求）";

        // ── ③ 逐条串行（⛔ 不并发：LLM 端点并发必 429） ──
        foreach (var clause in todo)
        {
            ct.ThrowIfCancellationRequested();
            var item = await GenerateOneAsync(
                standard, clause, prompt.Template!, settings, result.Model, pool, request.Overwrite, ct);
            result.Items.Add(item);
            if (item.Success) result.Succeeded++; else result.Failed++;
            result.Written += item.Written;
            result.Skipped += item.Skipped;
        }

        result.Success = result.Failed == 0;
        result.Message = (result.Failed == 0
            ? $"完成：{result.Succeeded} 条条款，写入 {result.Written} 个属性，跳过 {result.Skipped} 个"
            : $"部分失败：成功 {result.Succeeded} 条 / 失败 {result.Failed} 条；写入 {result.Written} 个属性")
            + preambleNote
            // ⚠️ 用了平台兜底提示词必须点名：这是「标准级提示词还没配」的信号，
            //    ⛔ 不能让它悄悄发生（否则 4 个标准跑出同一种味道，还以为是模型问题）
            + (result.PromptScope == "platform"
                ? $"；注意：标准【{standardCode}】尚无专属提示词，本次用的是平台级兜底提示词"
                : string.Empty);
        return result;
    }

    // ════════════════════════════════════════════════════════════════════
    // 前置条款判定
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 义务动词提示词（**只用于「有没有要求」的粗判**，⛔ 不用于判断属于 shall 还是 should ——
    /// 那是 LLM 的活）。中文只放「应 / 必须 / 须 / 宜」：⛔ 不放「可」——
    /// 「可能 / 相应 / 对应 / 适应」满篇都是，一放就把说明性条款全判成有要求。
    /// </summary>
    private static readonly string[] ObligationVerbHints =
    {
        "应", "必须", "须", "宜", "shall", "should", "may", "must",
    };

    /// <summary>
    /// 前置条款判定（见 <see cref="GenerateAsync"/> ②-a）：命中 ⇒ 这条不产生检查要求，跳过不生成。
    ///
    /// <para>① 条款号 <c>1 / 2 / 3</c> —— ISO 高层结构里固定是「范围 / 规范性引用文件 / 术语和定义」；
    /// ② 正文非空但一个义务动词都没有 —— 纯说明性条款。</para>
    ///
    /// <para>⚠️ 正文为空时**返回 false**（判不了就不猜）：宁可多生成一条，也不静默丢条款。</para>
    /// </summary>
    private static bool IsPreambleClause(CertPlatform.Shared.Entities.Cert.ISOClause clause)
    {
        var number = (clause.ClauseNumber ?? string.Empty).Trim().TrimEnd('.');
        if (number is "1" or "2" or "3") return true;

        var text = clause.Description;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var lower = text.ToLowerInvariant();
        return !ObligationVerbHints.Any(h => lower.Contains(h, StringComparison.Ordinal));
    }

    // ════════════════════════════════════════════════════════════════════
    // 单条条款
    // ════════════════════════════════════════════════════════════════════

    private async Task<ClauseAttrGenerateItem> GenerateOneAsync(
        CertPlatform.Shared.Entities.Cert.ISOStandard standard,
        CertPlatform.Shared.Entities.Cert.ISOClause clause,
        string template,
        AiFillSettings settings,
        string model,
        List<ClauseAttrDef> pool,
        bool overwrite,
        CancellationToken ct)
    {
        var item = new ClauseAttrGenerateItem
        {
            ClauseCode = clause.Code,
            ClauseNumber = clause.ClauseNumber ?? string.Empty,
            Title = clause.Title ?? string.Empty,
        };

        // ① 渲染提示词（占位符名与种子 SQL 逐字对应）
        var prompt = template
            .Replace(PhStandardName, $"{standard.StandardName}（{standard.StandardCode}）")
            .Replace(PhClauseNumber, clause.ClauseNumber ?? string.Empty)
            .Replace(PhClauseTitle, clause.Title ?? string.Empty)
            .Replace(PhClauseContent, clause.Description ?? string.Empty);

        // ② 调模型（ForceJson ⇒ 输出必须是 JSON；Temperature 压低求稳定）
        var resp = await _llm.CompleteAsync(new LlmInvokeRequest
        {
            BaseUrl = settings.BaseUrl,
            ApiKey = settings.ApiKey,
            Model = model,
            Temperature = 0.2f,
            MaxTokens = 8192,
            Prompt = prompt,
            ForceJson = true,
        });

        if (!resp.Success || resp.Json == null)
        {
            item.Success = false;
            item.Message = resp.Success ? "模型返回的不是合法 JSON" : resp.Message;
            item.RawOutput = Trim(resp.Content, 2000);
            return item;
        }

        item.RawOutput = Trim(resp.Content, 2000);
        item.PromptTokens = resp.PromptTokens ?? 0;
        item.CompletionTokens = resp.CompletionTokens ?? 0;

        // ③ JSON → 属性行
        using var doc = resp.Json;
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            item.Success = false;
            item.Message = "模型输出的 JSON 不是对象";
            return item;
        }

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var attr in pool)
        {
            if (!doc.RootElement.TryGetProperty(attr.Code, out var node)) continue;
            values[attr.Code] = Flatten(node, attr.ValueType);
        }

        if (values.Count == 0)
        {
            item.Success = false;
            item.Message = "模型输出里没有任何已知属性字段（检查提示词与属性池的 Code 是否一致）";
            return item;
        }

        // ④ 落库（三键唯一：StandardCode + ClauseCode + AttrCode）
        var sort = 0;
        foreach (var attr in pool.OrderBy(p => p.Sort))
        {
            if (!values.TryGetValue(attr.Code, out var value)) continue;
            sort += 10;

            var existing = await _db.Client.Queryable<ClauseAttr>()
                .Where(p => p.StandardCode == standard.StandardCode
                            && p.ClauseCode == clause.Code
                            && p.AttrCode == attr.Code)
                .FirstAsync();

            if (existing != null)
            {
                if (existing.IsDeleted)
                {
                    // 已删行仍占唯一键 ⇒ 不静默复活（可能与人工删除意图冲突），交给用户决定
                    item.Skipped++;
                    item.Message = Append(item.Message, $"属性【{attr.Code}】曾被删除但记录仍在库中，已跳过");
                    continue;
                }
                if (!overwrite)
                {
                    item.Skipped++;
                    continue;
                }
                existing.AttrValue = value;
                existing.UpdateBy = _user.UserCode;
                existing.UpdateTime = DateTime.Now;
                // ⚠️ 必须列级写回（守卫 R-A / 陷阱 ㉑）：全列写回会把别的列一并覆盖
                var up = await _db.UpdateAsync(
                    existing,
                    nameof(ClauseAttr.AttrValue),
                    nameof(ClauseAttr.UpdateBy),
                    nameof(ClauseAttr.UpdateTime));
                if (!up.Success)
                {
                    item.Success = false;
                    item.Message = Append(item.Message, $"更新【{attr.Code}】失败：{up.Error}");
                    return item;
                }
                item.Written++;
                continue;
            }

            var entity = new ClauseAttr
            {
                // ⚠️ 直插必须自己给 Code（框架只在 AddCore 里自动生成）
                Code = Guid.NewGuid().ToString("N"),
                StandardCode = standard.StandardCode,
                ClauseCode = clause.Code,
                AttrCode = attr.Code,
                ValueSource = ReferenceAttrs.Contains(attr.Code) ? "reference" : "fixed",
                AttrValue = value,
                Sort = sort,
                IsValid = 1,
                IsDeleted = false,
                CreateBy = _user.UserCode,
                CreateTime = DateTime.Now,
            };

            var ins = await _db.InsertAsync(entity);
            if (!ins.Success)
            {
                item.Success = false;
                item.Message = Append(item.Message, $"写入【{attr.Code}】失败：{ins.Error}");
                return item;
            }
            item.Written++;
        }

        item.Success = item.Written > 0 || item.Skipped > 0;
        if (string.IsNullOrEmpty(item.Message))
            item.Message = $"写入 {item.Written} 个属性" + (item.Skipped > 0 ? $"，跳过 {item.Skipped} 个" : string.Empty);
        return item;
    }

    // ════════════════════════════════════════════════════════════════════
    // 工具
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// JSON 节点 → 落库字符串（<b>按属性池声明的值类型</b>，⛔ 不按模型输出猜）。
    /// <list type="bullet">
    ///   <item><c>json</c> ⇒ 原样序列化（数组/对象都保留结构，前端按 JSON 解析）</item>
    ///   <item><c>bool</c> ⇒ <c>true</c>/<c>false</c></item>
    ///   <item><c>text</c>/<c>dict</c>/<c>prompt</c> ⇒ 取字符串；模型给了数组/对象就压成 JSON 串兜底</item>
    /// </list>
    /// </summary>
    private static string? Flatten(JsonElement node, string? valueType)
    {
        var vt = (valueType ?? "text").Trim().ToLowerInvariant();
        switch (node.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            case JsonValueKind.Array:
            case JsonValueKind.Object:
                // json 类型保留结构；其余类型模型不该给复合值，压成 JSON 串免得丢数据
                return node.GetRawText();
            case JsonValueKind.True:
                return vt == "bool" ? "true" : "1";
            case JsonValueKind.False:
                return vt == "bool" ? "false" : "0";
            case JsonValueKind.Number:
                return node.GetRawText();
            default:
                return node.GetString();
        }
    }

    private static string Trim(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    private static string Append(string? current, string more)
        => string.IsNullOrEmpty(current) ? more : current + "；" + more;
}

// ════════════════════════════════════════════════════════════════════════
// 契约 DTO（前端直接消费，字段 PascalCase）
// ════════════════════════════════════════════════════════════════════════

/// <summary>预生成入参</summary>
public sealed class ClauseAttrGenerateRequest
{
    /// <summary>标准短码（<c>iso9001</c>），与 <c>cert_clause_attr.StandardCode</c> 同口径</summary>
    public string StandardCode { get; set; } = string.Empty;

    /// <summary>指定条款（<c>cert_iso_clause.Code</c>）；为空 = 该标准全部叶子条款</summary>
    public List<string>? ClauseCodes { get; set; }

    /// <summary>已存在的属性是否覆盖（默认 false ⇒ 只补缺失的）</summary>
    public bool Overwrite { get; set; }

    /// <summary>单次最多跑几条（默认/上限 50，⛔ 防误点全量烧额度）</summary>
    public int MaxCount { get; set; } = 5;
}

/// <summary>单条条款的生成结果</summary>
public sealed class ClauseAttrGenerateItem
{
    public string ClauseCode { get; set; } = string.Empty;
    public string ClauseNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Written { get; set; }
    public int Skipped { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    /// <summary>模型原始输出（截断 2000 字，排障用）</summary>
    public string RawOutput { get; set; } = string.Empty;
}

/// <summary>整体结果</summary>
public sealed class ClauseAttrGenerateResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public int Written { get; set; }
    public int Skipped { get; set; }
    public string PromptCode { get; set; } = string.Empty;
    public int PromptVersion { get; set; }
    /// <summary>
    /// 本次用的提示词范围：<c>standard</c> = 该标准专属 · <c>platform</c> = 平台级兜底。
    /// <para>前端必须显示 —— 多标准隔离下，「用了兜底」等于「这个标准还没真正配置」。</para>
    /// </summary>
    public string PromptScope { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public List<ClauseAttrGenerateItem> Items { get; set; } = new();

    public static ClauseAttrGenerateResult Fail(string message)
        => new() { Success = false, Message = message };
}
