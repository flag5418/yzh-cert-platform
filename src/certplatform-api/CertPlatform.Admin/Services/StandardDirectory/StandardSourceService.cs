using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using YZH.Core.Api.Models.System;
using YZH.Core.DataBase.Interfaces;
using YZH.Core.Stand.Interfaces;
using CertPlatform.Admin.Entities.Cert;
using CertPlatform.Shared.DocExtraction;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Storage;
using CertPlatform.Shared.Services.DocExtraction;

namespace CertPlatform.Admin.Services.StandardDirectory;

/// <summary>
/// <b>标准原文 → Markdown → 条款</b> 的唯一实现（V3 知识层，2026-11-10）。
///
/// <para><b>为什么单独成服务</b>：同一份标准 Markdown 有两个消费者 ——
/// ① 本服务「自动生成条款」；② 企业文档内容提取时给模型看标准原文。
/// 两条链必须读<b>同一份</b> <c>cert_iso_standard.MarkdownPath</c>，
/// ⛔ 不允许各自再传一次、各存一份。</para>
///
/// <para><b>生成策略</b>：先按 Markdown 标题里的条款编号做<b>确定性解析</b>（国标正文本身就是
/// 编号结构，命中率高、零成本、可复现）；解析结果过少（&lt;5 条）才回落到 LLM 抽取，
/// 避免「一条都没解析出来却写进库」。</para>
/// </summary>
public class StandardSourceService
{
    private readonly IDbOrm _db;
    private readonly IObjectStorage _storage;
    private readonly IFileConvertCore _convert;
    private readonly LlmInvokeService _llm;
    private readonly ILogger<StandardSourceService> _logger;

    /// <summary>单次解析送模型的字符上限（与文档提取同一口径 3 万字）</summary>
    private const int LlmChunkChars = 30_000;

    /// <summary>条款编号：4 / 4.1 / 4.1.1 / A.1 / A.1.2</summary>
    private static readonly Regex NumberHead = new(
        @"^(?:#{1,6}\s+)?(?<num>(?:\d+(?:\.[\d]+)*|[A-Z]+(?:\.\d+)+))\s+(?<title>[^\n]{1,120})$",
        RegexOptions.Compiled);

    private static readonly Regex MarkdownHead = new(
        @"^\s{0,3}#{1,6}\s+(?<num>(?:\d+(?:\.[\d]+)*|[A-Z]+(?:\.\d+)+))\s+(?<title>[^\n]{1,120})$",
        RegexOptions.Compiled);

