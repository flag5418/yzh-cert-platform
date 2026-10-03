namespace CertPlatform.Shared.Office;

/// <summary>
/// 一次「文档填写」的会话状态 —— 编排器持有，各 Skill 累积写入。
///
/// <para><b>★ 为什么需要它（对应 39 号 §3.6，M-1 的解法）</b>：原设计让每个填充 Skill 各收一个
/// <see cref="OfficeFillRequest"/> 再返回新的；但 <see cref="OfficeFillRequest"/> 是复杂对象，
/// 走「声明式配置（JSON 存库）→ 反射调用」这条路时会被
/// <c>SkillExecutor.ConvertValue</c> <b>原样返回成 JsonElement</b>
/// （它只转 <c>string/bool/int/long/double/decimal/DateTime</c>，其余类型不报错也不转换），
/// 永远还原不成强类型 ⇒ <b>技术上走不通</b>。</para>
///
/// <para><b>本类的作用</b>：把「累积状态」收进一个<b>由编排器直接持有的对象实例</b>，
/// 通过 <c>SkillContext.Inputs</c> 传给 Skill（<c>ConvertValue</c> 对复杂类型原样透传）⇒
/// 绕开 JSON 往返，同时天然实现「<b>一份文档只落盘一次</b>」——
/// 各 Skill 只往 <see cref="Request"/> 里累积，⛔ 谁也不落盘。</para>
///
/// <para><b>★ 与 <see cref="OfficeFillRequest"/> 的分工</b>：本类是<b>会话</b>（含模板字节、归属表、待办、轨迹），
/// <see cref="Request"/> 只是其中的<b>填充指令</b>（层 1 真正消费的东西）。
/// 层 1 仍只认 <see cref="OfficeFillRequest"/> ⇒ <b>层 1 零改动</b>。</para>
/// </summary>
public sealed class FillSession
{
    /// <summary>模板文件编码（<c>cert_standard_directory_file.Code</c>），用于留痕</summary>
    public string TemplateFileCode { get; set; } = string.Empty;

    /// <summary>模板字节（.docx / .xlsx）</summary>
    public byte[] Template { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// 文件类型：<c>word</c> / <c>excel</c>。
    /// <para>★ 决定 <c>fill_table</c> 的定位参数形状（excel ⇒ <c>SheetName</c>+<c>StartRow</c>+<c>StartCol</c>；
    /// word ⇒ <c>TableTag</c>）—— 对外统一签名，对内分派。</para>
    /// </summary>
    public string FileKind { get; set; } = string.Empty;

    /// <summary>累积的填充指令 —— 各 Skill 往这里写，⛔ 不各自落盘</summary>
    public OfficeFillRequest Request { get; set; } = new();

    /// <summary>
    /// ★ 已认领锚点 → 来源 Skill。
    ///
    /// <para>用途一：<b>重复赋值检测</b>（同一锚点被两个来源写 ⇒ 必须报错，⛔ 不静默覆盖）。
    /// 用途二：审计（「这一格的值是谁写的」）。</para>
    ///
    /// <para>⚠️ 用 <see cref="StringComparer.Ordinal"/>：锚点键与文档中 <c>{{ }}</c> 内的文本
    /// <b>逐字一致</b>，⛔ 不做大小写折叠（与 <see cref="OfficeFillRequest.Values"/> 同口径）。</para>
    /// </summary>
    public Dictionary<string, string> AnchorOwners { get; set; } = new(StringComparer.Ordinal);

    /// <summary>待办清单（<c>src_manual</c> 等产出）</summary>
    public List<FillTodo> Todos { get; set; } = new();

    /// <summary>调用轨迹（哪个 Skill 写了什么），写入 <c>cert_doc_fill_log.SkillTrace</c></summary>
    public List<string> Trace { get; set; } = new();

    /// <summary>
    /// ★ 认领一个锚点（写值前必须调用）。
    ///
    /// <para>返回 <c>true</c> = 认领成功，可以写值；
    /// 返回 <c>false</c> = <b>该锚点已被别的 Skill 认领</b> ⇒ 调用方必须
    /// <c>SkillResult.Fail</c>，⛔ <b>不得静默覆盖</b>（否则「两套来源都写、后写的赢」
    /// 会让填充结果依赖 Skill 执行顺序 —— 不可复现且极难查）。</para>
    ///
    /// <para>⚠️ 同一 Skill 重复认领<b>同一锚点</b>也算冲突（会返回 <c>false</c>）：
    /// 一个 Skill 在一次执行里对同一锚点写两次本身就是逻辑错误。</para>
    /// </summary>
    /// <param name="anchorCode">锚点键</param>
    /// <param name="owner">来源 Skill 编码（如 <c>src_global_param</c>）</param>
    /// <param name="conflictOwner">冲突时的已有来源（无冲突为 null）</param>
    public bool TryClaim(string anchorCode, string owner, out string? conflictOwner)
    {
        conflictOwner = null;

        if (string.IsNullOrWhiteSpace(anchorCode))
            return false;

        if (AnchorOwners.TryGetValue(anchorCode, out var existing))
        {
            conflictOwner = existing;
            return false;
        }

        AnchorOwners[anchorCode] = owner;
        return true;
    }

    /// <summary>记一条调用轨迹（写进 <c>cert_doc_fill_log.SkillTrace</c>）</summary>
    public void Log(string skillCode, string message) =>
        Trace.Add($"[{skillCode}] {message}");
}

/// <summary>
/// 一处待人工填写 —— <c>src_manual</c> 的产出，⛔ <b>不产值</b>。
///
/// <para>让来源链的 <c>onMissing: todo</c> 有落点（38 号 §5.2）：全部来源都落空时，
/// 编排器把 <see cref="FillSession.Todos"/> 转成
/// <see cref="OfficeFillReport.Pendings"/>，从而在「一键看证据摘要」里显示
/// 「这一格需要人工填，提示是 XXX」。</para>
/// </summary>
public sealed class FillTodo
{
    /// <summary>锚点键</summary>
    public string AnchorCode { get; set; } = string.Empty;

    /// <summary>给填写人的提示（如「请填写企业名称」）</summary>
    public string Hint { get; set; } = string.Empty;

    /// <summary>产出它的 Skill（默认 <c>src_manual</c>）</summary>
    public string Source { get; set; } = "src_manual";
}
