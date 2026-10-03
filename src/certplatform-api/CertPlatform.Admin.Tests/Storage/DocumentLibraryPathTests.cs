using CertPlatform.Shared.Entities.Dir;
using CertPlatform.Shared.Storage;
using Xunit;

namespace CertPlatform.Admin.Tests.Storage;

/// <summary>
/// ★ 存储路径白名单与构造器单测（36 号 §4.2 表④）
///
/// <para><b>为什么必须有</b>：白名单漏改的失败模式是<b>静默</b>的 ——
/// <c>IsAllowedStoragePath()</c> 返回 false ⇒ 该库的 <c>download</c>/<c>file-preview</c>/
/// <c>file-markdown</c> 全部失败，而<b>业务失败恒 HTTP 200</b>，前端只看到「文件不存在」，
/// <b>无线索指向白名单</b>（见 <c>DocumentLibrary.cs</c> 类注释）。这种 bug 不可能靠点页面发现。</para>
///
/// <para>覆盖三条链：①白名单 <c>DocumentLibraryPath</c> ②路径构造 <c>PathBuilder</c>
/// ③归档（版本管理 D8）。</para>
/// </summary>
public class DocumentLibraryPathTests
{
    // 企业 Code / 阶段 Code 均取业务键原文（GUID 含 '-',⛔ 不做 CleanCode）
    private const string Ent = "846dec4b-c534-4c1a-9f2e-1a2b3c4d5e6f";
    private const string Stage = "11223344-5566-7788-99aa-bbccddeeff00";
    private const string NewPrefix = "enterprise-original-source";

    // ═══════════════════════════════════════════════
    // ① 白名单：新库必须放行
    // ═══════════════════════════════════════════════

    [Fact]
    public void IsAllowedStoragePath_OriginalSourceRootFile_IsAllowed()
        => Assert.True(DocumentLibraryPath.IsAllowedStoragePath(
            $"/{NewPrefix}/{Ent}/{Stage}/营业执照.pdf"));

    [Fact]
    public void IsAllowedStoragePath_OriginalSourceWithFolderAndProducts_IsAllowed()
    {
        // 业务文件夹 + 产物段 + 归档段，逐一确认都放行
        foreach (var p in new[]
        {
            $"/{NewPrefix}/{Ent}/{Stage}/4记录文件/风险管理报告.doc",
            $"/{NewPrefix}/{Ent}/{Stage}/4记录文件/pdf/风险管理报告.doc.pdf",
            $"/{NewPrefix}/{Ent}/{Stage}/4记录文件/markdown/风险管理报告.doc.md",
            // ★ 归一产物（S-1）：漏放行 ⇒ 归一上传后 download/preview 全失败，而业务失败恒 HTTP 200，
            //   前端只看到「文件不存在」，无线索指向白名单（见本类头注释）
            $"/{NewPrefix}/{Ent}/{Stage}/4记录文件/editable/风险管理报告.doc.docx",
            $"/{NewPrefix}/{Ent}/{Stage}/4记录文件/_archive/风险管理报告.doc.v3",
            $"/{NewPrefix}/{Ent}/{Stage}/4记录文件/pdf/_archive/风险管理报告.doc.pdf.v3",
        })
            Assert.True(DocumentLibraryPath.IsAllowedStoragePath(p), p);
    }

    [Fact]
    public void IsAllowedStoragePath_AllThreeLibraries_AreAllowed()
    {
        Assert.True(DocumentLibraryPath.IsAllowedStoragePath("/standard-directory/ORG/STD/STG/a.doc"));
        Assert.True(DocumentLibraryPath.IsAllowedStoragePath("/enterprise-documents/E/S/T/a.doc"));
        Assert.True(DocumentLibraryPath.IsAllowedStoragePath($"/{NewPrefix}/E/S/a.doc"));
    }

    // ═══════════════════════════════════════════════
    // ② 白名单：穿越 / 空段 / 伪造前缀必须拒绝
    // ═══════════════════════════════════════════════

