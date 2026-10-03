namespace CertPlatform.Shared.Office;

/// <summary>
/// 值字典中一个值的**类型**。
///
/// <para><b>为什么必须显式给类型</b>：NPOI 的 <c>ICell</c> 有一组重载
/// （<c>SetCellValue(string)</c> / <c>SetCellValue(double)</c> / <c>SetCellValue(DateTime)</c>），
/// 传错重载**不报错但结果错**——数字写成文本后 Excel 里「1,000」会变成左对齐的字符串，
/// 既不能求和也不能排序，而且**打开文件看不出异常**。
/// ⇒ 类型由<b>层 2（值字典）</b>显式给出，写入器<b>只按类型落笔，不做任何推断</b>。</para>
/// </summary>
public enum FillValueKind
{
    /// <summary>文本（默认）</summary>
    Text = 0,

    /// <summary>数值（Excel 走 <c>SetCellValue(double)</c>，可配 <see cref="FillValue.NumberFormat"/>)</summary>
    Number = 1,

    /// <summary>日期（Excel 走 <c>SetCellValue(DateTime)</c>，⛔ 必须显式 <c>DataFormat</c>，否则显示成序列号）</summary>
    Date = 2,

    /// <summary>布尔</summary>
    Bool = 3,

    /// <summary>Word 域（<c>doc_domain</c>）。⚠️ 要求锚点<b>独占一段</b>，见 <c>WordFieldWriter</c>。</summary>
    Field = 4,
}

/// <summary>
/// 值字典的一个条目 —— <b>层 2（AI / 规则）与层 1（NPOI 写入）之间的唯一契约</b>。
///
/// <para>分层铁律：<b>「AI 只产值，NPOI 只落笔」</b>。
/// 层 2 负责「这个框该填什么」（语义判断），层 1 负责「怎么把它写进 Word/Excel」（确定性写入）。
/// 本类型是两层之间传递的**唯一**载体 ⇒ 层 1 <b>⛔ 不得</b>做任何语义判断
/// （不猜值、不补值、不按类型转换值）。</para>
///
/// <para>为什么这样分：① <b>可重放</b>——同一值字典重跑必得同一文件；
/// ② <b>可审计</b>——出问题能归因到「值错了（层 2）」还是「写错了（层 1）」；
/// ③ <b>可换库</b>——换 OpenXML SDK / Aspose 时只换层 1，层 2 零改动。</para>
/// </summary>
public sealed class FillValue
{
    /// <summary>锚点键 —— 与文档中 <c>{{ }}</c> 内的文本**逐字一致**（Ordinal 比较，⛔ 不做大小写折叠）。</summary>
    public string AnchorCode { get; set; } = string.Empty;

    /// <summary>值类型</summary>
    public FillValueKind Kind { get; set; } = FillValueKind.Text;

    /// <summary>文本值。<see cref="FillValueKind.Text"/> 时使用；<see cref="FillValueKind.Field"/> 时为**域的显示文本**。</summary>
    public string? Text { get; set; }

    /// <summary>数值。<see cref="FillValueKind.Number"/> 时使用。</summary>
    public double? Number { get; set; }

    /// <summary>日期。<see cref="FillValueKind.Date"/> 时使用。</summary>
    public DateTime? Date { get; set; }

    /// <summary>布尔。<see cref="FillValueKind.Bool"/> 时使用。</summary>
    public bool? Bool { get; set; }

    /// <summary>
    /// Excel 数字/日期的格式串（如 <c>#,##0.00</c> / <c>yyyy-mm-dd</c> / <c>0.00%</c>）。
    /// <para>⚠️ 为 null 时写入器**只保留单元格原有格式**；原格式也是 <c>General</c> 时才落 NPOI 默认。
    /// ⛔ 不要在这里做「按值猜格式」——那是层 2 的职责。</para>
    /// </summary>
    public string? NumberFormat { get; set; }

