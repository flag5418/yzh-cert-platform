using System.Text;
using System.Text.RegularExpressions;

namespace CertPlatform.Auditor.Services.Ent;

/// <summary>
/// 多标准分发匹配器（04 分册 §四：M0–M3 分层匹配 + 归一化函数，纯计算不落库）。
///
/// <para><b>逐标准独立判定</b>（需求 3 硬约束）：对每个关联标准只用<b>该标准</b>的模板定义集
/// 匹配一次；未命中不进入该标准。同文件命中多标准 ⇒ 产出多行（各标准各存一份）。</para>
///
/// <para><b>匹配层级</b>（顺序命中即停）：</para>
/// <list type="table">
///   <item><term>M0</term><description>文件夹路径强匹配：上传相对路径的目录段 == 模板文件夹 FullPath（归一后），且文件名命中 M1/M2</description></item>
///   <item><term>M1</term><description>文件名精确（归一后相等）</description></item>
///   <item><term>M2</term><description>文件名主词包含：模板名主词（归一后）出现在上传名中</description></item>
///   <item><term>M3</term><description>目录 + 扩展名族 + 名称相关度，且<b>必须唯一胜出</b>（<see cref="DispatchMatchLevel.M3FolderExt"/>）；猜不准就不产出，文件落未归属交人工指派</description></item>
/// </list>
///
/// <para>归一化规则见 <see cref="NormalizeCore"/> / <see cref="NormalizeFolder"/>：
/// 去编码前缀 / 去括号注释 / 去序号前缀 / 去空白连字符 / 扩展名族折叠 / 大小写不敏感。</para>
/// </summary>
public static partial class DispatchMatcher
{
    /// <summary>匹配层级</summary>
    public enum DispatchMatchLevel
    {
        /// <summary>M0：文件夹路径强匹配 + 文件名命中</summary>
        M0Path = 0,
        /// <summary>M1：文件名精确（归一）</summary>
        M1Exact = 1,
        /// <summary>M2：文件名主词包含</summary>
        M2Contains = 2,
        /// <summary>M3：目录 + 扩展名族 + 名称相关度且唯一胜出（需人工确认）</summary>
        M3FolderExt = 3
    }

    /// <summary>
    /// M3 名称相关度门槛（中文二元组 bigram 重叠数）。
    /// <para>取 2 的原因：实测 <c>员工培训签到表</c> vs <c>培训签到及有效性评价表</c> 重叠 3（该命中），
    /// 而 <c>不合格品处置记录</c> vs <c>设备维修记录</c> 只重叠 1（<b>不该</b>命中）——1 是"都叫记录"的噪声。</para>
    /// </summary>
    public const int MinM3Affinity = 2;

    /// <summary>模板文件定义（匹配输入；由服务层从槽位行投影）</summary>
    public sealed class TemplateSlot
    {
        public required string SlotCode { get; init; }
        public required string FileName { get; init; }
        /// <summary>模板文件夹全路径（如 <c>4记录文件/技术类</c>；根为空串）</summary>
        public string FolderPath { get; init; } = "";
        public required string FolderCode { get; init; }
        public bool IsRequired { get; init; }
        /// <summary>模板声明的单文件上限（MB；&lt;= 0 = 不限）——超限出 <c>BlockReason</c>，不上传</summary>
        public int MaxSizeMB { get; init; }
        /// <summary>槽位已就位（有有效文件）——命中该槽位的计划行会被拒绝（必须走替换）</summary>
        public bool Occupied { get; init; }
    }

    /// <summary>待分发的上传文件</summary>
    public sealed class IncomingFile
    {
        public required string FileName { get; init; }
        /// <summary>相对路径（目录段 + 文件名，<c>/</c> 分隔；单文件上传时 = FileName）</summary>
        public string RelativePath { get; init; } = "";
        public long FileSize { get; init; }
    }

    /// <summary>计划行（命中，可执行）</summary>
    public sealed class DispatchRow
    {
        public required string FileName { get; init; }
        public string RelativePath { get; init; } = "";
        public long FileSize { get; init; }
        public required string StandardCode { get; init; }
        public required string StandardName { get; init; }
        public required string SlotCode { get; init; }
        public required string SlotFileName { get; init; }
        public required string FolderCode { get; init; }
        public string FolderPath { get; init; } = "";
        public DispatchMatchLevel Level { get; init; }
        /// <summary>M3 命中 = 标黄需人工确认</summary>
        public bool NeedsConfirm => Level == DispatchMatchLevel.M3FolderExt;
        /// <summary>非空 = 该行不可执行（如槽位已就位需走替换）</summary>
        public string? BlockReason { get; init; }
    }

