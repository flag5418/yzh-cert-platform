using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CertPlatform.Shared.Fill
{
    /// <summary>
    /// <b>语义分析结果提取器</b> —— 从「提示词工作台」两次 LLM 调用
    /// （<c>doc_group</c> 分类 / <c>doc_content</c> 作用）的返回 JSON 里，
    /// 按<b>统一口径</b>取出标签 / 作用 / 包含信息等字段。
    ///
    /// <para><b>★ 为什么放在 <c>CertPlatform.Shared</c></b>（2026-10-05）：调用方有两处，且分属不同项目 ——</para>
    /// <list type="number">
    /// <item><c>CertPlatform.Auditor</c> 的 <c>EnterpriseOriginalAnalyzeExecutor</c>
    /// —— 企业原始资料分析，结果落 <c>cert_enterprise_doc_profile</c>（画像表）；</item>
    /// <item><c>CertPlatform.Admin</c> 的 <c>StandardDocContractController</c>
    /// —— 标准文档语义分析，结果落 <c>cert_standard_doc_contract</c>（契约表）。</item>
    /// </list>
    ///
    /// <para>⚠️ <c>Admin</c> ⛔ <b>不引用</b> <c>Auditor</c>（引用方向是 Auditor → Admin）⇒
    /// 这两个方法<b>无法</b>从 Auditor 侧被 Admin 复用。若在 Admin 里再抄一遍就是
    /// <b>「复制即漂移」</b>：33 号的输出校验口径一旦分叉，两张表的画像字段会长出不同形状。
    /// 故下沉到双方都引用的 <c>Shared</c>。</para>
    ///
    /// <para>⚠️ 因此本类<b>不得</b>依赖 <c>PromptWorkbenchService.AnalyzeForQueueResult</c>
    /// （那个类型在 <c>CertPlatform.Admin</c> 里，<c>Shared</c> 引用它会形成<b>项目循环</b>）
    /// —— 需要的那一个字段（<c>ValidationMessages</c>）改为<b>显式传参</b>。</para>
    ///
    /// <para>纯静态、零依赖（只用 <c>System.Text.Json</c>），可安全被两侧共用。</para>
    /// </summary>
    public static class SemanticHints
    {
        /// <summary>从 JSON 对象里读一个字符串属性（非字符串 / 不存在 ⇒ <c>null</c>）</summary>
        public static string? ReadString(JsonElement el, string prop)
            => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        /// <summary>
        /// 解析 <c>doc_group</c> 的批次返回：文件名 → 该文件的结论对象。
        ///
        /// <para>期望结构 <c>{"items":[{"fileName":"…","tags":[…],"docCategory":"…",…}]}</c>；
        /// 文件名同时兼容 <c>fileName</c> 与 <c>name</c> 两个键，<b>大小写不敏感</b>。
        /// 匹配不上的文件不进 map，调用方按「无 L1 结论」继续走 L2。</para>
        /// </summary>
        public static Dictionary<string, JsonElement> ParseGroupItems(string json)
        {
            var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var it in items.EnumerateArray())
                    {
                        if (it.ValueKind != JsonValueKind.Object) continue;
                        var name = ReadString(it, "fileName") ?? ReadString(it, "name");
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        map[name!] = it.Clone();
                    }
                }
            }
            catch (JsonException) { /* 非法 JSON 已由上游输出校验拦过，这里只做防御 */ }
            return map;
        }

        /// <summary>
        /// 从 <c>doc_group</c> 结论里取「标签 / 理由 / 置信度 / 类型猜测 / 关键词 / 摘要 / 分类」。
        /// <para><paramref name="validationMessages"/> 是上游（<c>AnalyzeForQueueResult.ValidationMessages</c>）
        /// 的后端自动校正明细，会并入「理由」一并留痕，供人工复核。</para>
        /// </summary>
        public static (string? TagsJson, string? Reason, decimal? Conf, string? TypeGuess,
                       string? Keywords, string? Summary, string? Category)
            ExtractGroupHints(Dictionary<string, JsonElement> map, string fileName,
                              IReadOnlyList<string>? validationMessages = null)
        {
            if (!map.TryGetValue(fileName, out var g)) return (null, null, null, null, null, null, null);
            if (g.ValueKind != JsonValueKind.Object) return (null, null, null, null, null, null, null);

            var tagsJson = g.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Array
                ? t.GetRawText() : null;

            var reasonParts = new List<string>();
            var typeGuess = ReadString(g, "typeGuess");
            if (!string.IsNullOrWhiteSpace(typeGuess)) reasonParts.Add($"类型猜测：{typeGuess}");
            if (validationMessages != null) reasonParts.AddRange(validationMessages);

            decimal? conf = null;
            if (g.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number
                && c.TryGetDouble(out var cd)) conf = (decimal)Math.Round(cd, 2);

            return (tagsJson,
                    reasonParts.Count == 0 ? null : string.Join("；", reasonParts),
                    conf,
                    typeGuess,
                    ReadString(g, "keywords"),
                    ReadString(g, "summary"),
                    ReadString(g, "docCategory"));
        }

        /// <summary>
        /// 从 <c>doc_content</c> 结论里取「作用 / 作用置信度 / 包含信息 / 字段 / 表格 / 总体置信度」。
        /// <para>置信度键同时兼容 <c>purposeConfidence</c> 与 <c>docPurposeConfidence</c>。</para>
        /// </summary>
        public static (string? Purpose, decimal? PurposeConf, string? InfoItems,
                       string? Fields, string? Tables, decimal? Conf)
            ExtractContentHints(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return (null, null, null, null, null, null);

                decimal? Num(string prop)
                    => root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number
                       && v.TryGetDouble(out var d) ? (decimal)Math.Round(d, 2) : null;

                string? Arr(string prop)
                    => root.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Array
                       ? v.GetRawText() : null;

                return (ReadString(root, "purpose"), Num("purposeConfidence") ?? Num("docPurposeConfidence"),
                        Arr("infoItems"), Arr("fields"), Arr("tables"), Num("confidence"));
            }
            catch (JsonException) { return (null, null, null, null, null, null); }
        }
    }
}
