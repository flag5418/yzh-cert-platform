using System.Collections.Generic;
using System.Linq;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Shared.Workflow.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CertPlatform.Admin.Tests.Workflow;

/// <summary>
/// ★ <b>跨边界 config 键契约</b>单测（2026-10-16，P2）。
///
/// <para><b>要固化的契约</b>：后端读 <c>node.Config</c> / <c>param.SourceConfig</c> 一律走
/// <see cref="WorkflowConfigAccess"/>（<b>大小写不敏感</b>）。因此——</para>
/// <list type="number">
/// <item>存量 <c>rule_json</c> 的 <b>camelCase</b> 键（<c>docType</c>/<c>params</c>/…）继续命中；</item>
/// <item>迁移后前端写出的 <b>PascalCase</b> 键（<c>DocType</c>/<c>Params</c>/…）同样命中；</item>
/// <item>负向对照：<b>键不存在</b>时必须返回 <c>null</c>（证明「不是随便取一个值」的假通过）。</item>
/// </list>
///
/// <para>本夹具的形状对齐 <see cref="WorkflowConfigParserPortTests"/> 所述「真实快照混用大小写」，
/// 覆盖 docfield / doctable / constant / ai 四类消费 <c>config</c> 的节点。</para>
/// </summary>
public class WorkflowConfigContractTests
{
    /// <summary>真实形状：外层 camelCase，node 内 config 混用 camel（旧）/ Pascal（新）。</summary>
    private const string RealShapeRuleJson = """
    {
      "Version": 1,
      "WorkflowType": "validation",
      "Nodes": [
        { "nodeId": "start_n1", "nodeType": "start", "Title": "开始",
          "Config": {}, "Inputs": {}, "inputTypes": {}, "inputPorts": [], "outputPorts": [] },
        { "nodeId": "docfield_n1", "nodeType": "docfield", "Title": "文档字段",
          "Config": { "docType": "standard", "ruleCode": "R1", "fieldCode": "F1" },
          "Inputs": {}, "inputTypes": {}, "inputPorts": [], "outputPorts": [] },
        { "nodeId": "doctable_n1", "nodeType": "doctable", "Title": "文档表格",
          "Config": { "DocType": "enterprise", "RuleCode": "R2", "TableCode": "T2" },
          "Inputs": {}, "inputTypes": {}, "inputPorts": [], "outputPorts": [] },
        { "nodeId": "const_n1", "nodeType": "constant", "Title": "常量",
          "Config": { "value": "hello" },
          "Inputs": {}, "inputTypes": {}, "inputPorts": [], "outputPorts": [] },
        { "nodeId": "ai_n1", "nodeType": "ai_node", "Title": "AI",
          "Config": {
            "promptTemplate": "结论：{{p1}}",
            "outputType": "json",
            "model": "qwen-max",
            "systemPrompt": "你是审核助手",
            "params": [
              { "name": "p1", "sourceType": "constant", "value": "v1" }
            ]
          },
          "Inputs": {}, "inputTypes": {}, "inputPorts": [], "outputPorts": [] },
        { "nodeId": "end_n1", "nodeType": "end", "Title": "结束",
          "Config": {}, "Inputs": {}, "inputTypes": {}, "inputPorts": [], "outputPorts": [] }
      ],
      "Edges": [
        { "Source": "start_n1", "Target": "docfield_n1" },
        { "Source": "docfield_n1", "Target": "doctable_n1" },
        { "Source": "doctable_n1", "Target": "const_n1" },
        { "Source": "const_n1", "Target": "ai_n1" },
        { "Source": "ai_n1", "Target": "end_n1" }
      ]
    }
    """;

    private static WorkflowConfigParser Parser() =>
        new(NullLogger<WorkflowConfigParser>.Instance);

    private static Dictionary<string, WorkflowNodeConfig> Nodes()
        => Parser().Parse(RealShapeRuleJson).NodeMap;

    // ─── ① 入口本身的语义 ───────────────────────────────────────────

    [Fact]
    public void 入口_精确命中优先()
    {
        var dict = new Dictionary<string, object> { ["DocType"] = "pascal", ["docType"] = "camel" };

        Assert.Equal("pascal", dict.GetConfigValue("DocType")?.ToString());
        Assert.Equal("camel", dict.GetConfigValue("docType")?.ToString());
    }

    [Fact]
    public void 入口_忽略大小写命中()
    {
        var dict = new Dictionary<string, object> { ["DocType"] = "standard" };

        Assert.Equal("standard", dict.GetConfigValue("docType")?.ToString());
        Assert.Equal("standard", dict.GetConfigValue("DOCTYPE")?.ToString());
        Assert.Equal("standard", dict.GetConfigString("dOcTyPe"));
    }

