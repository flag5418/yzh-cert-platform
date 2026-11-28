using System;
using System.Collections.Generic;
using System.Linq;

namespace CertPlatform.Shared.Domain.Clauses
{
    /// <summary>
    /// 「大章节领域」（<c>ChapterDomain</c>）—— ISO 管理体系标准的第 4–10 章功能域。
    ///
    /// <para><b>为什么需要</b>：记录 / 过程类文档<b>从文档本身看不出属于哪一章</b>（实测 44/44 记录无条款号），
    /// 但章本身有语义 —— 只能靠<b>章节原型语义</b>把文档与条款归到同一「大章节」，再据此收敛召回空间。</para>
    ///
    /// <para><b>两级语义资产</b>（见 <c>docs/80-v4/23-...-V1.md</c> §2.1 / §2.1.1）：
    /// ① 条款侧 <c>ChapterDomain</c>：<b>确定性</b>映射（<c>Role</c> r1–r7 与 7 章 1:1；或章号 4–10）；
    /// ② 文档侧：<b>确定性档</b>（手册 <c>Toc</c> 章号 / 审核记录 <c>ClauseNumbers</c>）+ <b>语义档</b>（章节原型关键词）。</para>
    ///
    /// <para><b>纯函数</b>：不查库、可单测。⛔ 端内不得另写一份映射（守 A-R6「同一判据第二处即违规」）。</para>
    /// </summary>
    public static class ChapterDomain
    {
        public const string Env = "CD_ENV";         // 4  组织环境
        public const string Lead = "CD_LEAD";       // 5  领导作用
        public const string Decide = "CD_DECIDE";   // 6  策划
        public const string Support = "CD_SUPPORT"; // 7  支持
        public const string Run = "CD_RUN";         // 8  运行
        public const string Assess = "CD_ASSESS";   // 9  绩效评价
        public const string Improve = "CD_IMPROVE"; // 10 改进
        public const string Unknown = "CD_UNKNOWN"; // 判不出（⛔ 不静默丢弃、⛔ 不默认归 CD_RUN）

        /// <summary>7 个有效章域（不含 <see cref="Unknown"/>）。</summary>
        public static readonly string[] All = { Env, Lead, Decide, Support, Run, Assess, Improve };

        /// <summary>7 个有效章域 + <see cref="Unknown"/>。</summary>
        public static readonly string[] AllWithUnknown = { Env, Lead, Decide, Support, Run, Assess, Improve, Unknown };

        /// <summary>章域 → 章作用 <c>Role</c>（r1–r7，国标 4–10 章功能角色，与条款侧 1:1）。</summary>
        private static readonly Dictionary<string, string> RoleByDomain = new(StringComparer.Ordinal)
        {
            [Env] = "r1", [Lead] = "r2", [Decide] = "r3", [Support] = "r4",
            [Run] = "r5", [Assess] = "r6", [Improve] = "r7",
        };

        /// <summary>章号首段（4–10）→ 章域；非 Annex SL 结构（如 iso13485 第 4–8 章含义不同）不走此表。</summary>
        private static readonly Dictionary<string, string> DomainByChapter = new(StringComparer.Ordinal)
        {
            ["4"] = Env, ["5"] = Lead, ["6"] = Decide, ["7"] = Support,
            ["8"] = Run, ["9"] = Assess, ["10"] = Improve,
        };

        /// <summary>
        /// 章节原型定义（一等语义资产，<c>23</c> 号 §2.1.1）：名称 / 章作用 / 释义 / 典型证据 / 关键词。
        /// <para><b>关键词分级</b>：<see cref="Proto.Strong"/>（章域**唯一**标志，权重 3）用于压制噪声；
        /// <see cref="Proto.Normal"/>（支撑词，权重 1）作补充。扫描范围 = 文档语义骨架（简介 + 目录标题），
        /// ⛔ 不扫全文（全文噪声大、易误判）。</para>
        /// </summary>
        public sealed record Proto(
            string Code, string Name, string Role, string Purpose,
            string[] Strong, string[] Normal, string[] Evidence);

