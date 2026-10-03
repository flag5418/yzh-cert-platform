using CertPlatform.Shared.Office;
using Xunit;

namespace CertPlatform.Admin.Tests.Fill;

/// <summary>
/// <see cref="FillSession"/> 的契约测试 —— 重点是「<b>锚点重复赋值必须暴露</b>」。
///
/// <para>为什么这条最重要：若重复赋值被静默覆盖，填充结果就<b>依赖 Skill 执行顺序</b>——
/// 换个顺序结果就变，而报告里一切正常。这正是本仓反复出现的「静默错值」类缺陷，
/// 必须在最底层（会话对象）就堵死。</para>
/// </summary>
public class FillSessionTests
{
    [Fact]
    public void TryClaim_首次认领_成功()
    {
        var session = new FillSession();

        var ok = session.TryClaim("company_name", "src_global_param", out var conflict);

        Assert.True(ok);
        Assert.Null(conflict);
        Assert.Equal("src_global_param", session.AnchorOwners["company_name"]);
    }

    [Fact]
    public void TryClaim_同一锚点被第二个Skill认领_失败并回传已有来源()
    {
        var session = new FillSession();
        session.TryClaim("company_name", "src_global_param", out _);

        var ok = session.TryClaim("company_name", "src_ai_field", out var conflict);

        Assert.False(ok);
        Assert.Equal("src_global_param", conflict);
        // 已有来源不被覆盖
        Assert.Equal("src_global_param", session.AnchorOwners["company_name"]);
    }

    [Fact]
    public void TryClaim_同一Skill重复认领同一锚点_也算冲突()
    {
        var session = new FillSession();
        session.TryClaim("a", "src_ai_field", out _);

        var ok = session.TryClaim("a", "src_ai_field", out var conflict);

        Assert.False(ok);
        Assert.Equal("src_ai_field", conflict);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryClaim_锚点为空_失败(string? anchor)
    {
        var session = new FillSession();

        var ok = session.TryClaim(anchor!, "src_manual", out var conflict);

        Assert.False(ok);
        Assert.Null(conflict);
        Assert.Empty(session.AnchorOwners);
    }

    [Fact]
    public void 锚点键大小写敏感_与值字典同口径()
    {
        var session = new FillSession();
        session.TryClaim("Company_Name", "src_ai_field", out _);

        // Ordinal 比较 ⇒ 不同大小写是不同锚点，不应冲突
        var ok = session.TryClaim("company_name", "src_ai_field", out var conflict);

        Assert.True(ok);
        Assert.Null(conflict);
    }

    [Fact]
    public void Log_追加轨迹()
    {
        var session = new FillSession();

        session.Log("src_global_param", "写入 company_name");
        session.Log("fill_cell", "累积 1 处");

        Assert.Equal(2, session.Trace.Count);
        Assert.StartsWith("[src_global_param]", session.Trace[0]);
    }

    [Fact]
    public void 默认状态_干净()
    {
        var session = new FillSession();

        Assert.Empty(session.Template);
        Assert.Empty(session.AnchorOwners);
        Assert.Empty(session.Todos);
        Assert.Empty(session.Trace);
        Assert.NotNull(session.Request);
        // 未命中默认置空（38 号定死：模板里 {{x}} 是占位符，不是给人看的正文）
        Assert.False(session.Request.KeepUnresolvedAsIs);
        Assert.True(session.Request.FillHeader);
    }

    [Fact]
    public void FillTodo_默认来源是src_manual()
    {
        var todo = new FillTodo();

        Assert.Equal("src_manual", todo.Source);
    }
}