    /// <summary>Word 域的指令（如 <c>PAGE</c> / <c>NUMPAGES</c>）。仅 <see cref="FillValueKind.Field"/> 使用。</summary>
    public string? FieldInstruction { get; set; }

    /// <summary>
    /// 层 2 给出的可信度（0~1）。<b>层 1 不读它</b>，仅随报告回传供界面展示与审计。
    /// </summary>
    public double Confidence { get; set; } = 1.0;

    /// <summary>值来源说明（如「企业基础信息 · 企业全称」），仅用于审计与「一键看证据摘要」。</summary>
    public string? Source { get; set; }

    /// <summary>写入报告里展示的值（按类型格式化，截断到 120 字符）。</summary>
    public string ToDisplayText() => Kind switch
    {
        FillValueKind.Number => Number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        FillValueKind.Date => Date?.ToString("yyyy-MM-dd") ?? string.Empty,
        FillValueKind.Bool => Bool == true ? "是" : "否",
        FillValueKind.Field => Text ?? string.Empty,
        _ => Text ?? string.Empty,
    };
}

/// <summary>一次填充请求（= 一份模板 + 一本值字典）</summary>
public sealed class OfficeFillRequest
{
    /// <summary>
    /// 模板文件字节 —— <b>只接受 OOXML</b>（<c>.docx</c> / <c>.xlsx</c>）。
    ///
    /// <para>⚠️ <b>更正一条过期注释（2026-10-03）</b>：原文写「⛔ 不接受 <c>.doc</c> / <c>.xls</c> ——
    /// 上传侧已拒绝（21 号 Q-3）」，**与事实相反** —— 上传白名单 <c>allowedExts</c> <b>含</b>
    /// <c>doc</c>/<c>xls</c>，库内实测 <c>.doc</c> 570 份 / <c>.xls</c> 44 份（占 92%），
    /// 「上传侧拒绝」是**裁定存在但从未落地**。真正的原因在<b>库能力</b>：
    /// NPOI 2.7.2 <b>没有 <c>NPOI.HWPF</c></b>，<c>.doc</c> <b>连读都读不了</b>（不是「只读」）。</para>
    ///
    /// <para><b>⇒ 调用方必须先用归一产物</b>：取
    /// <c>PathBuilder.Product(file.StoragePath, PathBuilder.EditableSegment, ".docx")</c>
    /// 指向的 <c>editable/</c> 文件字节（`S-1` 归一链产出），
    /// ⛔ <b>不要把源文件字节直接喂进来</b> —— 会抛异常或静默产出错内容。</para>
    /// </summary>
    public byte[] Template { get; set; } = Array.Empty<byte>();

    /// <summary>值字典：锚点键 → 值。<b>Key 用 <see cref="StringComparer.Ordinal"/></b>。</summary>
    public IDictionary<string, FillValue> Values { get; set; } =
        new Dictionary<string, FillValue>(StringComparer.Ordinal);

    /// <summary>
    /// 未在值字典中命中的锚点如何处置。
    ///
    /// <para><b>★ 默认 <c>false</c> = 置空</b>（2026-10-02 用户规格：「针对填写或替换，
    /// <b>如果没有值则自动将填写内容赋值为空</b>」）。理由：模板里的 <c>{{xxx}}</c> 是**占位符**，
    /// 不是给人看的正文；留着它会让成品文件出现一串花括号，比空着更糟。</para>
    ///
    /// <para><c>true</c> = 保留原文（<c>{{company_name}}</c> 原样留在文件里）——
    /// 仅用于「模板调试」场景，便于人工看出哪一处没被填。</para>
    ///
    /// <para>⚠️ <b>两种都会记入 <see cref="OfficeFillReport.Pendings"/></b>，⛔ 不存在「静默丢弃」——
    /// 「空着」与「不知道空着」是两回事，后者是 26 号 §3.4.6 反复强调要消灭的缺陷。</para>
    /// </summary>
    public bool KeepUnresolvedAsIs { get; set; } = false;

