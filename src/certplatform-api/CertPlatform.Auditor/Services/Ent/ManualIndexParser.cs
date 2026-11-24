using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CertPlatform.Auditor.Services.Ent
{
    /// <summary>
    /// 质量手册「纲领层」的<b>确定性解析器</b>（零 LLM、零幻觉）。
    ///
    /// <para>手册里有三张「总指导」表，是审核员定位「每个检查项该读什么文档、哪个部门负责、
    /// 什么时候发布」的数据源。这里只解析其中两张结构化表：</para>
    /// <list type="bullet">
    /// <item><b>0.0 目录与标准条款对应表</b>：手册章节号 → ISO 条款号（含手册特有映射，
    /// 如 <c>4.5 → 7.5.1</c>、<c>9.1.4 → （空）</c>）—— 只能从手册读，不能写死。</item>
    /// <item><b>0.6 职能分配表</b>：ISO 条款 → 主要部门 ▲ / 相关部门 △（责任矩阵）。</item>
    /// </list>
    ///
    /// <para>设计原则：<b>内容驱动</b> —— 按表头/表格标记识别，文档里没有这两张表就返回 null，
    /// 不依赖「是否叫质量手册」的分类，避免把规则写死在文档类型判断上。</para>
    /// </summary>
    public static class ManualIndexParser
    {
        /// <summary>条款号头部：<c>4.1</c> / <c>7.1.1</c> / <c>9.2</c>（不带后续中文）</summary>
        private static readonly Regex ClauseHead = new Regex(@"^(\d+(?:\.\d+){0,2})", RegexOptions.Compiled);

        public sealed class TocRow
        {
            public string Section { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public string IsoClause { get; set; } = string.Empty;
        }

        public sealed class ResponsibilityRow
        {
            public string Clause { get; set; } = string.Empty;
            public List<string> Primary { get; set; } = new();
            public List<string> Related { get; set; } = new();
            public string Note { get; set; } = string.Empty;
        }

        public sealed class CompanyProfile
        {
            public string Name { get; set; } = string.Empty;
            public string Established { get; set; } = string.Empty;
            public string LegalRepresentative { get; set; } = string.Empty;
            public string RegisteredAddress { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public string BusinessScope { get; set; } = string.Empty;
        }

        public sealed class PolicyObjective
        {
            public string Policy { get; set; } = string.Empty;
            public List<string> Objectives { get; set; } = new();
        }

        public sealed class DeptDuty
        {
            public string Dept { get; set; } = string.Empty;
            public List<string> Duties { get; set; } = new();
        }

        /// <summary>0.3 管理者代表任命书：把「总经理=潘明海」「管理者代表=胥加娟」这层职务关系明示出来。</summary>
        public sealed class Appointment
        {
            public string GeneralManager { get; set; } = string.Empty;
            public string ManagerRep { get; set; } = string.Empty;
            public string Date { get; set; } = string.Empty;
        }

        /// <summary>手册封面受控元数据（文件编号/版本/编制/审核/批准/发布日期）——证据引用必须带全这些。</summary>
        public sealed class ManualMeta
        {
            public string DocCode { get; set; } = string.Empty;
            public string Version { get; set; } = string.Empty;
            public string ControlledState { get; set; } = string.Empty;
            public string PreparedBy { get; set; } = string.Empty;
            public string ReviewedBy { get; set; } = string.Empty;
            public string ApprovedBy { get; set; } = string.Empty;
            public string IssuedDate { get; set; } = string.Empty;
            public string EffectiveDate { get; set; } = string.Empty;
        }

        /// <summary>0.1 公司简介 / 0.3 任命书 / 0.4 方针目标 / 0.7 职责权限 四个前置事实段落。</summary>
        public sealed class FrontMatter
        {
            public ManualMeta? ManualMeta { get; set; }
            public CompanyProfile? Company { get; set; }
            public Appointment? Appointment { get; set; }
            public PolicyObjective? PolicyObjective { get; set; }
            public List<DeptDuty> DeptDuties { get; set; } = new();
        }

        public sealed class ManualIndex
        {
            /// <summary>来源标记：manual（确定性解析），与语义推断的 semantic_inferred 分档</summary>
            public string Source { get; set; } = "manual";
            public List<TocRow> TocMapping { get; set; } = new();
            public List<string> Departments { get; set; } = new();
            public List<ResponsibilityRow> ResponsibilityMatrix { get; set; } = new();
            /// <summary>前置事实：公司简介（4.x 环境）/ 方针目标（5.2）/ 部门职责（5.3）</summary>
            public FrontMatter? FrontMatter { get; set; }
        }

        /// <summary>单条条款的「事实证据提取属性」：去哪找事实 + 谁负责。</summary>
        public sealed class ClauseEvidenceSource
        {
            public string Clause { get; set; } = string.Empty;
            /// <summary>应读的手册章节号（含前置事实章节，如 4.1 → 0.1+4.1）</summary>
            public List<string> Sections { get; set; } = new();
            public List<string> PrimaryDepts { get; set; } = new();
            public List<string> RelatedDepts { get; set; } = new();
            /// <summary>命中的前置事实类型：公司简介 / 方针目标 / 部门职责</summary>
            public List<string> FactSources { get; set; } = new();
            /// <summary>true = 手册无映射，走「条款通用提取属性」（取本条款章节正文）</summary>
            public bool IsFallback { get; set; }
        }

        /// <summary>解析手册 Markdown；两张表都不存在时返回 <c>null</c>。</summary>
        public static ManualIndex? Parse(string? markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown)) return null;

            var lines = markdown!.Replace("\r", "").Split('\n');

            var index = new ManualIndex();
            index.TocMapping = ParseToc(lines);
            var resp = ParseResponsibility(lines);
            index.Departments = resp.Departments;
            index.ResponsibilityMatrix = resp.Rows;
            index.FrontMatter = ParseFrontMatter(lines);
            if (index.FrontMatter is not null)
                index.FrontMatter.ManualMeta = ParseManualMeta(lines);

            if (index.TocMapping.Count == 0 && index.ResponsibilityMatrix.Count == 0 && index.FrontMatter is null)
                return null;
            return index;
        }

        /// <summary>解析「0.0 目录与标准条款对应表」：序号 / 目录 / 条款号 三列。</summary>
        private static List<TocRow> ParseToc(string[] lines)
        {
            var rows = new List<TocRow>();
            var start = -1;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("目录与标准条款对应表")) { start = i; break; }
            }
            if (start < 0) return rows;

            var inTable = false;
            for (var i = start + 1; i < lines.Length && i < start + 150; i++)
            {
                var line = lines[i].Trim();
                if (!line.StartsWith("|"))
                {
                    if (inTable) break;   // 表格真正结束后再退出
                    continue;             // 标题与表格之间的空行/其他行跳过
                }
                inTable = true;
                if (IsSeparator(line)) continue;
                if (line.Contains("序号") || line.Contains("ISO9001") || line.Contains("条款号")) continue;

                var cells = SplitCells(line);
                // 列：序号 / 目录 / 条款号（后两列为空占位）
                if (cells.Count < 3) continue;
                var section = cells[0];
                if (section.Length == 0) continue;
                rows.Add(new TocRow
                {
                    Section = section,
                    Title = cells[1],
                    IsoClause = cells[2],
                });
            }
            return rows;
        }

        /// <summary>解析「0.6 职能分配表」：表头=部门，数据行=条款 + ▲主要/△相关。</summary>
        private static (List<string> Departments, List<ResponsibilityRow> Rows) ParseResponsibility(string[] lines)
        {
            var departments = new List<string>();
            var rows = new List<ResponsibilityRow>();

            // 1) 定位「职能分配表」章节标题：必须是 # 标题（避免命中目录表里的「0.6 职能分配表」条目）
            var sectionIdx = -1;
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].TrimStart();
                if (t.StartsWith("#") && lines[i].Contains("职能分配表")) { sectionIdx = i; break; }
            }
            if (sectionIdx < 0) return (departments, rows);

            // 2) 从标题往下找表头行：含「职能部门」的 | 行（区别于标题的「职能分配表」）
            var headerIdx = -1;
            for (var i = sectionIdx; i < lines.Length && i < sectionIdx + 120; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("|") && line.Contains("职能部门")) { headerIdx = i; break; }
            }
            if (headerIdx < 0) return (departments, rows);

            var header = SplitCells(lines[headerIdx]);
            // header[0] = 「标准要求职能部门」，header[1..] = 部门名
            for (var j = 1; j < header.Count; j++)
            {
                if (header[j].Length > 0) departments.Add(header[j]);
            }
            if (departments.Count == 0) return (departments, rows);

            var inTable = false;
            for (var i = headerIdx + 1; i < lines.Length && i < headerIdx + 120; i++)
            {
                var line = lines[i].Trim();
                if (!line.StartsWith("|"))
                {
                    if (inTable) break;
                    continue;
                }
                inTable = true;
                if (IsSeparator(line)) continue;

                var cells = SplitCells(line);
                if (cells.Count < 2) continue;

                var m = ClauseHead.Match(cells[0]);
                if (!m.Success) continue;

                var row = new ResponsibilityRow { Clause = m.Groups[1].Value };
                for (var j = 1; j < cells.Count && j - 1 < departments.Count; j++)
                {
                    var dept = departments[j - 1];
                    var mark = cells[j];
                    if (mark == "▲") row.Primary.Add(dept);
                    else if (mark == "△") row.Related.Add(dept);
                    else if (mark.Contains("不适用")) row.Note = mark;
                }
                rows.Add(row);
            }
            return (departments, rows);
        }

        /// <summary>解析前置事实层：0.1 公司简介 / 0.4 方针目标 / 0.7 职责权限（均为确定性抽取）。</summary>
        private static FrontMatter? ParseFrontMatter(string[] lines)
        {
            var company = ParseCompanyProfile(lines);
            var appointment = ParseAppointment(lines);
            var policy = ParsePolicyObjective(lines);
            var duties = ParseDeptDuties(lines);
            if (company is null && appointment is null && policy is null && duties.Count == 0) return null;

            var fm = new FrontMatter();
            fm.Company = company;
            fm.Appointment = appointment;
            fm.PolicyObjective = policy;
            fm.DeptDuties = duties;
            return fm;
        }

        /// <summary>解析手册封面受控元数据（前 30 行）：文件编号/版本/编制/审核/批准/发布·实施日期。</summary>
        private static ManualMeta? ParseManualMeta(string[] lines)
        {
            var meta = new ManualMeta();
            foreach (var raw in lines.Take(30))
            {
                var line = Regex.Replace(raw, @"[\s*]+", "");
                meta.DocCode = PickMeta(meta.DocCode, line, "文件编号");
                meta.Version = PickMeta(meta.Version, line, "版本号");
                meta.ControlledState = PickMeta(meta.ControlledState, line, "受控状态");
                meta.PreparedBy = PickMeta(meta.PreparedBy, line, "编制");
                meta.ReviewedBy = PickMeta(meta.ReviewedBy, line, "审核");
                meta.ApprovedBy = PickMeta(meta.ApprovedBy, line, "批准");
                if (meta.IssuedDate.Length == 0)
                    meta.IssuedDate = MatchGroup(line, @"发布日期[:：]?(\d{4}[./-]\d{1,2}[./-]\d{1,2})");
                if (meta.EffectiveDate.Length == 0)
                    meta.EffectiveDate = MatchGroup(line, @"实施日期[:：]?(\d{4}[./-]\d{1,2}[./-]\d{1,2})");
            }
            return meta.DocCode.Length == 0 && meta.ApprovedBy.Length == 0 && meta.IssuedDate.Length == 0
                ? null : meta;
        }

        /// <summary>取「标签：值」里的值；标签命中一次即返回。</summary>
        private static string PickMeta(string current, string line, string label)
        {
            if (current.Length > 0) return current;
            var idx = line.IndexOf(label, StringComparison.Ordinal);
            if (idx < 0) return current;
            return line.Substring(idx + label.Length).TrimStart('：', ':');
        }

        /// <summary>解析 0.1 公司简介：成立日期/法定代表人/注册地址/经营范围/名称/地址。</summary>
        private static CompanyProfile? ParseCompanyProfile(string[] lines)
        {
            var start = FindHeading(lines, "0.1", "公司简介");
            if (start < 0) return null;

            var sb = new StringBuilder();
            for (var i = start + 1; i < lines.Length; i++)
            {
                if (IsTopHeading(lines[i])) break;
                sb.AppendLine(lines[i]);
            }
            var text = sb.ToString();

            var cp = new CompanyProfile
            {
                Name = MatchGroup(text, @"公司名称（中文）[:：]?\s*([^\r\n]+)"),
                Established = MatchGroup(text, @"成立于(\d{4}年\d{1,2}月\d{1,2}日)"),
                LegalRepresentative = MatchGroup(text, @"法定代表人为([^。\s]+)"),
                RegisteredAddress = MatchGroup(text, @"注册地位于(.+?)，法定代表人为"),
                Address = MatchGroup(text, @"公司地址[:：]\s*([^\r\n]+)"),
                BusinessScope = MatchGroup(text, @"经营范围包括(.+?)(?:（除依法须经批准的项目外|$)"),
            };
            if (cp.Name.Length == 0 && cp.Established.Length == 0 && cp.LegalRepresentative.Length == 0
                && cp.RegisteredAddress.Length == 0 && cp.Address.Length == 0 && cp.BusinessScope.Length == 0)
                return null;
            return cp;
        }

        /// <summary>解析 0.4 质量方针、目标：方针（加粗句）+ 目标（含 ≥/% 的行）。</summary>
        private static PolicyObjective? ParsePolicyObjective(string[] lines)
        {
            var start = FindHeading(lines, "0.4", "方针");
            if (start < 0) return null;

            var po = new PolicyObjective();
            for (var i = start + 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (IsTopHeading(lines[i])) break;
                if (line.Length == 0) continue;

                if (po.Policy.Length == 0 && line.StartsWith("**") && line.EndsWith("**"))
                {
                    po.Policy = line.Trim('*').Trim();
                }
                else if (line.Contains('≥') || line.Contains('%'))
                {
                    var obj = CleanDuty(line);
                    if (obj.Length > 0) po.Objectives.Add(obj);
                }
            }
            if (po.Policy.Length == 0 && po.Objectives.Count == 0) return null;
            return po;
        }

        /// <summary>解析 0.7 职责权限：<c>## 部门</c> + 职责条目列表（兼容 - N）/N) 两种编号）。</summary>
        private static List<DeptDuty> ParseDeptDuties(string[] lines)
        {
            var result = new List<DeptDuty>();
            var start = FindHeading(lines, "0.7", "职责权限");
            if (start < 0) return result;

            DeptDuty? current = null;
            for (var i = start + 1; i < lines.Length; i++)
            {
                var raw = lines[i];
                var line = raw.Trim();
                if (IsTopHeading(raw)) break;

                if (line.StartsWith("##"))
                {
                    var dept = line.TrimStart('#').Trim().TrimEnd('：', ':').Trim();
                    if (dept.Length > 0)
                    {
                        current = new DeptDuty { Dept = dept };
                        result.Add(current);
                    }
                    continue;
                }

                if (current is null || line.Length == 0) continue;

                var duty = CleanDuty(line);
                if (duty.Length > 0) current.Duties.Add(duty);
            }
            return result;
        }

        /// <summary>
        /// 解析 0.3 管理者代表任命书：特任命 X 为管理者代表、总经理 Y、日期。
        /// <para>这是把「潘明海=总经理」「胥加娟=管理者代表」职务关系显式写入前置事实的关键，
        /// 否则 5.2/5.3 只能看到「批准人潘明海（无职务）」「总经理审批（无名字）」，从而误报身份未明示。</para>
        /// </summary>
        private static Appointment? ParseAppointment(string[] lines)
        {
            var start = FindHeading(lines, "0.3", "任命");
            if (start < 0) return null;

            var ap = new Appointment();
            for (var i = start + 1; i < lines.Length; i++)
            {
                if (IsTopHeading(lines[i])) break;
                var line = lines[i].Trim();

                if (ap.ManagerRep.Length == 0)
                    ap.ManagerRep = MatchGroup(line, @"特任命\s*([^\s为]+)\s*为管理者代表");
                if (ap.GeneralManager.Length == 0)
                    ap.GeneralManager = MatchGroup(line, @"总经理[:：]\s*([^\s*]+)");
                if (ap.Date.Length == 0)
                    ap.Date = MatchGroup(line, @"日期[:：]?\s*(\d{4}[./-]\d{1,2}[./-]\d{1,2})");
            }

            if (ap.GeneralManager.Length == 0 && ap.ManagerRep.Length == 0 && ap.Date.Length == 0) return null;
            return ap;
        }

        /// <summary>定位「# 编号 关键词」顶级标题行号；找不到返回 -1。</summary>
        private static int FindHeading(string[] lines, string num, string keyword)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].TrimStart();
                if (t.StartsWith("#") && lines[i].Contains(num) && lines[i].Contains(keyword)) return i;
            }
            return -1;
        }

        /// <summary>顶级标题（单个 #，非 ##/###）判断 —— 用作段落边界。</summary>
        private static bool IsTopHeading(string line)
        {
            var t = line.TrimStart();
            return t.StartsWith("#") && !t.StartsWith("##");
        }

        /// <summary>职责条目清洗：去 <c>- </c> 列表符与 <c>1）</c>/<c>1)</c>/<c>14 )</c> 编号。</summary>
        private static string CleanDuty(string line)
        {
            var s = line.Trim();
            if (s.StartsWith("- ") || s.StartsWith("-\t")) s = s[2..].Trim();
            // 兼容 1）/ 1) / 14 ) / 1\)（转义右括号）等编号形式
            var m = Regex.Match(s, @"^(\d+\s*\\?[)）]\s*)");
            if (m.Success) s = s[m.Length..].Trim();
            return s;
        }

        /// <summary>正则取第 1 个捕获组；未命中返回空串。</summary>
        private static string MatchGroup(string text, string pattern)
        {
            var m = Regex.Match(text, pattern, RegexOptions.Singleline);
            return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
        }

        /// <summary>条款 → 前置事实类型（默认话术：哪些章需要环境/方针/职责/任命事实）。</summary>
        private static readonly Dictionary<string, List<string>> ClauseFactMap = new(StringComparer.Ordinal)
        {
            ["4.1"] = new() { "公司简介" },
            ["4.2"] = new() { "公司简介" },
            ["5.1"] = new() { "任命书" },
            ["5.2"] = new() { "方针目标", "任命书" },
            ["5.3"] = new() { "部门职责", "任命书" },
        };

        /// <summary>事实类型 → 手册目录标题关键词（从 TocMapping 反查章节号，不写死章节号）。</summary>
        private static readonly Dictionary<string, string> FactKeywordMap = new(StringComparer.Ordinal)
        {
            ["公司简介"] = "公司简介",
            ["任命书"] = "任命书",
            ["方针目标"] = "方针",
            ["部门职责"] = "职责权限",
        };

        /// <summary>
        /// 由已解析的「手册纲领」为每条条款派生<b>事实证据提取属性</b>（确定性）：
        /// 应读章节（TocMapping 反查 + 前置事实章节）、负责部门（职能分配表）、
        /// 命中的前置事实；无任何映射时标记 <see cref="ClauseEvidenceSource.IsFallback"/>，
        /// 下游据此走「条款通用提取属性」。
        /// <para>条款与手册章节是<b>父子粒度</b>：叶子条款 4.4.1 / 5.2.1 要回退到 4.4 / 5.2，
        /// 故按「最长前缀」匹配，而不是精确相等。</para>
        /// </summary>
        public static List<ClauseEvidenceSource> BuildEvidenceSources(IEnumerable<string> clauseNumbers, ManualIndex index)
        {
            var result = new List<ClauseEvidenceSource>();

            var sectionByClause = index.TocMapping
                .Where(t => t.IsoClause.Length > 0)
                .GroupBy(t => t.IsoClause, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(t => t.Section).Distinct(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);

            var respByClause = index.ResponsibilityMatrix.ToDictionary(r => r.Clause, StringComparer.Ordinal);

            foreach (var clause in clauseNumbers.Distinct(StringComparer.Ordinal))
            {
                var src = new ClauseEvidenceSource { Clause = clause };

                // ① 本条款章节：叶子→父级回退（4.4.1 → 4.4）
                var secKey = BestPrefixKey(clause, sectionByClause.Keys);
                if (secKey is not null)
                    src.Sections.AddRange(sectionByClause[secKey]);

                // ② 前置事实：5.2.1 → 5.2 → 方针目标 + 任命书
                var factKey = BestPrefixKey(clause, ClauseFactMap.Keys);
                if (factKey is not null && ClauseFactMap.TryGetValue(factKey, out var facts))
                {
                    foreach (var fact in facts)
                    {
                        src.FactSources.Add(fact);
                        if (FactKeywordMap.TryGetValue(fact, out var keyword))
                        {
                            var factRow = index.TocMapping.FirstOrDefault(t => t.Title.Contains(keyword, StringComparison.Ordinal));
                            if (factRow is not null && !src.Sections.Contains(factRow.Section, StringComparer.Ordinal))
                                src.Sections.Add(factRow.Section);
                        }
                    }
                }

                // ③ 负责部门：8.5.1 → 8.5.1，7.1.5.1 → 7.1.5
                var respKey = BestPrefixKey(clause, respByClause.Keys);
                if (respKey is not null && respByClause.TryGetValue(respKey, out var resp))
                {
                    src.PrimaryDepts.AddRange(resp.Primary);
                    src.RelatedDepts.AddRange(resp.Related);
                }

                src.IsFallback = src.Sections.Count == 0;
                if (src.IsFallback) src.Sections.Add(clause);

                result.Add(src);
            }
            return result;
        }

        /// <summary>最长前缀匹配：<c>4.4.1</c> 命中 <c>4.4</c>（而非 <c>4</c>）；不跨编号段。</summary>
        private static string? BestPrefixKey(string clause, IEnumerable<string> keys)
        {
            string? best = null;
            foreach (var k in keys)
            {
                if (clause == k || clause.StartsWith(k + ".", StringComparison.Ordinal))
                {
                    if (best is null || k.Length > best.Length) best = k;
                }
            }
            return best;
        }

        /// <summary>按 <c>|</c> 拆单元格并去掉首尾空单元格（来自行首/行尾的竖线）。</summary>
        private static List<string> SplitCells(string line)
        {
            var cells = new List<string>();
            foreach (var part in line.Split('|'))
            {
                cells.Add(CleanCell(part));
            }
            if (cells.Count > 0 && cells[0].Length == 0) cells.RemoveAt(0);
            if (cells.Count > 0 && cells[^1].Length == 0) cells.RemoveAt(cells.Count - 1);
            return cells;
        }

        /// <summary>单元格清洗：去 Markdown 强调、反引号、HTML 换行，再 trim。</summary>
        private static string CleanCell(string cell)
        {
            if (string.IsNullOrEmpty(cell)) return string.Empty;
            return cell.Trim()
                .Replace("**", string.Empty)
                .Replace("`", string.Empty)
                .Replace("<br>", string.Empty)
                .Replace("<br/>", string.Empty)
                .Replace("<br />", string.Empty)
                .Trim();
        }

        /// <summary>表格分隔行（<c>|---|---|</c>）判断。</summary>
        private static bool IsSeparator(string line)
        {
            var s = line.Replace("|", string.Empty).Replace(" ", string.Empty)
                        .Replace("-", string.Empty).Replace(":", string.Empty).Trim();
            return s.Length == 0;
        }
    }
}
