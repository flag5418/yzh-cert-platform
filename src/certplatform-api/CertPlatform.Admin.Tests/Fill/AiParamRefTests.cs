using System.Collections.Generic;
using CertPlatform.Admin.Services.Ent;
using CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai;
using Xunit;

namespace CertPlatform.Admin.Tests.Fill;

/// <summary>
///     <b>AI 节点「带参数」</b>的契约测试 —— 参数引用列表（<c>SourceSpecEntry.AiParamRefs</c>）
///     与提示词替换（<c>AiFillPromptBuilder.RenderParamRefs</c>）。
///
///     <para><b>★ 为什么这一组必须有</b>：设计（<c>48</c> §2.2 ① / <c>61</c> S-1 / 原型 V4）要求
///     「AI 节点勾选全局参数，提示词里用 <c>{{参数}}</c> 引用」，但在此之前
///     <b>整个运行期没有任何一处读 <c>HasParam</c> / 参数引用 / <c>Prompt</c></b> ——
///     AI 节点 ② 段是<b>配了不生效</b>的假功能：用户在抽屉里选了参数、写了提示词、点了保存，
///     模型收到的却是「备注」字段，两边都不报错（静默失效）。</para>
///
///     <para><b>规格原话</b>（用户 2026-10-09）：「三个 ai 节点，有 3 个参数：1 参数 2 是否依赖企业资料
///     3 提示词」「ai 节点需要选择全局参数，引用到提示词中，<b>可能需要多个全局参数</b>」。</para>
/// </summary>
public class AiParamRefTests
{
    // ════════════════════════════════════════════════════════════════
    //  SourceSpecEntry.AiParamRefs —— 参数引用列表（多选 + 老数据兼容）
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void AiParamRefs_多参数_按顺序返回()
    {
        var e = new SourceSpecEntry
        {
            Kind = "ai_field",
            HasParam = true,
            Params = new List<string> { "enterprise.Name", "质量方针" },
        };

        Assert.Equal(new[] { "enterprise.Name", "质量方针" }, e.AiParamRefs());
    }

    [Fact]
    public void AiParamRefs_去空去重保序()
    {
        var e = new SourceSpecEntry
        {
            Kind = "ai_field",
            HasParam = true,
            Params = new List<string> { " a ", "", "a", "  ", "b" },
        };

        Assert.Equal(new[] { "a", "b" }, e.AiParamRefs());
    }

    [Fact]
    public void AiParamRefs_老数据_单值ref回退成单元素列表()
    {
        // 2026-10-09 之前 UI 只能单选一个参数 ⇒ 库里可能是 Kind=ai_field + HasParam + Ref
        var e = new SourceSpecEntry
        {
            Kind = "ai_field",
            HasParam = true,
            Ref = "enterprise.Name",
            Params = new List<string>(),
        };

        Assert.Equal(new[] { "enterprise.Name" }, e.AiParamRefs());
    }

    [Fact]
    public void AiParamRefs_新写法优先于老_ref()
    {
        var e = new SourceSpecEntry
        {
            Kind = "ai_field",
            HasParam = true,
            Ref = "old_code",
            Params = new List<string> { "new_a", "new_b" },
        };

        Assert.Equal(new[] { "new_a", "new_b" }, e.AiParamRefs());
    }

    [Fact]
    public void AiParamRefs_未勾选参数_即使有ref也返回空()
    {
        // HasParam=false ⇒「参数：无」⇒ 引用列表必须为空（否则执行期会去取值，与配置矛盾）
        var e = new SourceSpecEntry
        {
            Kind = "ai_semantic",
            HasParam = false,
            Ref = "enterprise.Name",
        };

        Assert.Empty(e.AiParamRefs());
    }

    // ════════════════════════════════════════════════════════════════
    //  AiFillPromptBuilder.RenderParamRefs —— {{参数}} 真替换
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void RenderParamRefs_单参数_替换成取值()
    {
        var text = AiFillPromptBuilder.RenderParamRefs(
            "请依据 {{enterprise.Name}} 的经营范围作答。",
            new Dictionary<string, string?> { ["enterprise.Name"] = "河北雄安尚龙认证有限公司" });

        Assert.Equal("请依据 河北雄安尚龙认证有限公司 的经营范围作答。", text);
        Assert.DoesNotContain("{{", text);
    }

    [Fact]
    public void RenderParamRefs_多参数_全部替换()
    {
        var text = AiFillPromptBuilder.RenderParamRefs(
            "{{enterprise.Name}}（统一社会信用代码 {{enterprise.CreditCode}}）",
            new Dictionary<string, string?>
            {
                ["enterprise.Name"] = "某某公司",
                ["enterprise.CreditCode"] = "91330XXX",
            });

        Assert.Equal("某某公司（统一社会信用代码 91330XXX）", text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RenderParamRefs_缺值_替换成未提供_绝不把token留给模型(string? value)
    {
        var text = AiFillPromptBuilder.RenderParamRefs(
            "范围：{{质量方针}}",
            new Dictionary<string, string?> { ["质量方针"] = value });

        Assert.Equal("范围：（未提供）", text);
        // ★ 关键：不能让模型看到字面量 token，否则它会当成正文抄进答案
        Assert.DoesNotContain("{{质量方针}}", text);
    }

    [Fact]
    public void RenderParamRefs_未引用的参数_不产生副作用()
    {
        var text = AiFillPromptBuilder.RenderParamRefs(
            "没有任何引用",
            new Dictionary<string, string?> { ["unused"] = "X" });

        Assert.Equal("没有任何引用", text);
    }

    [Fact]
    public void RenderParamRefs_空提示词_返回空串()
    {
        Assert.Equal(string.Empty,
            AiFillPromptBuilder.RenderParamRefs("", new Dictionary<string, string?> { ["a"] = "1" }));
    }

    [Fact]
    public void RenderParamRefs_键为空_跳过()
    {
        var text = AiFillPromptBuilder.RenderParamRefs(
            "原文",
            new Dictionary<string, string?> { [""] = "X" });

        Assert.Equal("原文", text);
    }

    [Fact]
    public void RenderParamRefs_同一参数引用多次_全部替换()
    {
        var text = AiFillPromptBuilder.RenderParamRefs(
            "{{p}} 与 {{p}}",
            new Dictionary<string, string?> { ["p"] = "值" });

        Assert.Equal("值 与 值", text);
    }
}
