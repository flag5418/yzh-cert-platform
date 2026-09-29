using System;
using System.Collections.Generic;
using System.Linq;
using CertPlatform.Shared.Entities.Dir;

namespace CertPlatform.Shared.Storage;

/// <summary>
/// MinIO 存储路径的**唯一构造器**（2026-09-26 起）。
///
/// <para><b>为什么必须收口到一处</b>：项目历史上路径格式至少改过 4 次，每次都新增一个生成方法
/// 而不删旧的，结果是 MinIO 里同时躺着 5 套格式 —— 实测 1182 个对象中只有 177 个还被数据库引用
/// （<c>standard-directory/{.converted, CB001CODE, ISO134852016, ISO90012015, iso40012016}/</c>）。
/// 根因不是"忘了删旧代码"，而是**没有唯一权威**：任何人任何时候都能再写一个拼路径的方法。
/// 本类就是那个唯一权威 —— <b>除本类外，任何地方不得手工拼接存储路径</b>。</para>
///
/// <para><b>三条构造规则</b>（《标准目录与企业资料-存储与编码规范-V4》§0）：</para>
/// <list type="number">
///   <item><b>身份段取实体的 <c>Code</c> 原文</b>（GUID 业务键），⛔ <b>不做 CleanCode</b>。
///         旧 <c>CleanCode</c> 会删掉 <c>-</c>，而 GUID 含 <c>-</c> ⇒ 路径段与数据库值
///         <b>不可逆地不一致</b>（<c>846dec4b-c534-…</c> 被写成 <c>846dec4bc534…</c>），
///         从此无法由路径反查实体。</item>
///   <item><b>身份段不得为空 —— 空则抛异常</b>。旧实现用
///         <c>segments.Where(s =&gt; !string.IsNullOrEmpty(s))</c> 静默丢弃空段，导致
///         <c>GenerateConvertedStoragePath("","","","",name)</c> 产出
///         <c>/standard-directory/.converted/{name}</c> —— 丢掉全部上下文，
///         不同文件夹的同名文件互相覆盖（2026-09-26 实测已发生 1 处数据丢失）。
///         本类宁可让上传失败，也不静默写错位置。</item>
///   <item><b>文件夹段与文件名段只去路径分隔符</b>，其余（空格 / 连字符 / 中文 / 括号）原样保留。</item>
/// </list>
///
/// <para><b>MinIO 语义提醒</b>：S3/MinIO 是扁平 key 存储，控制台按 <c>/</c> 分组**显示**成目录。
/// 因此"建库 / 建文件夹"**不需要任何 API 调用**，路径在代码里算好即可。</para>
/// </summary>
public static class PathBuilder
{
    #region 常量与保留段

    /// <summary>产物类型段：预览 PDF</summary>
    public const string PdfSegment = "pdf";

    /// <summary>产物类型段：提取 Markdown</summary>
    public const string MarkdownSegment = "markdown";

    /// <summary>归档段（仅企业资料库使用；标准目录库为单纯覆盖，无归档）</summary>
    public const string ArchiveSegment = "_archive";

    /// <summary>版本后缀前缀：<c>{文件名}.v{版本号}</c>，与历史表 <c>VersionNumber</c> 对齐</summary>
    public const string VersionPrefix = ".v";

    /// <summary>
    /// 保留段名 —— <b>业务文件夹名与文件名不得使用</b>。
    /// 否则会与产物目录 / 归档目录撞车（例如业务文件夹叫 <c>pdf</c>，会被
    /// <see cref="IsProductPath"/> 误判为产物路径）。
    /// </summary>
    public static readonly IReadOnlyList<string> ReservedSegments = new[]
    {
        PdfSegment, MarkdownSegment, ArchiveSegment
    };

    private static readonly HashSet<string> ReservedSegmentSet =
        new(ReservedSegments, StringComparer.OrdinalIgnoreCase);

    #endregion

    #region 标准目录库（仅覆盖，无版本）