    /// <summary>
    /// 是否处理**已有的**页眉（Word）。
    ///
    /// <para><b>★ 只做替换，⛔ 不新建</b>（2026-10-02 用户规格：「模板的页面（页眉）一般会定义一些
    /// 特定的信息，<b>我们需要的是进行替换</b>」）。页眉的字体/边框/图片/排版全部由模板作者设计，
    /// 程序只把其中的 <c>{{...}}</c> 换成值。</para>
    /// </summary>
    public bool FillHeader { get; set; } = true;

    /// <summary>
    /// 区域填充 —— <b>「表格填充」的抽象形态</b>（2026-10-02 用户规格）。
    ///
    /// <para>背景：参考实现（房产测绘 <c>YZH.Survey.Api</c> 的 <c>BaseReport</c> 系列）用
    /// <c>table.CreateCellParagraph(i, 0, value, ...)</c> <b>硬编码坐标</b>填充，
    /// 每换一张模板就要重写一遍 ⇒ 这正是用户说的「<b>用固定 skill 是比较困难的</b>」。</para>
    ///
    /// <para>本抽象把「坐标」与「数据」分离：坐标由层 2 给出（可从规则/配置/AI 来），
    /// 层 1 只负责按坐标落笔 ⇒ 同一套写入器可服务任意模板。</para>
    /// </summary>
    public List<OfficeFillRegion> Regions { get; set; } = new();

    /// <summary>自验收用的标记字符样式名（默认 <c>YZH_Mark</c>，见 16 号 V2 §8）</summary>
    public string MarkStyleId { get; set; } = "YZH_Mark";
}

/// <summary>区域填充的目标类型</summary>
public enum OfficeRegionKind
{
    /// <summary>Excel：从 <c>(StartRow, StartCol)</c> 起，逐行逐列写（0-based）</summary>
    ExcelRange = 0,

    /// <summary>Word：按 <c>{{table:Tag}}</c> 定位表格，<b>从该标记所在行起</b>逐行写</summary>
    WordTable = 1,
}

/// <summary>
/// 一次区域填充 —— <b>「表格填充」从硬编码坐标到数据驱动的抽象</b>。
///
/// <para><b>★ 为什么这样抽象</b>（对应用户 2026-10-02 规格）：</para>
/// <list type="bullet">
///   <item><b>Excel</b>：「从哪一行、哪一列开始，采用 json 结构进行填充」⇒
///         <see cref="StartRow"/> + <see cref="StartCol"/> + <see cref="Rows"/>（二维）。</item>
///   <item><b>Word</b>：「选择表格标签，采用 json 结构进行填充」⇒
///         <see cref="TableTag"/> + <see cref="Rows"/>（二维）。</item>
/// </list>
///
/// <para><b>★ 行数不足怎么办</b>：模板里数据区行数 &lt; <see cref="Rows"/> 时，
/// <b>克隆最后一行</b>（Word 深拷贝 <c>CT_Row</c> / Excel 复制行样式）补齐 ——
/// 因为「模板给了几行」只是排版示意，数据行数由业务决定。</para>
///
/// <para><b>★ 行数多余怎么办</b>：数据行数 &lt; 模板行数时，<b>多余行留空（写空串），⛔ 不删除</b> ——
/// 删除会连带把模板的合并单元格/边框结构破坏掉，而「样式与合并由模板控制」是本轮定死的边界。</para>
///
/// <para>⚠️ <see cref="Rows"/> 里的 <c>null</c> 元素 = 该格**写空**（与「不传」等价）。</para>
/// </summary>
public sealed class OfficeFillRegion
{
    /// <summary>目标类型</summary>
    public OfficeRegionKind Kind { get; set; }

    /// <summary>工作表名（<see cref="OfficeRegionKind.ExcelRange"/>）。⛔ null / 空 = 第一个工作表。</summary>
    public string? SheetName { get; set; }

    /// <summary>起始行（0-based，含）。</summary>
    public int StartRow { get; set; }

