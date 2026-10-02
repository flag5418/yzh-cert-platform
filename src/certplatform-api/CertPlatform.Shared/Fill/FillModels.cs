namespace CertPlatform.Shared.Fill;

/// <summary>
/// 文档填充 —— 锚点语法与解析上下文
///
/// <para><b>★ 统一锚点语法</b>：所有可变位置都写作 <c>{{...}}</c>，由 <b>token 内容</b>决定归属哪个 Resolver。
/// 这与 05 册 <c>22-填写单元属性分类与取值来源模型-V1.md</c> 的「填写单元」概念一一对应：</para>
///
/// <list type="table">
///   <listheader><term>写法</term><description>能力 / Resolver / 语义</description></listheader>
///   <item><term><c>{{company_name}}</c></term>
///         <description><b>全局参数</b> → <see cref="Resolvers.GlobalParamResolver"/>：
///         从「已解析的参数表」取值（企业端完善后的 <c>cert_fill_param_value</c>）。</description></item>
///   <item><term><c>{{enterprise.Name}}</c></term>
///         <description><b>替换</b> → <see cref="Resolvers.ReplaceResolver"/>：
///         按表达式直接取企业 / 机构 / 系统属性，<b>不经参数表</b>（用于「永远等于企业档案」的字段）。</description></item>
///   <item><term><c>{{@doc_no}}</c></term>
///         <description><b>页眉页脚</b> → <see cref="Resolvers.HeaderFooterResolver"/>：
///         文档级自动变量（编号 / 日期 / 页码 / 企业名），页眉页脚与正文共用。</description></item>
///   <item><term><c>{{ai:quality_policy}}</c></term>
///         <description><b>AI 生成</b> → <see cref="Resolvers.AiGenerateResolver"/>：
///         无法从任何结构化字段推出的段落（质量方针 / 目标 / 企业概况）。</description></item>
/// </list>
///
/// <para><b>为什么用统一语法而不是四套标记</b>：四套标记会让模板作者（机构/专家）记四套规则，
/// 且无法在一次扫描里统计「还剩多少处没填」。统一语法使 <see cref="FillReport"/> 能对整篇文档
/// 给出<b>可统计的完成度</b>——这正是 25 册纸面实验测出的 98.6% 确定性锚点的程序化表达。</para>
/// </summary>
public static class FillSyntax
{
    /// <summary>锚点正则：<c>{{</c> 与 <c>}}</c> 之间不允许再出现花括号（防跨段贪婪匹配）</summary>
    public const string TokenPattern = @"\{\{([^{}]+)\}\}";

    /// <summary>AI 生成前缀</summary>
    public const string AiPrefix = "ai:";

    /// <summary>页眉页脚 / 系统变量前缀</summary>
    public const string SysPrefix = "@";

    /// <summary>
    /// 表达式命名空间前缀（含 <c>.</c> 即视为表达式）。
    /// <para>⛔ 刻意<b>不</b>设 <c>doc.</c> 命名空间：文档元信息已有 <c>{{@doc_no}}</c> 一种写法
    /// （见 <see cref="Resolvers.HeaderFooterResolver"/>）。同一件事给两套写法，
    /// 就是本仓反复出现的「两套口径」缺陷 —— 模板作者会不知道用哪个，统计也会漏。</para>
    /// </summary>
    public static readonly string[] ExprNamespaces = { "enterprise.", "org.", "system." };

