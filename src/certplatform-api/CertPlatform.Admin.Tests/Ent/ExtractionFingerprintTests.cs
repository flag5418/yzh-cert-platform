using System.Reflection;
using CertPlatform.Shared.DocExtraction;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent;

public class ExtractionFingerprintTests
{
    private static string Fingerprint(params string[] values)
    {
        var type = typeof(DocSectionIndex).Assembly.GetType(
            "CertPlatform.Shared.DocExtraction.DocumentExtractionFingerprint");
        Assert.NotNull(type);
        var method = type!.GetMethod("Compute", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        return (string)method!.Invoke(null, new object[] { values })!;
    }

    [Fact]
    public void Fingerprint_ChangesForEachInputAndKeepsFieldBoundaries()
    {
        var original = Fingerprint("source", "standard", "prompt", "model");
        Assert.Equal(original, Fingerprint("source", "standard", "prompt", "model"));
        Assert.NotEqual(original, Fingerprint("source", "standard", "changed", "model"));
        Assert.NotEqual(original, Fingerprint("source", "standard2", "prompt", "model"));
        Assert.NotEqual(original, Fingerprint("source", "standard", "prompt", "other"));
        Assert.NotEqual(Fingerprint("a", "bc"), Fingerprint("ab", "c"));
    }
}
