using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CertPlatform.Shared.DocExtraction
{
    /// <summary>
    /// 文档结构指纹（确定性、可核验、零模型成本）。
    /// <para>一条 = 一个「原文客观证据」：<see cref="Key"/> 唯一标识、<see cref="Weight"/> 高/中/低、<see cref="Evidence"/> 供人工复核。</para>
    /// </summary>
    public sealed record DocFingerprint(string Key, string Weight, string Evidence);

    /// <summary>指纹输入的章节头（由画像器 / Admin 试跑从 Markdown 解析后传入）。</summary>
    public sealed record DocHeading(string Title, int Level, bool HasContent);

    /// <summary>
    /// ★ V4「结构指纹 + 角色定位」唯一提取器（Admin 试跑与 Auditor 线上共用；放共享层以避开守卫 A-R1）。
    ///
    /// <para><b>为什么不用文件名 / 标准条款号</b>：企业资料不全、命名各行其是，靠文件名子串匹配 = 猜。
    /// 本文只采信「原文里客观存在的结构证据」（颁布令 / 六段式骨架 / 表格密度 / 实例数据 …），
    /// 判不出就显式给 <c>unclassified</c>（待人工确认），⛔ 不兜底、不猜。</para>
    ///
    /// <para>角色定位的 <c>Mode</c> 取值与 <see cref="DocExtractionPromptCatalog"/> 的 <c>Mode</c> 逐字一致。</para>
    /// </summary>
    public static class DocFingerprintExtractor
    {
        public const string WHigh = "high";
        public const string WMid = "mid";
        public const string WLow = "low";

        /// <summary>角色定位结果（DocType 英文枚举 / DocTypeName 中文 / Mode / 置信度 / 命中指纹串）。</summary>
        public sealed record Classification(
            string DocType, string DocTypeName, string Mode, double Confidence, string Reason);

        private static readonly Regex ClauseNoRx =
            new(@"(?<![\d.])(?:10|[4-9])(?:\.\d{1,3}){1,3}(?![\d.])", RegexOptions.Compiled);

        private static readonly Regex ArticleRx =
            new(@"第[〇零一二三四五六七八九十百0-9]+条", RegexOptions.Compiled);

        private static readonly Regex ChapterRx =
            new(@"第[〇零一二三四五六七八九十百0-9]+章", RegexOptions.Compiled);

        private static readonly Regex TopChapterRx = new(@"^(\d{1,2})(?:\.\d+)*", RegexOptions.Compiled);

        private static readonly Regex DeptDutyRx =
            new(@"([\u4e00-\u9fa5]{2,8}(?:部|科|室|中心|处|组))\s*(?:负责|主管|归口|执行|管理)", RegexOptions.Compiled);

        private static readonly Regex DateRx = new(@"\d{4}\s*[-/年.]\s*\d{1,2}", RegexOptions.Compiled);

        /// <summary>从 Markdown 提结构指纹（确定性）。</summary>
        public static List<DocFingerprint> Extract(string markdown, IReadOnlyList<DocHeading>? headings)
        {
            var text = markdown ?? string.Empty;
            var fps = new List<DocFingerprint>();
            if (text.Length == 0) return fps;

            // ★ 2026-11-25：docx/pdf 转换常把「内 审 检 查 表」拆成汉字间空格 ⇒ 关键词匹配前先去字形间空白。
            //   否则标题带空格的检查表会漏命中 record.audit_or_review，被 context.* 抢判成组织环境清单。
            var norm = NormalizeCjk(text);

            var headingList = headings ?? Array.Empty<DocHeading>();
            var lines = text.Replace("\r", string.Empty).Split('\n');
            var tableLines = lines.Where(l => l.TrimStart().StartsWith("|", StringComparison.Ordinal)).ToList();
            var tableDataRows = tableLines.Count(l => !IsTableSeparator(l));
            var nonEmpty = lines.Count(l => l.Trim().Length > 0);
            var clauseCount = ClauseNoRx.Matches(text).Count;
            var articleCount = ArticleRx.Matches(text).Count;
            var baseCount = ArticleRx.Matches(text).Select(m => m.Value).Count();
            var hasChapter = ChapterRx.IsMatch(text);
            var dateCount = DateRx.Matches(text).Count;
            var deptDuties = DeptDutyRx.Matches(text)
                .Select(m => m.Groups[1].Value.Trim())
                .Where(s => s.Length > 0)
                .Distinct()
                .Count();
            var head = text.Substring(0, Math.Min(text.Length, text.Length / 3 + 1));
            var span = headingList
                .Select(h => TopChapterRx.Match(h.Title ?? string.Empty))
                .Where(m => m.Success)
                .Select(m => int.TryParse(m.Groups[1].Value, out var n) ? n : 0)
                .Where(n => n >= 4 && n <= 10)
                .Distinct()
                .Count();

            // ── 质量手册（特殊：颁布令 / 方针目标 / 条款对照表 / 组织职责 / 程序索引 / 章序覆盖）──
            if (ContainsAny(head, "颁布令", "发布令", "批准页", "批准令", "签署页", "批准发布"))
                fps.Add(new DocFingerprint("manual.issue_order", WHigh, "开头出现颁布/批准/签署页"));
            if (norm.Contains("质量方针", StringComparison.Ordinal)
                && (norm.Contains("质量目标", StringComparison.Ordinal) || norm.Contains("目标", StringComparison.Ordinal)))
                fps.Add(new DocFingerprint("manual.policy_objective", WHigh, "含质量方针与质量目标"));
            if ((norm.Contains("条款", StringComparison.Ordinal)
                 || norm.Contains("对应表", StringComparison.Ordinal)
                 || norm.Contains("对照", StringComparison.Ordinal))
                && clauseCount >= 10 && tableDataRows >= 4)
                fps.Add(new DocFingerprint("manual.clause_mapping", WHigh, "含标准条款号与手册章号的对照载体"));
            if (ContainsAny(text, "组织机构", "职责权限", "组织架构", "职责与权限") && deptDuties >= 2)
                fps.Add(new DocFingerprint("manual.org_duty", WMid, "含组织职责章节且列出多部门"));
            if (ContainsAny(text, "程序文件清单", "程序目录", "程序清单", "相关文件")
                && CountLinesContaining(norm, "程序") >= 3)
                fps.Add(new DocFingerprint("manual.procedure_index", WMid, "含程序文件索引且列出多个程序"));
            if (span >= 5)
                fps.Add(new DocFingerprint("manual.std_chapter_span", WMid, $"顶层章节覆盖标准 {span} 章"));

            // ── 程序文件（六段式骨架 / 跨部门）──
            var six = SectionHitCount(text, new[]
            {
                new[] { "目的" },
                new[] { "适用范围", "范围" },
                new[] { "职责" },
                new[] { "工作程序", "程序内容", "工作内容", "工作流程", "管理内容" },
                new[] { "相关记录", "相关文件", "相关表单", "引用文件" },
            });
            if (six >= 4)
                fps.Add(new DocFingerprint("procedure.six_section", WHigh, $"命中六段式骨架 {six} 段"));
            if (deptDuties >= 2)
                fps.Add(new DocFingerprint("procedure.cross_dept", WMid, $"跨部门职责表述 {deptDuties} 处"));

            // ── 作业指导书（操作步骤/参数 / 判定标准）──
            if (ContainsAny(text, "操作步骤", "作业步骤", "工艺参数", "技术参数", "操作要点", "作业要点"))
                fps.Add(new DocFingerprint("wi.steps_or_params", WHigh, "含操作步骤/工艺参数"));
            if (ContainsAny(text, "判定标准", "合格标准", "检验标准", "不良判定"))
                fps.Add(new DocFingerprint("wi.judgement", WMid, "含判定/检验标准"));

            // ── 制度 / 规范（第X条 / 第X章）──
            if (baseCount >= 3)
                fps.Add(new DocFingerprint("policy.article_style", WHigh, $"含「第X条」条款式 {baseCount} 处"));
            if (hasChapter && baseCount >= 1)
                fps.Add(new DocFingerprint("policy.chapter_article", WMid, "同时含「第X章」与「第X条」"));

            // ── 记录 / 表单（表格密度 / 实例数据 / 空白表单 / 审核评审）──
            if (nonEmpty > 20 && tableDataRows > 0 && tableDataRows / (double)nonEmpty >= 0.30)
                fps.Add(new DocFingerprint("record.table_density", WMid, $"表格行占比 {tableDataRows}/{nonEmpty}"));
            if (tableDataRows >= 3 && dateCount >= 2)
                fps.Add(new DocFingerprint("record.instance_data", WHigh, $"表格含实例数据（日期 {dateCount} 处）"));
            else if (tableDataRows > 0)
                fps.Add(new DocFingerprint("record.blank_form", WMid, "有表格结构但实例数据少（疑似空白表单）"));
            if (ContainsAny(text, "内审", "内部审核", "管理评审", "审核发现", "不符合项", "内审检查表"))
                fps.Add(new DocFingerprint("record.audit_or_review", WHigh, "含内审/管理评审/审核发现/不符合项"));
            if (headingList.Count(h => h.Level >= 2 && h.HasContent) >= 2)
                fps.Add(new DocFingerprint("record.has_sections", WMid, "含多个文档小节"));

            // ── 文件索引 / 清单 ──
            if (ContainsAny(text, "受控文件清单", "文件发放", "文件回收", "文件销毁", "记录清单", "外来文件清单", "受控文件目录"))
                fps.Add(new DocFingerprint("index.document_control", WHigh, "含受控文件/发放回收/记录清单"));
            if (ContainsAny(text, "清单", "名录", "一览表", "台账", "台帐") && tableDataRows >= 3)
                fps.Add(new DocFingerprint("index.entry_list", WMid, "条目清单且无正文段落"));

            // ── 组织环境 / 相关方清单 ──
            if (norm.Contains("相关方", StringComparison.Ordinal)
                && (tableDataRows >= 2 || norm.Contains("清单", StringComparison.Ordinal)))
                fps.Add(new DocFingerprint("context.parties", WHigh, "含相关方清单"));
            if (ContainsAny(text, "外部因素", "内部因素", "风险和机遇", "环境因素"))
                fps.Add(new DocFingerprint("context.factors", WHigh, "含内外部因素/风险机遇清单"));

            // ── 非 QMS（财务/人事/后勤…且无体系强指纹）──
            var qmsHigh = fps.Any(f => f.Weight == WHigh
                && !string.Equals(f.Key, "policy.article_style", StringComparison.Ordinal)
                && !f.Key.StartsWith("none.", StringComparison.Ordinal));
            if (!qmsHigh && ContainsAny(text, "财务", "考勤", "食堂", "宿舍", "报销", "薪资", "工资", "保安", "后勤"))
                fps.Add(new DocFingerprint("none.non_qms", WMid, "非 QMS 制度且无体系结构指纹"));

            return fps;
        }

        /// <summary>
        /// 角色定位（确定性）：按指纹规则定 <c>DocType</c> / <c>Mode</c>；
        /// 无任何指纹命中 ⇒ <c>unclassified</c>（待人工确认，⛔ 不猜）。
        /// </summary>
        public static Classification Classify(IReadOnlyList<DocFingerprint>? fingerprints)
        {
            var fps = fingerprints ?? Array.Empty<DocFingerprint>();
            bool Has(string key) => fps.Any(f => string.Equals(f.Key, key, StringComparison.Ordinal));
            var reason = string.Join("；", fps.Select(f => f.Key));

            // ★ 2026-11-25 顺序修正：手册组合规则 / 内审记录 必须早于「文件控制」。
            //   否则质量手册只要正文含「记录清单」就被 index.document_control 吞成 document_control；
            //   内审检查表标题带空格时同理被 document_control 抢判。
            var manualHigh = CountHas(fps, "manual.issue_order", "manual.policy_objective", "manual.clause_mapping");
            var manualMid = CountHas(fps, "manual.org_duty", "manual.procedure_index", "manual.std_chapter_span");
            if (manualHigh >= 2 || (manualHigh >= 1 && manualMid >= 2))
                return new Classification("manual", "质量手册", "manual_chapter", manualHigh >= 2 ? 0.9 : 0.8, reason);

            // ★ P1 分类护栏：程序文件（六段式骨架）/ 组织环境清单，即便正文提到「管理评审 / 内审」，
            //   也不得被 record.audit_or_review 抢判为「审核记录」—— 否则 r1 条款轴（体裁白名单）会把
            //   《组织环境程序》《内外部因素清单》整类过滤掉，只剩记录类噪声进上下文。
            if (Has("record.audit_or_review")
                && !Has("procedure.six_section")
                && !Has("context.parties")
                && !Has("context.factors"))
                return new Classification("audit_record", "审核记录", "audit_profile", 0.85, reason);

            if (Has("index.document_control"))
                return new Classification("document_control", "文件控制", "document_control", 0.9, reason);

            if (Has("none.non_qms"))
                return new Classification("other", "其他", "none", 0.6, reason);

            if (Has("procedure.six_section"))
                return new Classification("procedure", "程序文件", "process_profile", 0.8, reason);

            if (Has("wi.steps_or_params"))
                return new Classification("work_instruction", "作业指导书", "work_instruction_citation", 0.75, reason);

            if (Has("policy.article_style"))
                return new Classification("policy", "制度规范", "policy_profile", 0.75, reason);

            if (Has("context.parties") || Has("context.factors"))
                return new Classification("context_list", "组织环境清单", "context_list", 0.8, reason);

            if (Has("record.instance_data") || Has("record.table_density") || Has("record.blank_form"))
                return new Classification("record", "记录表单", "record_profile", 0.7, reason);

            if (Has("index.entry_list"))
                return new Classification("index", "文件索引", "index_only", 0.65, reason);

            return new Classification("unclassified", "待确认", "unclassified", 0.0,
                reason.Length == 0 ? "未命中任何结构指纹" : reason);
        }

        /// <summary>Mode → 中文显示名（与页面「提取模式」列一致）。</summary>
        public static string ModeName(string? mode) => (mode ?? string.Empty).Trim() switch
        {
            "manual_chapter" => "手册章节",
            "process_profile" => "过程画像",
            "record_profile" => "记录画像",
            "index_only" => "仅索引",
            "document_control" => "文件控制",
            "work_instruction_citation" => "指导书引用",
            "policy_profile" => "制度摘要",
            "context_list" => "组织环境清单",
            "audit_profile" => "审核画像",
            "clause_full" => "逐条款",
            "none" => "不提取",
            "unclassified" => "待确认",
            "legacy" => "逐条款",
            _ => mode ?? string.Empty,
        };

        /// <summary>Mode → 文档类型中文名（指南 / 列表过滤口径）。</summary>
        public static string DocTypeNameForMode(string? mode) => (mode ?? string.Empty).Trim() switch
        {
            "manual_chapter" => "质量手册",
            "process_profile" => "程序文件",
            "record_profile" => "记录表单",
            "index_only" => "文件索引",
            "document_control" => "文件控制",
            "work_instruction_citation" => "作业指导书",
            "policy_profile" => "制度规范",
            "context_list" => "组织环境清单",
            "audit_profile" => "审核记录",
            "none" => "其他",
            _ => "待确认",
        };

        private static int CountHas(IReadOnlyList<DocFingerprint> fps, params string[] keys)
            => keys.Count(k => fps.Any(f => string.Equals(f.Key, k, StringComparison.Ordinal)));

        /// <summary>汉字之间的空白（docx/pdf 转换常把「内 审 检 查 表」拆成字间空格 ⇒ 关键词漏命中）。</summary>
        private static readonly Regex CjkSpaceRx =
            new(@"(?<=[\u3400-\u9fff])[ \t\u00a0\u3000]+(?=[\u3400-\u9fff])", RegexOptions.Compiled);

        /// <summary>去掉汉字之间的空白（仅供关键词匹配，⛔ 不改动原文结构/偏移）。</summary>
        private static string NormalizeCjk(string text)
            => string.IsNullOrEmpty(text) ? string.Empty : CjkSpaceRx.Replace(text, string.Empty);

        /// <summary>是否含任一关键词（去汉字间空白后按 Ordinal 包含）。</summary>
        private static bool ContainsAny(string text, params string[] keywords)
        {
            var norm = NormalizeCjk(text);
            return keywords.Any(k => norm.Contains(k, StringComparison.Ordinal));
        }

        /// <summary>正文中「含该词的行」数量（用于程序索引等条目性判定）。</summary>
        private static int CountLinesContaining(string text, string keyword)
            => text.Replace("\r", string.Empty).Split('\n')
                .Count(l => l.Contains(keyword, StringComparison.Ordinal));

        /// <summary>Markdown 表格分隔行（|---|:--:|）判定。</summary>
        private static bool IsTableSeparator(string line)
        {
            var t = line.Trim();
            if (t.Length == 0 || t[0] != '|') return false;
            foreach (var c in t)
            {
                if (c is '|' or '-' or ':' or ' ' or '\t') continue;
                return false;
            }
            return true;
        }

        /// <summary>命中的「段落组」数量：每组关键词出现在正文即计 1（用于六段式骨架判定）。</summary>
        private static int SectionHitCount(string text, string[][] groups)
            => groups.Count(g => g.Any(k => text.Contains(k, StringComparison.Ordinal)));
    }
}