    public StandardSourceService(
        IDbOrm db,
        IObjectStorage storage,
        IFileConvertCore convert,
        LlmInvokeService llm,
        ILogger<StandardSourceService> logger)
    {
        _db = db;
        _storage = storage;
        _convert = convert;
        _llm = llm;
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════════════
    // 一、上传标准文件 → 转 Markdown → 落路径（并返回条款预览）
    // ════════════════════════════════════════════════════════════════════

    public async Task<StandardSourceUploadResult> UploadAsync(string standardKey, string fileName, byte[] content)
    {
        if (content == null || content.Length == 0)
            return StandardSourceUploadResult.Fail("上传文件为空");

        var std = await ResolveStandardAsync(standardKey);
        if (std == null)
            return StandardSourceUploadResult.Fail($"标准【{standardKey}】不存在");

        // 1) 原文留档（覆盖语义：同一标准只保留最近一次上传）
        var sourcePath = PathBuilder.StandardSource(std.Code, fileName);
        using (var src = new MemoryStream(content))
        {
            await _storage.UploadAsync(sourcePath.TrimStart('/'), src, content.Length);
        }

        // 2) 转换（与文档提取同一条内核：anydoc → OCR 兜底）
        var conv = await _convert.ConvertToMarkdownInlineAsync(fileName, content);
        if (!conv.Success || conv.Content == null || conv.Content.Length == 0)
            return StandardSourceUploadResult.Fail("标准文件转换失败：" + (string.IsNullOrWhiteSpace(conv.Message) ? "未知原因" : conv.Message));

        var markdown = Encoding.UTF8.GetString(conv.Content);
        var mdBytes = Encoding.UTF8.GetBytes(markdown);
        var mdPath = PathBuilder.StandardSourceMarkdown(std.Code, fileName);
        using (var mdStream = new MemoryStream(mdBytes))
        {
            await _storage.UploadAsync(mdPath.TrimStart('/'), mdStream, mdBytes.Length, "text/markdown");
        }

        // 3) 路径挂到标准行（唯一事实源）
        std.MarkdownPath = mdPath;
        std.MarkdownChars = markdown.Length;
        std.MarkdownSourceFile = fileName;
        std.MarkdownUpdatedTime = DateTime.Now;
        std.UpdateTime = DateTime.Now;
        await _db.UpdateAsync(std,
            nameof(ISOStandard.MarkdownPath),
            nameof(ISOStandard.MarkdownChars),
            nameof(ISOStandard.MarkdownSourceFile),
            nameof(ISOStandard.MarkdownUpdatedTime),
            nameof(ISOStandard.UpdateTime));

        // 4) 解析预览（先给人看，确认后才写库）
        var parsed = ParseClauses(markdown);
        if (parsed.Count < 5)
            parsed = await ExtractWithLlmAsync(std.StandardName, markdown);

        var existingCount = await CountExistingAsync(std.Code);
        return StandardSourceUploadResult.Ok(new StandardSourcePreview
        {
            StandardCode = std.StandardCode,
            StandardName = std.StandardName,
            MarkdownPath = mdPath,
            MarkdownChars = markdown.Length,
            SourceFile = fileName,
            ParsedCount = parsed.Count,
            ExistingCount = existingCount,
            Parser = parsed.Count >= 5 ? "markdown-heading" : "llm",
            Clauses = parsed,
        });
    }

    // ════════════════════════════════════════════════════════════════════
    // 二、预览（已有 Markdown 的标准，随时可重新解析）
    // ════════════════════════════════════════════════════════════════════

    public async Task<StandardSourcePreview?> PreviewAsync(string standardKey)
    {
        var std = await ResolveStandardAsync(standardKey);
        if (std == null) return null;
        if (string.IsNullOrWhiteSpace(std.MarkdownPath))
            return null;

        var markdown = await ReadMarkdownAsync(std.MarkdownPath!);
        if (string.IsNullOrEmpty(markdown)) return null;

        var parsed = ParseClauses(markdown);
        var parser = "markdown-heading";
        if (parsed.Count < 5)
        {
            parsed = await ExtractWithLlmAsync(std.StandardName, markdown);
            parser = "llm";
        }

        return new StandardSourcePreview
        {
            StandardCode = std.StandardCode,
            StandardName = std.StandardName,
            MarkdownPath = std.MarkdownPath,
            MarkdownChars = std.MarkdownChars,
            SourceFile = std.MarkdownSourceFile,
            ParsedCount = parsed.Count,
            ExistingCount = await CountExistingAsync(std.Code),
            Parser = parser,
            Clauses = parsed,
        };
    }

    // ════════════════════════════════════════════════════════════════════
    // 三、写入条款（默认只补缺失；Overwrite = 先清空再全量重建）
    // ════════════════════════════════════════════════════════════════════

    public async Task<StandardClauseApplyResult> ApplyAsync(string standardKey, bool overwrite)
    {
        var std = await ResolveStandardAsync(standardKey);
        if (std == null) return StandardClauseApplyResult.Fail("标准不存在");
        if (string.IsNullOrWhiteSpace(std.MarkdownPath))
            return StandardClauseApplyResult.Fail("该标准还没有原文 Markdown，请先上传标准文件");

        var markdown = await ReadMarkdownAsync(std.MarkdownPath!);
        if (string.IsNullOrEmpty(markdown))
            return StandardClauseApplyResult.Fail("标准 Markdown 读取失败");

        var parsed = ParseClauses(markdown);
        if (parsed.Count < 5)
            parsed = await ExtractWithLlmAsync(std.StandardName, markdown);
        if (parsed.Count == 0)
            return StandardClauseApplyResult.Fail("没有从 Markdown 中解析出条款（可先看预览确认原文结构）");

        var existing = await _db.Client.Queryable<ISOClause>()
            .Where(c => c.StandardCode == std.Code && !c.IsDeleted)
            .ToListAsync();

        // ★ 2026-11-11：overwrite = 「先清空，再增加」。
        //   原来只覆盖同编号的行，上传错标准后遗留的其它编号会一直留在库里（132 条 vs 94 条）。
        var cleared = 0;
        if (overwrite && existing.Count > 0)
        {
            foreach (var row in existing)
            {
                row.IsDeleted = true;
                row.IsValid = 0;
                row.DeleteTime = DateTime.Now;
                row.UpdateTime = DateTime.Now;
                await _db.UpdateAsync(row,
                    nameof(ISOClause.IsDeleted), nameof(ISOClause.IsValid),
                    nameof(ISOClause.DeleteTime), nameof(ISOClause.UpdateTime));
            }
            cleared = existing.Count;
            existing = new List<ISOClause>();

            // 条款重建后旧 ClauseCode 全部失效；属性行必须同步清掉，否则会留下
            // 「挂在已删条款上的孤儿属性」（页面按新条款查不到，但库里一直占着）。
            var attrs = await _db.Client.Queryable<ClauseAttr>()
                .Where(a => a.StandardCode == std.StandardCode && !a.IsDeleted)
                .ToListAsync();
            foreach (var attr in attrs)
            {
                attr.IsDeleted = true;
                attr.IsValid = 0;
                attr.UpdateTime = DateTime.Now;
                await _db.UpdateAsync(attr,
                    nameof(ClauseAttr.IsDeleted), nameof(ClauseAttr.IsValid),
                    nameof(ClauseAttr.UpdateTime));
            }
        }

        var byNumber = existing
            .Where(c => !string.IsNullOrWhiteSpace(c.ClauseNumber))
            .GroupBy(c => c.ClauseNumber.Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var maxSort = existing.Count == 0 ? 0 : existing.Max(c => c.Sort);
        var added = 0; var updated = 0; var skipped = 0;
        var codeByNumber = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in byNumber) codeByNumber[kv.Key] = kv.Value.Code ?? "";

        foreach (var item in parsed)
        {
            var number = item.ClauseNumber.Trim();
            if (number.Length == 0) continue;

            if (byNumber.TryGetValue(number, out var row))
            {
                codeByNumber[number] = row.Code ?? "";
                if (!overwrite) { skipped++; continue; }

                row.Title = item.Title;
                row.Description = item.Description;
                row.IsValid = 1;
                row.UpdateTime = DateTime.Now;
                await _db.UpdateAsync(row,
                    nameof(ISOClause.Title), nameof(ISOClause.Description),
                    nameof(ISOClause.IsValid), nameof(ISOClause.UpdateTime));
                updated++;
                continue;
            }

            // 父条款 = 编号去掉最后一段后，已存在（本批或库内）的最近祖先
            var parentNumber = TrimLastSegment(number);
            string? parentCode = null;
            while (parentNumber.Length > 0)
            {
                if (codeByNumber.TryGetValue(parentNumber, out var pc) && !string.IsNullOrEmpty(pc))
                {
                    parentCode = pc;
                    break;
                }
                parentNumber = TrimLastSegment(parentNumber);
            }

            maxSort += 10;
            var entity = new ISOClause
            {
                Code = Guid.NewGuid().ToString("N"),
                StandardCode = std.Code,
                ClauseNumber = number,
                Title = item.Title,
                Description = item.Description,
                ParentCode = parentCode,
                IsValid = 1,
                IsDeleted = false,
                Sort = maxSort,
                CreateTime = DateTime.Now,
            };
            await _db.InsertAsync(entity);
            codeByNumber[number] = entity.Code;
            byNumber[number] = entity;
            added++;
        }

        _logger.LogInformation("[StandardSource] 标准 {Std} 条款写入：新增 {Added} / 更新 {Updated} / 跳过 {Skipped}",
            std.StandardCode, added, updated, skipped);

        var result = new StandardClauseApplyResult
        {
            Success = true,
            StandardCode = std.StandardCode,
            Added = added,
            Updated = updated,
            Skipped = skipped,
            Cleared = cleared,
            Total = byNumber.Count,
        };

        // 写入成功后立刻重建「条款 Markdown」——派生视图，失败不阻塞条款落库
        try
        {
            var cm = await WriteClauseMarkdownAsync(std);
            result.ClauseMarkdownPath = cm.Path;
            result.ClauseMarkdownChars = cm.Chars;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StandardSource] 标准 {Std} 条款 Markdown 生成失败", std.StandardCode);
            result.MessageSuffix = "；条款 Markdown 生成失败，可稍后补建";
        }

        return StandardClauseApplyResult.Ok(result);
    }

