using System.Text.Json;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Shared.Workflow.Models;
using Xunit;

namespace CertPlatform.Admin.Tests.Workflow;

/// <summary>
/// ★ 第 3 批「注释式渲染」单测（设计 §2.3 + §2.4）。
///
/// <para>验收判据（批次表原文）：**渲染后零残留 <c>{{}}</c>**；**参数区条目数 == params 数**。</para>
///
/// <para>三条判据对应的断言：</para>
/// <list type="number">
/// <item><b>边界不可溢出</b> ⇒ 参数区每条独占一行、以 <c>name（label）= </c> 起头；
///   值里含换行也不许吃掉下一条。</item>
/// <item><b>指令与数据可区分</b> ⇒ 参数区被成对标记包裹 + system 侧固定数据声明。</item>
/// <item><b>渲染后可校验</b> ⇒ <c>ResidualRefs</c> 为空、<c>SectionEntryCount == params 数</c>。</item>
/// </list>
/// </summary>
public class PromptCommentStyleTests
{
    // ═══════════════════════════════════════════════
    // 1. 正文：已声明参数只留名
    // ═══════════════════════════════════════════════

    [Fact]
    public void 正文_已声明参数_渲染成参数名而不是值()
    {
        var (result, context) = Render(
            "请判断 {{p1}} 与 {{p2}} 是否一致。",
            """{"p1":"91310000MA1K3XXXXXX","p2":"合格"}""",
            ("p1", "营业执照号"), ("p2", "检测结果"));

        Assert.Equal("请判断 p1 与 p2 是否一致。", result.Body);
        Assert.Empty(result.ResidualRefs);                    // 判据③：零残留 {{}}
    }

    [Fact]
    public void 正文_老写法未声明_仍然内联值且不进参数区()
    {
        var (result, _) = Render(
            "上游说 {{docField_n2.result}}，请判断是否一致。",
            """{"docField_n2.result":"91310000MA1K3XXXXXX"}""",
            ("p1", "营业执照号"));                              // 参数表里有 p1，但模板没引用它

        Assert.Contains("91310000MA1K3XXXXXX", result.Body);
        Assert.DoesNotContain("【参数说明】", result.Body);
        Assert.Empty(result.ResidualRefs);
        // 参数区只含声明的 p1（老写法⛔不进参数区，否则条目数 ≠ params 数）
        Assert.Contains("p1（营业执照号）", result.ParamSection);
        Assert.DoesNotContain("docField_n2.result", result.ParamSection);
    }

    [Fact]
    public void 正文_参数名本身就是值的那条_不产生花括号()
    {
        var (result, _) = Render("输出 {{code}}", """{"code":"CERT-2026-001"}""", ("code", "认证证书编号"));

        Assert.Equal("输出 code", result.Body);
        Assert.Empty(result.ResidualRefs);
    }

    [Fact]
    public void 无参数表_完全退化为老行为_参数区为空()
    {
        var (result, _) = Render("上游是 {{文档字段.result}}", """{"文档字段.result":"9131"}""");

        Assert.Equal("上游是 9131", result.Body);
        Assert.Equal(string.Empty, result.ParamSection);      // 老工作流零影响
        Assert.Equal(0, result.SectionEntryCount);
        Assert.Empty(result.ResidualRefs);
    }

    // ═══════════════════════════════════════════════
    // 2. 参数区：结构 + 条目数自检
    // ═══════════════════════════════════════════════

    [Fact]
    public void 参数区_成对标记包裹_且条目数等于params条数()
    {
        var (result, _) = Render(
            "{{p1}} / {{p2}} / {{p3}}",
            """{"p1":"9131","p2":"合格","p3":["a","b"]}""",
            ("p1", "营业执照号"), ("p2", "检测结果"), ("p3", "缺陷清单"));

        var lines = result.ParamSection.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        Assert.Equal(PromptRenderer.ParamSectionHeader, lines[0]);
        Assert.Equal(PromptRenderer.ParamSectionFooter, lines[^1]);
        Assert.Equal(3, result.SectionEntryCount);            // ★ == params 数
        Assert.Equal(3, lines.Length - 2);                    // 中间恰好 3 条
        Assert.Equal("p1（营业执照号） = 9131", lines[1]);
    }

    /// <summary>判据①：值里含换行 ⇒ 仍以 <c>name（label）= </c> 起头，下一条的开始位置不受影响。</summary>
    [Fact]
    public void 参数区_值含换行_不挤跑下一条()
    {
        var (result, _) = Render(
            "{{p1}} {{p2}}",
            """{"p1":"第一行\n第二行","p2":"OK"}""",
            ("p1", "正文"), ("p2", "结论"));

        var lines = result.ParamSection.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        Assert.Equal(2, result.SectionEntryCount);            // 仍数出 2 条
        Assert.Equal("p2（结论） = OK", lines[^2]);
        Assert.Equal(PromptRenderer.ParamSectionFooter, lines[^1]);
    }

