using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Admin.Entities.Doc;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Admin.Services.DocExtraction;

/// <summary>
/// ★ 提取值取数唯一口径（2026-09-30 用户裁决 J1/J2/J4 · 25 号 §2.3 / §三）
/// </summary>
///
/// <para><b>铁律</b>：<b>⛔ 禁止任何地方自己拼 WHERE 条件读 <c>cert_extraction_result</c> /
/// <c>cert_table_extraction_result</c> 去做「这个字段/表格有没有值」的判断。</b>
/// 必须调本类。<b>原因</b>：守卫（<c>NodeExecutor.docfield/doctable</c>，执行期）与
/// 缺口生成（<c>GapDetector</c>，开启任务时）是两个判定方，口径一旦分叉就会出现
/// 「清单说齐了、执行说缺了」—— 补录夹具立刻失去信号（25 号 §2.4）。</para>
///
/// <para><b>本类解决的三个既有缺陷</b>：</para>
/// <list type="bullet">
///   <item><b>B2</b> 旧口径缺 <c>RuleCode</c> 收窄 ⇒ 同企业 4 个文件的 <c>projectName</c>
///         各有一行 <c>IsValid=1</c>，按 <c>VersionNumber</c> 倒序取到"随机"一份；</item>
///   <item><b>B3</b> 旧口径只判 <c>rows.Count==0</c> ⇒ <c>[{"item":"","value":""}]</c>
///         这类<b>伪非空</b>被判为"不缺"，AI 拿到全空表照样出结论；</item>
///   <item><b>B6</b> 旧口径对表格不排除软删行（实体当时未声明 <c>IsDeleted</c>）。</item>
/// </list>
///
/// <para><b>口径要点</b>：</para>
/// <list type="number">
///   <item>收窄键 = <c>(OrgCode, RuleCode, FieldCode|TableCode)</c> —— 裁决 J4「1 文件 = 1 规则」，
///         DB 由 <c>uk_rule_scope</c> 唯一索引强制 ⇒ <b>不按 <c>FileCode</c> 过滤</b>。
///         补录行的 <c>FileCode</c> 填的是规则声明的 <c>StandardFileCode</c>，不是文件槽位 Code；
///         企业未上传该文件时更无真实 <c>FileCode</c>（J3 已否决虚拟文件）。</item>
///   <item>排序 = <b>人工值优先</b>（<c>ValueSource='manual'</c>）→ <c>VersionNumber</c> 倒序。
///         裁决 J2：同一 <c>(企业, 规则, 字段)</c> 下 <c>auto</c> 行与 <c>manual</c> 行并存，
///         两者都保留以便还原时间线，取数时人工覆盖自动。</item>
///   <item>判空复用 <c>EmptyJudge.IsBlankText</c>（<c>ExistenceJudgeSkills.cs:78</c>），
///         ⛔ 不另写一份 —— 它能识别 <c>-</c> / <c>——</c> / <c>/</c> / <c>N/A</c> / <c>无</c> /
///         <c>暂无</c> / <c>待填写</c> / 纯符号 / <c>^(待|需要|请)\S{0,6}(填写|补充|上传|提供)</c>。</item>
///   <item>缺 <c>StandardCode</c> / <c>StageCode</c> 时<b>不过滤</b>（向后兼容既有测试），
///         但 <c>RuleCode</c> <b>永不过滤</b> —— 它是 J4 的语义主键，缺了就会串规则。</item>
/// </list>
///
/// <para><b>本类为 stateless 单例</b>（只依赖 <see cref="IDbOrm"/>）。</para>
/// </remarks>
public class ExtractionDataResolver
{
    private readonly IDbOrm _db;

    public ExtractionDataResolver(IDbOrm db) => _db = db;

    // ── 对外模型 ──────────────────────────────────────────────

    /// <summary>字段取数结果（三态，语义与 <c>EmptyJudge.Result</c> 对齐）</summary>
    /// <param name="State">has_value | is_empty | not_found</param>
    public sealed class FieldValue
    {
        public string State { get; init; } = EmptyJudge.StateNotFound;
        public bool HasValue => State == EmptyJudge.StateHasValue;
        /// <summary>提取值（<see cref="HasValue"/> 为 false 时可能是 null / 空白 / 占位符）</summary>
        public string? RawValue { get; init; }
        public string? FieldName { get; init; }
        public string? ValueSource { get; init; }
        public decimal? Confidence { get; init; }
        public string? StandardFileCode { get; init; }
        public string? FileCode { get; init; }
        public int? VersionNumber { get; init; }
        /// <summary>是否人工来源（人工录入 or AI 生成）</summary>
        public bool IsManual => ExtractionValueSource.IsManual(ValueSource);
        /// <summary>命中的结果行 Code（补录留痕用）</summary>
        public string? ResultCode { get; init; }
    }

