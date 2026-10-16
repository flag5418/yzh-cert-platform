using System.Collections.Generic;
using System.Linq;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Shared.Workflow.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CertPlatform.Admin.Tests.Workflow;

/// <summary>
/// ★ <b>真实运行路径的端口声明保真</b>单测（2026-10-15）。
///
/// <para><b>为什么必须有它</b>：<c>NodeExecutor.IsDeclaredLinkPort</c> 的判据来自
/// <c>WorkflowNodeConfig.InputPorts[].BindMode</c>。真实运行（非配置期测试）时，这个字段是
/// <b>从落库的 <c>rule_json</c> 反序列化</b>出来的 —— 一旦反序列化把它丢掉，
/// 「连线端口取不到值 ⇒ 置空 + 回传清单」这个修复在<b>生产链路上就是惰性的</b>，
/// 而且<b>不报任何错</b>（正是本项目最典型的静默失效）。</para>
///
/// <para><b>本套用库里真实快照的形状</b>：<c>inputPorts</c> 里的字段是
/// <b>PascalCase</b>（<c>Name</c> / <c>BindMode</c>），而外层键是 camelCase
/// （<c>nodeId</c> / <c>inputPorts</c>）—— 两套大小写混用，靠
/// <c>WorkflowConfigParser.JsonOptions.PropertyNameCaseInsensitive = true</c> 才对得上。
/// 这里把这个依赖<b>显式固化</b>。</para>
/// </summary>
public class WorkflowConfigParserPortTests
{
    /// <summary>库里真实快照的形状（PascalCase 端口字段 + camelCase 外层键）。</summary>
    private const string RealShapeRuleJson = """
    {
      "Version": 1,
      "WorkflowType": "validation",
      "Nodes": [
        { "nodeId": "start_n1", "nodeType": "start", "Title": "开始",
          "Config": {}, "Inputs": {}, "inputTypes": {}, "inputPorts": [], "outputPorts": [] },
        { "nodeId": "stamp_judge_level_n1", "nodeType": "skill", "Title": "judge盖章级别",
          "skillCode": "stamp_judge_level", "Config": {},
          "Inputs": { "result": "docField_n1", "judge_code": "J1", "verdict": "is_empty_n1" },
          "inputTypes": { "result": "link", "judge_code": "link", "verdict": "link" },
          "inputPorts": [
            { "Name": "result",     "BindMode": "LinkOrConstant" },
            { "Name": "judge_code", "BindMode": "ConstantOnly"   },
            { "Name": "verdict",    "BindMode": "LinkOrConstant" },
            { "Name": "no_bind_mode" }
          ],
          "outputPorts": [] },
        { "nodeId": "end_n1", "nodeType": "end", "Title": "结束",
          "Config": {}, "Inputs": { "result": "stamp_judge_level_n1" },
          "inputTypes": { "result": "link" }, "inputPorts": [], "outputPorts": [] }
      ],
      "Edges": [
        { "Source": "start_n1", "Target": "stamp_judge_level_n1" },
        { "Source": "stamp_judge_level_n1", "Target": "end_n1" }
      ]
    }
    """;

    private static WorkflowNodeConfig ParseSkillNode()
    {
        var parser = new WorkflowConfigParser(NullLogger<WorkflowConfigParser>.Instance);
        var parsed = parser.Parse(RealShapeRuleJson);
        return parsed.NodeMap["stamp_judge_level_n1"];
    }

    /// <summary>
    /// ★ 核心：真实形状下 <c>InputPorts[].BindMode</c> <b>必须被保留</b>。
    /// <para>若此条失败 ⇒ <c>NodeExecutor</c> 的「常量保护」判据在生产链路上失效。</para>
    /// </summary>
    [Fact]
    public void 真实ruleJson形状_BindMode被保留()
    {
        var node = ParseSkillNode();

        // 先证「端口声明本身没被丢掉」（否则下面的断言可能因为空集合而假通过）
        Assert.Equal(4, node.InputPorts.Count);

        var byName = node.InputPorts.ToDictionary(p => p.Name, p => p.BindMode);
        Assert.Equal("LinkOrConstant", byName["result"]);
        Assert.Equal("ConstantOnly", byName["judge_code"]);
        Assert.Equal("LinkOrConstant", byName["verdict"]);
    }

