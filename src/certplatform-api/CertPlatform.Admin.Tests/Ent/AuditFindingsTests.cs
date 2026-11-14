using System.Reflection;
using System.Text.Json;
using CertPlatform.Auditor.Entities.Dir;
using CertPlatform.Auditor.Services.Expert;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent;

public class AuditFindingsTests
{
    private static List<CertEvidenceRow> Project(DocumentExtraction extraction)
    {
        var method = typeof(CertTaskService).GetMethod("ProjectDocSlices",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (List<CertEvidenceRow>)method!.Invoke(null,
            new object[] { extraction, "item", new HashSet<string> { "4.1" }, 0 })!;
    }

    private static DocumentExtraction Extraction(string status, bool stale, string text)
        => new()
        {
            Code = "result",
            FileCode = "file",
            ExtractStatus = status,
            IsStale = stale,
            ContentJson = JsonSerializer.Serialize(new
            {
                DocMeta = new { FileName = "manual" },
                Clauses = new[] { new { ClauseNumber = "4.1", Anchor = "原文定位", Excerpts = new[] { text } } }
            })
        };

    [Theory]
    [InlineData("failed", false)]
    [InlineData("done", true)]
    public void Findings_ExcludeIncompleteOrStaleExtraction(string status, bool stale)
        => Assert.Empty(Project(Extraction(status, stale, "文档内容")));

    [Fact]
    public void Findings_PreserveFullExcerptAndSourceAnchor()
    {
        var text = new string('文', 3500);
        var row = Assert.Single(Project(Extraction("done", false, text)));
        Assert.Equal(text, row.Snippet);
        Assert.Equal("4.1", row.Anchor);
        Assert.Equal("原文定位", row.SourceAnchor);
    }

    [Fact]
    public void ModelContext_LabelsLegacyExcerptsAsUnverified()
    {
        var clause = new ClauseContext { ClauseNumber = "4.1", Title = "组织环境" };
        clause.Slices.Add(new ClauseDocSlice { FileName = "手册", Excerpts = new List<string> { "提取内容" } });
        var context = new CertCheckContext { Clauses = new List<ClauseContext> { clause } };
        Assert.Contains("未经逐字核验", context.RenderDocHits());
    }

    [Fact]
    public void ModelContext_IncludesClauseAttributesAsHints()
    {
        var clause = new ClauseContext
        {
            ClauseNumber = "4.1",
            Title = "组织环境",
            Description = "确定相关内外部因素",
            Attrs = new Dictionary<string, string?>
            {
                ["ClauseIntent"] = "识别组织环境",
                ["Process"] = "战略管理",
                ["EvidenceForm"] = "记录",
                ["Severity"] = "一般"
            }
        };
        var text = new CertCheckContext { Clauses = new List<ClauseContext> { clause } }.RenderClauseInfo();
        Assert.Contains("确定相关内外部因素", text);
        Assert.Contains("识别组织环境", text);
        Assert.Contains("战略管理", text);
        Assert.Contains("记录", text);
        Assert.Contains("不能直接", text);
    }
}
