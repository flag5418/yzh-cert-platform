using System;
using System.Collections.Generic;
using System.Reflection;
using CertPlatform.Admin.Services.StandardDirectory;
using CertPlatform.Shared.Entities.Cert;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent;

public class ClauseMarkdownInputTests
{
    private static Type Reader()
    {
        var type = typeof(ISOClause).Assembly.GetType("CertPlatform.Shared.DocExtraction.ClauseMarkdownInput");
        Assert.NotNull(type);
        return type!;
    }

    [Fact]
    public void SelectPath_RequiresClauseMarkdownWithoutOriginalFallback()
    {
        var method = Reader().GetMethod("SelectPath", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var standard = new ISOStandard { MarkdownPath = "original.md", ClauseMarkdownPath = "clauses.md" };
        Assert.Equal("clauses.md", method!.Invoke(null, new object[] { standard }));
        standard.ClauseMarkdownPath = null;
        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { standard }));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }

    [Fact]
    public void ReadSection_UsesOnlySelectedClauseFromGeneratedMarkdown()
    {
        var clause = new ISOClause { Code = "a", ClauseNumber = "4.1", Title = "环境", Description = "完整要求" };
        var child = new ISOClause { Code = "b", ParentCode = "a", ClauseNumber = "4.1.1", Title = "子条款", Description = "子要求" };
        var markdown = StandardSourceService.RenderClauseMarkdown(new ISOStandard(), new List<ISOClause> { clause, child });
        var method = Reader().GetMethod("ReadSection", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var section = (string)method!.Invoke(null, new object[] { markdown, clause })!;
        Assert.Contains("完整要求", section);
        Assert.DoesNotContain("子要求", section);
    }

    [Fact]
    public void ReadSection_RejectsStaleOrDuplicateClause()
    {
        var clause = new ISOClause { Code = "a", ClauseNumber = "4.1", Title = "环境", Description = "当前要求" };
        var stale = new ISOClause { Code = "a", ClauseNumber = "4.1", Title = "环境", Description = "旧要求" };
        var standard = new ISOStandard();
        var markdown = StandardSourceService.RenderClauseMarkdown(standard, new List<ISOClause> { stale });
        var method = Reader().GetMethod("ReadSection", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        var error = Assert.Throws<TargetInvocationException>(() => method!.Invoke(null, new object[] { markdown, clause }));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        markdown = StandardSourceService.RenderClauseMarkdown(standard, new List<ISOClause> { clause, clause });
        error = Assert.Throws<TargetInvocationException>(() => method!.Invoke(null, new object[] { markdown, clause }));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }
}
