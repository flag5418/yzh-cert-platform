using System.Reflection;
using CertPlatform.Shared.DocExtraction;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent;

public class DocumentSourceLocatorTests
{
    [Theory]
    [InlineData("abc😀证据文本", "证据文本", 5)]
    [InlineData("重复内容，重复内容", "重复内容", null)]
    [InlineData("没有原文", "模型编写", null)]
    [InlineData("任意内容", "", null)]
    public void LocateUnique_RequiresOneExactMatch(string source, string quote, int? expected)
    {
        var type = typeof(DocSectionIndex).Assembly.GetType(
            "CertPlatform.Shared.DocExtraction.DocumentSourceLocator");
        Assert.NotNull(type);
        var method = type!.GetMethod("LocateUnique", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.Equal(expected, (int?)method!.Invoke(null, new object[] { source, quote }));
    }
}