        public static readonly IReadOnlyList<Proto> Prototypes = new List<Proto>
        {
            new(Env, "组织环境", "r1", "理解组织及其环境、相关方的需求与期望、确定管理体系范围",
                new[] { "组织环境", "内外部因素", "外部因素", "内部因素", "相关方", "利益相关方", "理解组织" },
                new[] { "管理体系范围", "体系范围", "环境分析" },
                new[] { "组织环境分析报告", "相关方需求期望清单", "体系范围说明" }),

            new(Lead, "领导作用", "r2", "最高管理者的领导与承诺、方针、岗位职责与权限、沟通协商",
                new[] { "质量方针", "环境方针", "职业健康安全方针", "管理者代表", "最高管理者", "领导作用", "管理承诺" },
                new[] { "方针", "职责权限", "岗位职责", "沟通协商", "企业文化" },
                new[] { "方针发布", "职责权限规定", "管理者代表任命" }),

            new(Decide, "策划", "r3", "应对风险和机遇的措施、质量目标及其实现策划、变更策划",
                new[] { "风险和机遇", "风险与机遇", "风险机遇", "质量目标", "体系变更", "变更策划", "环境目标", "职业健康安全目标" },
                new[] { "风险", "机遇", "目标", "策划", "应对措施" },
                new[] { "风险和机遇评估表", "质量目标分解表", "变更策划记录" }),

            new(Support, "支持", "r4", "资源、能力与意识、基础设施、监视和测量资源、知识、成文信息",
                // ★ 2026-11-27：补记录类表头语汇（培训签到 / 岗位能力 / 质量意识 → 7.2；文件借阅/更改/销毁/外来文件 → 7.5.3）。
                //   这些词只出现在记录表单的表头/标题里，原关键词表未覆盖 ⇒ 纯表格记录恒落 CD_UNKNOWN。
                new[] { "人力资源", "培训", "基础设施", "监视测量资源", "监视和测量资源", "测量设备", "计量", "校准", "检定", "组织知识", "文件化信息", "成文信息", "文件控制", "记录控制", "文件更改", "文件借阅", "借阅复制", "文件销毁", "记录销毁", "销毁清单", "外来文件", "岗位能力", "质量意识", "培训计划", "签到", "受控文件", "文件清单", "文件发放", "文件回收", "设备台账", "设备维修", "设备保养", "维护保养", "特殊岗位" },
                new[] { "资源", "能力", "意识", "设备", "工作环境", "沟通", "信息交流" },
                new[] { "人力资源程序", "培训记录", "设备台账", "文件清单", "记录清单" }),

            new(Run, "运行", "r5", "运行策划与控制、设计开发、采购与外部提供、生产与服务提供、放行、不合格输出",
                // ★ 2026-11-27：补过程记录表头语汇（巡检/巡线 → 8.6；出厂检验/首件/质检日报 → 8.6；发货/装箱单/生产通知 → 8.5）。
                new[] { "设计开发", "采购", "供方", "供应商", "外包", "外部提供", "生产", "制造", "服务提供", "放行", "不合格输出", "标识", "追溯", "防护", "顾客财产", "交付", "运行策划", "运行控制", "巡检", "巡线", "出厂检验", "检验报告", "质检日报", "日报表", "首件", "进料检验", "来料检验", "来料检查", "发货", "送货", "入库", "领料", "装箱单", "生产通知", "生产日报", "作业指示书", "过程确认", "销售", "合同" },
                new[] { "运行", "售后", "安装", "过程控制", "储运", "包装" },
                new[] { "生产流程", "采购记录", "检验记录", "放行记录", "不合格品处理单" }),

            new(Assess, "绩效评价", "r6", "监视测量分析评价、内部审核、管理评审、顾客满意、合规性评价",
                new[] { "内部审核", "内审", "管理评审", "顾客满意", "合规性评价", "绩效评价", "数据分析", "满意度" },
                new[] { "监视", "测量", "分析", "评价", "监测", "绩效" },
                new[] { "内审报告", "管理评审报告", "顾客满意度调查", "数据分析报告" }),

            new(Improve, "改进", "r7", "不符合与纠正措施、持续改进",
                new[] { "持续改进", "纠正措施", "不符合", "纠正预防", "预防措施", "改进措施" },
                new[] { "改进", "不合格", "纠正" },
                new[] { "纠正措施报告", "不符合项报告", "改进记录" }),
        };

        /// <summary>章作用 <c>Role</c>（r1–r7）→ 章域（1:1）。未知 ⇒ <see cref="Unknown"/>。</summary>
        public static string FromRole(string? role)
        {
            var r = (role ?? string.Empty).Trim().ToLowerInvariant();
            return r switch
            {
                "r1" => Env, "r2" => Lead, "r3" => Decide, "r4" => Support,
                "r5" => Run, "r6" => Assess, "r7" => Improve,
                _ => Unknown,
            };
        }

