using System.Reflection;
using CertPlatform.Auditor.Services.Expert;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent;

public class AuditSuggestionTests
{
    [Theory]
    [InlineData("pass")]
    [InlineData("unknown")]
    public void EmptyDoubts_DoNotAutomaticallySuggestPass(string modelConclusion)
    {
        var method = typeof(CertCheckEngineService).GetMethod(
            "ResolveSuggestedConclusion", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        var conclusion = new CertCheckConclusionDto { Conclusion = modelConclusion };
        Assert.Null(method!.Invoke(null, new object?[] { conclusion }));
    }
}
