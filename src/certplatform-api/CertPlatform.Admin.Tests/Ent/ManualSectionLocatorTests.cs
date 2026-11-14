using System.Reflection;
using CertPlatform.Shared.DocExtraction;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent;

public class ManualSectionLocatorTests
{
    private static (int Start, int End)? Locate(string markdown, string clause, string anchor)
    {
        var type = typeof(DocSectionIndex).Assembly.GetType(
            "CertPlatform.Shared.DocExtraction.DocumentManualSectionLocator");
        Assert.NotNull(type);
        var method = type!.GetMethod("Locate", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        return ((int Start, int End)?)method!.Invoke(null, new object[] { markdown, clause, anchor });
    }

    [Fact]
    public void Locate_PreservesCompleteSectionAndStopsAtNextHeading()
    {
        const string text = "## 4.1 环境\r\n4.1.1 组织因素\r\n4.1.4 监视和评审\r\n4.1.5 正负因素\r\n## 4.2 相关方\r\n其他";
        var range = Locate(text, "4.1", "组织因素");
        Assert.NotNull(range);
        var section = text[range!.Value.Start..range.Value.End];
        Assert.Contains("4.1.4", section);
        Assert.Contains("4.1.5", section);
        Assert.DoesNotContain("4.2", section);
    }

    [Fact]
    public void Locate_RejectsAmbiguousHeadingOrAnchor()
    {
        Assert.Null(Locate("## 4.1 目录\n目录\n## 4.1 正文\n组织因素", "4.1", "组织因素"));
        Assert.Null(Locate("## 4.1 环境\n组织因素 组织因素", "4.1", "组织因素"));
    }
}