    /// <summary>起始列（0-based，含）。</summary>
    public int StartCol { get; set; }

    /// <summary>表格标签（<see cref="OfficeRegionKind.WordTable"/>）—— 对应模板里的 <c>{{table:Tag}}</c>。</summary>
    public string? TableTag { get; set; }

    /// <summary>数据：行 × 列。每格一个 <see cref="FillValue"/>；<c>null</c> = 写空。</summary>
    public List<List<FillValue?>> Rows { get; set; } = new();

    /// <summary>区域可读描述（写入报告，如 <c>Sheet1!A3</c> / <c>表格[table:items]</c>）</summary>
    public string Describe() => Kind switch
    {
        OfficeRegionKind.ExcelRange =>
            $"{SheetName ?? "(第 1 个工作表)"}!{CellRef(StartRow, StartCol)}",
        OfficeRegionKind.WordTable => $"表格[{{{{table:{TableTag}}}}}]",
        _ => Kind.ToString(),
    };

    /// <summary>0-based 行列 → A1 引用（如 <c>(2,1)</c> → <c>B3</c>）</summary>
    internal static string CellRef(int row0, int col0)
    {
        var sb = new System.Text.StringBuilder();
        var c = col0;
        do
        {
            sb.Insert(0, (char)('A' + c % 26));
            c = c / 26 - 1;
        } while (c >= 0);

        return sb.Append(row0 + 1).ToString();
    }
}

/// <summary>命中的位置类别（用于报告可读性与问题定位）</summary>
public enum FillLocationKind
{
    BodyParagraph = 0,
    TableCell = 1,

    /// <summary>页眉（★ 只做替换，⛔ 不新建 —— 见 <see cref="OfficeFillRequest.FillHeader"/>）</summary>
    Header = 2,

    /// <summary>⚠️ 已弃用：页脚不做（2026-10-02 用户规格：页脚属 Office 模板设计能力，不在本系统范围）。</summary>
    Footer = 3,

    ExcelCell = 4,

    /// <summary>区域填充 · Excel（起始行列 + 二维数据）</summary>
    ExcelRegion = 5,

    /// <summary>区域填充 · Word 表格（表格标签 + 二维数据）</summary>
    WordTableRegion = 6,
}

/// <summary>一次成功写入的记录</summary>
public sealed class OfficeFillHit
{
    /// <summary>锚点原文，如 <c>{{company_name}}</c></summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>锚点键，如 <c>company_name</c> / <c>enterprise.Name</c></summary>
    public string AnchorCode { get; set; } = string.Empty;

    /// <summary>写入位置类别</summary>
    public FillLocationKind LocationKind { get; set; }

    /// <summary>写入位置的可读描述，如 <c>正文·第 12 段</c> / <c>表格[1] 行 2 列 3</c> / <c>页眉[默认]</c> / <c>Sheet1!B7</c></summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>写入的值（已按类型格式化，截断 120 字符）</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>★ 锚点是否**跨 run**（= 触发 W9 归一化路径）。用于验证模板质量与回归。</summary>
    public bool CrossRun { get; set; }

    /// <summary>值来源说明（透传自 <see cref="FillValue.Source"/>）</summary>
    public string? Source { get; set; }
}

/// <summary>一处未写入的锚点（= 待办）</summary>
public sealed class OfficeFillPending
{
    public string Token { get; set; } = string.Empty;
    public string AnchorCode { get; set; } = string.Empty;
    public FillLocationKind LocationKind { get; set; }
    public string Location { get; set; } = string.Empty;

    /// <summary>未写入原因：<c>未提供值</c> / <c>跨域锚点</c> / <c>锚点未独占段落（域写入要求）</c></summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 一次**区域填充**的执行记录。
///
/// <para>⚠️ 刻意与 <see cref="OfficeFillHit"/> 分开、且**不计入 <see cref="OfficeFillReport.Total"/>**：
/// 锚点填充是「把模板里的占位符换掉」（完成度可统计），区域填充是「往数据区写 N 行」
/// （完成度无意义 —— 数据有几行就是几行）。混在一起会让完成度失真。</para>
/// </summary>
public sealed class OfficeFillRegionHit
{
    /// <summary>区域描述，如 <c>Sheet1!A3</c> / <c>表格[{{table:items}}]</c></summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>是否找到填充目标。Word 表格标签找不到时记 <c>false</c>（模板写错标签）。</summary>
    public bool Matched { get; set; }

