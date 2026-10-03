using System.Text.Json;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Shared.Entities.Cert;
using CertPlatform.Admin.Entities.Cert;
using Xunit;

namespace CertPlatform.Admin.Tests.Nc;

/// <summary>
/// ★ NcPromptAssembler 单元测试
/// <para>核心保障：★ 画布上的 ai_node 只配 promptCode，判定框架由服务端统一装配
/// —— 装配器必须把占位符全部填上，漏填要显式报出（不能静默送出残缺 Prompt）。</para>
/// </summary>
public class NcPromptAssemblerTests
{
    private const string RuleJson = """
    {
      "start": { "NodeId": "s", "NodeType": "start" },
      "dimensions": [
        { "No": 1, "Name": "文件审批记录完整性", "ProcessCode": "P-7.5",
          "Require": "每份受控文件均有审批人签字与审批日期",
          "JudgeCriteria": "抽样 5 份，全部有签字且有日期 → conform；有缺 → nonconform",
          "SeverityIfFailed": "minor" },
        { "No": 2, "Name": "文件变更控制", "ProcessCode": "P-7.5",
          "Require": "文件变更需记录变更原因、内容、审批人",
          "JudgeCriteria": "有变更记录且三项齐全 → conform；记录缺失 → unverifiable",
          "SeverityIfFailed": "minor" }
      ]
    }
    """;

    private static NcJudgePrompt JudgeTpl() => new()
    {
        PromptCode = "nc_judge",
        SystemPrompt = "你是体系认证审核员，只依据材料判断。",
        UserTemplate = "【审核标准】{{__RULE__.clause}}\n"
                     + "【条款原文】{{__RULE__.clauseContent}}\n"
                     + "【检查维度清单】\n{{__RULE__.dimensions}}\n"
                     + "【企业材料】\n{{__DATA__}}\n"
                     + "严格输出 JSON。",
        Temperature = 0.10m,
        MaxTokens   = 2000,
        Version     = 1
    };

    // ═══════════════════════════════════════════════
    // 占位符替换
    // ═══════════════════════════════════════════════
    [Fact]
    public void BuildJudgePrompt_ReplacesAllPlaceholders()
    {
        var (sys, user) = NcPromptAssembler.BuildJudgePrompt(
            JudgeTpl(), RuleJson,
            clauseNumber: "7.5.2", clauseTitle: "文件的标识",
            clauseContent: "组织应确保文件得到唯一标识。",
            dataMaterial: "【文件审批台账】\n共 5 份，3 份无签字");

        // ★ 全部 __ 占位符被替换
        Assert.Empty(NcPromptAssembler.FindUnresolvedPlaceholders(user));
        Assert.Contains("7.5.2 文件的标识", user);
        Assert.Contains("组织应确保文件得到唯一标识", user);
        Assert.Contains("3 份无签字", user);
        Assert.Equal("你是体系认证审核员，只依据材料判断。", sys);
    }

    [Fact]
    public void BuildJudgePrompt_KeepsNodeRefsForEngine()
    {
        // ★ {{节点名.端口}} 必须保留 —— 那是引擎后续解析的
        var tpl = JudgeTpl();
        tpl.UserTemplate += "\n上游数据：{{取数字段.fieldValue}}";

        var (_, user) = NcPromptAssembler.BuildJudgePrompt(
            tpl, RuleJson, "7.5", "标识", "原文", "材料");

        Assert.Contains("{{取数字段.fieldValue}}", user);
    }

    // ═══════════════════════════════════════════════
    // 维度清单渲染
    // ═══════════════════════════════════════════════
    [Fact]
    public void BuildJudgePrompt_RendersAllDimensionFields()
    {
        var (_, user) = NcPromptAssembler.BuildJudgePrompt(
            JudgeTpl(), RuleJson, "7.5", "标识", "原文", "材料");

        Assert.Contains("维度 1：文件审批记录完整性", user);
        Assert.Contains("过程代号：P-7.5", user);
        Assert.Contains("条款要求：每份受控文件均有审批人签字与审批日期", user);
        Assert.Contains("判定标准：抽样 5 份", user);
        Assert.Contains("不符合时严重度：minor", user);
        Assert.Contains("维度 2：文件变更控制", user);
    }

    [Fact]
    public void BuildJudgePrompt_NoDimensions_ShowsWarning()
    {
        var (_, user) = NcPromptAssembler.BuildJudgePrompt(
            JudgeTpl(), """{"start":{"NodeId":"s"}}""", "7.5", "标识", "原文", "材料");

        Assert.Contains("规则未配置检查维度", user);
        Assert.Empty(NcPromptAssembler.FindUnresolvedPlaceholders(user));
    }

    [Fact]
    public void BuildJudgePrompt_EmptyDataMaterial_ShowsWarning()
    {
        var (_, user) = NcPromptAssembler.BuildJudgePrompt(
            JudgeTpl(), RuleJson, "7.5", "标识", "原文", dataMaterial: "");

        Assert.Contains("未提供任何材料", user);
    }

    [Fact]
    public void BuildJudgePrompt_NullClause_ShowsPlaceholder()
    {
        var (_, user) = NcPromptAssembler.BuildJudgePrompt(
            JudgeTpl(), RuleJson, clauseNumber: null, clauseTitle: null, clauseContent: null, "材料");

        Assert.Contains("未提供条款号与标题", user);
        Assert.Contains("（未提供）", user);     // clauseContent
    }