    /// <summary>
    /// <b>补建条款 Markdown</b>：不重新解析原文、不新增/更新条款，只按现有
    /// <c>cert_iso_clause</c> 渲染派生视图。用于存量标准与生成失败后的重试。
    /// </summary>
    public async Task<StandardClauseMarkdownResult> RebuildClauseMarkdownAsync(string standardKey)
    {
        var std = await ResolveStandardAsync(standardKey);
        if (std == null) return StandardClauseMarkdownResult.Fail("标准不存在");

        try
        {
            var cm = await WriteClauseMarkdownAsync(std);
            return StandardClauseMarkdownResult.Ok(std.StandardCode, cm.Path, cm.Chars, cm.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StandardSource] 标准 {Std} 条款 Markdown 补建失败", std.StandardCode);
            return StandardClauseMarkdownResult.Fail(ex.Message);
        }
    }

    /// <summary>
    /// 取条款 Markdown 文本（页面预览 / 核对用）。不重新生成，只读已落库的路径。
    /// </summary>
    public async Task<StandardClauseMarkdownPreview?> PreviewClauseMarkdownAsync(string standardKey)
    {
        var std = await ResolveStandardAsync(standardKey);
        if (std == null) return null;

        var path = std.ClauseMarkdownPath;
        if (string.IsNullOrWhiteSpace(path)) return null;

        var markdown = await ReadMarkdownAsync(path!);
        if (string.IsNullOrEmpty(markdown)) return null;

        return new StandardClauseMarkdownPreview
        {
            StandardCode = std.StandardCode,
            StandardName = std.StandardName,
            ClauseMarkdownPath = path,
            ClauseMarkdownChars = std.ClauseMarkdownChars,
            ClauseCount = std.ClauseMarkdownChars > 0
                ? await CountExistingAsync(std.Code)
                : 0,
            Markdown = markdown,
            Length = markdown.Length,
        };
    }