    /// <summary>实际写入的数据行数</summary>
    public int RowCount { get; set; }

    /// <summary>因「数据行数 &gt; 模板行数」而**克隆**出来的行数</summary>
    public int ClonedRows { get; set; }

    /// <summary>未匹配时的说明（如「模板里没有 <c>{{table:items}}</c> 标签」）</summary>
    public string? Message { get; set; }
}

/// <summary>
/// 文件级填充报告 —— <b>「一键看证据摘要」在文件层的落点</b>。
///
/// <para>⚠️ 与 <see cref="CertPlatform.Shared.Fill.FillReport"/> 的区别：
/// 那个是**文本引擎**（跑在 markdown 上）的报告，这个是**文件写入**（NPOI）的报告。
/// 两者统计口径一致（<c>Completion = Resolved / Total</c>，<c>Total = 0</c> 时记 0），
/// ⛔ 不要在这里改成「无锚点记 1.0」—— 会与文本引擎、与企业端 <c>liveCompletion</c> 三处不一致。</para>
/// </summary>
public sealed class OfficeFillReport
{
    /// <summary>锚点总数</summary>
    public int Total => Hits.Count + Pendings.Count;

    /// <summary>已写入数</summary>
    public int Resolved => Hits.Count;

    /// <summary>待办数</summary>
    public int Pending => Pendings.Count;

    /// <summary>完成度（0~1）。无锚点时记 <b>0</b>（= 无可统计项，由界面显示空态）。</summary>
    public double Completion => Total == 0 ? 0.0 : (double)Resolved / Total;

    /// <summary>跨 run 命中的锚点数（W9 回归指标）</summary>
    public int CrossRunCount => Hits.Count(h => h.CrossRun);

    /// <summary>
    /// ★ 自验收①：填充后**仍残留**的 <c>{{...}}</c> 锚点原文（去重、保序）。
    /// <para>与 <see cref="Pendings"/> 互为冗余校验：Pendings 是「我知道没填」，
    /// LeftoverTokens 是「我扫出来的没填」——两者不一致就说明遍历有漏（如漏了某个表格/页眉）。</para>
    /// </summary>
    public List<string> LeftoverTokens { get; set; } = new();

    /// <summary>
    /// ★ 自验收②：填充后仍带 <paramref name="markStyleId"/> 字符样式的 run 数。
    /// <para>仅当模板真的定义了该样式时才有意义（未定义时恒 0，属正常）。</para>
    /// </summary>
    public int LeftoverMarkRuns { get; set; }

    /// <summary>自验收结论：无残留锚点且无残留标记</summary>
    public bool Verified => LeftoverTokens.Count == 0 && LeftoverMarkRuns == 0;

    public List<OfficeFillHit> Hits { get; set; } = new();
    public List<OfficeFillPending> Pendings { get; set; } = new();

    /// <summary>区域填充记录（★ 不计入 <see cref="Total"/>，见 <see cref="OfficeFillRegionHit"/>）</summary>
    public List<OfficeFillRegionHit> Regions { get; set; } = new();

    /// <summary>本报告覆盖的文件类型：<c>word</c> / <c>excel</c></summary>
    public string Kind { get; set; } = string.Empty;
}

/// <summary>文件级填充结果</summary>
public sealed class OfficeFillResult
{
    /// <summary>填充后的文件字节</summary>
    public byte[] Output { get; set; } = Array.Empty<byte>();

    /// <summary>填充报告</summary>
    public OfficeFillReport Report { get; set; } = new();
}