        /// <summary>章域 → 章作用 <c>Role</c>（r1–r7）。未知 ⇒ null。</summary>
        public static string? RoleOf(string? domain)
            => RoleByDomain.TryGetValue((domain ?? string.Empty).Trim(), out var r) ? r : null;

        /// <summary>
        /// 章号 → 章域（<b>确定性</b>，仅适用 Annex SL 结构：4 环境 / 5 领导 / 6 策划 / 7 支持 / 8 运行 / 9 评价 / 10 改进）。
        /// <para>取编号首段（<c>7.1.5</c> ⇒ <c>7</c>）；首段不在 4–10 ⇒ null（如手册的 1/2/3 章、iso13485 的 4–8 章语义不同，⛔ 不硬套）。</para>
        /// </summary>
        public static string? FromClauseNumber(string? clauseNumber)
        {
            var n = (clauseNumber ?? string.Empty).Trim();
            if (n.Length == 0) return null;
            var head = n.Split('.', 2)[0];
            return DomainByChapter.TryGetValue(head, out var d) ? d : null;
        }

        /// <summary>
        /// 条款侧章域（确定性）：优先读属性 <c>ChapterDomain</c>；缺则用 <c>Role</c>；再缺用章号。⛔ 永不返回 null。
        /// </summary>
        public static string ResolveClause(string? chapterDomainAttr, string? role, string? clauseNumber)
        {
            var explicitDomain = Normalize(chapterDomainAttr);
            if (explicitDomain != Unknown) return explicitDomain;
            var byRole = FromRole(role);
            if (byRole != Unknown) return byRole;
            return FromClauseNumber(clauseNumber) ?? Unknown;
        }

        /// <summary>一批条款号 → 章域集合（确定性档，去重、去未知）。</summary>
        public static List<string> FromClauseNumbers(IEnumerable<string>? clauseNumbers)
        {
            var set = new List<string>();
            if (clauseNumbers == null) return set;
            foreach (var n in clauseNumbers)
            {
                var d = FromClauseNumber(n);
                if (d != null && !set.Contains(d)) set.Add(d);
            }
            return set;
        }

        /// <summary>从目录标题清单提取章域（确定性档）：逐条按章号映射，去重、去不可判。</summary>
        public static List<string> FromTitles(IEnumerable<string>? titles)
        {
            var domains = new List<string>();
            if (titles == null) return domains;
            foreach (var t in titles)
            {
                if (!IsChapterTitle(t)) continue;
                var d = FromClauseNumber(t);
                if (d != null && !domains.Contains(d)) domains.Add(d);
            }
            return domains;
        }

        /// <summary>
        /// 标题是否「像章标题」—— 章号之后必须还有标题文字。
        /// <para>⛔ 纯编号（<c>"4"</c>／<c>"8.1"</c>）与点号枚举（<c>"1.笔试以…"</c>）一律不算：
        /// 表格序号、正文列举在转 Markdown 后常被误当标题（实测《培训记录》目录含裸数字 <c>1–8</c> ⇒ 误判 5 个章域）。</para>
        /// </summary>
        private static bool IsChapterTitle(string? title)
        {
            var t = (title ?? string.Empty).Trim();
            if (t.Length == 0) return false;
            var j = 0;
            while (j < t.Length && char.IsDigit(t[j])) j++;
            if (j == 0 || j >= t.Length) return false;   // 不以数字开头 / 纯编号
            if (t[j] == '.')
            {
                // "4.1 理解组织" 允许；"1.笔试以…" 拒绝（点号后紧接正文且无空格）
                var k = j + 1;
                while (k < t.Length && char.IsDigit(t[k])) k++;
                return k < t.Length && char.IsWhiteSpace(t[k]);
            }
            return char.IsWhiteSpace(t[j]) || t[j] > 0x2E80;
        }

        /// <summary>文档侧章域判定的来源标记。</summary>
        public const string SrcClauseNumbers = "clause_numbers";
        public const string SrcToc = "toc";
        public const string SrcSemantic = "semantic";

        /// <summary>语义档（LLM 兜底）—— 确定性 + 关键词档均判不出时，由模型按章节原型语义判定。</summary>
        public const string SrcSemanticLlm = "semantic_llm";
        public const string SrcUnknown = "unknown";