    // ═══════════════════════════════════════════════
    // ★ 未替换占位符检出（发现配置错误的关键）
    // ═══════════════════════════════════════════════
    [Fact]
    public void FindUnresolvedPlaceholders_DetectsLeftover()
    {
        var tpl = JudgeTpl();
        tpl.UserTemplate += "\n{{__RULE__.neverFilled}}";   // ★ 模板里有个不存在的占位符

        var (_, user) = NcPromptAssembler.BuildJudgePrompt(
            tpl, RuleJson, "7.5", "标识", "原文", "材料");

        var unresolved = NcPromptAssembler.FindUnresolvedPlaceholders(user);
        Assert.Contains("{{__RULE__.neverFilled}}", unresolved);
    }

    [Fact]
    public void FindUnresolvedPlaceholders_IgnoresNodeRefs()
    {
        // ★ 节点引用不算"未解析"（引擎后续会处理）
        var tpl = JudgeTpl();
        tpl.UserTemplate += "\n{{节点A.output}} {{节点B.result}}";

        var (_, user) = NcPromptAssembler.BuildJudgePrompt(
            tpl, RuleJson, "7.5", "标识", "原文", "材料");

        Assert.Empty(NcPromptAssembler.FindUnresolvedPlaceholders(user));
    }

    [Fact]
    public void FindUnresolvedPlaceholders_Deduplicates()
    {
        var tpl = JudgeTpl();
        tpl.UserTemplate += "\n{{__X__.a}} {{__X__.a}} {{__X__.b}}";

        var (_, user) = NcPromptAssembler.BuildJudgePrompt(
            tpl, RuleJson, "7.5", "标识", "原文", "材料");

        Assert.Equal(2, NcPromptAssembler.FindUnresolvedPlaceholders(user).Count);
    }

    // ═══════════════════════════════════════════════
    // dimensions 解析
    // ═══════════════════════════════════════════════
    [Fact]
    public void ExtractDimensions_ParsesAllFields()
    {
        var dims = NcPromptAssembler.ExtractDimensions(RuleJson);

        Assert.Equal(2, dims.Count);
        Assert.Equal(1, dims[0].No);
        Assert.Equal("文件审批记录完整性", dims[0].Name);
        Assert.Equal("P-7.5", dims[0].ProcessCode);
        Assert.Equal("minor", dims[0].SeverityIfFailed);
    }

    [Fact]
    public void ExtractDimensions_SortsByNo()
    {
        var json = """{"dimensions":[{"No":3,"Name":"C"},{"No":1,"Name":"A"},{"No":2,"Name":"B"}]}""";
        var dims = NcPromptAssembler.ExtractDimensions(json);
        Assert.Equal(new[] { 1, 2, 3 }, dims.Select(d => d.No));
    }

    [Fact]
    public void ExtractDimensions_InvalidJson_ReturnsEmptyNotThrow()
    {
        // ★ 解析失败不抛异常（会中断工作流）
        var dims = NcPromptAssembler.ExtractDimensions("{不是合法JSON");
        Assert.Empty(dims);
    }

    [Fact]
    public void ExtractDimensions_MissingNo_FiltersOut()
    {
        var json = """{"dimensions":[{"No":0,"Name":"无效"},{"No":1,"Name":"有效"}]}""";
        var dims = NcPromptAssembler.ExtractDimensions(json);
        Assert.Single(dims);
        Assert.Equal(1, dims[0].No);
    }

    [Fact]
    public void ExtractDimensions_NoDimensionsKey_ReturnsEmpty()
    {
        Assert.Empty(NcPromptAssembler.ExtractDimensions("""{"start":{"NodeId":"s"}}"""));
    }

    [Fact]
    public void ExtractDimensions_NullJson_ReturnsEmpty()
    {
        Assert.Empty(NcPromptAssembler.ExtractDimensions(null));
    }

    // ═══════════════════════════════════════════════
    // nc_conclude 装配
    // ═══════════════════════════════════════════════
    [Fact]
    public void BuildConcludePrompt_InjectsRuleConclusionVerbatim()
    {
        var tpl = new NcConcludePrompt
        {
            PromptCode = "nc_conclude",
            SystemPrompt = "你是报告撰写人，不得改变结论等级。",
            UserTemplate = "结论等级：{{__RULE__.conclusion}}\n"
                         + "依据维度：{{__RULE__.basis}}\n"
                         + "未检查项：{{__RULE__.unverified}}\n"
                         + "维度明细：{{__NODE__.dimensions}}\n"
                         + "输出 {\"reason\":\"...\"}"
        };

        var (sys, user) = NcPromptAssembler.BuildConcludePrompt(
            tpl,
            conclusion:    "基本符合",
            basisText:     "维度2、维度3",
            unverifiedText: "维度4",
            dimensionsText: "维度2 发放记录不符合");

        Assert.Contains("结论等级：基本符合", user);
        Assert.Contains("依据维度：维度2、维度3", user);
        Assert.Contains("未检查项：维度4", user);
        Assert.Contains("不得改变结论等级", sys);
        Assert.Empty(NcPromptAssembler.FindUnresolvedPlaceholders(user));
    }

    // ═══════════════════════════════════════════════
    // 材料拼装
    // ═══════════════════════════════════════════════
    [Fact]
    public void BuildDataMaterial_SkipsEmptyItems()
    {
        var text = NcPromptAssembler.BuildDataMaterial(new[]
        {
            new KeyValuePair<string, string>("字段A", "有值"),
            new KeyValuePair<string, string>("字段B", ""),          // ★ 空的应跳过
            new KeyValuePair<string, string>("字段C", "  "),        // ★ 空白应跳过
            new KeyValuePair<string, string>("表格D", "rows=3"),
        });

        Assert.Contains("字段A", text);
        Assert.DoesNotContain("字段B", text);
        Assert.DoesNotContain("字段C", text);
        Assert.Contains("表格D", text);
    }
}
