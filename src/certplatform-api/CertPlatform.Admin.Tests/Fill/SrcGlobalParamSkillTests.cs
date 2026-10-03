using CertPlatform.Admin.Services.Workflow.Skills.Fill;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Shared.Fill;
using CertPlatform.Shared.Office;
using Xunit;

namespace CertPlatform.Admin.Tests.Fill;

/// <summary>
/// <see cref="SrcGlobalParamSkill.BuildValue"/> 的契约测试 —— 只测<b>纯函数</b>部分
/// （不依赖 DB / DI，故可在单测里跑）。
///
/// <para>覆盖 39 号 §4.6 的 T1~T5，外加「三种维护方式」的分支
/// （<c>auto</c> / <c>both</c> / <c>manual</c>）—— 这三条<b>必须由 <c>ParamValueResolver</c> 决定</b>，
/// 本 Skill 只做搬运；用例的作用是钉死「Skill 没有自己重写一套判定」。</para>
/// </summary>
public class SrcGlobalParamSkillTests
{
    // ─────────────────────────── 基础设施 ───────────────────────────

    private static FillParamDef Def(
        string code = "company_name",
        string name = "企业全称",
        string valueType = "text",
        string mode = "auto",
        string? expr = "enterprise.Name",
        string? defValue = null) => new()
    {
        ParamCode = code,
        ParamName = name,
        ValueType = valueType,
        MaintainMode = mode,
        SourceExpr = expr,
        DefaultValue = defValue,
    };

    private static EnterpriseInfo Ent(string? name = "某某有限公司") => new() { Name = name };

    private static (bool Ok, FillValue? Value, string? Error) Run(
        FillParamDef def,
        EnterpriseInfo? ent = null,
        string? savedValue = null,
        string? savedSource = null,
        bool savedEdited = false,
        string anchor = "company_name",
        string? valueKind = null,
        string? numberFormat = null)
        => SrcGlobalParamSkill.BuildValue(
            def, ent ?? Ent(), savedValue, savedSource, savedEdited, anchor, valueKind, numberFormat);

    // ─────────────────────────── T1 / T5 ───────────────────────────

    [Fact]
    public void T1_auto模式_取企业档案值()
    {
        var (ok, value, error) = Run(Def());

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(FillValueKind.Text, value!.Kind);
        Assert.Equal("某某有限公司", value.Text);
        Assert.Equal("company_name", value.AnchorCode);
        Assert.Equal(1.0, value.Confidence);
    }

    [Fact]
    public void T1b_来源标注_含企业全称()
    {
        var (_, value, _) = Run(Def());

        // 与 ParamValueResolver 的 SourceRef 口径一致（「企业基础信息 · 企业全称」）
        Assert.Contains("企业全称", value!.Source);
    }

    [Fact]
    public void T5_锚点编码与参数编码不同_以锚点为准()
    {
        var (_, value, _) = Run(Def(), anchor: "ent_name_in_doc");

        Assert.Equal("ent_name_in_doc", value!.AnchorCode);
    }

    // ─────────────────────────── 三种维护方式 ───────────────────────────

    [Fact]
    public void auto_忽略企业已填值_恒取企业档案()
    {
        // ★ auto 的语义是「永远等于企业档案」⇒ 即使有已填值也不用
        //   （否则企业改了档案而值表未刷新 ⇒ 文档印着旧值且无人察觉）
        var (ok, value, _) = Run(Def(mode: "auto"), savedValue: "旧的名字");

        Assert.True(ok);
        Assert.Equal("某某有限公司", value!.Text);
    }

    [Fact]
    public void both_有企业填写值_用企业值()
    {
        var (ok, value, _) = Run(Def(mode: "both"), savedValue: "企业自己填的名字");

        Assert.True(ok);
        Assert.Equal("企业自己填的名字", value!.Text);
    }