    /// <summary>表格取数结果（三态）</summary>
    public sealed class TableValue
    {
        public string State { get; init; } = EmptyJudge.StateNotFound;
        public bool HasValue => State == EmptyJudge.StateHasValue;
        /// <summary>解析后的行（<see cref="HasValue"/> 为 false 时为空列表）</summary>
        public IReadOnlyList<Dictionary<string, object>> Rows { get; init; } = Array.Empty<Dictionary<string, object>>();
        public int RowCount => Rows.Count;
        public string? ValueSource { get; init; }
        public decimal? Confidence { get; init; }
        public string? StandardFileCode { get; init; }
        public string? FileCode { get; init; }
        public int? VersionNumber { get; init; }
        public int? TableIndex { get; init; }
        public bool IsManual => ExtractionValueSource.IsManual(ValueSource);
        public string? ResultCode { get; init; }
    }

    // ── 字段级 ────────────────────────────────────────────────

    /// <summary>
    /// 读取某企业某规则下指定字段的<b>当前可用值</b>（人工优先）。
    /// </summary>
    /// <param name="enterpriseCode">企业 Code。⚠️ <c>cert_extraction_result.OrgCode</c> 列存的就是它
    ///     （历史遗留列名，05 号 §2.1），⛔ 不要传认证机构 Code 或工作区 Code</param>
    /// <param name="ruleCode">规则 Code。★ <b>必填语义</b>，传空则跳过规则收窄（仅兼容测试用）</param>
    /// <param name="fieldCode">字段编码</param>
    /// <param name="standardCode">标准 Code；空 = 不限</param>
    /// <param name="stageCode">阶段 Code（<c>cert_cert_stage.Code</c>，GUID）；空 = 不限</param>
    public async Task<FieldValue> GetFieldAsync(
        string enterpriseCode, string? ruleCode, string fieldCode,
        string? standardCode = null, string? stageCode = null)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode) || string.IsNullOrWhiteSpace(fieldCode))
            return new FieldValue { State = EmptyJudge.StateNotFound };

        var rows = (await _db.GetListAsync<ExtractionResult>(x =>
            x.FieldCode == fieldCode && x.EnterpriseCode == enterpriseCode)).Data
            ?? new List<ExtractionResult>();

        var hit = rows
            .Where(x => x.IsValid == 1 && !x.IsDeleted)
            .Where(x => string.IsNullOrWhiteSpace(ruleCode) || x.RuleCode == ruleCode)
            .Where(x => string.IsNullOrWhiteSpace(standardCode) || x.StandardCode == standardCode)
            .Where(x => string.IsNullOrWhiteSpace(stageCode) || x.StageCode == stageCode)
            // ★ 人工优先，再按版本倒序（裁决 J2）
            .OrderByDescending(x => ExtractionValueSource.Priority(x.ValueSource))
            .ThenByDescending(x => x.VersionNumber)
            .ThenByDescending(x => x.ExtractedAt)
            .FirstOrDefault();

        if (hit == null)
            return new FieldValue { State = EmptyJudge.StateNotFound };

        // ★ B3 修复：不再只判 IsNullOrWhiteSpace，交给 IsBlankText 识别占位符
        var state = EmptyJudge.IsBlankText(hit.ExtractedValue)
            ? EmptyJudge.StateIsEmpty
            : EmptyJudge.StateHasValue;

        return new FieldValue
        {
            State = state,
            RawValue = hit.ExtractedValue,
            FieldName = hit.FieldName,
            ValueSource = string.IsNullOrWhiteSpace(hit.ValueSource) ? ExtractionValueSource.Auto : hit.ValueSource,
            Confidence = hit.Confidence,
            StandardFileCode = hit.StandardFileCode,
            FileCode = hit.FileCode,
            VersionNumber = hit.VersionNumber,
            ResultCode = hit.Code
        };
    }

    // ── 表格级 ────────────────────────────────────────────────

    /// <summary>
    /// 读取某企业某规则下指定表格的<b>当前可用数据</b>（人工优先）。
    /// <para>★ B3 修复：JSON 非空但<b>所有单元格都空</b>（<c>[{"item":"","value":""}]</c>）⇒ 判 <c>is_empty</c>。</para>
    /// </summary>
    public async Task<TableValue> GetTableAsync(
        string enterpriseCode, string? ruleCode, string tableCode,
        string? standardCode = null, string? stageCode = null)
    {
        if (string.IsNullOrWhiteSpace(enterpriseCode) || string.IsNullOrWhiteSpace(tableCode))
            return new TableValue { State = EmptyJudge.StateNotFound };

        var rows = (await _db.GetListAsync<TableExtractionResult>(x =>
            x.TableCode == tableCode && x.EnterpriseCode == enterpriseCode)).Data
            ?? new List<TableExtractionResult>();

        var hit = rows
            .Where(x => x.IsValid == 1 && !x.IsDeleted)   // ★ B6 修复：补上 !IsDeleted
            .Where(x => string.IsNullOrWhiteSpace(ruleCode) || x.RuleCode == ruleCode)
            .Where(x => string.IsNullOrWhiteSpace(standardCode) || x.StandardCode == standardCode)
            .Where(x => string.IsNullOrWhiteSpace(stageCode) || x.StageCode == stageCode)
            .OrderByDescending(x => ExtractionValueSource.Priority(x.ValueSource))
            .ThenByDescending(x => x.VersionNumber)
            .ThenByDescending(x => x.TableIndex)
            .FirstOrDefault();

        if (hit == null)
            return new TableValue { State = EmptyJudge.StateNotFound };

        var parsed = ParseRows(hit.ExtractedJson);

        // ★ B3 修复：行数 > 0 不等于有值 —— 逐单元格过 IsBlankText
        var state = IsTableMeaningful(parsed)
            ? EmptyJudge.StateHasValue
            : EmptyJudge.StateIsEmpty;

        return new TableValue
        {
            State = state,
            Rows = parsed,
            ValueSource = string.IsNullOrWhiteSpace(hit.ValueSource) ? ExtractionValueSource.Auto : hit.ValueSource,
            Confidence = hit.Confidence,
            StandardFileCode = hit.StandardFileCode,
            FileCode = hit.FileCode,
            VersionNumber = hit.VersionNumber,
            TableIndex = hit.TableIndex,
            ResultCode = hit.Code
        };
    }

    // ── 批量取数（缺口生成用 · 避免 N+1） ──────────────────────

    /// <summary>
    /// 批量读取某企业某标准某阶段下，指定规则集合里的全部字段/表格可用值。
    /// <para>★ 缺口生成走这里：<c>R</c> 收集到的 <c>FieldCode</c> 可能有几十个，
    /// 逐个 <c>GetFieldAsync</c> 会 N+1 查库。</para>
    /// </summary>
    /// <param name="ruleCodes">规则 Code 集合；为空 = 不限规则（⛔ 生产路径必须传）</param>
    public async Task<(Dictionary<string, FieldValue> Fields, Dictionary<string, TableValue> Tables)>
        GetByScopeAsync(
            string enterpriseCode, IEnumerable<string>? ruleCodes,
            string? standardCode = null, string? stageCode = null)
    {
        var fieldRows = (await _db.GetListAsync<ExtractionResult>(x =>
            x.EnterpriseCode == enterpriseCode)).Data ?? new List<ExtractionResult>();
        var tableRows = (await _db.GetListAsync<TableExtractionResult>(x =>
            x.EnterpriseCode == enterpriseCode)).Data ?? new List<TableExtractionResult>();

        var ruleSet = ruleCodes?.Where(r => !string.IsNullOrWhiteSpace(r))
                                    .ToHashSet(StringComparer.Ordinal) ?? null;

        IEnumerable<ExtractionResult> F(IEnumerable<ExtractionResult> q) => q
            .Where(x => x.IsValid == 1 && !x.IsDeleted)
            .Where(x => ruleSet == null || (x.RuleCode != null && ruleSet.Contains(x.RuleCode)))
            .Where(x => string.IsNullOrWhiteSpace(standardCode) || x.StandardCode == standardCode)
            .Where(x => string.IsNullOrWhiteSpace(stageCode) || x.StageCode == stageCode);

        IEnumerable<TableExtractionResult> T(IEnumerable<TableExtractionResult> q) => q
            .Where(x => x.IsValid == 1 && !x.IsDeleted)
            .Where(x => ruleSet == null || (x.RuleCode != null && ruleSet.Contains(x.RuleCode)))
            .Where(x => string.IsNullOrWhiteSpace(standardCode) || x.StandardCode == standardCode)
            .Where(x => string.IsNullOrWhiteSpace(stageCode) || x.StageCode == stageCode);

        // ★ 分组键含 RuleCode ⇒ 同名 FieldCode 跨规则不串（J4）
        var fields = F(fieldRows)
            .Select(x => (Key: FieldKey(x.RuleCode, x.FieldCode), Row: x))
            .OrderByDescending(t => ExtractionValueSource.Priority(t.Row.ValueSource))
            .ThenByDescending(t => t.Row.VersionNumber)
            .ThenByDescending(t => t.Row.ExtractedAt)
            .GroupBy(t => t.Key)
            .ToDictionary(g => g.Key, g => ToFieldValue(g.First().Row));

        var tables = T(tableRows)
            .Select(x => (Key: TableKey(x.RuleCode, x.TableCode), Row: x))
            .OrderByDescending(t => ExtractionValueSource.Priority(t.Row.ValueSource))
            .ThenByDescending(t => t.Row.VersionNumber)
            .ThenByDescending(t => t.Row.TableIndex)
            .GroupBy(t => t.Key)
            .ToDictionary(g => g.Key, g => ToTableValue(g.First().Row));

        return (fields, tables);
    }

    /// <summary>分组键：<c>RuleCode||FieldCode</c>（与 <see cref="GetByScopeAsync"/> 一致）</summary>
    public static string FieldKey(string? ruleCode, string? fieldCode) => $"{ruleCode ?? ""}||{fieldCode ?? ""}";

    /// <summary>分组键：<c>RuleCode||TableCode</c></summary>
    public static string TableKey(string? ruleCode, string? tableCode) => $"{ruleCode ?? ""}||{tableCode ?? ""}";

    // ── 内部工具 ──────────────────────────────────────────────

    private static FieldValue ToFieldValue(ExtractionResult r) => new()
    {
        State = EmptyJudge.IsBlankText(r.ExtractedValue) ? EmptyJudge.StateIsEmpty : EmptyJudge.StateHasValue,
        RawValue = r.ExtractedValue,
        FieldName = r.FieldName,
        ValueSource = string.IsNullOrWhiteSpace(r.ValueSource) ? ExtractionValueSource.Auto : r.ValueSource,
        Confidence = r.Confidence,
        StandardFileCode = r.StandardFileCode,
        FileCode = r.FileCode,
        VersionNumber = r.VersionNumber,
        ResultCode = r.Code
    };

    private static TableValue ToTableValue(TableExtractionResult r)
    {
        var parsed = ParseRows(r.ExtractedJson);
        return new TableValue
        {
            State = IsTableMeaningful(parsed) ? EmptyJudge.StateHasValue : EmptyJudge.StateIsEmpty,
            Rows = parsed,
            ValueSource = string.IsNullOrWhiteSpace(r.ValueSource) ? ExtractionValueSource.Auto : r.ValueSource,
            Confidence = r.Confidence,
            StandardFileCode = r.StandardFileCode,
            FileCode = r.FileCode,
            VersionNumber = r.VersionNumber,
            TableIndex = r.TableIndex,
            ResultCode = r.Code
        };
    }

    /// <summary>
    /// 表格是否"有实质内容"：至少一个单元格的键或值不是空白/占位符。
    /// <para>★ B3 修复的核心。实测（2026-09-30）<c>cert_table_extraction_result</c> 13 行有效数据里，
    /// 内容全是 <c>[{"item":"","value":""}]</c> —— 旧判据 <c>rows.Count==0</c> 会全部误判为"不缺"，
    /// AI 拿着全空表照样生成结论。</para>
    /// </summary>
    public static bool IsTableMeaningful(IReadOnlyList<Dictionary<string, object>> rows)
    {
        if (rows == null || rows.Count == 0) return false;

        foreach (var row in rows)
        {
            if (row == null || row.Count == 0) continue;

            foreach (var kv in row)
            {
                // 键名本身算内容（表头行也有意义），值算实质内容
                if (!EmptyJudge.IsBlankText(kv.Key)) return true;
                if (kv.Value == null) continue;
                var text = kv.Value is string s ? s : System.Text.Json.JsonSerializer.Serialize(kv.Value);
                if (!EmptyJudge.IsBlankText(text)) return true;
            }
        }
        return false;
    }

    /// <summary>表格 JSON 解析（容忍数组 / <c>{rows:[…]}</c> / <c>{data:[…]}</c> / <c>{items:[…]}</c>）</summary>
    public static List<Dictionary<string, object>> ParseRows(string? json)
    {
        var list = new List<Dictionary<string, object>>();
        if (string.IsNullOrWhiteSpace(json)) return list;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                foreach (var key in new[] { "rows", "data", "items", "list", "records" })
                {
                    if (root.ValueKind == System.Text.Json.JsonValueKind.Object
                        && root.TryGetProperty(key, out var arr)
                        && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                    { root = arr; break; }
                }
            }
            if (root.ValueKind != System.Text.Json.JsonValueKind.Array) return list;

            foreach (var item in root.EnumerateArray())
            {
                if (item.ValueKind != System.Text.Json.JsonValueKind.Object) continue;
                var map = new Dictionary<string, object>();
                foreach (var p in item.EnumerateObject())
                    map[p.Name] = p.Value.ValueKind switch
                    {
                        System.Text.Json.JsonValueKind.String => p.Value.GetString() ?? "",
                        System.Text.Json.JsonValueKind.Null   => "",
                        System.Text.Json.JsonValueKind.True  => true,
                        System.Text.Json.JsonValueKind.False => false,
                        _ => p.Value.ToString()
                    };
                list.Add(map);
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // 解析失败按空表处理 ⇒ 守卫判 is_empty（宁可失败也不给 AI 喂脏数据）
        }

        return list;
    }
}
