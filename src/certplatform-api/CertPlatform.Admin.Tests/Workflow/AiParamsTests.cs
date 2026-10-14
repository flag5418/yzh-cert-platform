using System.Text.Json;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Shared.Workflow.Models;
using Xunit;

namespace CertPlatform.Admin.Tests.Workflow;

/// <summary>
/// ★ AI 节点「参数表」唯一口径单测（设计 §2.1 / §2.2 / §2.7）。
///
/// <para>覆盖三件事：</para>
/// <list type="number">
/// <item><b>解析口径</b>：`config.params` 在落库前（数组）与落库后（JSON 字符串 / JsonElement）
///   三种形态都要吃得下 —— 口径错一个字，参数就静默解析成空表。</item>
/// <item><b>依赖边收集</b>（参数即边）：`params[].nodeId` + 提示词正则兜底两条来源，
///   且兜底必须能用<b>节点标题</b>反查出节点 ID（老写法 {{中文标题.端口}}）。</item>
/// <item><b>不许误伤</b>：已登记的参数名（p1）不得被当成节点；skill/constant 来源不产生依赖边。</item>
/// </list>
/// </summary>
public class AiParamsTests
{
    // ═══════════════════════════════════════════════
    // 1. ParseDeclaredParams：三种落库形态
    // ═══════════════════════════════════════════════

    [Fact]
    public void 参数表_数组形态_解析出全部字段()
    {
        var node = NodeWithConfig("""
            {"params":[{"name":"p1","label":"营业执照号","sourceType":"node","nodeId":"docField_n2","portName":"result"}]}
            """);

        var decls = AiParams.ParseDeclaredParams(node);

        var d = Assert.Single(decls);
        Assert.Equal("p1", d.Name);
        Assert.Equal("营业执照号", d.Label);
        Assert.Equal("node", d.SourceType);
        Assert.Equal("docField_n2", d.NodeId);
        Assert.Equal("result", d.PortName);
        Assert.True(d.IsNodeSource);
    }

    /// <summary>
    /// 反序列化成 <c>Dictionary&lt;string,object&gt;</c> 时，System.Text.Json 会把值装箱成
    /// <c>JsonElement</c> —— 这是测试路径（请求体）与运行路径（ruleJson）最常见的形态。
    /// </summary>
    [Fact]
    public void 参数表_JsonElement形态_同样解析得出()
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(
            """{"params":[{"name":"p2","nodeId":"docTable_n3"}]}""")!;
        var node = new WorkflowNodeConfig { Config = dict };

        var d = Assert.Single(AiParams.ParseDeclaredParams(node));