    [Fact]
    public void both_无企业填写值_自动带出企业档案()
    {
        var (ok, value, _) = Run(Def(mode: "both"), savedValue: null);

        Assert.True(ok);
        Assert.Equal("某某有限公司", value!.Text);
    }

    [Fact]
    public void manual_有企业填写值_用企业值()
    {
        var (ok, value, _) = Run(Def(mode: "manual", expr: null), savedValue: "手工填的");

        Assert.True(ok);
        Assert.Equal("手工填的", value!.Text);
    }

    [Fact]
    public void manual_无企业填写值_用默认值()
    {
        var (ok, value, _) = Run(Def(mode: "manual", expr: null, defValue: "默认值"), savedValue: null);

        Assert.True(ok);
        Assert.Equal("默认值", value!.Text);
    }

    // ─────────────────────────── T3 未命中 ───────────────────────────

    [Fact]
    public void T3_企业档案为空_失败而非返回空值()
    {
        var (ok, value, error) = Run(Def(), ent: Ent(null));

        Assert.False(ok);
        Assert.Null(value);
        Assert.Contains("未取到", error);
        Assert.Contains("company_name", error);
    }

    [Fact]
    public void manual_无值无默认_失败()
    {
        var (ok, _, error) = Run(Def(mode: "manual", expr: null, defValue: null));

        Assert.False(ok);
        Assert.Contains("未取到", error);
    }

    // ─────────────────────────── T4 类型 ───────────────────────────

    [Fact]
    public void T4_声明number但值非数字_失败而非降级成文本()
    {
        var def = Def(valueType: "number", expr: "enterprise.CreditCode");
        var ent = new EnterpriseInfo { CreditCode = "ABC" };

        var (ok, value, error) = Run(def, ent, valueKind: "number");

        Assert.False(ok);
        Assert.Null(value);
        Assert.Contains("不是合法数字", error);
    }

    [Fact]
    public void T4b_声明number且值是数字_成功()
    {
        var def = Def(valueType: "number", expr: "enterprise.EmployeeCount");
        var ent = new EnterpriseInfo { EmployeeCount = 128 };

        var (ok, value, _) = Run(def, ent, valueKind: "number");

        Assert.True(ok);
        Assert.Equal(FillValueKind.Number, value!.Kind);
        Assert.Equal(128d, value.Number);
    }

    [Fact]
    public void 值类型缺省_用参数定义的值类型()
    {
        var def = Def(valueType: "date", expr: "enterprise.ArchiveDate");
        var ent = new EnterpriseInfo { ArchiveDate = new System.DateTime(2026, 10, 3) };

        var (ok, value, _) = Run(def, ent, valueKind: null);

        Assert.True(ok);
        Assert.Equal(FillValueKind.Date, value!.Kind);
        Assert.Equal(new System.DateTime(2026, 10, 3), value.Date!.Value.Date);
    }

    [Fact]
    public void 显式值类型_覆盖参数定义()
    {
        // 定义说是 text，但调用方显式要 number
        var def = Def(valueType: "text", expr: "enterprise.EmployeeCount");
        var ent = new EnterpriseInfo { EmployeeCount = 7 };

        var (ok, value, _) = Run(def, ent, valueKind: "number");

        Assert.True(ok);
        Assert.Equal(FillValueKind.Number, value!.Kind);
    }

    // ─────────────────────────── 格式串 ───────────────────────────

    [Fact]
    public void 格式串_透传到FillValue()
    {
        var (_, value, _) = Run(Def(), numberFormat: "#,##0.00");

        Assert.Equal("#,##0.00", value!.NumberFormat);
    }

    // ─────────────────────────── 边界 ───────────────────────────

    [Fact]
    public void 参数定义为空_失败()
    {
        var (ok, value, error) = SrcGlobalParamSkill.BuildValue(
            null!, Ent(), null, null, false, "a", null, null);

        Assert.False(ok);
        Assert.Null(value);
        Assert.Contains("参数定义不能为空", error);
    }
}
