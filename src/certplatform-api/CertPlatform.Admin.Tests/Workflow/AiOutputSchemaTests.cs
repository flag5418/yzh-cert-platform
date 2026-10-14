using System.Text.Json;
using CertPlatform.Admin.Services.Workflow;
using Xunit;

namespace CertPlatform.Admin.Tests.Workflow;

/// <summary>
/// ★ AI 节点「结构化输出 Schema 校验」唯一口径单测（第 5 批 · 设计 §4 表「outputType 增 json + Schema 校验」）。
///
/// <para>覆盖三件事：</para>
/// <list type="number">
/// <item><b>schema 三种落库形态</b>：JsonElement / Dictionary / JSON 字符串都要吃得下 ——
///   只吃一种 = 静默解析成「未配 schema」，校验形同虚设（与 params 同口径教训）。</item>
/// <item><b>类型表达式全集</b>：基础类型 / [] 数组 / a|b 枚举 / ? 可选 / any —— 逐条判对错。</item>
/// <item><b>不静默放行</b>：缺必填 / 类型不符 / 越界 / 根非对象 ⇒ 都要产出违规清单（非空）。</item>
/// </list>
/// </summary>
public class AiOutputSchemaTests
{
    // ═══════════════════════════════════════════════
    // 1. schema 三种落库形态
    // ═══════════════════════════════════════════════

    [Fact]
    public void schema_字符串形态_同样校验()
    {
        var schema = """{"verdict":"pass|fail|unknown","confidence":"number"}""";
        var value = Obj(("verdict", "pass"), ("confidence", 0.9));

        Assert.Empty(AiOutputSchema.Validate(schema, value));
    }

    [Fact]
    public void schema_JsonElement形态_同样校验()
    {
        using var doc = JsonDocument.Parse("""{"verdict":"pass|fail|unknown"}""");
        var value = Obj(("verdict", "fail"));

        Assert.Empty(AiOutputSchema.Validate(doc.RootElement, value));
    }

    /// <summary>
    /// 前端 textarea 存的是<b>逐行「字段:类型」</b>（不是 JSON）—— 后端必须两种形态都吃得下，
    /// 只吃 JSON = 前端配了 schema 却静默不生效（校验形同虚设）。
    /// </summary>
    [Fact]
    public void schema_逐行字段冒号类型形态_同样校验()
    {
        var schema = """
            verdict:pass|fail|unknown
            confidence:number
            quotes:string[]
            """;
        var value = Obj(("verdict", "unknown"), ("confidence", 0.8), ("quotes", new List<object> { "原文" }));

        Assert.Empty(AiOutputSchema.Validate(schema, value));

        // 缺必填字段仍要报出来（证明逐行解析真的生效了）
        var v = AiOutputSchema.Validate(schema, Obj(("verdict", "pass")));
        Assert.Equal(2, v.Count);
    }

    [Fact]
    public void schema_缺失或空_恒通过()
    {
        var value = Obj(("anything", 1));

        Assert.Empty(AiOutputSchema.Validate(null, value));
        Assert.Empty(AiOutputSchema.Validate("", value));
        Assert.Empty(AiOutputSchema.Validate("""{}""", value));
    }

    // ═══════════════════════════════════════════════
    // 2. 基础类型
    // ═══════════════════════════════════════════════

    [Fact]
    public void 必填字段缺失_报违规()
    {
        var schema = Obj(("verdict", "string"), ("reason", "string"));
        var value = Obj(("verdict", "ok"));

        var v = AiOutputSchema.Validate(schema, value);

        Assert.Single(v);
        Assert.Contains("reason", v[0]);
    }

    [Fact]
    public void 类型不符_报违规并指出期望与实际()
    {
        var schema = Obj(("confidence", "number"));
        var value = Obj(("confidence", "很高"));

        var v = AiOutputSchema.Validate(schema, value);

        Assert.Single(v);
        Assert.Contains("number", v[0]);
        Assert.Contains("string", v[0]);
    }

    [Fact]
    public void 类型全对_通过()
    {
        var schema = Obj(
            ("verdict", "string"),
            ("confidence", "number"),
            ("pass", "boolean"),
            ("items", "any"));
        var value = Obj(
            ("verdict", "pass"),
            ("confidence", 0.95),
            ("pass", true),
            ("items", new List<object> { 1, "x" }));

        Assert.Empty(AiOutputSchema.Validate(schema, value));
    }

    // ═══════════════════════════════════════════════
    // 3. 数组 / 枚举 / 可选 / any
    // ═══════════════════════════════════════════════

    [Fact]
    public void 数组_元素类型逐个校验()
    {
        var schema = Obj(("quotes", "string[]"));
        var value = Obj(("quotes", new List<object> { "原文A", 42 }));

        var v = AiOutputSchema.Validate(schema, value);

        Assert.Single(v);
        Assert.Contains("quotes[1]", v[0]);
    }

    [Fact]
    public void 枚举_越界报违规_命中通过()
    {
        var schema = Obj(("verdict", "pass|fail|unknown"));

        Assert.Empty(AiOutputSchema.Validate(schema, Obj(("verdict", "unknown"))));
        Assert.Single(AiOutputSchema.Validate(schema, Obj(("verdict", "maybe"))));
    }

    [Fact]
    public void 可选字段_缺失不报_但类型不符仍报()
    {
        var schema = Obj(("missing", "string?"));

        Assert.Empty(AiOutputSchema.Validate(schema, Obj(("other", 1))));

        var v = AiOutputSchema.Validate(Obj(("missing", "number?")), Obj(("missing", "文本")));
        Assert.Single(v);
    }

    [Fact]
    public void 根非对象_报违规()
    {
        var v = AiOutputSchema.Validate(Obj(("a", "string")), new List<object> { 1, 2 });

        Assert.Single(v);
        Assert.Contains("对象", v[0]);
    }

    // ═══════════════════════════════════════════════
    // 4. ExtractJson（json 转换的前置：剥围栏）
    // ═══════════════════════════════════════════════

    [Theory]
    [InlineData("{\"a\":1}", "{\"a\":1}")]
    [InlineData("```json\n{\"a\":1}\n```", "{\"a\":1}")]
    [InlineData("```\n{\"a\":1}\n```", "{\"a\":1}")]
    [InlineData("前置说明 {\"a\":1} 后置", "{\"a\":1}")]
    public void 剥围栏_取到纯净JSON(string llmOutput, string expected)
    {
        var (ok, jsonText, _) = PromptRenderer.ExtractJson(llmOutput);

        Assert.True(ok);
        Assert.Equal(expected, jsonText);
    }

    [Fact]
    public void 空输出_提取失败()
    {
        var (ok, _, error) = PromptRenderer.ExtractJson("   ");

        Assert.False(ok);
        Assert.NotNull(error);
    }

    // ═══════════════════════════════════════════════

    private static Dictionary<string, object> Obj(params (string Key, object Value)[] kv)
    {
        var d = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (k, v) in kv) d[k] = v;
        return d;
    }
}
