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

        /// <summary>
        /// 文档侧 <c>ProcessArea</c> 自由文本 → <see cref="process"/> 码 的**归一表**（纯配置，跨标准不变）。
        ///
        /// <para>用途：过程轴 RM2 由"关键词包含"升级为「码 ∩ 码」，消除"采购过程 / 供应商管理 / 供方评审"
        /// 等同义异写法靠字符串碰运气的问题。见 <c>docs/80-v4/19-召回路径实施方案-V1.md</c> §三 RM2。</para>
        /// <para>⛔ 未知文本返回空集（由 <see cref="MatchesProcessAxis"/> 退回关键词），⛔ 不猜。</para>
        /// </summary>
        private static readonly (string Marker, string[] Keys)[] AreaToKeys =
        {
            ("gm", new[] { "management" }), ("管代", new[] { "management" }), ("管理层", new[] { "management" }),
            ("行政", new[] { "hr" }), ("人事", new[] { "hr" }), ("人力资源", new[] { "hr" }), ("培训", new[] { "hr" }),
            ("销售", new[] { "sales" }), ("顾客", new[] { "sales" }), ("客户", new[] { "sales" }), ("市场", new[] { "sales" }),
            ("采购", new[] { "purchase" }), ("供方", new[] { "purchase" }), ("供应商", new[] { "purchase" }), ("外包", new[] { "purchase" }),
            ("生产", new[] { "production" }), ("制造", new[] { "production" }), ("工序", new[] { "production" }), ("装配", new[] { "production" }),
            ("检验", new[] { "inspection" }), ("检测", new[] { "inspection" }), ("试验", new[] { "inspection" }), ("化验", new[] { "inspection" }),
            ("仓储", new[] { "warehouse" }), ("仓库", new[] { "warehouse" }), ("库存", new[] { "warehouse" }), ("物流", new[] { "warehouse" }),
            ("设备", new[] { "metrology" }), ("计量", new[] { "metrology" }), ("校准", new[] { "metrology" }), ("仪器", new[] { "metrology" }),
            ("设计", new[] { "design" }), ("研发", new[] { "design" }),
            ("售后", new[] { "after_sales" }), ("投诉", new[] { "after_sales" }), ("顾客满意", new[] { "after_sales" })
        };

        /// <summary>文档过程域文本归一为 Process 码集合（未知文本 ⇒ 空集，⛔ 不猜）。</summary>
        public static HashSet<string> NormalizeAreaKeys(string? processArea)
        {
            var area = (processArea ?? string.Empty).Trim();
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (area.Length == 0) return set;
            var lower = area.ToLowerInvariant();
            foreach (var (marker, keys) in AreaToKeys)
            {
                if (!area.Contains(marker, StringComparison.Ordinal) && !lower.Contains(marker, StringComparison.Ordinal))
                    continue;
                foreach (var k in keys) set.Add(k);
            }
            return set;
        }

        /// <summary>
        /// 过程轴 RM2 匹配：优先「码 ∩ 码」（归一表），归一不出码时退回「关键词包含」。
        /// <para>⛔ 两条路都为否 ⇒ false（宁可不召回，也不靠字符串碰运气）。</para>
        /// </summary>
        public static bool MatchesProcessAxis(string? processArea, IReadOnlyCollection<string> clauseKeys)
        {
            if (clauseKeys.Count == 0) return false;
            var areaKeys = NormalizeAreaKeys(processArea);
            if (areaKeys.Count > 0) return areaKeys.Overlaps(clauseKeys);
            return Matches(processArea, KeywordsFor(clauseKeys));
        }

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
