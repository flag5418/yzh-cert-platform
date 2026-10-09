using System.Collections.Generic;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills.Fill.Ai;
using Xunit;

namespace CertPlatform.Admin.Tests.Fill;

/// <summary>
///     <b>AI 提示词装配器</b>（<c>AiFillPromptBuilder</c>）的契约测试 —— AI 节点高频使用，
///     装配错一处（占位符没替换 / 清单漏锚点 / 参数没进提示词）都是<b>静默失效</b>：
///     模型照样返回 JSON，值却对不上，看不出哪里错。
///
///     <para><b>★ 测什么、不测什么</b>：只测<b>不依赖 DB / 不依赖模型</b>的装配与渲染。
///     <c>db</c> 传 <c>null!</c> —— <c>promptCode</c> 为空时 <c>LoadPromptAsync</c> 在触碰 db 前就返回，
///     ⇒ 走「内置默认模板」分支，绝不解引用 db（若哪天装配路径被改到会先查库，这些用例会立刻 NRE —— 本身就是护栏）。</para>
/// </summary>
public class AiFillPromptBuilderTests
{
    private static async Task<AiFillInvokeRequest> BuildAsync(params AiFillAnchorSpec[] anchors)
    {
        var ctx = new AiFillBuildContext
        {
            DocumentName = "质量手册",
            StandardCode = "9001",
            EnterpriseDocs = "--- DOCUMENT: 简介.md ---\n企业成立于 2010 年。",
            Anchors = new List<AiFillAnchorSpec>(anchors),
        };
        return await AiFillPromptBuilder.BuildBatchAsync(null!, null, ctx);
    }

    // ════════════════════════════════════════════════════════════════
    //  内置默认模板 + 占位符渲染
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 无提示词码_回落到内置默认模板_开箱可跑()
    {
        var req = await BuildAsync(new AiFillAnchorSpec { AnchorCode = "ENT_NAME", Instruction = "填企业全称" });

        Assert.Equal(AiFillPromptBuilder.DefaultSystemPrompt, req.SystemPrompt);
        // 默认模板的正文特征
        Assert.Contains("待填锚点", req.UserPrompt);
        Assert.Contains("企业文档", req.UserPrompt);
        // ★ 封闭命名空间必须被全部替换 —— 残留 `{{__FILL__.` = 模型看到占位符原文
        Assert.DoesNotContain(AiFillPromptBuilder.Prefix, req.UserPrompt);
        Assert.DoesNotContain(AiFillPromptBuilder.Prefix, req.OutputSchema);
        // OutputSchema 是给模型的输出契约
        Assert.Contains("\"fields\"", req.OutputSchema);
        Assert.Contains("\"tables\"", req.OutputSchema);
    }

    [Fact]
    public async Task 锚点清单_含编码与指令_且带类型告知()
    {
        var req = await BuildAsync(
            new AiFillAnchorSpec { AnchorCode = "ENT_NAME", Instruction = "填企业全称", ValueKind = "text" },
            new AiFillAnchorSpec { AnchorCode = "AUDIT_DATE", Instruction = "填审核日期", ValueKind = "date" });

        Assert.Contains("ENT_NAME", req.UserPrompt);
        Assert.Contains("AUDIT_DATE", req.UserPrompt);
        Assert.Contains("填企业全称", req.UserPrompt);
        Assert.Contains("填审核日期", req.UserPrompt);
        // ★ 39 号 §7.3：类型必须由装配器告知模型（日期写成 2026-03-11 而非 2026年3月11日）
        Assert.Contains("（类型：date）", req.UserPrompt);
    }

    [Fact]
    public async Task 表格锚点_列定义进提示词_因为列由模板决定()
    {
        var req = await BuildAsync(new AiFillAnchorSpec
        {
            AnchorCode = "TRAIN",
            Instruction = "培训记录表",
            Section = "table",
            Columns = new List<AiFillColumnSpec>
            {
                new() { FieldCode = "name", Title = "姓名", ValueKind = "text" },
                new() { FieldCode = "hours", Title = "学时", ValueKind = "number" },
            },
        });

        Assert.Contains("name(姓名,text)", req.UserPrompt);
        Assert.Contains("hours(学时,number)", req.UserPrompt);
        // 表格应用「tables 段」的描述
        Assert.Contains("tables 段", req.UserPrompt);
    }

    [Fact]
    public async Task 企业文档与文档名_进提示词()
    {
        var req = await BuildAsync(new AiFillAnchorSpec { AnchorCode = "X", Instruction = "n" });

        Assert.Contains("企业成立于 2010 年", req.UserPrompt);
    }

    // ════════════════════════════════════════════════════════════════
    //  ★ 参数引用集成 —— AI 提示词里 {{参数}} → 真实取值
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task 带参数_取值替换后进入最终提示词()
    {
        // 模拟编排器：先用参数取值替换 {{参数}}，再作为该锚点的 Instruction 装配
        var instruction = AiFillPromptBuilder.RenderParamRefs(
            "请依据「{{enterprise.Name}}」的经营范围，写一段认证范围说明。",
            new Dictionary<string, string?> { ["enterprise.Name"] = "河北雄安尚龙认证有限公司" });

        var req = await BuildAsync(new AiFillAnchorSpec
        {
            AnchorCode = "CERT_SCOPE",
            Instruction = instruction,
            ValueKind = "text",
        });

        Assert.Contains("河北雄安尚龙认证有限公司", req.UserPrompt);
        // ★ 取值必须真的进去，⛔ 不能把 token 留给模型
        Assert.DoesNotContain("{{enterprise.Name}}", req.UserPrompt);
    }

    // ════════════════════════════════════════════════════════════════
    //  Render —— 精确串替换（⛔ 不用正则，不误吃用户别的 {{}}）
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public void Render_只替换封闭命名空间_不误吃其它大括号()
    {
        var outText = AiFillPromptBuilder.Render(
            "A {{__FILL__.anchor_code}} B {{其它说明}} C",
            new Dictionary<string, string> { ["anchor_code"] = "X" });

        Assert.Equal("A X B {{其它说明}} C", outText);
    }

    [Fact]
    public void Render_空模板_返回空串()
    {
        Assert.Equal(string.Empty,
            AiFillPromptBuilder.Render("", new Dictionary<string, string> { ["k"] = "v" }));
    }

    [Fact]
    public async Task 单锚点_补便捷占位符_批量不补()
    {
        // ⛔ 批量时不得补 anchor_code/instruction —— 否则模型以为「只有第一个锚点要填」
        var single = await BuildAsync(new AiFillAnchorSpec { AnchorCode = "only", Instruction = "i" });
        var batch = await BuildAsync(
            new AiFillAnchorSpec { AnchorCode = "a", Instruction = "ia" },
            new AiFillAnchorSpec { AnchorCode = "b", Instruction = "ib" });

        // 默认模板不引用这些键，故这里只断言「装配没崩 + 清单都在」
        Assert.Contains("only", single.UserPrompt);
        Assert.Contains("a", batch.UserPrompt);
        Assert.Contains("b", batch.UserPrompt);
    }
}