    /// <summary>
    /// 负向对照：<b>没写 <c>BindMode</c></b> 的端口 ⇒ 取 <see cref="PortConfig"/> 的默认值 <c>"Link"</c>。
    /// <para>证明上一条读到的是<b>真实属性</b>（而不是某种「总是返回同一个常量」的假通过）。</para>
    /// </summary>
    [Fact]
    public void 负向对照_未声明BindMode的端口取默认值()
    {
        var node = ParseSkillNode();
        var port = node.InputPorts.Single(p => p.Name == "no_bind_mode");

        Assert.Equal("Link", port.BindMode);   // PortConfig.BindMode 的默认值
    }

    /// <summary>大小写兼容：外层键 <c>inputPorts</c>（camelCase）能被 <c>InputPorts</c> 属性接住。</summary>
    [Fact]
    public void 外层camelCase键被接住()
    {
        var node = ParseSkillNode();
        Assert.NotEmpty(node.InputPorts);
        Assert.Equal("stamp_judge_level", node.SkillCode);
    }

    /// <summary>
    /// 驼峰 <c>bindMode</c> 也应可用（未来若有人把序列化策略改成 camelCase，不至于静默失效）。
    /// </summary>
    [Fact]
    public void 驼峰bindMode同样被接住()
    {
        const string camel = """
        {
          "Version": 1, "WorkflowType": "validation",
          "Nodes": [
            { "nodeId": "start_n1", "nodeType": "start", "Inputs": {}, "inputPorts": [] },
            { "nodeId": "s_n1", "nodeType": "skill", "skillCode": "stamp_judge_level",
              "Inputs": { "result": "src_n1" }, "inputTypes": { "result": "link" },
              "inputPorts": [ { "name": "result", "bindMode": "ConstantOnly" } ] },
            { "nodeId": "end_n1", "nodeType": "end", "Inputs": { "result": "s_n1" }, "inputPorts": [] }
          ],
          "Edges": [
            { "Source": "start_n1", "Target": "s_n1" },
            { "Source": "s_n1", "Target": "end_n1" }
          ]
        }
        """;

        var parser = new WorkflowConfigParser(NullLogger<WorkflowConfigParser>.Instance);
        var parsed = parser.Parse(camel);

        var port = Assert.Single(parsed.NodeMap["s_n1"].InputPorts);
        Assert.Equal("result", port.Name);
        Assert.Equal("ConstantOnly", port.BindMode);
    }

    /// <summary>
    /// ★ 端到端：真实形状的 rule_json → 解析 → 交给 <c>NodeExecutor</c>，
    /// 「声明为连线的端口取不到上游值」必须被识别（证明两个环节的口径确实对上了）。
    /// </summary>
    [Fact]
    public async System.Threading.Tasks.Task 真实形状下_未解析连线端口被识别()
    {
        var node = ParseSkillNode();
        node.NodeType = "end";   // 用纯本地节点，避免触碰 skill / AI / 取数器

        var executor = new NodeExecutor(
            skillRegistry: null!,
            aiNodeExecutor: null!,
            db: null!,
            wfLogger: new WorkflowLogger(NullLogger<WorkflowLogger>.Instance),
            logger: NullLogger<NodeExecutor>.Instance,
            resolver: null!);

        var r = await executor.ExecuteAsync(
            node, "t", "i",
            new Dictionary<string, object>(),          // 无上游（模拟单节点测试）
            new Dictionary<string, object>(),
            System.Threading.CancellationToken.None);

        // result / verdict 声明为连线且无上游 ⇒ 进清单；judge_code 是 ConstantOnly ⇒ 不进
        Assert.Contains("result", r.UnresolvedInputs!);
        Assert.Contains("verdict", r.UnresolvedInputs!);
        Assert.DoesNotContain("judge_code", r.UnresolvedInputs!);
    }
}