    [Fact]
    public void 入口_负向对照_不存在的键返回空()
    {
        var dict = new Dictionary<string, object> { ["DocType"] = "standard" };

        Assert.Null(dict.GetConfigValue("DocTypeX"));
        Assert.False(dict.TryGetConfigValue("nope", out var v));
        Assert.Null(v);
        Assert.Equal("fallback", dict.GetConfigString("nope", "fallback"));
    }

    // ─── ② 真实形状：camel（旧）与 Pascal（新）键都能读到 ──────────────

    [Fact]
    public void docfield_旧camelCase配置可读()
    {
        var node = Nodes()["docfield_n1"];

        Assert.Equal("standard", node.GetConfigString("DocType"));
        Assert.Equal("R1", node.GetConfigString("RuleCode"));
        Assert.Equal("F1", node.GetConfigString("FieldCode"));
    }

    [Fact]
    public void docfield_同时用camel字面量也能读()
    {
        var node = Nodes()["docfield_n1"];

        // 迁移期前端仍写 camelCase ⇒ 入口必须两边都命中
        Assert.Equal("standard", node.GetConfigString("docType"));
        Assert.Equal("R1", node.GetConfigString("ruleCode"));
        Assert.Equal("F1", node.GetConfigString("fieldCode"));
    }

    [Fact]
    public void doctable_新PascalCase配置可读()
    {
        var node = Nodes()["doctable_n1"];

        Assert.Equal("enterprise", node.GetConfigString("DocType"));
        Assert.Equal("R2", node.GetConfigString("RuleCode"));
        Assert.Equal("T2", node.GetConfigString("TableCode"));
    }

    [Fact]
    public void constant_常量值可读且能回落()
    {
        var node = Nodes()["const_n1"];

        Assert.Equal("hello", node.GetConfigString("Value"));
        Assert.Equal("", node.GetConfigString("ValueX"));
    }

    // ─── ③ AI 节点：promptTemplate / params / model / systemPrompt ─────

    [Fact]
    public void ai节点_提示词与模型配置可读()
    {
        var node = Nodes()["ai_n1"];

        Assert.Equal("结论：{{p1}}", node.GetConfigString(AiParams.PromptKey));
        Assert.Equal("json", node.GetConfigString("OutputType"));
        Assert.Equal("qwen-max", node.GetConfigString("Model"));
        Assert.Equal("你是审核助手", node.GetConfigString("SystemPrompt"));
    }

    [Fact]
    public void ai节点_旧camelCase键params可解析出参数表()
    {
        var node = Nodes()["ai_n1"];

        var decls = AiParams.ParseDeclaredParams(node);

        var only = Assert.Single(decls);
        Assert.Equal("p1", only.Name);
        Assert.Equal("constant", only.SourceType);
        Assert.Equal("v1", only.Value?.ToString());
    }

    [Fact]
    public void ai节点_新PascalCase键Params同样可解析()
    {
        var node = Nodes()["ai_n1"];
        node.Config["Params"] = node.Config["params"];   // 模拟迁移后前端写成 PascalCase
        node.Config.Remove("params");

        var decls = AiParams.ParseDeclaredParams(node);

        Assert.Single(decls);
        Assert.Equal("p1", decls[0].Name);
    }

    [Fact]
    public void ai节点_负向对照_键名不符则无参数()
    {
        var node = Nodes()["ai_n1"];
        node.Config.Remove("params");
        node.Config["ParamsX"] = "[]";   // 形似但不同

        Assert.Empty(AiParams.ParseDeclaredParams(node));
    }

    [Fact]
    public void ai节点_提示词引用参与依赖收集_新旧提示词键均可()
    {
        System.Func<string, string?> resolve = key => key == "docfield_n1" ? "docfield_n1" : null;

        var node = Nodes()["ai_n1"];
        // 换成「引用节点标题」的模板：未被声明为参数名 ⇒ 应识别为一条依赖
        node.Config["promptTemplate"] = "字段：{{docfield_n1.result}}";

        var camelIds = AiParams.CollectSourceNodeIds(node, resolve);
        Assert.Equal(new[] { "docfield_n1" }, camelIds);

        // 迁移后：提示词键写成 PascalCase，结果必须一致
        node.Config["PromptTemplate"] = node.Config["promptTemplate"];
        node.Config.Remove("promptTemplate");

        var pascalIds = AiParams.CollectSourceNodeIds(node, resolve);
        Assert.Equal(new[] { "docfield_n1" }, pascalIds);
    }
}
