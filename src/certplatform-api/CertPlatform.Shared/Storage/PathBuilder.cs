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

    /// <summary>
    /// ★ 产物类型段：可编辑版本（2026-10-03 新增）——
    /// <c>.doc → .docx</c> / <c>.xls → .xlsx</c> / <c>.ppt → .pptx</c> 的 LibreOffice 归一产物。
    ///
    /// <para><b>为什么必须单独占一个段</b>：填写引擎（NPOI）<b>只能读 OOXML</b> ——
    /// NPOI 2.7.2 <b>没有 <c>NPOI.HWPF</c></b>，<c>.doc</c> 连读都读不了。
    /// 而本库实测 668 份文件中 <c>.doc</c> 567 份 / <c>.xls</c> 44 份（<b>91.5%</b>），
    /// 不归一就等于「92% 的模板填不了」。</para>
    ///
    /// <para><b>为什么不与源文件同目录（兄弟路径）</b>：同目录会出现
    /// <c>风险管理报告.doc</c>（源）与 <c>风险管理报告.docx</c>（产物）并存的情况，
    /// 而同 stem 不同扩展名的文件在本项目实测<b>真实存在</b>（如
    /// <c>XASL-QR-014 年度内审计划.doc</c> 与 <c>.xls</c>）⇒ 兄弟路径会与既有业务文件<b>撞名</b>。
    /// 独立段由构造保证唯一。</para>
    ///
    /// <para><b>为什么产物文件名保留完整原文件名</b>：与 <see cref="PdfSegment"/> /
    /// <see cref="MarkdownSegment"/> 同一理由（见 <see cref="Product"/>），
    /// 形如 <c>…/editable/风险管理报告.doc.docx</c>。看起来冗余，但一致性优先 ——
    /// 三段产物用同一套命名规则，排查时不必记三套。</para>
    /// </summary>
    public const string EditableSegment = "editable";

    /// <summary>
    /// ★ 目录段：空白模板（2026-10-03 新增，`37` 号 §4.3）——
    /// 标准目录库下、与源文件**同层级**的 <c>_template/</c> 子目录，
    /// 存放「带 <c>{{}}</c> 标签 + 书签 + <c>YZH_Mark</c> 标记」的空白模板。
    ///
    /// <para><b>为什么与源文件同层级，而不是独立库</b>：模板是**源文档的派生物**
    /// （同机构 × 标准 × 阶段 × 文件夹），与 <c>pdf/</c> / <c>markdown/</c> / <c>editable/</c>
    /// 同属「挂在源文件旁」的目录。独立库会引入第二套身份段与第二套权限，
    /// 且「删标准文档时忘记删模板」会变成常态。</para>
    ///
    /// <para><b>⚠️ 与产物段的本质区别（决定了它不进 <see cref="IsProductPath"/>）</b>：
    /// 产物段由 <see cref="Product"/> 从源路径**派生**（文件名 = 完整原名 + 新扩展名）；
    /// 而 <c>_template/</c> 由 <see cref="TemplateFile"/> **构造**（同 <see cref="StandardFile"/>
    /// 的段结构再插一段，文件名 = 上传时的原始名）。
    /// 两者生命周期也不同：产物可随时重算，模板是**人工标注的资产**，丢了要重标。</para>
    ///
    /// <para><b>下划线前缀</b>：与 <see cref="ArchiveSegment"/> 一致，表示「系统目录、非业务文件夹」，
    /// 人工排查时一眼可辨。</para>
    /// </summary>
    public const string TemplateSegment = "_template";

    /// <summary>
    /// ★ 目录段：**试填预览**（2026-10-06 新增，`52` §12.4 裁定 A）——
    /// 标准目录库下、与 <see cref="TemplateSegment"/> **同级**的 <c>_preview/</c> 子目录，
    /// 存放「把空白模板试填一遍后转出来的 PDF」。
    ///
    /// <para><b>格式</b>：<c>…/{Folder}/_preview/{模板名}.docx.pdf</c>（<b>固定 key，重复试填覆盖</b>）。</para>
    ///
    /// <para><b>★ 为什么必须独立于 <see cref="PdfSegment"/></b>：<c>pdf/</c> 段已被
    /// 「<b>源文件</b>的预览 PDF」占用（实测 185 行在用）。试填产物来自<b>空白模板</b>
    /// （不是源文件），复用同一段会让两者算出<b>同一个 key ⇒ 互相覆盖</b>。</para>
    ///
    /// <para><b>★ 为什么固定 key、不带时间戳</b>：试填是「看看填出来长什么样」，不是正式产物
    /// ⇒ 不需要历史版本。固定 key 让空间占用<b>恒定</b>（每模板最多 1 个 PDF），
    /// 贴合用户「节约后台空间」的口径。⚠️ 副作用：并发试填同一模板会互相覆盖 ——
    /// 试填是单人操作，可接受。</para>
    ///
    /// <para><b>⚠️ 与 <see cref="IsProductPath"/> 并列而非包含</b>：<c>IsProductPath</c> 的 3 个段
    /// <b>恰好等于 <see cref="Product"/> 接受的 3 个 <c>productKind</c></b>（收到第 4 种会抛异常）
    /// —— 这是一条不变量。把 <c>_preview</c> 塞进去会让「判定通过」与「能否派生」不再等价。</para>
    /// </summary>
    public const string PreviewSegment = "_preview";

    /// <summary>归档段（企业资料库 + 企业原始资料库使用；标准目录库为单纯覆盖，无归档）</summary>
    public const string ArchiveSegment = "_archive";

    /// <summary>版本后缀前缀：<c>{文件名}.v{版本号}</c>，与历史表 <c>VersionNumber</c> 对齐</summary>
    public const string VersionPrefix = ".v";

    /// <summary>
    /// 保留段名 —— <b>业务文件夹名与文件名不得使用</b>。
    /// 否则会与产物目录 / 系统目录撞车（例如业务文件夹叫 <c>pdf</c>，会被
    /// <see cref="IsProductPath"/> 误判为产物路径）。
    ///
    /// <para>★ 2026-10-06 起共 <b>6 个</b>（新增 <see cref="PreviewSegment"/>）。
    /// ⚠️ <b>前端 <c>cert-share/src/composables/useFileTree.ts</c> 的 <c>RESERVED_SEGMENTS</c>
    /// 必须与本列表逐字一致</b> —— 漏一个，MinIO 里那个目录就会在文件树里
    /// 显示成「幽灵业务文件夹」。</para>
    /// </summary>
    public static readonly IReadOnlyList<string> ReservedSegments = new[]
    {
        PdfSegment, MarkdownSegment, EditableSegment, TemplateSegment, PreviewSegment, ArchiveSegment
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

    /// <summary>
    /// 标准目录库 · **空白模板**路径（`37` 号 §4.3）。
    /// <para>格式：<c>/standard-directory/{OrgCode}/{StandardCode}/{StageCode}/{FolderPath}/_template/{FileName}</c></para>
    ///
    /// <para><b>实现 = 复用 <see cref="StandardFile"/> 再插一段</b>（⛔ 不重写一遍拼接逻辑 ——
    /// 重写就会出现两套格式，而本类存在的唯一理由就是「只有一处权威」）。
    /// 身份段规则（取 <c>Code</c> 原文、空则抛异常、保留段校验）因此**自动继承**。</para>
    ///
    /// <para><b>为什么插在文件名前</b>：源文件 <c>…/4记录文件/风险管理报告.doc</c> 与模板
    /// <c>…/4记录文件/_template/风险管理报告.docx</c> 同层级并列 ⇒ 整文件夹搬迁/删除时两者一起走；
    /// 且模板名与源名一致（仅扩展名可能不同），人工对照直观。</para>
    ///
    /// <para>⚠️ <b>换版归档</b>走 <see cref="Archive"/> ⇒
    /// <c>…/_template/_archive/风险管理报告.docx.v1</c>，与源文件归档语义一致（同算法）。</para>
    /// </summary>
    /// <param name="orgCode">机构 <c>certification_body.Code</c></param>
    /// <param name="standardCode">标准 <c>cert_iso_standard.Code</c>（GUID）</param>
    /// <param name="stageCode">阶段 <c>cert_cert_stage.Code</c>（GUID）</param>
    /// <param name="folderPath">相对配置根的文件夹路径，<c>/</c> 分隔；可为空（文件直接位于根）</param>
    /// <param name="fileName">模板文件名（含扩展名，通常 <c>.docx</c> / <c>.xlsx</c>）</param>
    public static string TemplateFile(
        string? orgCode, string? standardCode, string? stageCode,
        string? folderPath, string? fileName)
    {
        // StandardFile 已保证：以 / 开头、身份段非空、至少 5 段（库前缀 + 3 身份段 + 文件名）
        var src = StandardFile(orgCode, standardCode, stageCode, folderPath, fileName);
        var lastSlash = src.LastIndexOf('/');
        return src[..lastSlash] + "/" + TemplateSegment + src[lastSlash..];
    }

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
    /// <para>使用方：企业资料库（<c>enterprise-documents</c>）+ 企业原始资料库
    /// （<c>enterprise-original-source</c>，36 号 D8）。
    /// ⚠️ 标准目录库的**源文件**为单纯覆盖，不调用本方法；
    /// 但 <see cref="TemplateSegment"/> 下的**空白模板换版时走本方法**
    /// （`37` 号 §4.3：<c>…/_template/_archive/风险管理报告.docx.v1</c>）——
    /// 模板是人工标注的资产，误覆盖不可挽回。</para>
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

    #region 企业原始资料库（企业散乱原始资料，输入库 + 版本管理）

    /// <summary>
    /// 企业原始资料库存储路径。
    /// <para>格式：<c>/enterprise-original-source/{EnterpriseCode}/{StageCode}/{FolderPath}/{FileName}</c></para>
    ///
    /// <para>★ <b>为什么少一段（无 StandardCode）</b>：本库是<b>输入</b>库 —— 企业把散乱资料传上来时
    /// 还没对标准，标准是后续规范化阶段才确定的。而 <see cref="EnterpriseFile"/> 是<b>输出</b>库，
    /// 按「阶段 × 标准 × 槽位」组织。两者生命周期不同，混库会导致「重新规范化时分不清哪些是原始、
    /// 哪些是产物」。</para>
    ///
    /// <para>★ <b>版本管理（D8）</b>：本库沿用企业资料库的**同文件夹归档**语义 ——
    /// 替换文件时先 <c>Rename(旧 → <see cref="Archive"/>)</c> 再向<b>同一路径</b>写新字节，
    /// 于是 <c>StoragePath</c> 恒定、外部引用（预览 / 下载 / 语义分析输入）永不失效。
    /// 归档路径形如 <c>…/{Ent}/{Stage}/4记录文件/_archive/风险管理报告.doc.v3</c>。
    /// ⛔ 归档算法只有 <see cref="Archive"/> 一个，禁止另写。</para>
    ///
    /// <para>⚠️ <b>历史版本在 MinIO 侧不可枚举</b>：本项目无「按前缀列举 MinIO 对象」的代码路径，
    /// 历史版本清单一律走 DB（<c>cert_enterprise_original_file_version</c> 表）。</para>
    /// </summary>
    /// <param name="enterpriseCode">企业 <c>cert_enterprise.Code</c>（GUID 业务键原文）</param>
    /// <param name="stageCode">阶段 <c>cert_cert_stage.Code</c>（GUID 业务键原文）</param>
    /// <param name="folderPath">相对 <c>{Ent}/{Stage}/</c> 的文件夹路径，<c>/</c> 分隔；可为空（文件直接位于该阶段根）</param>
    /// <param name="fileName">原始文件名（含扩展名）</param>
    public static string EnterpriseOriginalSource(
        string? enterpriseCode, string? stageCode, string? folderPath, string? fileName)
        => Join(
            DocumentLibraryPath.EnterpriseOriginalSourcePrefix,
            Identity(enterpriseCode, "企业编码 EnterpriseCode"),
            Identity(stageCode,     "阶段编码 StageCode"),
            Folder(folderPath),
            FileName(fileName));

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
    /// ED： /standard-directory/{Std}/{Stage}/4记录文件/editable/风险管理报告.doc.docx
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
    /// <param name="productKind"><see cref="PdfSegment"/> / <see cref="MarkdownSegment"/> / <see cref="EditableSegment"/></param>
    /// <param name="targetExt">目标扩展名（含点，如 <c>.pdf</c> / <c>.md</c> / <c>.docx</c>）</param>
    /// <returns>产物路径（以 <c>/</c> 开头）；<paramref name="storagePath"/> 为空或段数不足时返回空串</returns>
    /// <remarks>
    /// <b>返回空串而非抛异常是刻意保留的契约</b>：4 处调用方（<c>OfficeConvertService</c> ×2、
    /// <c>DocExtractionRuleService.AI</c> ×2）都以 <c>string.IsNullOrEmpty(targetPath)</c> 判定
    /// "源文件缺少存储路径"，并据此把链路标记为 failed。收紧成抛异常会把这 4 处变成未捕获异常。
    /// </remarks>
    public static string Product(string? storagePath, string productKind, string targetExt)
    {
        if (string.IsNullOrWhiteSpace(storagePath)) return "";

        if (productKind != PdfSegment && productKind != MarkdownSegment && productKind != EditableSegment)
            throw new ArgumentException(
                $"产物类型段必须是「{PdfSegment}」「{MarkdownSegment}」或「{EditableSegment}」，实际为「{productKind}」",
                nameof(productKind));

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
    /// ★ 从**空白模板路径**派生「试填预览 PDF」路径（`52` §12.4 裁定 A，2026-10-06 新增）。
    ///
    /// <para><b>规则</b>：把模板路径里的 <see cref="TemplateSegment"/> 段替换为
    /// <see cref="PreviewSegment"/>，并给文件名追加产物扩展名。</para>
    /// <code>
    /// 模板：…/4记录文件/_template/风险管理报告.docx
    /// 预览：…/4记录文件/_preview/风险管理报告.docx.pdf
    /// </code>
    ///
    /// <para><b>★ 为什么不能复用 <see cref="Product"/></b>：① <c>Product</c> 从
    /// <b>源文件</b>路径派生，而试填产物来自<b>空白模板</b>；② <c>Product</c> 只接受
    /// <c>pdf</c>/<c>markdown</c>/<c>editable</c> 三个段，传 <c>_preview</c> 会抛
    /// <see cref="ArgumentException"/>；③ 复用 <c>pdf/</c> 段会与「源文件预览 PDF」
    /// （实测 185 行在用）<b>同 key 互相覆盖</b>。</para>
    ///
    /// <para><b>★ 固定 key（不带时间戳）</b>：试填不需要历史版本 ⇒ 每模板最多 1 个 PDF，
    /// 空间占用恒定（用户口径「节约后台空间」）。</para>
    ///
    /// <para>⚠️ 路径里找不到 <see cref="TemplateSegment"/> 段时（非常规调用）退化为
    /// <b>在文件名前插入</b> <see cref="PreviewSegment"/> —— 宁可落在同层级，
    /// 也不静默返回一个错误位置。</para>
    /// </summary>
    /// <param name="templatePath">空白模板的存储路径（<see cref="TemplateFile"/> 的产出）</param>
    /// <param name="targetExt">目标扩展名（含点，如 <c>.pdf</c>）</param>
    /// <returns>预览产物路径（以 <c>/</c> 开头）；入参为空或段数不足时返回<b>空串</b></returns>
    /// <remarks>
    /// 返回空串而非抛异常，与 <see cref="Product"/> 保持同一契约 ——
    /// 调用方以 <c>string.IsNullOrEmpty</c> 判定「缺少存储路径」并据此失败。
    /// </remarks>
    public static string PreviewFromTemplate(string? templatePath, string targetExt)
    {
        if (string.IsNullOrWhiteSpace(templatePath)) return "";

        var segs = Segments(templatePath);
        if (segs.Length < 2) return "";

        var ext = string.IsNullOrEmpty(targetExt)
            ? ""
            : (targetExt.StartsWith('.') ? targetExt : "." + targetExt);

        var name = Sanitize(segs[^1]) + ext;
        var parent = segs[..^1].ToList();

        var idx = parent.FindIndex(s => s.Equals(TemplateSegment, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) parent[idx] = PreviewSegment;
        else parent.Add(PreviewSegment);

        return $"/{string.Join("/", parent)}/{name}";
    }

    /// <summary>
    /// 判断给定路径是否为「产物路径」（位于 <c>pdf/</c>、<c>markdown/</c> 或 <c>editable/</c> 段下）。
    /// <para>用途：删除文件时避免把产物目录误当业务目录；以及排查历史脏数据。</para>
    /// </summary>
    public static bool IsProductPath(string? path)
        => Segments(path).Any(s => s.Equals(PdfSegment, StringComparison.OrdinalIgnoreCase)
                                || s.Equals(MarkdownSegment, StringComparison.OrdinalIgnoreCase)
                                || s.Equals(EditableSegment, StringComparison.OrdinalIgnoreCase));

    /// <summary>判断给定路径是否为「归档路径」（含 <c>_archive</c> 段）。</summary>
    public static bool IsArchivePath(string? path)
        => Segments(path).Any(s => s.Equals(ArchiveSegment, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 判断给定路径是否为「空白模板路径」（含 <c>_template</c> 段）。
    ///
    /// <para>用途：① 删除标准文档时一并清理模板；② 排查「模板被误当业务文件」；
    /// ③ 上传/替换时拒绝把模板写进业务目录。</para>
    ///
    /// <para>⚠️ 与 <see cref="IsProductPath"/> <b>并列而非包含</b> —— 模板不是产物
    /// （产物可重算，模板是人工标注的资产）。所以 <see cref="IsProductPath"/> 不认
    /// <c>_template</c>，本方法也不认 <c>pdf</c>/<c>markdown</c>/<c>editable</c>。</para>
    /// </summary>
    public static bool IsTemplatePath(string? path)
        => Segments(path).Any(s => s.Equals(TemplateSegment, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 判断给定路径是否为「试填预览路径」（含 <c>_preview</c> 段）。
    ///
    /// <para><b>★ 为什么与 <see cref="IsProductPath"/> 并列，而不是并进去</b>：
    /// <see cref="IsProductPath"/> 的 3 个段<b>恰好等于 <see cref="Product"/> 接受的
    /// 3 个 <c>productKind</c></b> —— 这是一条不变量（传第 4 种会抛
    /// <see cref="ArgumentException"/>）。把 <c>_preview</c> 塞进 <c>IsProductPath</c>
    /// 会让「判定通过」与「能否派生」不再等价，是下一个静默分叉的入口。
    /// ⇒ <b>三个判定并列：产物 / 模板 / 试填预览</b>，各自 <c>false</c> 于另两者。</para>
    ///
    /// <para>用途与 <see cref="IsTemplatePath"/> 相同：① 删除标准文档时一并清理；
    /// ② 排查「试填产物被误当业务文件」；③ 上传/替换时拒绝把产物写进业务目录。</para>
    /// </summary>
    public static bool IsPreviewPath(string? path)
        => Segments(path).Any(s => s.Equals(PreviewSegment, StringComparison.OrdinalIgnoreCase));

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