    /// <summary>
    /// 标准目录库存储路径。
    /// <para>格式：<c>/standard-directory/{OrgCode}/{StandardCode}/{StageCode}/{FolderPath}/{FileName}</c></para>
    ///
    /// <para>★ <b>含机构段</b>（决策⑳修订，2026-09-27 用户改判）：标准目录是<b>各机构的标准落地
    /// 目录模板</b> —— 主表按机构隔离（uk = OrgCode,StandardCode,StageCode），不同机构对同一
    /// 标准 × 阶段的目录结构与文件允许不同。路径<b>必须</b>同步含 <c>{OrgCode}</c>：两机构各自合法的
    /// 同名文件（FullPath 按 config 分域）若无机构段会算出<b>相同物理 key</b>，标准库又是「纯覆盖
    /// 无归档」语义 ⇒ 互相覆盖丢数据。原「平台全局库、无机构段」（2026-09-26 决策⑳）作废。</para>
    /// <para>需按机构前缀枚举对象时走 DB：<c>WHERE OrgCode=?</c> 取机构下目录配置再拼前缀
    /// （本项目无任何按前缀列举 MinIO 的代码路径）。</para>
    /// </summary>
    /// <param name="orgCode">机构 <c>certification_body.Code</c></param>
    /// <param name="standardCode">标准 <c>cert_iso_standard.Code</c>（GUID）</param>
    /// <param name="stageCode">阶段 <c>cert_cert_stage.Code</c>（GUID）</param>
    /// <param name="folderPath">相对配置根的文件夹路径，<c>/</c> 分隔；可为空（文件直接位于根）</param>
    /// <param name="fileName">原始文件名（含扩展名）</param>
    public static string StandardFile(
        string? orgCode, string? standardCode, string? stageCode,
        string? folderPath, string? fileName)
        => Join(
            DocumentLibraryPath.StandardDirectoryPrefix,
            Identity(orgCode, "机构编码 OrgCode"),
            Identity(standardCode, "标准编码 StandardCode"),
            Identity(stageCode, "阶段编码 StageCode"),
            Folder(folderPath),
            FileName(fileName));

    #endregion

    #region 企业资料库（覆盖 + 归档 + 版本）

    /// <summary>
    /// 企业资料库存储路径。
    /// <para>格式：<c>/enterprise-documents/{EnterpriseCode}/{StandardCode}/{StageCode}/{FolderPath}/{FileName}</c></para>
    /// <para>★ <b>首段 = 企业 Code</b>（2026-09-27 用户裁定，与标准目录对称）：两库统一规则
    /// 「<b>首段 = 资料归属主体的 Code</b>」—— 标准目录归属主体是机构，企业资料归属主体是企业。
    /// <c>cert_enterprise</c> 与机构严格 1:1（<c>OrgCode</c> 单列、uk 含 OrgCode）⇒ EnterpriseCode
    /// 已隐含 OrgCode，无需冗余机构段；且该企业全部材料聚在同一前缀下，整企业导出 / 清理 / 审计
    /// 是单前缀操作（原结构 <c>{Org}/{Std}/{Stage}/{Enterprise}</c> 把企业段夹在第 4 层，
    /// 一个企业的材料散落在每个标准 × 阶段组合下，无法单前缀取全，作废）。
    /// 按机构枚举企业对象时走 DB：<c>WHERE OrgCode=?</c> 取企业列表再拼前缀。</para>
    /// </summary>
    /// <param name="enterpriseCode">企业 <c>cert_enterprise.Code</c>（GUID）</param>
    /// <inheritdoc cref="StandardFile"/>
    public static string EnterpriseFile(
        string? enterpriseCode, string? standardCode, string? stageCode,
        string? folderPath, string? fileName)
        => Join(
            DocumentLibraryPath.EnterpriseDocumentsPrefix,
            Identity(enterpriseCode, "企业编码 EnterpriseCode"),
            Identity(standardCode, "标准编码 StandardCode"),
            Identity(stageCode, "阶段编码 StageCode"),
            Folder(folderPath),
            FileName(fileName));