        /// <summary>
        /// <b>文档侧章域判定</b>（三级优先级，⛔ 不硬推）：
        /// ① 已提取条款号（审核记录）→ 确定性；
        /// ② 目录标题章号（<b>仅质量手册</b>：手册目录 = ISO 章号）→ 确定性；
        /// ③ 语义骨架关键词（记录 / 程序 / 作业指导）→ 语义档。
        /// <para>返回 <c>(章域清单, 来源)</c>；来源 ∈ <c>clause_numbers / toc / semantic / unknown</c>。
        /// 判不出时返回 <c>[CD_UNKNOWN]</c>，⛔ 不默认归 CD_RUN。</para>
        /// </summary>
        /// <param name="tocIsManual">目录是否可信为 ISO 章号目录（<b>仅质量手册</b>）。
        /// 程序文件 / 作业指导书 / 记录表单各自的「4 工作程序 / 5 相关文件」是自有编号，与 ISO 章号同形不同义 ⇒ 采信即误判（实测 39 份被误判）。</param>
        /// <param name="title">文档名（文件名）—— 记录/表单类正文常为噪声，文件名是最强语义信号（加权命中）。</param>
        public static (List<string> Domains, string Source) ResolveDocument(
            IEnumerable<string>? clauseNumbers, IEnumerable<string>? tocTitles, string? semanticText,
            string? title = null, bool tocIsManual = false)
        {
            var byClause = FromClauseNumbers(clauseNumbers);
            if (byClause.Count > 0) return (byClause, SrcClauseNumbers);

            if (tocIsManual)
            {
                var byToc = FromTitles(tocTitles);
                if (byToc.Count >= 4) return (byToc, SrcToc);   // 真手册目录至少覆盖 4 章；否则视为噪声落入语义档
            }

            var byText = FromText(semanticText, title: title);
            if (byText.Count > 0) return (byText, SrcSemantic);

            return (new List<string> { Unknown }, SrcUnknown);
        }

        /// <summary>
        /// 语义档：按章节原型关键词对<b>文档语义骨架</b>（简介 + 目的 + 范围 + 目录标题）与<b>文件名</b>打分，取命中章域（可多值，上限 3）。
        /// <para>打分 = Σ(强关键词 × 3) + Σ(普通关键词 × 1)；文件名（<paramref name="title"/>）命中加权 ×3
        /// ⇒ 记录/表单类正文嘈杂（流水 / 程序清单），文件名才是可靠判据。0 分 ⇒ 返回空列表（由调用方落 <see cref="Unknown"/>）。</para>
        /// </summary>
        public static List<string> FromText(string? text, int maxDomains = 3, string? title = null)
        {
            var s = text ?? string.Empty;
            var t = title ?? string.Empty;
            if (s.Length == 0 && t.Length == 0) return new List<string>();

            var scored = new List<(string Code, int Score)>();
            foreach (var p in Prototypes)
            {
                var score = 0;
                foreach (var k in p.Strong)
                    if (s.Contains(k, StringComparison.Ordinal)) score += 3;
                foreach (var k in p.Normal)
                    if (s.Contains(k, StringComparison.Ordinal)) score += 1;
                if (t.Length > 0)
                {
                    foreach (var k in p.Strong)
                        if (t.Contains(k, StringComparison.Ordinal)) score += 9;
                    foreach (var k in p.Normal)
                        if (t.Contains(k, StringComparison.Ordinal)) score += 3;
                }
                if (score > 0) scored.Add((p.Code, score));
            }

            if (scored.Count == 0) return new List<string>();
            return scored
                .OrderByDescending(x => x.Score)
                .ThenBy(x => Array.IndexOf(All, x.Code))
                .Take(maxDomains)
                .Select(x => x.Code)
                .ToList();
        }

        /// <summary>章域码归一到已知值；未知 / 空 ⇒ <see cref="Unknown"/>。</summary>
        public static string Normalize(string? code)
        {
            var c = (code ?? string.Empty).Trim();
            return c.Length > 0 && Array.IndexOf(AllWithUnknown, c) >= 0 ? c : Unknown;
        }

        /// <summary>章域中文名（上屏用）。</summary>
        public static string Name(string? code)
        {
            var p = Prototypes.FirstOrDefault(x => string.Equals(x.Code, Normalize(code), StringComparison.Ordinal));
            return p?.Name ?? "未判定";
        }

        /// <summary>章域 + 中文名清单（如 <c>CD_ASSESS 绩效评价</c>），供提示词 / 日志。</summary>
        public static string Describe(IEnumerable<string>? domains)
        {
            var list = (domains ?? Enumerable.Empty<string>())
                .Select(Normalize).Distinct(StringComparer.Ordinal).ToList();
            return list.Count == 0
                ? "未判定"
                : string.Join("、", list.Select(d => $"{d} {Name(d)}"));
        }
    }
}