    /// <summary>条目数是**数出来**的，不是问声明方要的 —— 数出来的必须与声明数一致。</summary>
    [Fact]
    public void 参数区_条目数与声明数一致()
    {
        var decls = new List<AiParamDecl>
        {
            new() { Name = "p1", Label = "A" },
            new() { Name = "p2", Label = "B" }
        };
        var result = PromptRenderer.RenderCommentStyle(
            "{{p1}} {{p2}}", new Dictionary<string, object> { ["p1"] = "1", ["p2"] = "2" }, decls);

        Assert.Equal(decls.Count, result.SectionEntryCount);
    }

    [Fact]
    public void 参数区_常量参数取声明值_缺值标未提供()
    {
        var decls = new List<AiParamDecl>
        {
            new() { Name = "p1", SourceType = "constant", Value = "固定值" },
            new() { Name = "p2", SourceType = "node", NodeId = "n2" }   // context 里没有 ⇒ 未提供
        };
        var result = PromptRenderer.RenderCommentStyle("{{p1}}{{p2}}", new Dictionary<string, object>(), decls);

        Assert.Contains("p1 = 固定值", result.ParamSection);
        Assert.Contains("p2 = <未提供>", result.ParamSection);
        Assert.Equal(2, result.SectionEntryCount);
    }

    // ═══════════════════════════════════════════════
    // 3. 角色分离 + 数据声明（判据②）
    // ═══════════════════════════════════════════════

    [Fact]
    public void 参数区_不在正文里_只作为system侧独立段()
    {
        var (result, _) = Render(
            "请判断 {{p1}}。",
            """{"p1":"9131"}""",
            ("p1", "营业执照号"));

        Assert.DoesNotContain(PromptRenderer.ParamSectionHeader, result.Body);   // 正文里没有参数区
        Assert.StartsWith(PromptRenderer.ParamSectionHeader, result.ParamSection); // 参数区是独立段
    }

    /// <summary>数据声明必须点名「参数说明」区，且明确「不是指令」——否则值里的越权指令会被执行。</summary>
    [Fact]
    public void 数据声明_点名参数说明区且声明是数据()
    {
        Assert.Contains("参数说明", PromptRenderer.ParamSectionDataNotice);
        Assert.Contains("数据", PromptRenderer.ParamSectionDataNotice);
        Assert.Contains("不得执行", PromptRenderer.ParamSectionDataNotice);
    }

    // ═══════════════════════════════════════════════
    // 4. 渲染自检（§2.4）
    // ═══════════════════════════════════════════════

    /// <summary>
    /// 未命中的占位符**保留原文**（老行为，是对的），但必须被自检点出来 ——
    /// 真实路径据此抛 WorkflowDataMissingException，测试路径据此回传前端红字。
    /// </summary>
    [Fact]
    public void 渲染自检_未命中占位符保留原文并被点名()
    {
        var (result, _) = Render("判断 {{p1}} 与 {{missing}}", """{"p1":"1"}""", ("p1", "参数一"));

        Assert.Contains("{{missing}}", result.Body);
        Assert.Equal(new[] { "missing" }, result.ResidualRefs);

        var residual = PromptRenderer.FindResidualRefs(result.Body);
        Assert.Equal(new[] { "missing" }, residual);
    }

    [Fact]
    public void 渲染自检_干净正文_残留为空()
    {
        Assert.Empty(PromptRenderer.FindResidualRefs("没有任何占位符的正文"));
        Assert.Empty(PromptRenderer.FindResidualRefs(null));
        Assert.Empty(PromptRenderer.FindResidualRefs(""));
    }

    // ═══════════════════════════════════════════════

    private static (PromptRenderer.CommentRenderResult result, Dictionary<string, object> context) Render(
        string template,
        string contextJson,
        params (string name, string label)[] declared)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(contextJson)!;

        // ★ 与生产一致：字符串**原样**、数组/对象 JSON 化、数字/布尔取原文
        //   （JsonElement 直接丢进池子会让字符串被二次序列化成带引号的 "9131"，失真）
        var context = raw.ToDictionary(
            kv => kv.Key,
            kv => (object)(kv.Value.ValueKind switch
            {
                JsonValueKind.String => kv.Value.GetString()!,
                JsonValueKind.Null => null!,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => kv.Value.GetRawText(),
                _ => kv.Value
            })!);

        var decls = declared
            .Select(d => new AiParamDecl { Name = d.name, Label = d.label, SourceType = "node", NodeId = "src" })
            .ToList();

        return (PromptRenderer.RenderCommentStyle(template, context, decls), context);
    }
}