    [Theory]
    [InlineData(null, "null 路径")]
    [InlineData("", "空串")]
    [InlineData("   ", "纯空白")]
    public void IsAllowedStoragePath_Empty_IsRejected(string? input, string because)
        => Assert.False(DocumentLibraryPath.IsAllowedStoragePath(input), because);

    [Theory]
    [InlineData("../standard-directory/a.doc", "父目录穿越")]
    [InlineData("./a.doc", "当前目录穿越")]
    [InlineData("standard-directory//a.doc", "中间空段")]
    [InlineData("standard-directory/a.doc/", "尾部空段")]
    public void IsAllowedStoragePath_TraversalOrEmptySegment_IsRejected(string path, string because)
        => Assert.False(DocumentLibraryPath.IsAllowedStoragePath(path), because);

    [Theory]
    [InlineData("foo/standard-directory/x", "伪造：库前缀不在首段")]
    [InlineData("foo/enterprise-documents/x", "伪造：企业库前缀不在首段")]
    [InlineData("foo/enterprise-original-source/x", "★伪造：新库前缀不在首段")]
    [InlineData("xxenterprise-original-source/E/S/a.doc", "★前缀仅以前缀匹配即放行（无边界）")]
    [InlineData("/some-other-library/E/S/a.doc", "未知库")]
    public void IsAllowedStoragePath_ForgedOrUnknownPrefix_IsRejected(string path, string because)
        => Assert.False(DocumentLibraryPath.IsAllowedStoragePath(path), because);

    // ═══════════════════════════════════════════════
    // ③ 前缀解析
    // ═══════════════════════════════════════════════

    [Theory]
    [InlineData("/standard-directory/a/b/c", DocumentLibrary.StandardDirectory)]
    [InlineData("/enterprise-documents/a/b/c", DocumentLibrary.EnterpriseDocuments)]
    [InlineData("/enterprise-original-source/a/b/c", DocumentLibrary.EnterpriseOriginalSource)]
    public void Resolve_KnownPrefix_ResolvesToLibrary(string path, DocumentLibrary expected)
        => Assert.Equal(expected, DocumentLibraryPath.Resolve(path));

    [Theory]
    [InlineData("ENTERPRISE-ORIGINAL-SOURCE/E/S/a.doc")]   // 大小写不敏感
    [InlineData("\\enterprise-original-source\\E\\S\\a.doc")] // 反斜杠
    [InlineData("  /enterprise-original-source/E/S/a.doc  ")] // 前后空白 + 前导斜杠
    public void Resolve_NormalizesPathShape(string path)
        => Assert.Equal(DocumentLibrary.EnterpriseOriginalSource, DocumentLibraryPath.Resolve(path));

    [Fact]
    public void Resolve_Unknown_IsUnknown()
    {
        Assert.Equal(DocumentLibrary.Unknown, DocumentLibraryPath.Resolve(null));
        Assert.Equal(DocumentLibrary.Unknown, DocumentLibraryPath.Resolve(""));
        Assert.Equal(DocumentLibrary.Unknown, DocumentLibraryPath.Resolve("nope/a/b"));
    }

    [Fact]
    public void PrefixOf_EveryLibrary_RoundTripsThroughResolve()
    {
        foreach (var lib in new[]
        {
            DocumentLibrary.StandardDirectory,
            DocumentLibrary.EnterpriseDocuments,
            DocumentLibrary.EnterpriseOriginalSource
        })
        {
            var prefix = DocumentLibraryPath.PrefixOf(lib);
            Assert.NotEmpty(prefix);
            Assert.Contains(prefix, DocumentLibraryPath.Prefixes);
            Assert.Equal(lib, DocumentLibraryPath.Resolve($"/{prefix}/x/y/z"));
        }
    }