    /// <summary>未归属文件（D4：单列展示，可人工指派或忽略；不指派不入库不上传）</summary>
    public sealed class UnmatchedFile
    {
        public required string FileName { get; init; }
        public string RelativePath { get; init; } = "";
        public long FileSize { get; init; }
        /// <summary>未命中原因：<c>no_standard</c>（该阶段无标准）/ <c>no_match</c>（全标准都无命中）/ <c>ambiguous</c>（有候选但分不清，见 <see cref="MinM3Affinity"/>）</summary>
        public string Reason { get; init; } = "no_match";
    }

    /// <summary>同标准内冲突（D5：多文件命中同一槽位 → 人工选「只取一份 / 全部上传」）</summary>
    public sealed class DispatchConflict
    {
        public required string StandardCode { get; init; }
        public required string StandardName { get; init; }
        public required string SlotCode { get; init; }
        public required string SlotFileName { get; init; }
        public required List<string> FileNames { get; init; }
    }

    /// <summary>分发计划（纯计算结果）</summary>
    public sealed class DispatchPlan
    {
        public List<DispatchRow> Rows { get; init; } = new();
        public List<UnmatchedFile> Unmatched { get; init; } = new();
        public List<DispatchConflict> Conflicts { get; init; } = new();
        /// <summary>跨标准命中（同文件 → N 标准各一份），供前端提示「将各存一份（共 N 份）」</summary>
        public List<KeyValuePair<string, List<string>>> CrossStandard { get; init; } = new();
    }

    /// <summary>一个标准的匹配输入（模板槽位集 + 展示名）</summary>
    public sealed class StandardScope
    {
        public required string StandardCode { get; init; }
        public required string StandardName { get; init; }
        public required List<TemplateSlot> Slots { get; init; }
    }

