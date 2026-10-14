using CertPlatform.Shared.Storage;
using Xunit;
using static CertPlatform.Shared.Storage.UploadFilePolicy;

namespace CertPlatform.Admin.Tests.Storage;

/// <summary>
/// ★ 上传后缀策略验收用例（2026-10-14 用户铁律：全平台上传只认 upload-file-policy.json 一张表）
///
/// <para><b>为什么必须有</b>：上传白名单的失败模式是<b>静默</b>的 —— 拒绝发生在业务层
/// （恒 HTTP 200），前端只看到一句错误文案，无线索指向「谁的清单和事实源漂了」。
/// 这四个用例是用户验收口径的固化：exe 拒 / 系统噪声静默 / html 拒 / webp 过。</para>
///
/// <para>与前端 <c>cert-admin/src/pages/workflow/upload-policy.test.ts</c> 逐条对应
/// （两端同源于 <c>upload-file-policy.json</c>，生成器保证清单一致）。</para>
/// </summary>
public class UploadFilePolicyTests
{
    // ─────────── 用户验收四例 ───────────

    [Fact]
    public void Check_Exe_IsRejected()
    {
        var v = Check("a.exe");
        Assert.False(v.Ok);
        Assert.Equal(PolicyReason.BadExtension, v.Reason);
    }

    [Fact]
    public void Check_DsStore_IsSystemNoise_NotBadExtension()
    {
        var v = Check(".DS_Store");
        Assert.False(v.Ok);
        Assert.Equal(PolicyReason.SystemNoise, v.Reason);
        Assert.True(v.IsSystemNoise);
        Assert.True(IsSystemNoise(".DS_Store"));
        // 前缀规则：Spotlight / Trashes / Office 锁文件同样命中（穷举黑名单列不全的兜底）
        Assert.True(IsSystemNoise(".Spotlight-V100"));
        Assert.True(IsSystemNoise("~$合同.doc"));
        Assert.True(IsSystemNoise("Thumbs.db"));
        Assert.True(IsSystemNoise("THUMBS.DB")); // OrdinalIgnoreCase，与前端 toLowerCase 同口径
    }

    [Fact]
    public void Check_Html_IsRejected_ScriptBlacklist()
    {
        var v = Check("evil.html");
        Assert.False(v.Ok);
        Assert.Equal(PolicyReason.BadExtension, v.Reason);
    }

    [Fact]
    public void Check_WebpImage_IsAllowed()
    {
        var v = Check("照片.webp");
        Assert.True(v.Ok);
        Assert.Equal(".webp", v.Extension);
        Assert.Equal(".webp", ExtOf("照片.WEBP"));
    }

    // ─────────── 口径边界（与前端契约逐字对齐） ───────────

    [Fact]
    public void Check_Zip_IsRejected_ArchiveNotOpenByDefault()
    {
        Assert.False(Check("bundle.zip").Ok);
    }

    [Fact]
    public void ExtOf_EdgeCases_NoLeadingDot_NoTrailingDot()
    {
        Assert.Equal("", ExtOf(".gitignore"));   // 首字符即点（隐藏文件）
        Assert.Equal("", ExtOf("file."));        // 尾点
        Assert.Equal("", ExtOf("noext"));        // 无扩展名
        Assert.Equal("", ExtOf(null));
        Assert.Equal(".docx", ExtOf("A.B.合同.DOCX")); // 最后一个点，大小写归一
    }

    [Fact]
    public void Check_NoExtension_IsRejected()
    {
        var v = Check("README");
        Assert.False(v.Ok);
        Assert.Equal(PolicyReason.BadExtension, v.Reason);
        Assert.Equal("", v.Extension);
    }
}