    /// <summary>是否 AI 生成锚点（<c>{{ai:xxx}}</c>）</summary>
    public static bool IsAiToken(string key) =>
        key.StartsWith(AiPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>是否页眉页脚 / 系统变量锚点（<c>{{@xxx}}</c>）</summary>
    public static bool IsSysToken(string key) =>
        key.StartsWith(SysPrefix, StringComparison.Ordinal);

    /// <summary>是否表达式锚点（<c>{{enterprise.Name}}</c>）</summary>
    public static bool IsExprToken(string key) =>
        ExprNamespaces.Any(ns => key.StartsWith(ns, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 从锚点**内容**中拆出「键」与「格式串」—— 支持 <c>{{key:format}}</c>（如 <c>{{amount:#,##0.00}}</c>）。
    ///
    /// <para><b>★ 为什么要拆</b>：Excel 的「值」与「显示格式」是两件事，且<b>格式由模板作者决定</b>
    /// （不是程序按值去猜）。把格式写在锚点里，模板作者一眼能看出这个格子最终显示成什么样；
    /// 程序侧只做「把 format 原样交给 <c>DataFormat</c>」，<b>⛔ 不做任何格式推断</b>。</para>
    ///
    /// <para><b>与 <c>ai:</c> 前缀的歧义怎么消</b>：<c>{{ai:quality_policy}}</c> 里的冒号是<b>命名空间</b>而非格式。
    /// 故拆分顺序是：<b>先剥已知前缀，再从剩余部分拆最后一个冒号</b>
    /// （用最后一个而非第一个，因为格式串自身可能含冒号，如 <c>hh:mm:ss</c>）。</para>
    ///
    /// <list type="table">
    ///   <listheader><term>输入</term><description>结果</description></listheader>
    ///   <item><term><c>amount:#,##0.00</c></term><description>key=<c>amount</c>，format=<c>#,##0.00</c></description></item>
    ///   <item><term><c>ai:quality_policy</c></term><description>key=<c>ai:quality_policy</c>，format=<c>null</c>（剥前缀后已无冒号）</description></item>
    ///   <item><term><c>ai:amount:0.00</c></term><description>key=<c>ai:amount</c>，format=<c>0.00</c></description></item>
    ///   <item><term><c>enterprise.Name</c></term><description>key=<c>enterprise.Name</c>，format=<c>null</c></description></item>
    ///   <item><term><c>@doc_date:yyyy年MM月dd日</c></term><description>key=<c>@doc_date</c>，format=<c>yyyy年MM月dd日</c></description></item>
    /// </list>
    ///
    /// <para>⚠️ 返回的 <c>Key</c> <b>含前缀</b>（与值字典 <c>AnchorCode</c> 逐字一致）；
    /// <c>":"</c> 后为空（如 <c>amount:</c>）时视为<b>无格式</b>，⛔ 不返回空格式串。</para>
    /// </summary>
    public static (string Key, string? Format) SplitFormat(string inner)
    {
        var text = (inner ?? string.Empty).Trim();

        var prefixLen = 0;
        if (text.StartsWith(AiPrefix, StringComparison.OrdinalIgnoreCase)) prefixLen = AiPrefix.Length;
        else if (text.StartsWith(SysPrefix, StringComparison.Ordinal)) prefixLen = SysPrefix.Length;

        var search = text[prefixLen..];
        var idx = search.LastIndexOf(':');
        if (idx < 0) return (text, null);

        var fmt = search[(idx + 1)..].Trim();
        if (fmt.Length == 0) return (text, null);

        return (text[..(prefixLen + idx)].Trim(), fmt);
    }

    /// <summary>
    /// 是否已被前三个 Resolver 认领。
    /// <para><b>用途</b>：<see cref="Resolvers.GlobalParamResolver"/> 是兜底 Resolver，
    /// 它用本方法<b>主动拒收</b>前三类 token。这样一旦引擎的 Resolver 顺序被改错，
    /// 症状是「报告里出现『锚点无归属解析器』的待办」——<b>看得见</b>；
    /// 若改成无条件接收，症状是「表达式被当成参数名，报『无此参数』」——<b>指错方向</b>。</para>
    /// </summary>
    public static bool IsClaimedByOthers(string key) =>
        IsAiToken(key) || IsSysToken(key) || IsExprToken(key);
}

/// <summary>
/// 企业属性快照（填充引擎的输入之一）。
/// <para>刻意**不复用** <c>Enterprise</c> 实体：引擎在 <c>CertPlatform.Shared</c>，
/// 该工程不引用 <c>YZH.Core.DataBase</c>，且引擎必须是**纯函数**（可单测、可离线跑）。</para>
/// </summary>
public sealed class EnterpriseInfo
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? CreditCode { get; set; }
    public string? LegalPerson { get; set; }
    public string? Province { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public string? IndustryType { get; set; }
    public int? EmployeeCount { get; set; }
    public string? CertScope { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? EnterpriseNo { get; set; }
    public DateTime? ArchiveDate { get; set; }

    /// <summary>按属性名取字符串值（大小写不敏感）。未知名返回 null。</summary>
    public string? Get(string attr) => attr?.Trim() switch
    {
        null => null,
        "Code" => Code,
        "Name" => Name,
        "ShortName" => ShortName,
        "CreditCode" => CreditCode,
        "LegalPerson" => LegalPerson,
        "Province" => Province,
        "City" => City,
        "Address" => Address,
        "IndustryType" => IndustryType,
        "EmployeeCount" => EmployeeCount?.ToString(),
        "CertScope" => CertScope,
        "ContactName" => ContactName,
        "ContactPhone" => ContactPhone,
        "ContactEmail" => ContactEmail,
        "EnterpriseNo" => EnterpriseNo,
        "ArchiveDate" => ArchiveDate?.ToString("yyyy-MM-dd"),
        _ => null,
    };
}

/// <summary>机构属性快照（认证机构 / 专家工作区）</summary>
public sealed class OrgInfo
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Address { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }

    public string? Get(string attr) => attr?.Trim() switch
    {
        null => null,
        "Code" => Code,
        "Name" => Name,
        "ShortName" => ShortName,
        "Address" => Address,
        "ContactName" => ContactName,
        "ContactPhone" => ContactPhone,
        _ => null,
    };
}

/// <summary>文档级信息（页眉页脚自动变量）</summary>
public sealed class DocInfo
{
    /// <summary>文档编号（如 <c>YZH-QM-2026-001</c>）</summary>
    public string? No { get; set; }

    /// <summary>文档名称</summary>
    public string? Title { get; set; }

    /// <summary>版本号（如 <c>A/0</c>）</summary>
    public string? Version { get; set; }

    /// <summary>标准编号（如 <c>GB/T 19001-2016</c>）</summary>
    public string? StandardNo { get; set; }

    /// <summary>阶段名称（如 <c>初次认证</c>）</summary>
    public string? StageName { get; set; }

    /// <summary>页码占位（引擎不翻页，仅提供替换值）</summary>
    public string? Page { get; set; }
}

/// <summary>
/// 填充上下文 —— 引擎的<b>唯一</b>输入。
/// <para>纯数据、无服务依赖，便于单测与「同一份输入必得同一份输出」的复现性。</para>
/// </summary>
public sealed class FillContext
{
    /// <summary>待填充的模板正文（Markdown 或纯文本）</summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>参数编码 → 参数值（企业端完善后的值；<b>全局参数</b>能力的取数口）</summary>
    public IDictionary<string, string> Params { get; set; } = new Dictionary<string, string>();

    /// <summary>参数编码 → 参数名（用于报告里显示「哪一项没填」）</summary>
    public IDictionary<string, string> ParamNames { get; set; } = new Dictionary<string, string>();

    /// <summary>企业属性（<b>替换</b>能力中 <c>enterprise.*</c> 的取数口）</summary>
    public EnterpriseInfo Enterprise { get; set; } = new();

    /// <summary>机构属性（<c>org.*</c>）</summary>
    public OrgInfo Org { get; set; } = new();

    /// <summary>文档信息（页眉页脚自动变量）</summary>
    public DocInfo Doc { get; set; } = new();

    /// <summary>页眉模板（本身也可含锚点）</summary>
    public string? HeaderTemplate { get; set; }

    /// <summary>页脚模板（本身也可含锚点）</summary>
    public string? FooterTemplate { get; set; }

    /// <summary>是否启用 AI 生成。关闭时 <c>{{ai:*}}</c> 一律记为「待生成」。</summary>
    public bool AiEnabled { get; set; }

    /// <summary>基准时间（注入而非 <c>DateTime.Now</c>，保证可复现）</summary>
    public DateTime Now { get; set; } = DateTime.Now;
}

/// <summary>一次成功的锚点替换记录</summary>
public sealed class FillHit
{
    /// <summary>原始锚点原文，如 <c>{{enterprise.Name}}</c></summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>锚点键，如 <c>enterprise.Name</c> / <c>company_name</c> / <c>ai:quality_policy</c></summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>归属能力：global | replace | headerFooter | ai</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>替换后的值（截断到 120 字符，仅供报告展示）</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>值来源说明，如「企业基础信息 · 企业全称」</summary>
    public string Source { get; set; } = string.Empty;
}

/// <summary>一处未解析的锚点（= 待办）</summary>
public sealed class FillPending
{
    public string Token { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;

    /// <summary>未解析原因：未填写 / 无此参数 / AI 未启用 / 表达式无法求值</summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 填充报告 —— <b>「一键看证据摘要」的核心数据</b>（05 册 21 号 §定位）。
/// <para>把「文档填了多少」变成可统计、可展示、可归档的数字。</para>
/// </summary>
public sealed class FillReport
{
    /// <summary>锚点总数</summary>
    public int Total => Hits.Count + Pendings.Count;

    /// <summary>已解析数</summary>
    public int ResolvedCount => Hits.Count;

    /// <summary>待办数</summary>
    public int PendingCount => Pendings.Count;

    /// <summary>
    /// 完成度（0~1）。**无锚点时记 0**（= 「无可统计项」，由界面显示空态）。
    /// <para>⛔ 不要改成 1.0：企业端 <c>liveCompletion</c> 在 total=0 时返回 0，
    /// 两处口径必须一致，否则同一个「没有参数定义」的状态会一处显示 100%、一处显示 0%。</para>
    /// </summary>
    public double Completion => Total == 0 ? 0.0 : (double)ResolvedCount / Total;

    /// <summary>按能力统计：<c>kind → (已解析, 待办)</c></summary>
    public Dictionary<string, int[]> ByKind { get; set; } = new();

    public List<FillHit> Hits { get; set; } = new();

    public List<FillPending> Pendings { get; set; } = new();
}

/// <summary>引擎输出：填充后的正文 + 报告</summary>
public sealed class FillResult
{
    /// <summary>填充后的正文</summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>填充后的页眉（未配置则为 null）</summary>
    public string? Header { get; set; }

    /// <summary>填充后的页脚（未配置则为 null）</summary>
    public string? Footer { get; set; }

    /// <summary>填充报告</summary>
    public FillReport Report { get; set; } = new();
}
