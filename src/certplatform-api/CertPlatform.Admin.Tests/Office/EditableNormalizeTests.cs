using CertPlatform.Admin.Services.StandardDirectory;
using CertPlatform.Shared.Storage;
using Xunit;

namespace CertPlatform.Admin.Tests.Office;

/// <summary>
/// ★ 归一链（S-1，2026-10-03）判据单测：旧二进制格式 → OOXML。
///
/// <para><b>为什么必须有</b>：<see cref="OfficeConvertService.EditableTargetFormat"/> 是
/// 「该文件要不要归一」的<b>全项目唯一判据</b>（入队点 + 执行器都调它）。
/// 判据错的失败模式是<b>静默</b>的 ——</para>
/// <list type="bullet">
///   <item><b>漏判</b>（该归一却返回 null）：<c>.doc</c> 不进归一链 ⇒ 填写引擎拿不到
///         <c>.docx</c> ⇒ 用户看到「模板无法解析」，但<b>全链路零报错</b>。</item>
///   <item><b>误判</b>（不该归一却返回格式）：每次上传都白跑一次 LibreOffice 容器调用。</item>
/// </list>
///
/// <para>★ 判据的物理根因：NPOI 2.7.2 <b>没有 <c>NPOI.HWPF</c></b> ⇒ <c>.doc</c> 连读都读不了。
/// 本库实测 668 份中 <c>.doc</c> 567 / <c>.xls</c> 44（<b>91.5%</b>）—— 判据错了等于 92% 的模板不可用。</para>
/// </summary>
public class EditableNormalizeTests
{
    // ═══════════════════════════════════════════════
    // ① 需要归一的三种旧格式
    // ═══════════════════════════════════════════════

    [Theory]
    [InlineData("风险管理报告.doc", "docx")]
    [InlineData("年度内审计划.xls", "xlsx")]
    [InlineData("培训课件.ppt", "pptx")]
    public void EditableTargetFormat_LegacyBinary_ReturnsTargetFormat(string fileName, string expected)
        => Assert.Equal(expected, OfficeConvertService.EditableTargetFormat(fileName));

    /// <summary>
    /// ★ 大小写不敏感：Windows 上传的 <c>.DOC</c> 与 <c>.doc</c> 必须同一判据 ——
    /// 用 <c>==</c> 比扩展名会漏掉大写形式，而漏判是静默的。
    /// </summary>
    [Theory]
    [InlineData("风险管理报告.DOC", "docx")]
    [InlineData("年度内审计划.XLS", "xlsx")]
    [InlineData("培训课件.Ppt", "pptx")]
    public void EditableTargetFormat_IsCaseInsensitive(string fileName, string expected)
        => Assert.Equal(expected, OfficeConvertService.EditableTargetFormat(fileName));

    // ═══════════════════════════════════════════════
    // ② 不需要归一的格式（返回 null = 该链不适用）
    // ═══════════════════════════════════════════════

    [Theory]
    [InlineData("风险管理报告.docx")]   // 已是 OOXML
    [InlineData("年度内审计划.xlsx")]
    [InlineData("培训课件.pptx")]
    [InlineData("营业执照.pdf")]        // PDF 原样透传，不归一
    [InlineData("扫描件.jpg")]          // 图片走 OCR，不归一
    [InlineData("说明.txt")]
    [InlineData("无扩展名")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EditableTargetFormat_NotLegacyBinary_ReturnsNull(string? fileName)
        => Assert.Null(OfficeConvertService.EditableTargetFormat(fileName));

    /// <summary>
    /// ★ 只认**最后一个**扩展名：<c>a.doc.txt</c> 是文本文件，不是 Word 文档。
    /// 用 <c>Contains(".doc")</c> 之类会误判 ⇒ 白跑容器调用。
    /// </summary>
    [Theory]
    [InlineData("归档说明.doc.txt")]
    [InlineData("备份.doc.bak")]
    public void EditableTargetFormat_OnlyLastExtensionCounts(string fileName)
        => Assert.Null(OfficeConvertService.EditableTargetFormat(fileName));

    // ═══════════════════════════════════════════════
    // ③ 判据 × 路径构造（端到端：判据产出什么格式，产物就落到哪个路径）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 归一链的实际取值链：<c>EditableTargetFormat(FileName)</c> → <c>PathBuilder.Product(StoragePath, EditableSegment, "." + fmt)</c>。
    /// <para>这里把两步串起来断言 —— 判据与路径段是**两个**改动点，任一改动都要看到最终路径变化。</para>
    /// </summary>
    [Theory]
    [InlineData("风险管理报告.doc", "docx")]
    [InlineData("年度内审计划.xls", "xlsx")]
    public void EditableTargetFormat_FeedsProductPath(string fileName, string expectedExt)
    {
        var src = "/standard-directory/ORG/STD/STAGE/4记录文件/" + fileName;
        var fmt = OfficeConvertService.EditableTargetFormat(fileName);
        Assert.NotNull(fmt);

        var target = PathBuilder.Product(src, PathBuilder.EditableSegment, "." + fmt);
        Assert.EndsWith($"/editable/{fileName}.{expectedExt}", target);
        Assert.True(PathBuilder.IsProductPath(target));
    }
}