    /// <summary>
    /// 归档路径：在**文件名前**插入 <c>_archive</c> 段，并给文件名追加 <c>.v{版本号}</c>。
    ///
    /// <para>对源文件与两种产物**同一套算法**（都是"父段 + <c>_archive</c> + 原名 + 后缀"）：</para>
    /// <code>
    /// 源：   …/{Ent}/4记录文件/风险管理报告.doc
    ///      → …/{Ent}/4记录文件/_archive/风险管理报告.doc.v3
    /// PDF：  …/{Ent}/4记录文件/pdf/风险管理报告.doc.pdf
    ///      → …/{Ent}/4记录文件/pdf/_archive/风险管理报告.doc.pdf.v3
    /// </code>
    ///
    /// <para><b>幂等</b>：归档目标带 <c>.v{n}</c>，重试不会互相覆盖；
    /// 但<b>已是归档路径的入参直接抛异常</b> —— 二次归档意味着调用方状态机出错，必须响。</para>
    /// </summary>
    /// <param name="storagePath">当前（未归档）的存储路径</param>
    /// <param name="versionNumber">被归档内容的版本号，从 1 开始</param>
    public static string Archive(string? storagePath, int versionNumber)
    {
        if (versionNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(versionNumber), versionNumber, "版本号从 1 开始");

        var segs = Segments(storagePath);
        if (segs.Length < 2)
            throw new ArgumentException(
                $"无法归档：路径至少需要「库前缀 + 文件名」两段，实际为「{storagePath}」", nameof(storagePath));

        if (segs.Any(s => s.Equals(ArchiveSegment, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"路径已是归档路径，禁止二次归档：{storagePath}");

        var name = Sanitize(segs[^1]);
        var parent = string.Join("/", segs[..^1]);
        return $"/{parent}/{ArchiveSegment}/{name}{VersionPrefix}{versionNumber}";
    }

    #endregion

    #region 产物路径（预览 PDF / 提取 Markdown）

    /// <summary>
    /// 从**源文件存储路径**派生产物路径（预览 PDF / 提取 Markdown）。
    ///
    /// <para>规则：把源文件名替换为「完整原文件名 + 产物扩展名」，并插入产物类型段。</para>
    /// <code>
    /// 源：  /standard-directory/{Std}/{Stage}/4记录文件/风险管理报告.doc
    /// PDF：/standard-directory/{Std}/{Stage}/4记录文件/pdf/风险管理报告.doc.pdf
    /// MD： /standard-directory/{Std}/{Stage}/4记录文件/markdown/风险管理报告.doc.md
    /// </code>
    ///
    /// <para><b>为什么从源路径派生，而不是重新拼装各编码段</b>：</para>
    /// <list type="number">
    ///   <item><b>修掉空参 bug</b>：旧 <c>GenerateConvertedStoragePath("","","","",fileName)</c>
    ///         把产物写成 <c>/standard-directory/.converted/{文件名}</c>，丢掉全部上下文
    ///         → 不同文件夹的同名文件互相覆盖（实测已发生）。派生法天然免疫。</item>
    ///   <item><b>修掉文件夹改名失配</b>：源路径随文件夹改名而变，产物跟着走；重新拼装则会与新路径脱节。</item>
    ///   <item><b>天然隔离租户 / 标准 / 阶段</b>：产物与源文件同层级，不会跨工作区串。</item>
    /// </list>
    ///
    /// <para><b>产物文件名为何保留完整原文件名</b>：同一文件夹内可能存在同 stem 不同扩展名的文件
    /// （如 <c>XASL-QR-014 年度内审计划.doc</c> 与 <c>.xls</c>，本项目实测存在这种命名习惯）。
    /// 若产物只取 stem，两者会互相覆盖。保留原扩展名可**由构造保证唯一**。</para>
    /// </summary>
    /// <param name="storagePath">源文件在 MinIO 的存储路径</param>
    /// <param name="productKind"><see cref="PdfSegment"/> 或 <see cref="MarkdownSegment"/></param>
    /// <param name="targetExt">目标扩展名（含点，如 <c>.pdf</c> / <c>.md</c>）</param>
    /// <returns>产物路径（以 <c>/</c> 开头）；<paramref name="storagePath"/> 为空或段数不足时返回空串</returns>
    /// <remarks>
    /// <b>返回空串而非抛异常是刻意保留的契约</b>：4 处调用方（<c>OfficeConvertService</c> ×2、
    /// <c>DocExtractionRuleService.AI</c> ×2）都以 <c>string.IsNullOrEmpty(targetPath)</c> 判定
    /// "源文件缺少存储路径"，并据此把链路标记为 failed。收紧成抛异常会把这 4 处变成未捕获异常。
    /// </remarks>
    public static string Product(string? storagePath, string productKind, string targetExt)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return "";

        if (productKind != PdfSegment && productKind != MarkdownSegment)
            throw new ArgumentException(
                $"产物类型段必须是「{PdfSegment}」或「{MarkdownSegment}」，实际为「{productKind}」", nameof(productKind));

        var segs = Segments(storagePath);
        if (segs.Length < 2) return "";

        var ext = string.IsNullOrEmpty(targetExt)
            ? ""
            : (targetExt.StartsWith('.') ? targetExt : "." + targetExt);

        var name = Sanitize(segs[^1]) + ext;
        var parent = string.Join("/", segs[..^1]);
        return $"/{parent}/{productKind}/{name}";
    }

    /// <summary>
    /// 判断给定路径是否为「产物路径」（位于 <c>pdf/</c> 或 <c>markdown/</c> 段下）。
    /// <para>用途：删除文件时避免把产物目录误当业务目录；以及排查历史脏数据。</para>
    /// </summary>
    public static bool IsProductPath(string? path)
        => Segments(path).Any(s => s.Equals(PdfSegment, StringComparison.OrdinalIgnoreCase)
                                || s.Equals(MarkdownSegment, StringComparison.OrdinalIgnoreCase));

    /// <summary>判断给定路径是否为「归档路径」（含 <c>_archive</c> 段）。</summary>
    public static bool IsArchivePath(string? path)
        => Segments(path).Any(s => s.Equals(ArchiveSegment, StringComparison.OrdinalIgnoreCase));

    #endregion

    #region 路径解析

    /// <summary>
    /// 拆段：反斜杠转正斜杠，去空白、去前导 <c>/</c>、去空段、去穿越片段（<c>.</c> / <c>..</c>）。
    /// <para>这是全项目唯一的路径拆段入口 —— 各处 <c>Split('/')</c> 的手写版本对空段的处理不一致，
    /// 是"同一路径在不同代码里算出不同段号"的根源。</para>
    /// </summary>
    public static string[] Segments(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return Array.Empty<string>();

        return path.Replace('\\', '/').Trim().Trim('/')
                   .Split('/', StringSplitOptions.RemoveEmptyEntries)
                   .Select(s => s.Trim())
                   .Where(s => s.Length > 0 && s != "." && s != "..")
                   .ToArray();
    }

    /// <summary>取第 <paramref name="index"/> 段（0-based）；越界返回 <c>null</c>。</summary>
    public static string? SegmentAt(string? path, int index)
    {
        if (index < 0) return null;
        var segs = Segments(path);
        return index < segs.Length ? segs[index] : null;
    }

    #endregion

    #region 私有：段清洗

    /// <summary>
    /// 身份段（机构 / 标准 / 阶段 / 企业 / 配置 / 文件夹行的 <c>Code</c>）：
    /// 只做「去路径分隔符 + Trim」的最小清洗，⛔ <b>不删 <c>-</c></b>（GUID 含 <c>-</c>）。
    /// 空值 / 穿越片段一律抛异常。
    /// </summary>
    private static string Identity(string? code, string what)
    {
        var v = Sanitize(code);
        if (v.Length == 0)
            throw new ArgumentException(
                $"存储路径段「{what}」为空。身份段不允许为空 —— 静默丢弃会产出「丢掉上下文」的错误路径，"
                + "导致不同文件夹的同名文件互相覆盖（2026-09-26 实测数据丢失根因）。", what);
        if (v is "." or "..")
            throw new ArgumentException($"存储路径段「{what}」非法：{v}", what);
        return v;
    }

    /// <summary>文件夹路径段：可为空（文件直接位于配置根）；逐段清洗并校验保留段名。</summary>
    private static string? Folder(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return null;

        var segs = Segments(folderPath);
        if (segs.Length == 0) return null;

        foreach (var s in segs)
            if (ReservedSegmentSet.Contains(s))
                throw new ArgumentException(
                    $"文件夹路径不得使用保留段名「{s}」（保留段：{string.Join(" / ", ReservedSegments)}）",
                    nameof(folderPath));

        return string.Join("/", segs);
    }

    /// <summary>文件名段：只去路径分隔符，保留空格 / 连字符 / 中文 / 括号；不得为空或为保留段名。</summary>
    private static string FileName(string? fileName)
    {
        var v = Sanitize(fileName);
        if (v.Length == 0)
            throw new ArgumentException("文件名不能为空", nameof(fileName));
        if (ReservedSegmentSet.Contains(v))
            throw new ArgumentException(
                $"文件名不得为保留段名「{v}」（保留段：{string.Join(" / ", ReservedSegments)}）", nameof(fileName));
        return v;
    }

    /// <summary>最小清洗：去 <c>/</c> 与 <c>\</c>，再 Trim。</summary>
    private static string Sanitize(string? s)
        => (s ?? "").Replace("/", "").Replace("\\", "").Trim();

    /// <summary>拼接为以 <c>/</c> 开头的路径；<c>null</c> / 空段由调用方保证已校验或本就可选。</summary>
    private static string Join(params string?[] segments)
    {
        var parts = segments
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!.Trim('/'));
        return "/" + string.Join("/", parts);
    }

    #endregion
}
