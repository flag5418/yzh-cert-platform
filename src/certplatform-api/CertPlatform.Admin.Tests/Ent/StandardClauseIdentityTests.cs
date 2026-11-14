using System.Linq;
using CertPlatform.Admin.Services.StandardDirectory;
using CertPlatform.Shared.Domain.Clauses;
using CertPlatform.Shared.Entities.Cert;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent;

public class StandardClauseIdentityTests
{
    [Fact]
    public void StableCode_SeparatesStandardVersionAndFullClauseNumber()
    {
        var method = typeof(ClauseRefs).GetMethod("CreateStableCode");
        Assert.NotNull(method);

        string Code(string standard, int version, string number) =>
            (string)method!.Invoke(null, new object[] { standard, version, number })!;

        var code = Code("iso9001", 2016, "4.1");
        Assert.Equal(code, Code(" ISO9001 ", 2016, "4.1"));
        Assert.NotEqual(code, Code("iso9001", 2025, "4.1"));
        Assert.NotEqual(code, Code("iso14001", 2016, "4.1"));
        Assert.NotEqual(Code("iso9001", 2016, "A.1"), Code("iso9001", 2016, "1"));
        Assert.True(code.Length <= 36);
    }

    [Fact]
    public void Parse_KeepsSameClauseInDifferentStandardVersions()
    {
        const string json = """
            [
              {"StandardNo":"iso9001","VersionYear":2016,"ClauseNumber":"4.1"},
              {"StandardNo":"iso9001","VersionYear":2025,"ClauseNumber":"4.1"}
            ]
            """;

        var refs = ClauseRefs.Parse(json);

        Assert.Equal(2, refs.Count);
        Assert.Contains("\"VersionYear\":2016", ClauseRefs.ToJson(refs));
        Assert.Contains("\"VersionYear\":2025", ClauseRefs.ToJson(refs));
    }

    [Fact]
    public void RebuildPlan_UsesSameStableCodeForSameClause()
    {
        var method = typeof(StandardSourceService).GetMethod("BuildRebuildPlan");
        Assert.NotNull(method);
        var standard = new ISOStandard { Code = "standard", StandardCode = "iso9001", VersionYear = 2016 };
        var parsed = new[] {
            new ParsedClause { ClauseNumber = "4", Title = "组织" },
            new ParsedClause { ClauseNumber = "4.1", Title = "环境", Description = "要求" }
        };

        var rows = (System.Collections.Generic.List<ISOClause>)method!.Invoke(
            null, new object[] { standard, parsed })!;
        var parent = rows.Single(c => c.ClauseNumber == "4");
        var child = rows.Single(c => c.ClauseNumber == "4.1");
        Assert.Equal(ClauseRefs.CreateStableCode("iso9001", 2016, "4.1"), child.Code);
        Assert.Equal(parent.Code, child.ParentCode);
        var again = (System.Collections.Generic.List<ISOClause>)method.Invoke(
            null, new object[] { standard, parsed })!;
        Assert.Equal(child.Code, again.Single(c => c.ClauseNumber == "4.1").Code);
    }

    [Theory]
    [InlineData("A.1", "A.1")]
    [InlineData("A.1.2", "A.1.2")]
    [InlineData("e 4.1 理解组织及其环境", "4.1")]
    public void NormalizeNumber_PreservesAppendixIdentity(string input, string expected)
    {
        Assert.Equal(expected, ClauseRefs.NormalizeNumber(input));
    }

    [Fact]
    public void ParseClauses_PrefersBodyOverEarlierTableOfContents()
    {
        const string markdown = """
            # 目次
            4.2.1 总则
            4.2.2 质量手册
            # 4.2.1 总则
            组织应建立质量管理体系文件。
            # 4.2.2 质量手册
            组织应编制质量手册。
            """;

        var clauses = StandardSourceService.ParseClauses(markdown);

        Assert.Contains("组织应建立质量管理体系文件", clauses.Single(c => c.ClauseNumber == "4.2.1").Description);
        Assert.Contains("组织应编制质量手册", clauses.Single(c => c.ClauseNumber == "4.2.2").Description);
    }
}