    /// <summary>读取已生成的条款 Markdown；未生成返回 null。</summary>
    public async Task<string?> ReadClauseMarkdownAsync(string standardKey)
    {
        var preview = await PreviewClauseMarkdownAsync(standardKey);
        return preview?.Markdown;
    }

    /// <summary>
    /// 按 <c>cert_iso_clause</c> 渲染条款 Markdown 并上传 MinIO，同时回写标准行的三个字段。
    /// <para>★ 只读条款表 → 派生文件；⛔ 不反向解析该文件，条款表始终是事实源。</para>
    /// </summary>
    private async Task<(string Path, int Chars, int Count)> WriteClauseMarkdownAsync(ISOStandard std)
    {
        var clauses = await _db.Client.Queryable<ISOClause>()
            .Where(c => c.StandardCode == std.Code && !c.IsDeleted && c.IsValid == 1)
            .OrderBy(c => c.Sort)
            .ToListAsync();

        if (clauses == null || clauses.Count == 0)
            throw new InvalidOperationException("该标准还没有有效条款，无法生成条款 Markdown");

        var markdown = RenderClauseMarkdown(std, clauses);
        var path = PathBuilder.StandardSourceClauseMarkdown(std.Code);
        var bytes = Encoding.UTF8.GetBytes(markdown);
        using (var stream = new MemoryStream(bytes))
        {
            await _storage.UploadAsync(path.TrimStart('/'), stream, bytes.Length, "text/markdown");
        }

        std.ClauseMarkdownPath = path;
        std.ClauseMarkdownChars = markdown.Length;
        std.ClauseMarkdownUpdatedTime = DateTime.Now;
        std.UpdateTime = DateTime.Now;
        await _db.UpdateAsync(std,
            nameof(ISOStandard.ClauseMarkdownPath),
            nameof(ISOStandard.ClauseMarkdownChars),
            nameof(ISOStandard.ClauseMarkdownUpdatedTime),
            nameof(ISOStandard.UpdateTime));

        _logger.LogInformation("[StandardSource] 标准 {Std} 条款 Markdown 已生成：{Chars} 字 / {Count} 条",
            std.StandardCode, markdown.Length, clauses.Count);

        return (path, markdown.Length, clauses.Count);
    }