        Assert.Equal("p2", d.Name);
        Assert.Equal("docTable_n3", d.NodeId);
        Assert.Equal("node", d.SourceType);       // 缺省 sourceType ⇒ node
        Assert.Equal("result", d.PortName);       // 缺省端口 ⇒ result
    }

    [Fact]
    public void 参数表_字符串化JSON_同样解析得出()
    {
        var node = NodeWithConfig("""
            {"params":"[{\"name\":\"p3\",\"nodeId\":\"n9\"}]"}
            """);

        var d = Assert.Single(AiParams.ParseDeclaredParams(node));
        Assert.Equal("p3", d.Name);
        Assert.Equal("n9", d.NodeId);
    }

    [Theory]
    [InlineData(null)]                                  // 没有 params 键
    [InlineData("{}")]                                  // 空对象
    [InlineData("""{"params":[]}""")]                   // 空数组
    [InlineData("""{"params":"这不是JSON"}""")]           // 坏 JSON ⇒ 静默空表，⛔ 不许抛
    [InlineData("""{"params":{"name":"p1"}}""")]         // 形状不对（对象而非数组）
    public void 参数表_缺失或坏数据_一律返回空且不抛(string? configJson)
    {
        var node = configJson == null
            ? new WorkflowNodeConfig { Config = new Dictionary<string, object>() }
            : NodeWithConfig(configJson);

        Assert.Empty(AiParams.ParseDeclaredParams(node));
    }

    [Fact]
    public void 参数表_无名条目被丢弃_但其余条目不受影响()
    {
        var node = NodeWithConfig("""
            {"params":[{"name":"","nodeId":"n1"},{"name":"p1","nodeId":"n2"},{"name":"   ","nodeId":"n3"}]}
            """);

        var d = Assert.Single(AiParams.ParseDeclaredParams(node));
        Assert.Equal("p1", d.Name);
    }

    [Fact]
    public void 参数表_null节点_返回空不抛()
    {
        Assert.Empty(AiParams.ParseDeclaredParams(null));
    }

    // ═══════════════════════════════════════════════
    // 2. CollectSourceNodeIds：参数即边
    // ═══════════════════════════════════════════════

    [Fact]
    public void 依赖边_参数表里的nodeId_按声明序收集且去重()
    {
        var node = NodeWithConfig("""
            {"params":[
              {"name":"p1","sourceType":"node","nodeId":"docField_n2"},
              {"name":"p2","sourceType":"node","nodeId":"docTable_n3"},
              {"name":"p3","sourceType":"node","nodeId":"docField_n2"},
              {"name":"p4","sourceType":"constant","value":"固定值"},
              {"name":"p5","sourceType":"skill"}
            ]}
            """);

        var ids = AiParams.CollectSourceNodeIds(node);

        Assert.Equal(new[] { "docField_n2", "docTable_n3" }, ids);
    }

    /// <summary>
    /// 老写法：参数表不存在，提示词里写 <c>{{文档字段.result}}</c>。
    /// refKey 是<b>节点标题</b>，必须经解析器反查成<b>节点 ID</b> —— 直接把标题当 ID 会找不到节点，
    /// 预执行就静默跳过（这正是「参数即边」最容易漏的一处）。
    /// </summary>
    [Fact]
    public void 依赖边_提示词老写法_标题反查成节点ID()
    {
        var node = NodeWithConfig("""
            {"promptTemplate":"请判断 {{文档字段.result}} 是否等于 {{docTable_n3.rows}}"}
            """);

        var ids = AiParams.CollectSourceNodeIds(node, refKey => refKey switch
        {
            "文档字段" => "docField_n2",
            "docTable_n3" => "docTable_n3",
            _ => null
        });

        Assert.Equal(new[] { "docField_n2", "docTable_n3" }, ids);
    }

    /// <summary>
    /// §2.7.1 兼容策略：参数表<b>已登记</b>的参数名不许再当节点解析 ——
    /// 否则标题恰好叫 p1 的节点会被误拉进预执行。
    /// </summary>
    [Fact]
    public void 依赖边_已登记参数名_不再当节点()
    {
        var node = NodeWithConfig("""
            {"promptTemplate":"{{p1}} 与 {{文档字段.result}} 比对",
             "params":[{"name":"p1","sourceType":"node","nodeId":"docField_n2"}]}
            """);

        // 解析器对任何 refKey 都说「是节点」—— 只有 p1 应该被排除
        var ids = AiParams.CollectSourceNodeIds(node, _ => "任意节点");

        Assert.Equal(new[] { "docField_n2", "任意节点" }, ids);
    }

    [Fact]
    public void 依赖边_无参数且无提示词_返回空()
    {
        var node = NodeWithConfig("""{"outputType":"string"}""");

        Assert.Empty(AiParams.CollectSourceNodeIds(node));
    }

    // ═══════════════════════════════════════════════
    // 3. SplitRef：与前端 splitRef 同口径
    // ═══════════════════════════════════════════════

    [Theory]
    [InlineData("p1", "p1", "")]                        // 短参数名（无端口）
    [InlineData("文档字段.result", "文档字段", "result")]  // 中文标题 + 端口
    [InlineData("docField_n2.result", "docField_n2", "result")]
    [InlineData("", "", "")]
    public void 拆引用_与前端splitRef逐字一致(string fullKey, string refKey, string portName)
    {
        var (actualRef, actualPort) = AiParams.SplitRef(fullKey);

        Assert.Equal(refKey, actualRef);
        Assert.Equal(portName, actualPort);
    }

    /// <summary>占位符正则必须与前端 <c>PLACEHOLDER_PATTERN</c> 同口径（三套口径不一致是本次改造的病根）。</summary>
    [Fact]
    public void 占位符正则_同时认参数名中文标题与nodeId()
    {
        const string template = "{{p1}} {{文档字段.result}} {{docField_n2.result}}";

        var keys = PromptRenderer.Placeholder.Matches(template)
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Equal(new[] { "p1", "文档字段.result", "docField_n2.result" }, keys);
    }

    // ═══════════════════════════════════════════════

    private static WorkflowNodeConfig NodeWithConfig(string configJson)
    {
        var config = JsonSerializer.Deserialize<Dictionary<string, object>>(configJson)!;
        return new WorkflowNodeConfig { NodeId = "ai_n6", NodeType = "ai_node", Config = config };
    }
}
