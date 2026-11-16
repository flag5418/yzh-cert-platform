using System.Text.Json;
using System.Text.RegularExpressions;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 过程域召回：把「检查项条款的 <c>cert_clause_attr.Process</c> 属性」翻译成中文关键词，
    /// 再去匹配文档产物里的 <c>ProcessArea</c>（自由文本，如「采购过程」「供应商准入评审」）。<para>
    /// 用途：记录类/程序类文档拿不到条款号时，靠「过程」召回，而不是靠代码猜条款号。<br/>
    /// ⛔ 硬约束：本模块只产出「要不要把这份文档找出来」，<b>永不产出条款号</b>——条款号只能来自文档自身，否则就是串条款。
    /// </para>
    /// </summary>
    public static class ProcessAreaRecall
    {
        /// <summary>
        /// 证据行锚点文案：⛔ 不写"过程召回/记录召回"这类内部术语 —— 审核员看不懂，
        /// 报告的"证据来源"栏也解释不清。这里直接写"这份文件是什么、凭什么被找来"。
        /// </summary>
        public const string ProgramAnchor = "同过程程序/指导书";

        /// <summary>记录轴锚点：同一过程下的执行记录。</summary>
        public const string RecordAnchor = "同过程记录";

        /// <summary>Process 属性键 → 中文过程域关键词。</summary>
        private static readonly Dictionary<string, string[]> Map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["purchase"] = new[] { "采购", "供方", "供应商", "外购", "外包", "进料", "来料" },
            ["inspection"] = new[] { "检验", "检测", "检查", "试验", "化验", "抽检", "首件", "巡检" },
            ["production"] = new[] { "生产", "制造", "工序", "装配", "加工", "过程运行", "产线" },
            ["sales"] = new[] { "销售", "顾客", "客户", "交付", "发货", "订单", "合同", "订货" },
            ["warehouse"] = new[] { "仓储", "库存", "入库", "出库", "领料", "物料", "物流" },
            ["metrology"] = new[] { "计量", "校准", "检定", "测量设备", "监测设备", "仪器" },
            ["hr"] = new[] { "人力", "员工", "培训", "招聘", "岗位", "绩效", "能力" },
            ["management"] = new[] { "方针", "目标", "管理评审", "内审", "内部审核", "风险", "合规", "变更" },
            ["design"] = new[] { "设计", "开发", "图纸", "样件" },
            ["after_sales"] = new[] { "售后", "投诉", "退货", "顾客满意", "维修", "服务" }
        };

        /// <summary>从条款属性值（形如 <c>["purchase","inspection"]</c> 或 <c>purchase</c>）抽取 Process 键。</summary>
        public static List<string> ParseKeys(string? attrValue)
        {
            var text = (attrValue ?? string.Empty).Trim();
            if (text.Length == 0) return new List<string>();

            var tokens = new List<string>();
            if (text.StartsWith("[", StringComparison.Ordinal))
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            var s = el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
                            if (!string.IsNullOrWhiteSpace(s)) tokens.Add(s!.Trim());
                        }
                }
                catch (JsonException)
                {
                    tokens.AddRange(Regex.Matches(text, "\"([^\"]+)\"").Select(m => m.Groups[1].Value));
                }
            }
            else
            {
                tokens.AddRange(text.Split(new[] { ',', '，', ';', '；', '|' },
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }

            return tokens.Select(t => t.Trim('"', ' ', '[', ']'))
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Process 键 → 中文关键词集合；未知键直接忽略（不猜）。</summary>
        public static HashSet<string> KeywordsFor(IEnumerable<string> processKeys)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in processKeys)
                if (Map.TryGetValue(key, out var words))
                    foreach (var w in words) set.Add(w);
            return set;
        }

        /// <summary>文档过程域文本是否落在这些关键词上。空关键词/空文本一律 false（宁可不召回）。</summary>
        public static bool Matches(string? processArea, IReadOnlySet<string> keywords)
        {
            var area = (processArea ?? string.Empty).Trim();
            if (area.Length == 0 || keywords.Count == 0) return false;
            foreach (var kw in keywords)
                if (area.Contains(kw, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