    // ─────────────────────────────────────────────────────────
    // 归一化（04 分册 §4.2，集中一处）
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 编码前缀：<c>XASL-QM 名称</c> / <c>XASL-QP-001 名称</c> / <c>XM-02-0007 名称</c>。
    /// <para>★ 末段（<c>-001</c>）是<b>可选</b>的：实测模板 167 条里 148 条是 <c>XASL-XX-NNN 名称</c>，
    /// 另 19 条是 <c>XASL-XX 名称</c>（如旗舰槽位 <c>XASL-QM 质量手册</c>）。若把末段写成必选，
    /// 这 19 条的主词永远剥不掉编码前缀 ⇒ M1/M2 全不命中 ⇒ 一律掉进 M3 随机分配（实测过）。</para>
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z]{2,6}[-_][A-Za-z]{1,4}(?:[-_]\d{1,4})?\s*")]
    private static partial Regex CodePrefixRegex();

    /// <summary>全角 ASCII → 半角</summary>
    public static string FoldWidth(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch >= 0xFF01 && ch <= 0xFF5E) sb.Append((char)(ch - 0xFEE0));
            else if (ch == 0x3000) sb.Append(' ');
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>扩展名族折叠：docx≡doc、xlsx≡xls、pptx≡ppt、jpeg≡jpg（小写）</summary>
    public static string NormalizeExt(string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? "")?.ToLowerInvariant().TrimStart('.') ?? "";
        return ext switch
        {
            "docx" => "doc",
            "xlsx" => "xls",
            "pptx" => "ppt",
            "jpeg" => "jpg",
            _ => ext
        };
    }

    /// <summary>
    /// 文件名主词归一：去扩展名 → 全角折叠 → 去编码前缀 → 去括号注释 → 去空白/连字符/下划线/点 → 小写。
    /// <para>例：<c>XASL-TR-001 年度验证计划（2026版）.doc</c> → <c>年度验证计划2026版</c></para>
    /// </summary>
    public static string NormalizeCore(string? fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName ?? "") ?? "";
        stem = FoldWidth(stem);
        stem = CodePrefixRegex().Replace(stem, "");
        stem = BracketRegex().Replace(stem, "");
        stem = JunkCharsRegex().Replace(stem, "");
        return stem.ToLowerInvariant();
    }

    /// <summary>文件夹段/路径归一：去括号注释（含「可根据企业实际修改」）→ 去序号前缀 → 去空白连字符 → 小写</summary>
    public static string NormalizeFolder(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return "";
        var s = FoldWidth(folderPath);
        var segs = s.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(seg =>
            {
                seg = BracketRegex().Replace(seg, "");
                seg = LeadingDigitsRegex().Replace(seg, "");
                seg = JunkCharsRegex().Replace(seg, "");
                return seg.ToLowerInvariant();
            })
            .Where(seg => seg.Length > 0);
        return string.Join("/", segs);
    }

    [GeneratedRegex(@"[（(][^（）()]*[)）]")]
    private static partial Regex BracketRegex();

    [GeneratedRegex(@"^[\s\d０-９]+[、．.\-]?\s*")]
    private static partial Regex LeadingDigitsRegex();

    [GeneratedRegex(@"[\s_\-—–·～~]+")]
    private static partial Regex JunkCharsRegex();

    /// <summary>
    /// 名称相关度（M3 用）：一方完整包含另一方记满分，否则按<b>中文二元组（bigram）重叠数</b>计分。
    /// <para>为什么用 bigram 而不是分词：模板名几乎无空格分词可用，而 bigram 天然适配中文连续词
    /// （<c>培训签到</c> / <c>签到表</c> 会共享 <c>培训</c>·<c>训签</c>·<c>签到</c>），且对语序不敏感。</para>
    /// </summary>
    public static int NameAffinity(string? a, string? b)
    {
        var x = a ?? "";
        var y = b ?? "";
        if (x.Length == 0 || y.Length == 0) return 0;
        if (x.Contains(y, StringComparison.Ordinal) || y.Contains(x, StringComparison.Ordinal)) return 100;

        var grams = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i + 2 <= x.Length; i++) grams.Add(x.Substring(i, 2));

        var hit = 0;
        for (var i = 0; i + 2 <= y.Length; i++)
            if (grams.Contains(y.Substring(i, 2))) hit++;
        return hit;
    }

    // ─────────────────────────────────────────────────────────
    // 分层匹配
    // ─────────────────────────────────────────────────────────

    /// <summary>对单个标准匹配一个文件：M0 → M1 → M2 → M3，命中即停；未命中返回 null</summary>
    public static (TemplateSlot Slot, DispatchMatchLevel Level)? MatchSingle(
        IncomingFile file, StandardScope standard)
    {
        if (standard.Slots.Count == 0) return null;

        var relPath = (file.RelativePath ?? file.FileName).Replace('\\', '/').TrimStart('/');
        var dirPart = relPath.Contains('/') ? relPath[..relPath.LastIndexOf('/')] : "";
        // ★ 文件名以 IncomingFile.FileName 为准；RelativePath 只提供目录段。
        //   旧实现拿 RelativePath 末段当文件名，客户端两者不一致时静默 no_match（无任何提示）。
        var fileName = string.IsNullOrWhiteSpace(file.FileName)
            ? (relPath.Contains('/') ? relPath[(relPath.LastIndexOf('/') + 1)..] : relPath)
            : file.FileName;

        var normDir = NormalizeFolder(dirPart);
        var normName = NormalizeCore(fileName);
        var ext = NormalizeExt(fileName);

        // 目录段 → 模板文件夹归一路径索引（只含「有槽位」的文件夹，空目录不是键）
        var byFolder = standard.Slots
            .GroupBy(s => NormalizeFolder(s.FolderPath))
            .ToDictionary(g => g.Key, g => g.ToList());

        // 目录候选：整段优先；再退一级（兼容「用户选了整个外层文件夹」导致 webkitRelativePath 多出的那一级根目录）
        foreach (var dir in DirCandidates(normDir))
        {
            if (dir.Length > 0 && byFolder.TryGetValue(dir, out var folderSlots))
            {
                var hit = MatchByName(folderSlots, normName);
                if (hit != null) return (hit.Value.Slot, DispatchMatchLevel.M0Path);
            }
        }

        // M1 / M2：全标准范围文件名匹配（跨文件夹同名单槽位取第一个未占用）
        var globalHit = MatchByName(standard.Slots, normName);
        if (globalHit != null) return (globalHit.Value.Slot, globalHit.Value.Level);

        // M3：目录定位 + 扩展名族 + 名称相关度唯一胜出。猜不准就返回 null（落未归属交人工指派）。
        var m3 = MatchByFolderAffinity(byFolder, normDir, normName, ext);
        if (m3 != null) return (m3, DispatchMatchLevel.M3FolderExt);

        return null;
    }

    /// <summary>
    /// M3：先按<b>完整相对目录</b>定位模板文件夹（等值，或文件目录在其之下），
    /// 再在同扩展名槽位里挑名称相关度最高者，<b>并列即放弃</b>。
    /// <para>⚠️ 旧实现只取目录<b>首段</b>（<c>4记录文件/生产类/x.doc</c> → 只看 <c>4记录文件</c>），
    /// 再返回该文件夹的 <c>slots[0]</c>，实测把 <c>不合格品处置记录.doc</c>（生产类）塞进了
    /// <c>4记录文件/其它/培训签到及有效性评价表.doc</c>——跨目录 + 跨语义双重错配，且前端默认已勾选。</para>
    /// </summary>
    private static TemplateSlot? MatchByFolderAffinity(
        Dictionary<string, List<TemplateSlot>> byFolder, string normDir, string normName, string ext)
    {
        if (normDir.Length == 0 || ext.Length == 0) return null;

        // 定位文件夹：文件目录 == 模板目录，或文件目录在模板目录之下（更深一级）
        var folderKey = byFolder.Keys
            .Where(k => k.Length > 0
                        && (k == normDir || normDir.StartsWith(k + "/", StringComparison.Ordinal)))
            .OrderByDescending(k => k.Length)
            .ThenBy(k => k, StringComparer.Ordinal)
            .FirstOrDefault();
        if (folderKey == null) return null;

        var ranked = byFolder[folderKey]
            .Where(s => NormalizeExt(s.FileName) == ext)
            .Select(s => (Slot: s, Score: NameAffinity(normName, NormalizeCore(s.FileName))))
            .Where(x => x.Score >= MinM3Affinity)
            .OrderByDescending(x => x.Score)
            .ToList();

        // 唯一胜出才给行：并列说明「分不清」，交给人工指派而不是替用户猜
        if (ranked.Count != 1) return null;
        return ranked[0].Slot;
    }

    /// <summary>目录候选：整段优先，最多再退一级（<c>外层文件夹/真实目录</c> → <c>真实目录</c>）</summary>
    private static IEnumerable<string> DirCandidates(string normDir)
    {
        yield return normDir;
        var slash = normDir.IndexOf('/');
        if (slash > 0) yield return normDir[(slash + 1)..];
    }

    /// <summary>按文件名匹配槽位集：M1 精确优先，M2 主词包含次之；同层多命中取第一个未占用</summary>
    private static (TemplateSlot Slot, DispatchMatchLevel Level)? MatchByName(
        List<TemplateSlot> slots, string normName)
    {
        if (normName.Length == 0) return null;
        var exact = slots.Where(s => NormalizeCore(s.FileName) == normName).ToList();
        if (exact.Count > 0) return (Pick(exact), DispatchMatchLevel.M1Exact);

        var contains = slots.Where(s =>
        {
            var stem = NormalizeCore(s.FileName);
            return stem.Length >= 2 && normName.Contains(stem, StringComparison.Ordinal);
        }).ToList();
        if (contains.Count > 0) return (Pick(contains), DispatchMatchLevel.M2Contains);

        return null;
    }

    /// <summary>同分多命中：优先未占用槽位，其次必传</summary>
    private static TemplateSlot Pick(List<TemplateSlot> candidates)
        => candidates.FirstOrDefault(c => !c.Occupied)
           ?? candidates.OrderByDescending(c => c.IsRequired).First();

    // ─────────────────────────────────────────────────────────
    // 计划构建（逐标准独立 → 未归属 → 冲突 → 跨标准）
    // ─────────────────────────────────────────────────────────

    /// <summary>
    /// 构建分发计划（纯计算，不落库不写对象）。
    /// <para>逐标准独立判定；仅当文件在<b>所有</b>标准都未命中才进未归属（04 分册 §四）。</para>
    /// </summary>
    public static DispatchPlan BuildPlan(List<IncomingFile> files, List<StandardScope> standards)
    {
        // 入参去重（按关联键）：同一份文件被送来两次时，若不先去重会为它产出两行同槽位计划，
        // 进而在 UI 上造出「同文件跨标准命中 9001标准 + 9001标准」「文件自己和自己冲突」这类假告警。
        var deduped = files
            .GroupBy(FileKeyOf, StringComparer.Ordinal)
            .Select(g => g.First())
            .ToList();

        // 局部累积 → 末尾一次性构造（DispatchPlan 的属性是 init-only，禁止构造后再赋值）
        var rows = new List<DispatchRow>();
        var unmatched = new List<UnmatchedFile>();
        // key = 相对路径（同名不同目录必须分开，见 P0-3）；单文件上传时退化为文件名
        var hitByFile = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var file in deduped)
        {
            var anyHit = false;
            // 至少有一个标准给出了「有把握但分不清」的候选 ⇒ 归因为歧义（前端文案不同，指导人工指派）
            var anyAmbiguous = false;

            foreach (var std in standards)
            {
                var hit = MatchSingle(file, std);
                if (hit == null)
                {
                    if (HasFolderCandidate(std, file)) anyAmbiguous = true;
                    continue;
                }
                anyHit = true;
                var slot = hit.Value.Slot;
                rows.Add(new DispatchRow
                {
                    FileName = file.FileName,
                    RelativePath = file.RelativePath,
                    FileSize = file.FileSize,
                    StandardCode = std.StandardCode,
                    StandardName = std.StandardName,
                    SlotCode = slot.SlotCode,
                    SlotFileName = slot.FileName,
                    FolderCode = slot.FolderCode,
                    FolderPath = slot.FolderPath,
                    Level = hit.Value.Level,
                    BlockReason = BlockReasonOf(slot, file)
                });

                var key = FileKeyOf(file);
                if (!hitByFile.TryGetValue(key, out var list))
                    hitByFile[key] = list = new List<string>();
                // 同一文件在同一标准内重复出现（前端重复添加 / 同名同路径）不重复计入
                if (!list.Contains(std.StandardName)) list.Add(std.StandardName);
            }

            if (!anyHit)
                unmatched.Add(new UnmatchedFile
                {
                    FileName = file.FileName,
                    RelativePath = file.RelativePath,
                    FileSize = file.FileSize,
                    Reason = standards.Count == 0 ? "no_standard" : anyAmbiguous ? "ambiguous" : "no_match"
                });
        }

        // 跨标准命中（≥2 个标准）
        var crossStandard = hitByFile
            .Where(kv => kv.Value.Count > 1)
            .Select(kv => new KeyValuePair<string, List<string>>(kv.Key, kv.Value))
            .ToList();

        // 同标准内冲突：多个文件命中同一槽位
        var conflicts = rows
            .GroupBy(r => new { r.StandardCode, r.StandardName, r.SlotCode, r.SlotFileName })
            .Where(g => g.Count() > 1)
            .Select(g => new DispatchConflict
            {
                StandardCode = g.Key.StandardCode,
                StandardName = g.Key.StandardName,
                SlotCode = g.Key.SlotCode,
                SlotFileName = g.Key.SlotFileName,
                FileNames = g.Select(r => r.FileName).Distinct().ToList()
            })
            .ToList();

        return new DispatchPlan
        {
            Rows = rows,
            Unmatched = unmatched,
            Conflicts = conflicts,
            CrossStandard = crossStandard
        };
    }

    /// <summary>计划行的不可执行原因：模板单文件上限优先于「已就位」（超限是硬拦，替换也过不去）</summary>
    private static string? BlockReasonOf(TemplateSlot slot, IncomingFile file)
    {
        if (slot.MaxSizeMB > 0 && file.FileSize > (long)slot.MaxSizeMB * 1024 * 1024)
            return $"超过模板单文件上限 {slot.MaxSizeMB}MB（当前 {file.FileSize / 1024 / 1024}MB）";
        return slot.Occupied ? "该槽位已有就位文件，请在标准卡片内使用「替换」" : null;
    }

    /// <summary>该标准下是否存在「目录与扩展名都对得上、但名称分不清」的候选（M3 放弃的典型场景）</summary>
    private static bool HasFolderCandidate(StandardScope standard, IncomingFile file)
    {
        var relPath = (file.RelativePath ?? file.FileName).Replace('\\', '/').TrimStart('/');
        var dirPart = relPath.Contains('/') ? relPath[..relPath.LastIndexOf('/')] : "";
        var normDir = NormalizeFolder(dirPart);
        var ext = NormalizeExt(file.FileName);
        if (normDir.Length == 0 || ext.Length == 0) return false;
        return standard.Slots.Any(s => NormalizeExt(s.FileName) == ext
                                      && NormalizeFolder(s.FolderPath) == normDir);
    }

    /// <summary>跨标准聚合键：相对路径优先（同名不同目录是不同文件）</summary>
    private static string FileKeyOf(IncomingFile file)
        => string.IsNullOrWhiteSpace(file.RelativePath) ? file.FileName : file.RelativePath;
}
