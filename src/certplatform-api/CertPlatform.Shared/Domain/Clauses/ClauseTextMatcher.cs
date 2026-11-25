using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CertPlatform.Shared.Domain.Clauses
{
    /// <summary>
    /// 条款释义语义匹配器 —— <b>RM7 唯一入口</b>：自实现 BM25，无外部依赖。
    ///
    /// <para>见 <c>docs/80-v4/16-条款释义匹配方案-V1.md</c> §四：
    /// 语料 = 条款 <c>ClauseText</c>（Title+Body+Items+Notes）；中文用字符 2-gram、英数按
    /// <c>[A-Za-z0-9]+</c> 切词；BM25 参数 k1=1.2、b=0.75；每标准 ≤ 80 条款，规模极小，可解释、可单测。</para>
    ///
    /// <para><b>纯内存、构建后只读</b>：由调用方（<c>CertTaskService</c>）按检查项条款构建一次；
    /// 端内⛔不另写 <c>Queryable</c>（守 A-R6）。</para>
    /// </summary>
    public sealed class ClauseTextMatcher
    {
        private const double K1 = 1.2;
        private const double B = 0.75;
        private const int DefaultTopK = 8;

        private readonly List<string> _numbers;
        private readonly Dictionary<string, Dictionary<string, int>> _tf;
        private readonly Dictionary<string, int> _len;
        private readonly Dictionary<string, int> _df;
        private readonly double _avgLen;
        private readonly int _n;

        private ClauseTextMatcher(
            List<string> numbers,
            Dictionary<string, Dictionary<string, int>> tf,
            Dictionary<string, int> len,
            Dictionary<string, int> df)
        {
            _numbers = numbers;
            _tf = tf;
            _len = len;
            _df = df;
            _n = numbers.Count;
            _avgLen = _n == 0 ? 0 : len.Values.Average();
        }

        /// <summary>语料条款数。</summary>
        public int Count => _n;

        /// <summary>无语料（调用方据此跳过 RM7）。</summary>
        public bool IsEmpty => _n == 0;

        /// <summary>建索引。条款号重复时取首条；空文本条款不进语料。</summary>
        public static ClauseTextMatcher Build(IEnumerable<(string ClauseNumber, string Text)> clauses)
        {
            var numbers = new List<string>();
            var tf = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            var len = new Dictionary<string, int>(StringComparer.Ordinal);
            var df = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var (rawNo, text) in clauses ?? Enumerable.Empty<(string, string)>())
            {
                var no = (rawNo ?? string.Empty).Trim();
                if (no.Length == 0 || tf.ContainsKey(no)) continue;

                var tokens = Tokenize(text);
                if (tokens.Count == 0) continue;

                var bag = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var t in tokens)
                    bag[t] = bag.TryGetValue(t, out var c) ? c + 1 : 1;
                foreach (var t in bag.Keys)
                    df[t] = df.TryGetValue(t, out var c) ? c + 1 : 1;

                numbers.Add(no);
                tf[no] = bag;
                len[no] = tokens.Count;
            }

            return new ClauseTextMatcher(numbers, tf, len, df);
        }

        /// <summary>按 BM25 打分，返回 <c>clauseNumber + score</c>（降序，取 topK）。</summary>
        public List<ClauseMatch> Rank(string? query, int topK = 0)
        {
            var result = new List<ClauseMatch>();
            if (_n == 0) return result;

            var qTokens = Tokenize(query);
            if (qTokens.Count == 0) return result;
            var qUnique = qTokens.Distinct(StringComparer.Ordinal).ToList();
            var norm = _avgLen <= 0 ? 1 : _avgLen;

            foreach (var no in _numbers)
            {
                var bag = _tf[no];
                var dl = _len[no];
                double score = 0;
                foreach (var q in qUnique)
                {
                    if (!bag.TryGetValue(q, out var f) || f <= 0) continue;
                    var n = _df.TryGetValue(q, out var dfq) ? dfq : 0;
                    if (n == 0) continue;

                    var idf = Math.Log(1 + (_n - n + 0.5) / (n + 0.5));
                    var denom = f + K1 * (1 - B + B * dl / norm);
                    if (denom <= 0) continue;
                    score += idf * (f * (K1 + 1)) / denom;
                }
                if (score > 0)
                    result.Add(new ClauseMatch { ClauseNumber = no, Score = score });
            }

            return result
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.ClauseNumber, StringComparer.Ordinal)
                .Take(topK <= 0 ? DefaultTopK : topK)
                .ToList();
        }

        /// <summary>
        /// 把 <c>ClauseText</c> 的 JSON（<c>{Title,Body,Items[],Notes[]}</c>）拍平为匹配文本。
        /// <para>非 JSON（旧数据直接存正文）时原样返回；解析失败也退回原串，⛔ 不抛异常拖垮召回。</para>
        /// </summary>
        public static string FlattenClauseText(string? attrValue)
        {
            var s = (attrValue ?? string.Empty).Trim();
            if (s.Length == 0) return string.Empty;
            if (s[0] != '{' && s[0] != '[') return s;

            try
            {
                using var doc = JsonDocument.Parse(s);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.String) return root.GetString() ?? string.Empty;
                if (root.ValueKind != JsonValueKind.Object) return s;

                var sb = new StringBuilder();
                foreach (var name in new[] { "Title", "Body" })
                    if (root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String)
                        sb.Append(v.GetString()).Append(' ');
                foreach (var name in new[] { "Items", "Notes" })
                    if (root.TryGetProperty(name, out var arr) && arr.ValueKind == JsonValueKind.Array)
                        foreach (var it in arr.EnumerateArray())
                            if (it.ValueKind == JsonValueKind.String) sb.Append(it.GetString()).Append(' ');
                return sb.ToString().Trim();
            }
            catch (JsonException)
            {
                return s;
            }
        }

        /// <summary>
        /// 切词：CJK 字符 → 字符 2-gram（不定长尾字单独成词）；英数 → 按 <c>[A-Za-z0-9]+</c> 切词并小写；
        /// 其余（标点/空白）为分隔符。免词典、对中文稳（16 号 §四）。
        /// </summary>
        public static List<string> Tokenize(string? text)
        {
            var tokens = new List<string>();
            var s = text ?? string.Empty;
            var ascii = new StringBuilder();
            var cjk = new List<char>();

            void FlushAscii()
            {
                if (ascii.Length > 0)
                {
                    tokens.Add(ascii.ToString().ToLowerInvariant());
                    ascii.Clear();
                }
            }

            void FlushCjk()
            {
                if (cjk.Count >= 2)
                    for (var i = 0; i + 1 < cjk.Count; i++)
                        tokens.Add(new string(new[] { cjk[i], cjk[i + 1] }));
                else if (cjk.Count == 1)
                    tokens.Add(cjk[0].ToString());
                cjk.Clear();
            }

            foreach (var ch in s)
            {
                if (IsAsciiWord(ch)) { FlushCjk(); ascii.Append(ch); }
                else if (IsCjk(ch)) { FlushAscii(); cjk.Add(ch); }
                else { FlushAscii(); FlushCjk(); }
            }
            FlushAscii();
            FlushCjk();
            return tokens;
        }

        private static bool IsAsciiWord(char c)
            => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        private static bool IsCjk(char c)
            => (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF);
    }

    /// <summary>RM7 命中项：条款号 + BM25 分数（降序输出）。</summary>
    public sealed class ClauseMatch
    {
        public string ClauseNumber { get; set; } = string.Empty;
        public double Score { get; set; }
    }
}