    /// <summary>
    /// 条款 Markdown 渲染（纯函数，可单测）。
    /// <para>标题层级按条款号段数：4 → 二级，4.1 → 三级，4.1.1 → 四级；
    /// 每条附条款号、父条款、是否叶子、标准要求原文。</para>
    /// </summary>
    public static string RenderClauseMarkdown(ISOStandard std, List<ISOClause> clauses)
    {
        var byCode = clauses
            .Where(c => !string.IsNullOrWhiteSpace(c.Code))
            .GroupBy(c => c.Code!.Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var childCount = clauses
            .Where(c => !string.IsNullOrWhiteSpace(c.ParentCode))
            .GroupBy(c => c.ParentCode!.Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var sb = new StringBuilder();
        sb.Append("# ").Append(std.StandardName ?? std.StandardCode).Append(" 条款索引（派生视图）\n\n");
        sb.Append("> 由 `cert_iso_clause` 自动渲染，**禁止人工编辑**；条款表才是事实源。\n");
        sb.Append("> 原始标准 Markdown：`").Append(std.MarkdownPath ?? "").Append("`\n");
        sb.Append("> 标准编号：`").Append(std.StandardCode ?? "").Append("`");
        sb.Append("　条款数：").Append(clauses.Count);
        sb.Append("　生成时间：").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("\n\n");

        foreach (var c in clauses)
        {
            var number = (c.ClauseNumber ?? "").Trim();
            if (number.Length == 0) continue;

            var depth = number.Count(ch => ch == '.') + 1;
            var level = Math.Clamp(depth + 1, 2, 6);
            var title = OneLine(c.Title);
            var parentNumber = "";
            if (!string.IsNullOrWhiteSpace(c.ParentCode) && byCode.TryGetValue(c.ParentCode!.Trim(), out var p))
                parentNumber = (p.ClauseNumber ?? "").Trim();
            var isLeaf = !childCount.ContainsKey((c.Code ?? "").Trim());

            sb.Append(new string('#', level)).Append(' ').Append(number);
            if (title.Length > 0) sb.Append(' ').Append(title);
            sb.Append('\n');
            sb.Append("- 条款号：").Append(number).Append('\n');
            sb.Append("- 父条款：").Append(parentNumber.Length == 0 ? "（无）" : parentNumber).Append('\n');
            sb.Append("- 叶子：").Append(isLeaf ? "是" : "否").Append('\n');
            sb.Append("- 标准要求：\n");
            var desc = (c.Description ?? "").Replace("\r\n", "\n").Trim();
            sb.Append(desc.Length == 0 ? "（无正文）" : desc).Append("\n\n");
        }

        return sb.ToString().TrimEnd() + "\n";
    }

    private static string OneLine(string? text)
        => (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();

    // ════════════════════════════════════════════════════════════════════
    // 解析内核（通用，任何标准 Markdown 都走这里）
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>从标准 Markdown 解析条款列表</b>（确定性，零成本）。
    /// <para>识别 <c>### 4.1 标题</c> 这类带编号的标题；正文 = 该标题到下一个编号标题之间的内容。</para>
    /// </summary>
    public static List<ParsedClause> ParseClauses(string markdown)
    {
        var result = new List<ParsedClause>();
        if (string.IsNullOrWhiteSpace(markdown)) return result;

        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        ParsedClause? current = null;
        var body = new StringBuilder();

        void Flush()
        {
            if (current == null) return;
            current.Description = CleanDescription(body.ToString());
            if (current.Description.Length > 5000)
                current.Description = current.Description.Substring(0, 5000);
            result.Add(current);
            body.Clear();
            current = null;
        }

        foreach (var raw in lines)
        {
            var line = raw ?? "";

            // 附录 / 前言 / 参考文献是条款正文的硬边界：遇到就收口，
            // ⛔ 不能继续把附录表格、参考文献灌进最后一条条款（9001 的 10.3、14001 的 10.3 都踩过）。
            // PDF 里这些字常被拆成「附 录 A」，所以先去掉空白再判断。
            if (IsBoundaryHeading(line))
            {
                Flush();
                current = null;
                continue;
            }

            var head = MarkdownHead.Match(line);
            if (!head.Success) head = NumberHead.Match(line.TrimStart());

            if (head.Success)
            {
                var num = head.Groups["num"].Value.Trim();

                // ★ 2026-11-11：PDF 里的「新旧标准条款对照表」也会被 anydoc 升成标题，
                //   形如 `### 4.3 确定范围 & 4.1 总要求 \\`，正文只有 `\hline`。
                //   这类行不是条款，必须在 `seen` 之前丢掉；否则它会抢在真正条款前面占住编号，
                //   真正的 `### 4.3 确定范围` 反而被去重掉（13485 因此多出 44 条）。
                if (IsCrossReferenceRow(head.Groups["title"].Value))
                {
                    // ⛔ 不写进正文：它只是对照表的一行，会把上一节正文污染成表符。
                    continue;
                }

                var title = CleanTitle(head.Groups["title"].Value);

                // 过滤：目录点线（…… / ...）、误命中年份、引言（0.x）、附录（A.x）
                //   引言与附录不是审核对象，⛔ 不进条款树（否则「按原文生成」会多出一批不可审核条款）
                if (num == "0" || num.Contains("--") || title.Length == 0 || title.StartsWith("－") || IsYearLike(num)
                    || IsTocTitle(title)
                    || num.StartsWith("0.", StringComparison.Ordinal)
                    || Regex.IsMatch(num, @"^[A-Z]\."))
                {
                    // 引言 / 附录编号同样收口：它们不是审核对象，不能并入上一条条款正文。
                    if (num.StartsWith("0.", StringComparison.Ordinal) || Regex.IsMatch(num, @"^[A-Z]\."))
                    {
                        Flush();
                        current = null;
                    }
                    else if (current != null) body.AppendLine(line);
                    continue;
                }

                Flush();
                SplitClauseTitle(title, out var clauseTitle, out var inlineBody);
                current = new ParsedClause
                {
                    ClauseNumber = num,
                    Title = clauseTitle.Length > 200 ? clauseTitle.Substring(0, 200) : clauseTitle,
                };
                if (inlineBody.Length > 0) body.AppendLine(inlineBody);
                continue;
            }

            if (current != null) body.AppendLine(line);
        }

        Flush();

        // 去重（同编号重复出现时保留第一次）并按编号排序稳定
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return result
            .Where(c => c.ClauseNumber.Length > 0 && seen.Add(c.ClauseNumber))
            .OrderBy(c => c.ClauseNumber, ClauseNumberComparer.Instance)
            .ToList();
    }

    /// <summary>附录 / 前言 / 参考文献标题（允许字间夹空格，如「附 录 A」）。</summary>
    private static bool IsBoundaryHeading(string line)
    {
        var t = Regex.Replace((line ?? "").Trim(), @"\s+", "");
        t = Regex.Replace(t, @"^#{1,6}", "");
        return Regex.IsMatch(t, @"^(附录|前言|参考文献)");
    }

    /// <summary>
    /// 把「标题 + 同行的正文」拆开。anydoc 常把标题和正文挤在同一行：
    /// <c>10.1 总则 组织应确定改进的机会…</c>。只取前面的短标题，后面的内容当正文。
    /// </summary>
    private static void SplitClauseTitle(string title, out string clauseTitle, out string inlineBody)
    {
        clauseTitle = (title ?? "").Trim();
        inlineBody = "";
        if (clauseTitle.Length <= 24 && !clauseTitle.Contains("。") && !clauseTitle.Contains("：")
            && !clauseTitle.Contains(":") && !clauseTitle.Contains("；") && !clauseTitle.Contains(";")) return;

        var m = Regex.Match(clauseTitle, @"\s+");
        if (!m.Success || m.Index == 0) return;

        var head = clauseTitle.Substring(0, m.Index).Trim();
        var rest = clauseTitle.Substring(m.Index).Trim();
        if (head.Length == 0 || head.Length > 30 || rest.Length < 8) return;

        clauseTitle = head;
        inlineBody = rest;
    }

    /// <summary>
    /// 判断标题是否是「目录行」。PDF 的目录会被 anydoc 升成标题，形如
    /// <c>### 10.1 总则 … … … … 13</c>；点线中间可能夹空格，不能用 Contains("……")。
    /// 规则：去掉空白后仍出现 2 个以上连续的点线字符（<c>…</c> / <c>.</c>）。
    /// </summary>
    private static bool IsTocTitle(string? title)
    {
        var t = Regex.Replace(title ?? "", @"\s+", "");
        return Regex.IsMatch(t, @"[.…]{2,}");
    }

    private static bool IsCrossReferenceRow(string? title)
    {
        var t = (title ?? "").Trim();
        if (Regex.IsMatch(t, @"\\\\\s*$")) return true;          // 行尾 LaTeX 换行符
        return Regex.IsMatch(t, @"&\s*(?:\d+(?:\.\d+)*|[A-Z]+(?:\.\d+)+)(?:\s|$)");
    }

    private static string CleanTitle(string? title)
    {
        var t = CleanLatex(title);
        // PDF 表格里常把「本条款标题 & 对应旧版条款号 标题 \\」拼成一行；
        // 只保留 & 左边属于本标准条款的标题，右边是对照列，不是标题。
        t = Regex.Replace(t, @"\s*&\s*(?:\d+(?:\.\d+)*|[A-Z]+(?:\.\d+)+)\s+.*$", "").Trim();
        t = Regex.Replace(t, @"[\s\.。]+$", "");
        return t;
    }

    /// <summary>
    /// 清洗 PDF / anydoc 转 Markdown 时残留的 LaTeX 表符：
    /// <c>\\</c>（换行）、<c>\hline</c>、<c>\begin{tabular}</c>、<c>\multicolumn</c> 等。
    /// </summary>
    private static string CleanLatex(string? text)
    {
        var s = (text ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\u00a0", " ");
        s = s.Replace("\\\\", " ");                       // 两个反斜杠 = LaTeX 换行
        s = Regex.Replace(s, @"\\(?:hline|toprule|midrule|bottomrule|endhead|endfirsthead|endfoot|endlastfoot|noalign)", " ");
        s = Regex.Replace(s, @"\\(?:begin|end)\s*\{[^}]*\}", " ");
        s = Regex.Replace(s, @"\\(?:multicolumn|caption)\s*\{[^}]*\}(?:\{[^}]*\})?", " ");
        s = Regex.Replace(s, @"\\[a-zA-Z]+", " ");          // 其它 LaTeX 命令
        s = s.Replace("\\", " ");                           // 残留单个反斜杠
        s = s.Replace("{", " ").Replace("}", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    /// <summary>正文按行清洗：丢弃 LaTeX 表线、纯条款对照行、页码、HTML 分页注释。</summary>
    private static string CleanDescription(string? body)
    {
        var s = (body ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        var kept = new List<string>();
        foreach (var raw in s.Split('\n'))
        {
            var line = CleanLatex(raw);
            if (line.Length == 0) continue;
            if (Regex.IsMatch(line, @"^<!--.*-->$")) continue;
            if (Regex.IsMatch(line, @"^[&|]+$")) continue;
            if (Regex.IsMatch(line, @"^(?:&\s*)?(?:\d+(?:\.\d+)*|[A-Z]+(?:\.\d+)+)(?:\s|$)")) continue;
            if (Regex.IsMatch(line, @"^\d{1,3}$")) continue;
            // PDF 两端对齐会把中文拆成「组 织 应」，中文与中文之间的空格不是语义空格，直接收掉。
            line = Regex.Replace(line, @"(?<=[\u4e00-\u9fa5])\s+(?=[\u4e00-\u9fa5])", "");
            kept.Add(line);
        }

        var text = Regex.Replace(string.Join("\n", kept), @"\n{3,}", "\n\n").Trim();
        return text;
    }

    private static bool IsYearLike(string num) => num.Length == 4 && num.StartsWith("19") || num.Length == 4 && num.StartsWith("20");

    private static string TrimLastSegment(string number)
    {
        var idx = number.LastIndexOf('.');
        return idx < 0 ? "" : number.Substring(0, idx);
    }

    // ════════════════════════════════════════════════════════════════════
    // LLM 兜底（仅当确定性解析失败时）
    // ════════════════════════════════════════════════════════════════════

    private async Task<List<ParsedClause>> ExtractWithLlmAsync(string standardName, string markdown)
    {
        var ai = await ReadAiSettingsAsync();
        if (string.IsNullOrWhiteSpace(ai.ApiKey)) return new List<ParsedClause>();

        var chunks = SplitByLines(markdown, LlmChunkChars);
        var merged = new Dictionary<string, ParsedClause>(StringComparer.Ordinal);

        foreach (var chunk in chunks)
        {
            var prompt = "你是标准化文件解析器。下面是中国国家标准《" + standardName + "》的一段 Markdown 原文。\n"
                + "任务：抽取本段出现的**条款**（带编号的条文），输出 JSON 数组。\n"
                + "规则：\n"
                + "1. ClauseNumber 原样照抄原文编号（如 4.1、8.2.3、A.1），不要重排、不要补号；\n"
                + "2. Title 用原文条款标题；没有标题时用正文首句概括（≤20 字）；\n"
                + "3. Description 摘录该条款正文，保留原文表述，≤500 字；\n"
                + "4. 只输出原文中真实出现的条款，不要编造；前言/引言/封面/目次 不属于条款，不要输出；\n"
                + "5. 只输出 JSON 数组，不要任何解释。\n\n"
                + "【原文片段】\n" + chunk;

            var resp = await _llm.CompleteAsync(new LlmInvokeRequest
            {
                BaseUrl = ai.BaseUrl,
                ApiKey = ai.ApiKey,
                Model = ai.Model,
                Temperature = 0.1f,
                MaxTokens = Math.Min(ai.MaxTokens, 8192),
                Prompt = prompt,
                ForceJson = true,
            });

            if (!resp.Success) continue;
            try
            {
                var raw = StripFence(resp.Content);
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var number = el.TryGetProperty("ClauseNumber", out var n) ? (n.GetString() ?? "").Trim() : "";
                    if (number.Length == 0 || merged.ContainsKey(number)) continue;
                    if (number.StartsWith("0.", StringComparison.Ordinal) || Regex.IsMatch(number, @"^[A-Z]\.")) continue;
                    merged[number] = new ParsedClause
                    {
                        ClauseNumber = number,
                        Title = el.TryGetProperty("Title", out var t) ? (t.GetString() ?? "").Trim() : "",
                        Description = el.TryGetProperty("Description", out var d) ? (d.GetString() ?? "").Trim() : "",
                    };
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[StandardSource] LLM 条款 JSON 解析失败，跳过本段");
            }
        }

        return merged.Values
            .OrderBy(c => c.ClauseNumber, ClauseNumberComparer.Instance)
            .ToList();
    }

    private static List<string> SplitByLines(string text, int chunkChars)
    {
        var chunks = new List<string>();
        var sb = new StringBuilder();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (sb.Length + line.Length + 1 > chunkChars && sb.Length > 0)
            {
                chunks.Add(sb.ToString());
                sb.Clear();
            }
            sb.AppendLine(line);
        }
        if (sb.Length > 0) chunks.Add(sb.ToString());
        return chunks;
    }

    // ════════════════════════════════════════════════════════════════════
    // 基础设施
    // ════════════════════════════════════════════════════════════════════

    private async Task<ISOStandard?> ResolveStandardAsync(string key)
    {
        var k = (key ?? "").Trim();
        if (k.Length == 0) return null;
        var list = await _db.Client.Ado.SqlQueryAsync<ISOStandard>(
            "SELECT * FROM cert_iso_standard WHERE (StandardCode = @k OR Code = @k) AND IsDeleted = 0 AND IsValid = 1 LIMIT 1",
            new { k });
        return list?.FirstOrDefault();
    }

    private async Task<int> CountExistingAsync(string stdGuid)
    {
        var list = await _db.Client.Queryable<ISOClause>()
            .Where(c => c.StandardCode == stdGuid && !c.IsDeleted)
            .ToListAsync();
        return list.Count;
    }

    private async Task<string> ReadMarkdownAsync(string markdownPath)
    {
        try
        {
            var (stream, _) = await _storage.DownloadAsync(markdownPath.TrimStart('/'));
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[StandardSource] 读取标准 Markdown 失败: {Path}", markdownPath);
            return "";
        }
    }

    private async Task<AiCfg> ReadAiSettingsAsync()
    {
        var s = new AiCfg();
        var rows = await _db.Client.Queryable<SysConfig>()
            .Where(x => x.Category == "ai_model" && !x.IsDeleted)
            .Select(x => new { x.ConfigKey, x.ConfigValue })
            .ToListAsync();
        foreach (var row in rows)
        {
            switch (row.ConfigKey)
            {
                case "ai_api_key": s.ApiKey = row.ConfigValue ?? ""; break;
                case "ai_base_url": s.BaseUrl = row.ConfigValue ?? s.BaseUrl; break;
                case "ai_model_name": s.Model = row.ConfigValue ?? s.Model; break;
                case "ai_max_tokens":
                    if (int.TryParse(row.ConfigValue, out var mt)) s.MaxTokens = mt;
                    break;
            }
        }
        return s;
    }

    private static string StripFence(string text)
    {
        var t = (text ?? "").Trim();
        if (!t.StartsWith("```", StringComparison.Ordinal)) return t;
        var nl = t.IndexOf('\n');
        if (nl < 0) return t;
        t = t.Substring(nl + 1);
        var end = t.LastIndexOf("```", StringComparison.Ordinal);
        return (end >= 0 ? t.Substring(0, end) : t).Trim();
    }

    private sealed class AiCfg
    {
        public string ApiKey { get; set; } = "";
        public string BaseUrl { get; set; } = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        public string Model { get; set; } = "qwen-plus";
        public int MaxTokens { get; set; } = 8192;
    }
}

/// <summary>条款编号排序（4 &lt; 4.1 &lt; 4.1.1 &lt; 5；附录 A.1 排在最后）</summary>
public sealed class ClauseNumberComparer : IComparer<string>
{
    public static readonly ClauseNumberComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        var xs = Split(x); var ys = Split(y);
        var xn = xs[0].StartsWith("A", StringComparison.OrdinalIgnoreCase) ? int.MaxValue : 0;
        var yn = ys[0].StartsWith("A", StringComparison.OrdinalIgnoreCase) ? int.MaxValue : 0;
        if (xn != yn) return xn.CompareTo(yn);

        var i = 0;
        while (i < xs.Count && i < ys.Count)
        {
            var a = int.TryParse(xs[i], out var av) ? av : 0;
            var b = int.TryParse(ys[i], out var bv) ? bv : 0;
            if (a != b) return a.CompareTo(b);
            i++;
        }
        return xs.Count.CompareTo(ys.Count);
    }

    private static List<string> Split(string? s)
        => (s ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
}

public sealed class ParsedClause
{
    public string ClauseNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class StandardSourcePreview
{
    public string StandardCode { get; set; } = "";
    public string StandardName { get; set; } = "";
    public string MarkdownPath { get; set; } = "";
    public int MarkdownChars { get; set; }
    public string? SourceFile { get; set; }
    public int ParsedCount { get; set; }
    public int ExistingCount { get; set; }
    /// <summary>markdown-heading / llm</summary>
    public string Parser { get; set; } = "";
    public List<ParsedClause> Clauses { get; set; } = new();
}

public sealed class StandardSourceUploadResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public StandardSourcePreview? Data { get; set; }

    public static StandardSourceUploadResult Ok(StandardSourcePreview data)
        => new() { Success = true, Message = "已转换并解析，确认后可写入条款", Data = data };

    public static StandardSourceUploadResult Fail(string message)
        => new() { Success = false, Message = message };
}

public sealed class StandardClauseApplyResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string StandardCode { get; set; } = "";
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    /// <summary>overwrite=true 时被软删除的旧条款数</summary>
    public int Cleared { get; set; }
    public int Total { get; set; }

    /// <summary>条款 Markdown 路径（派生视图；生成失败时为空）</summary>
    public string? ClauseMarkdownPath { get; set; }

    /// <summary>条款 Markdown 字符数</summary>
    public int ClauseMarkdownChars { get; set; }

    /// <summary>附加到成功消息末尾的说明（如生成失败）</summary>
    public string? MessageSuffix { get; set; }

    public static StandardClauseApplyResult Ok(StandardClauseApplyResult r)
    {
        r.Message = $"条款写入完成：清空 {r.Cleared}，新增 {r.Added}，更新 {r.Updated}，跳过已存在 {r.Skipped}"
                    + (r.ClauseMarkdownChars > 0 ? $"；条款 Markdown 已生成（{r.ClauseMarkdownChars} 字）" : "")
                    + (r.MessageSuffix ?? "");
        return r;
    }

    public static StandardClauseApplyResult Fail(string message)
        => new() { Success = false, Message = message };
}

/// <summary>条款 Markdown 补建结果</summary>
public sealed class StandardClauseMarkdownResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string StandardCode { get; set; } = "";
    public string? ClauseMarkdownPath { get; set; }
    public int ClauseMarkdownChars { get; set; }
    public int ClauseCount { get; set; }

    public static StandardClauseMarkdownResult Ok(string standardCode, string path, int chars, int count)
        => new()
        {
            Success = true,
            StandardCode = standardCode,
            ClauseMarkdownPath = path,
            ClauseMarkdownChars = chars,
            ClauseCount = count,
            Message = $"条款 Markdown 已生成：{count} 条 / {chars} 字",
        };

    public static StandardClauseMarkdownResult Fail(string message)
        => new() { Success = false, Message = message };
}

/// <summary>条款 Markdown 预览（只读）</summary>
public sealed class StandardClauseMarkdownPreview
{
    public string StandardCode { get; set; } = "";
    public string StandardName { get; set; } = "";
    public string ClauseMarkdownPath { get; set; } = "";
    public int ClauseMarkdownChars { get; set; }
    public int ClauseCount { get; set; }
    public string Markdown { get; set; } = "";
    public int Length { get; set; }
}
