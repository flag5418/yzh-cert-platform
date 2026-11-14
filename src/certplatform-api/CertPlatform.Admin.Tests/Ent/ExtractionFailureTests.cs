using System.Reflection;
using CertPlatform.Auditor.Entities.Dir;
using CertPlatform.Auditor.Services.Ent;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent;

public class ExtractionFailureTests
{
    [Fact]
    public void StandardChunks_CoverTheEntireSourceWithoutTruncation()
    {
        var method = typeof(ClauseExtractExecutor).GetMethod(
            "BuildStandardChunks", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var source = "第一段\n第二段\n最后一段";
        var chunks = (System.Collections.Generic.List<string>)method!.Invoke(
            null, new object[] { source, 6, 8 })!;
        Assert.True(chunks.Count > 1);
        Assert.Equal(source, string.Concat(chunks));
        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, 6));
    }

    [Fact]
    public void StandardChunks_RejectIncompleteCoverage()
    {
        var method = typeof(ClauseExtractExecutor).GetMethod(
            "BuildStandardChunks", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var error = Assert.Throws<TargetInvocationException>(
            () => method!.Invoke(null, new object[] { "0123456789", 3, 2 }));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    [Fact]
    public void StandardChunks_RejectMissingSource()
    {
        var method = typeof(ClauseExtractExecutor).GetMethod(
            "BuildStandardChunks", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var error = Assert.Throws<TargetInvocationException>(
            () => method!.Invoke(null, new object[] { "  ", 3, 2 }));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    [Theory]
    [InlineData("4.2.5", true)]
    [InlineData("4.2.55", true)]
    [InlineData("4.2", false)]
    [InlineData("14.2.5", false)]
    public void ClauseLookup_UsesExactNumbersFromFullContent(string number, bool expected)
    {
        var method = typeof(DocumentExtractionService).GetMethod(
            "MatchesClause", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var row = new DocumentExtraction
        {
            ClauseNumbers = "",
            ContentJson = "{\"Clauses\":[{\"ClauseNumber\":\"4.2.5\"},{\"ClauseNumber\":\"4.2.55\"}]}"
        };
        Assert.Equal(expected, method!.Invoke(null, new object?[] { row, number }));
    }

    [Fact]
    public void ClauseLookup_DoesNotTrustIndexWhenContentIsMalformed()
    {
        var method = typeof(DocumentExtractionService).GetMethod(
            "MatchesClause", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var row = new DocumentExtraction { ClauseNumbers = "4.1", ContentJson = "{bad" };
        Assert.Equal(false, method!.Invoke(null, new object?[] { row, "4.1" }));
    }

    [Fact]
    public void FailedAttempt_PreservesPreviousContentAndMarksItStale()
    {
        var method = typeof(DocumentExtractionService).GetMethod(
            "MarkFailedAttempt", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var old = new DocumentExtraction
        {
            ContentJson = "{\"old\":true}", ClauseNumbers = "4.1",
            SourceSha256 = "old-hash", ExtractStatus = "done"
        };
        method!.Invoke(null, new object?[] { old, "提取失败", "user", DateTime.UnixEpoch });
        Assert.Equal("{\"old\":true}", old.ContentJson);
        Assert.Equal("4.1", old.ClauseNumbers);
        Assert.Equal("old-hash", old.SourceSha256);
        Assert.Equal("failed", old.ExtractStatus);
        Assert.True(old.IsStale);
    }
}