    [Fact]
    public void PrefixOf_Unknown_IsEmpty()
    {
        Assert.Equal("", DocumentLibraryPath.PrefixOf(DocumentLibrary.Unknown));
        Assert.Equal("", DocumentLibraryPath.PrefixOf((DocumentLibrary)99));
    }

    [Fact]
    public void Prefixes_ContainsExactlyThreeLibraries()
        => Assert.Equal(3, DocumentLibraryPath.Prefixes.Count);

    [Theory]
    [InlineData($"/{NewPrefix}/E/S/a.doc", DocumentLibrary.EnterpriseOriginalSource, true)]
    [InlineData($"/{NewPrefix}/E/S/a.doc", DocumentLibrary.EnterpriseDocuments, false)]
    [InlineData("/enterprise-documents/E/S/T/a.doc", DocumentLibrary.EnterpriseOriginalSource, false)]
    public void IsUnder_MatchesPrefixOnly(string path, DocumentLibrary lib, bool expected)
        => Assert.Equal(expected, DocumentLibraryPath.IsUnder(path, lib));

    [Fact]
    public void IsUnder_UnknownLibrary_IsAlwaysFalse()
    {
        Assert.False(DocumentLibraryPath.IsUnder($"/{NewPrefix}/E/S/a.doc", DocumentLibrary.Unknown));
        Assert.False(DocumentLibraryPath.IsUnder(null, DocumentLibrary.StandardDirectory));
    }
}

/// <summary>★ <see cref="PathBuilder"/> 构造器单测（含 D8 版本管理归档链）</summary>
public class PathBuilderTests
{
    private const string Ent = "846dec4b-c534-4c1a-9f2e-1a2b3c4d5e6f";
    private const string Stage = "11223344-5566-7788-99aa-bbccddeeff00";
    private const string NewPrefix = "enterprise-original-source";

    // ═══════════════════════════════════════════════
    // ① EnterpriseOriginalSource 构造
    // ═══════════════════════════════════════════════

    [Fact]
    public void EnterpriseOriginalSource_RootFile_NoStandardSegment()
    {
        var p = PathBuilder.EnterpriseOriginalSource(Ent, Stage, null, "营业执照.pdf");
        Assert.Equal($"/{NewPrefix}/{Ent}/{Stage}/营业执照.pdf", p);
    }

    [Fact]
    public void EnterpriseOriginalSource_EmptyFolder_BehavesLikeRoot()
    {
        Assert.Equal(
            PathBuilder.EnterpriseOriginalSource(Ent, Stage, null, "a.pdf"),
            PathBuilder.EnterpriseOriginalSource(Ent, Stage, "", "a.pdf"));
        Assert.Equal(
            PathBuilder.EnterpriseOriginalSource(Ent, Stage, null, "a.pdf"),
            PathBuilder.EnterpriseOriginalSource(Ent, Stage, "   ", "a.pdf"));
    }

    [Fact]
    public void EnterpriseOriginalSource_NestedFolder_PreservesSlashAndUnicode()
    {
        var p = PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4 记录文件/2026年度", "内审 报告（终稿）.docx");
        Assert.Equal($"/{NewPrefix}/{Ent}/{Stage}/4 记录文件/2026年度/内审 报告（终稿）.docx", p);
    }

    /// <summary>GUID 含 '-' ⇒ ⛔ 不得 CleanCode，否则路径与 DB 值不可逆地不一致。</summary>
    [Fact]
    public void EnterpriseOriginalSource_KeepsGuidDashes()
    {
        var p = PathBuilder.EnterpriseOriginalSource(Ent, Stage, null, "a.pdf");
        Assert.Contains(Ent, p);
        Assert.Contains("-", p);
    }

    [Theory]
    [InlineData(null, "空企业Code")]
    [InlineData("", "空企业Code")]
    [InlineData("   ", "空白企业Code")]
    public void EnterpriseOriginalSource_EmptyEnterprise_Throws(string? ent, string because)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => PathBuilder.EnterpriseOriginalSource(ent, Stage, null, "a.pdf"));
        Assert.Contains("EnterpriseCode", ex.Message);
        Assert.NotNull(because);
    }

    [Theory]
    [InlineData(null, "空阶段Code")]
    [InlineData("", "空阶段Code")]
    public void EnterpriseOriginalSource_EmptyStage_Throws(string? stage, string because)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => PathBuilder.EnterpriseOriginalSource(Ent, stage, null, "a.pdf"));
        Assert.Contains("StageCode", ex.Message);
        Assert.NotNull(because);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnterpriseOriginalSource_EmptyFileName_Throws(string? fileName)
        => Assert.Throws<ArgumentException>(
            () => PathBuilder.EnterpriseOriginalSource(Ent, Stage, null, fileName));

    /// <summary>业务文件夹名不得撞保留段名，否则 <c>pdf/</c> 目录被当成产物目录、<c>_archive/</c> 被当成归档目录。</summary>
    [Theory]
    [InlineData("pdf", "撞 PDF 产物段")]
    [InlineData("PDF", "撞 PDF 产物段（大小写不敏感）")]
    [InlineData("markdown", "撞 Markdown 产物段")]
    [InlineData("_archive", "撞归档段")]
    [InlineData("4记录/pdf", "嵌套层级里撞保留段名")]
    public void EnterpriseOriginalSource_ReservedFolderSegment_Throws(string folder, string because)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => PathBuilder.EnterpriseOriginalSource(Ent, Stage, folder, "a.pdf"));
        Assert.Contains("保留段", ex.Message);
        Assert.NotNull(because);
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("markdown")]
    [InlineData("_archive")]
    public void EnterpriseOriginalSource_ReservedFileName_Throws(string fileName)
        => Assert.Throws<ArgumentException>(
            () => PathBuilder.EnterpriseOriginalSource(Ent, Stage, null, fileName));

    /// <summary>路径分隔符必须被剥离，否则一个文件名可伪造出子目录（写入别处）。</summary>
    [Fact]
    public void EnterpriseOriginalSource_StripsSeparatorsFromFileName()
    {
        var p = PathBuilder.EnterpriseOriginalSource(Ent, Stage, null, "a/b.pdf");
        Assert.Equal($"/{NewPrefix}/{Ent}/{Stage}/ab.pdf", p);
    }

    [Fact]
    public void EnterpriseOriginalSource_ResultAlwaysPassesWhitelist()
    {
        foreach (var folder in new[] { null, "4记录文件", "a/b/c" })
        foreach (var name in new[] { "x.doc", "y.docx", "z.pdf", "w.xlsx", "v.pptx" })
        {
            var p = PathBuilder.EnterpriseOriginalSource(Ent, Stage, folder, name);
            Assert.True(DocumentLibraryPath.IsAllowedStoragePath(p), p);
        }
    }

    // ═══════════════════════════════════════════════
    // ② 产物派生（PDF / Markdown）—— 零改动复用
    // ═══════════════════════════════════════════════

    [Fact]
    public void Product_DerivesPdfAndMarkdown_KeepingFullOriginalName()
    {
        var src = PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4记录文件", "风险管理报告.doc");
        Assert.Equal($"/{NewPrefix}/{Ent}/{Stage}/4记录文件/pdf/风险管理报告.doc.pdf",
            PathBuilder.Product(src, PathBuilder.PdfSegment, ".pdf"));
        Assert.Equal($"/{NewPrefix}/{Ent}/{Stage}/4记录文件/markdown/风险管理报告.doc.md",
            PathBuilder.Product(src, PathBuilder.MarkdownSegment, ".md"));
    }

    /// <summary>同 stem 不同扩展名的两个文件，产物必须区分开（沿用既有设计）。</summary>
    [Fact]
    public void Product_SameStemDifferentExtension_DoNotCollide()
    {
        var a = PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4记录文件", "XASL-QR-014 年度内审计划.doc");
        var b = PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4记录文件", "XASL-QR-014 年度内审计划.xls");
        Assert.NotEqual(PathBuilder.Product(a, PathBuilder.PdfSegment, ".pdf"),
                        PathBuilder.Product(b, PathBuilder.PdfSegment, ".pdf"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Product_EmptySource_ReturnsEmptyNotThrow(string? src)
    {
        // ⚠️ 契约：返回空串而非抛异常（4 处调用方靠 string.IsNullOrEmpty 判 failed）
        Assert.Equal("", PathBuilder.Product(src, PathBuilder.PdfSegment, ".pdf"));
        Assert.Equal("", PathBuilder.Product(src, PathBuilder.MarkdownSegment, ".md"));
    }

    [Fact]
    public void Product_TooFewSegments_ReturnsEmpty()
        => Assert.Equal("", PathBuilder.Product("/只有文件名.doc", PathBuilder.PdfSegment, ".pdf"));

    [Theory]
    [InlineData("docx")]
    [InlineData("md")]
    [InlineData("")]
    public void Product_InvalidKind_Throws(string kind)
    {
        var src = PathBuilder.EnterpriseOriginalSource(Ent, Stage, null, "a.doc");
        Assert.Throws<ArgumentException>(() => PathBuilder.Product(src, kind, ".pdf"));
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("markdown")]
    [InlineData("editable")]
    public void Product_ProductsAreMarkedAsProductPath(string kind)
    {
        var src = PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4记录文件", "a.doc");
        Assert.True(PathBuilder.IsProductPath(PathBuilder.Product(src, kind, ".pdf")));
        Assert.False(PathBuilder.IsProductPath(src));
    }

    /// <summary>
    /// ★ 归一链产物（S-1，2026-10-03）：<c>.doc</c> 源 → <c>editable/</c> 段下的 <c>.docx</c>。
    /// <para>产物文件名保留**完整原文件名**（含原扩展名）—— 与 pdf/markdown 同一规则，
    /// 避免同 stem 不同扩展名的文件互相覆盖（本项目实测存在 <c>X.doc</c> 与 <c>X.xls</c> 并存）。</para>
    /// </summary>
    [Theory]
    [InlineData("风险管理报告.doc", "docx")]
    [InlineData("年度内审计划.xls", "xlsx")]
    [InlineData("培训课件.ppt", "pptx")]
    public void Product_EditableSegment_NormalizedArtifact(string fileName, string targetExt)
    {
        var src = PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4记录文件", fileName);
        Assert.Equal(
            $"/{NewPrefix}/{Ent}/{Stage}/4记录文件/editable/{fileName}.{targetExt}",
            PathBuilder.Product(src, PathBuilder.EditableSegment, "." + targetExt));
    }

    /// <summary>归一产物必须与源路径**不同**（同目录会与既有业务文件撞名）。</summary>
    [Fact]
    public void Product_EditableSegment_DiffersFromSource()
    {
        var src = PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4记录文件", "a.doc");
        var ed = PathBuilder.Product(src, PathBuilder.EditableSegment, ".docx");
        Assert.NotEqual(src, ed);
        Assert.True(PathBuilder.IsProductPath(ed));
        // ⛔ 归一产物是「产物」，不得被当成归档路径
        Assert.False(PathBuilder.IsArchivePath(ed));
    }

    /// <summary>归一产物同样享受「源路径为空 → 返回空串」的契约（执行器靠它判 failed）。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Product_EditableSegment_EmptySource_ReturnsEmpty(string? src)
        => Assert.Equal("", PathBuilder.Product(src, PathBuilder.EditableSegment, ".docx"));

    /// <summary>
    /// ★ 归一产物必须被**三个库**的白名单放行（S-1）。
    /// <para>漏放行 ⇒ 归一上传后 <c>download</c>/<c>file-preview</c> 全失败，
    /// 而业务失败恒 HTTP 200 ⇒ 前端只看到「文件不存在」，**无线索指向白名单**。</para>
    /// <para>⚠️ 白名单按**首段库前缀**判定（与段名无关），本测试锁定该事实 ——
    /// 若日后改成按段名校验，新增的产物段就会静默失效。</para>
    /// </summary>
    [Fact]
    public void IsAllowedStoragePath_EditableProduct_AllowedInAllLibraries()
    {
        var sources = new[]
        {
            PathBuilder.StandardFile(Ent, Ent, Stage, "4记录文件", "风险管理报告.doc"),
            PathBuilder.EnterpriseFile(Ent, Ent, Stage, "4记录文件", "风险管理报告.doc"),
            PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4记录文件", "风险管理报告.doc"),
        };

        foreach (var src in sources)
        {
            var ed = PathBuilder.Product(src, PathBuilder.EditableSegment, ".docx");
            Assert.True(PathBuilder.IsProductPath(ed), $"应为产物路径: {ed}");
            Assert.True(DocumentLibraryPath.IsAllowedStoragePath(ed), $"白名单应放行: {ed}");
        }
    }

    /// <summary>路径解析器不得把 <c>editable</c> 段吃掉（段号稳定是产物定位的前提）。</summary>
    [Fact]
    public void Product_EditableSegment_SegmentLayoutIsStable()
    {
        var src = PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4记录文件", "a.doc");
        var ed = PathBuilder.Product(src, PathBuilder.EditableSegment, ".docx");
        Assert.Equal(PathBuilder.Segments(src).Length + 1, PathBuilder.Segments(ed).Length);
        Assert.Equal("editable", PathBuilder.SegmentAt(ed, PathBuilder.Segments(src).Length - 1));
    }

    // ═══════════════════════════════════════════════
    // ②b 空白模板 _template/（37 号 §4.3）
    // ═══════════════════════════════════════════════

    private const string StdPrefix = "standard-directory";
    private const string Std = "aabbccdd-1122-3344-5566-778899aabbcc";

    /// <summary>模板与源文件**同层级并列**（模板是源文档的派生物，不是独立库）。</summary>
    [Fact]
    public void TemplateFile_SitsNextToSource_InTemplateSubdir()
    {
        var src = PathBuilder.StandardFile(Ent, Std, Stage, "4记录文件", "风险管理报告.doc");
        var tpl = PathBuilder.TemplateFile(Ent, Std, Stage, "4记录文件", "风险管理报告.docx");

        Assert.Equal($"/{StdPrefix}/{Ent}/{Std}/{Stage}/4记录文件/_template/风险管理报告.docx", tpl);
        // 父级段完全一致 ⇒ 整文件夹搬迁/删除时两者一起走
        Assert.Equal(
            string.Join("/", PathBuilder.Segments(src)[..^1]),
            string.Join("/", PathBuilder.Segments(tpl)[..^2]));
    }

    [Fact]
    public void TemplateFile_RootFile_NoFolderSegment()
        => Assert.Equal(
            $"/{StdPrefix}/{Ent}/{Std}/{Stage}/_template/a.docx",
            PathBuilder.TemplateFile(Ent, Std, Stage, null, "a.docx"));

    /// <summary>模板段插在**文件名前**（不是追加在末尾）—— 段号稳定是定位的前提。</summary>
    [Fact]
    public void TemplateFile_InsertsSegmentBeforeFileName()
    {
        var segs = PathBuilder.Segments(
            PathBuilder.TemplateFile(Ent, Std, Stage, "4记录文件", "a.docx"));
        Assert.Equal("_template", segs[^2]);
        Assert.Equal("a.docx", segs[^1]);
    }

    /// <summary>身份段规则**自动继承** <c>StandardFile</c>：空则抛异常（⛔ 不静默丢段）。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TemplateFile_EmptyOrg_Throws(string? org)
        => Assert.Throws<ArgumentException>(
            () => PathBuilder.TemplateFile(org, Std, Stage, null, "a.docx"));

    [Fact]
    public void TemplateFile_KeepsGuidDashes()
    {
        var tpl = PathBuilder.TemplateFile(Ent, Std, Stage, null, "a.docx");
        Assert.Contains(Std, tpl);
        Assert.Contains(Stage, tpl);
    }

    /// <summary>★ 模板不是产物 —— 两个判定必须**并列**，不能互相包含。</summary>
    [Fact]
    public void IsTemplatePath_IsNotProductPath()
    {
        var tpl = PathBuilder.TemplateFile(Ent, Std, Stage, "4记录文件", "a.docx");
        Assert.True(PathBuilder.IsTemplatePath(tpl));
        Assert.False(PathBuilder.IsProductPath(tpl));

        var src = PathBuilder.StandardFile(Ent, Std, Stage, "4记录文件", "a.doc");
        Assert.False(PathBuilder.IsTemplatePath(src));
    }

    /// <summary>模板换版归档走**同一个** <c>Archive</c> 算法（37 号 §4.3）。</summary>
    [Fact]
    public void TemplateFile_Archive_UsesSameAlgorithm()
        => Assert.Equal(
            $"/{StdPrefix}/{Ent}/{Std}/{Stage}/4记录文件/_template/_archive/风险管理报告.docx.v1",
            PathBuilder.Archive(
                PathBuilder.TemplateFile(Ent, Std, Stage, "4记录文件", "风险管理报告.docx"), 1));

    /// <summary>模板路径必须过白名单 —— ⛔ 无需新增库前缀（白名单按**首段库前缀**判定，与段名无关）。</summary>
    [Fact]
    public void TemplateFile_ResultPassesWhitelist()
        => Assert.True(DocumentLibraryPath.IsAllowedStoragePath(
            PathBuilder.TemplateFile(Ent, Std, Stage, "4记录文件", "a.docx")));

    /// <summary>业务文件夹不得叫 <c>_template</c>（否则与系统目录撞车）。</summary>
    [Fact]
    public void ReservedSegments_BusinessFolderNamedTemplate_Throws()
        => Assert.Throws<ArgumentException>(
            () => PathBuilder.StandardFile(Ent, Std, Stage, "_template", "a.docx"));

    // ═══════════════════════════════════════════════
    // ③ D8 归档（版本管理）—— 同文件夹 _archive/{名}.v{n}
    // ═══════════════════════════════════════════════

    [Fact]
    public void Archive_SameFolder_InsertsArchiveSegmentAndVersionSuffix()
    {
        var src = PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4记录文件", "风险管理报告.doc");
        Assert.Equal(
            $"/{NewPrefix}/{Ent}/{Stage}/4记录文件/_archive/风险管理报告.doc.v3",
            PathBuilder.Archive(src, 3));
    }

    [Fact]
    public void Archive_Products_UseSameAlgorithm()
    {
        var src = PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4记录文件", "风险管理报告.doc");
        var pdf = PathBuilder.Product(src, PathBuilder.PdfSegment, ".pdf");
        Assert.Equal(
            $"/{NewPrefix}/{Ent}/{Stage}/4记录文件/pdf/_archive/风险管理报告.doc.pdf.v2",
            PathBuilder.Archive(pdf, 2));
    }

    /// <summary>归档幂等：同一个 (路径, 版本) 永远算出同一个结果，重试不会互相覆盖。</summary>
    [Fact]
    public void Archive_IsIdempotent_SameInputSameOutput()
    {
        var src = PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4记录文件", "a.doc");
        Assert.Equal(PathBuilder.Archive(src, 1), PathBuilder.Archive(src, 1));
    }

    [Fact]
    public void Archive_IsMarkedAsArchivePath()
    {
        var src = PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4记录文件", "a.doc");
        var arc = PathBuilder.Archive(src, 1);
        Assert.True(PathBuilder.IsArchivePath(arc));
        Assert.False(PathBuilder.IsArchivePath(src));
        Assert.True(DocumentLibraryPath.IsAllowedStoragePath(arc));
    }

    /// <summary>二次归档 = 调用方状态机出错，必须响，不能静默产生 _archive/_archive。</summary>
    [Fact]
    public void Archive_AlreadyArchived_Throws()
    {
        var src = PathBuilder.EnterpriseOriginalSource(Ent, Stage, "4记录文件", "a.doc");
        var arc = PathBuilder.Archive(src, 1);
        Assert.Throws<InvalidOperationException>(() => PathBuilder.Archive(arc, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Archive_VersionBelowOne_Throws(int v)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PathBuilder.Archive("/a/b/c.doc", v));

    [Fact]
    public void Archive_TooFewSegments_Throws()
        => Assert.Throws<ArgumentException>(() => PathBuilder.Archive("/只有文件名.doc", 1));

    // ═══════════════════════════════════════════════
    // ④ 拆段
    // ═══════════════════════════════════════════════

    [Theory]
    [InlineData("/a/b/c", new[] { "a", "b", "c" })]
    [InlineData("a/b/c", new[] { "a", "b", "c" })]
    [InlineData("\\a\\b\\c", new[] { "a", "b", "c" })]
    [InlineData("/a//b/", new[] { "a", "b" })]
    [InlineData("/a/./b", new[] { "a", "b" })]
    [InlineData("/a/../b", new[] { "a", "b" })]
    [InlineData("  /a/ b /c ", new[] { "a", "b", "c" })]
    public void Segments_Normalizes(string input, string[] expected)
        => Assert.Equal(expected, PathBuilder.Segments(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Segments_Empty_ReturnsEmptyArray(string? input)
        => Assert.Empty(PathBuilder.Segments(input));

    [Fact]
    public void SegmentAt_OutOfRange_ReturnsNull()
    {
        var segs = PathBuilder.Segments("/a/b");
        Assert.Equal("a", PathBuilder.SegmentAt("/a/b", 0));
        Assert.Equal("b", PathBuilder.SegmentAt("/a/b", 1));
        Assert.Null(PathBuilder.SegmentAt("/a/b", 2));
        Assert.Null(PathBuilder.SegmentAt("/a/b", -1));
        Assert.Equal(2, segs.Length);
    }

    /// <summary>既有三库互不误判（防止前缀改动引入串库）。</summary>
    [Fact]
    public void ReservedSegments_ContainsProductAndArchiveNames()
    {
        Assert.Contains(PathBuilder.PdfSegment, PathBuilder.ReservedSegments);
        Assert.Contains(PathBuilder.MarkdownSegment, PathBuilder.ReservedSegments);
        // ★ 归一链产物段（S-1）：漏登记则业务文件夹可以叫 editable，
        //   产物与业务文件同段 ⇒ IsProductPath 误判 + 产物被当业务文件删除
        Assert.Contains(PathBuilder.EditableSegment, PathBuilder.ReservedSegments);
        // ★ 空白模板目录段（37 号 §4.3）：漏登记则业务文件夹可以叫 _template，
        //   与系统模板目录撞车 ⇒ 模板被当业务文件删除（模板是人工标注资产，丢了要重标）
        Assert.Contains(PathBuilder.TemplateSegment, PathBuilder.ReservedSegments);
        Assert.Contains(PathBuilder.ArchiveSegment, PathBuilder.ReservedSegments);
    }

    /// <summary>保留段名不得被业务文件夹/文件名使用（否则与产物目录撞车）。</summary>
    [Fact]
    public void ReservedSegments_BusinessFolderNamedEditable_Throws()
        => Assert.Throws<ArgumentException>(() =>
            PathBuilder.StandardFile(Ent, Ent, Stage, "editable", "a.docx"));
}