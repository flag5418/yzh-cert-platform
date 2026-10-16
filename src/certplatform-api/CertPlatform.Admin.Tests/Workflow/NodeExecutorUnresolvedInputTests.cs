using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Shared.Workflow.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CertPlatform.Admin.Tests.Workflow;

/// <summary>
/// ★ 「连线端口取不到上游值」的处理单测（2026-10-15 修）。
///
/// <para><b>被修的缺陷</b>：配置期<b>单节点测试</b>刻意不执行上游
/// （<c>RunSingleNodeAsync</c> 的 <c>sharedOutputs</c> 传空）⇒ 连线端口本来就没值。
/// 而 <c>NodeExecutor.ResolveInputValue</c> 修复前的做法是
/// <b>只写一条 <c>LogWarning</c>，然后把「源节点 id」当业务值返回</b> ⇒
/// 一个字符串 <c>"is_empty_n1"</c> 被当成 <c>verdict</c> 送进节点 ⇒ 节点只能报
/// 「verdict 非法：is_empty_n1」，把<b>测试环境的能力边界</b>伪装成
/// <b>节点的配置错误</b>（用户实测上报）。</para>
///
/// <para><b>修后</b>：声明为连线的端口取不到值 ⇒ 值置 <c>null</c>
/// （= 诚实的「没有值」）+ 记入 <c>NodeExecutionResult.UnresolvedInputs</c> 回传前端。</para>
///
/// <para><b>★★ 本套最关键的负向对照 = 「常量保护」</b>：前端
/// <c>PortControl</c> 里 <c>ConstantOnly</c> 端口是个纯输入框、<b>从不发 <c>update:inputType</c></b>
/// ⇒ 它的 <c>inputType</c> 恒为默认的 <c>"link"</c>。若按前端送来的 <c>inputTypes</c> 判定，
/// 会把<b>已经填好的常量</b>（如 <c>judge_code = "企业资料不全"</c>）一起打成 <c>null</c>。
/// 故判据必须是端口<b>声明的 <c>BindMode</c></b>（见 <c>NodeExecutor.IsDeclaredLinkPort</c>）。</para>
/// </summary>
public class NodeExecutorUnresolvedInputTests
{
    // ══════════════════════════════════════════════════════════════
    // 1. 核心行为：未解析的连线端口
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 声明为连线的端口取不到上游值 ⇒ <b>值置 null</b>（⛔ 不是源节点 id）+ 记入清单。
    /// <para>这正是用户上报的场景：<c>verdict</c> 连到 <c>is_empty_n1</c>，
    /// 而 <c>is_empty_n1</c> 在单节点测试里根本没被执行。</para>
    /// </summary>
    [Fact]
    public async Task 声明为连线的端口取不到上游值_值置空并记入清单()
    {
        var r = await Run("end",
            inputs: new Dictionary<string, string> { ["result"] = "is_empty_n1" },
            inputTypes: new Dictionary<string, string> { ["result"] = "link" },
            inputPorts: Ports(("result", "LinkOrConstant")),
            sharedOutputs: new Dictionary<string, object>());   // ★ 单节点测试：没有上游

        Assert.Equal(new[] { "result" }, r.UnresolvedInputs);

        // ★ 核心断言：值**不是**那个节点 id（修复前这里会是 "is_empty_n1"）
        Assert.NotEqual("is_empty_n1", r.Output["result"] as string);
    }

    // ══════════════════════════════════════════════════════════════
    // 2. ★★ 负向对照：常量必须被保护（否则会连带打死 judge_code）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// ★ <c>ConstantOnly</c> 端口带着**过期的 <c>"link"</c> 类型**过来（前端实测如此）
    /// ⇒ 必须仍按<b>常量</b>处理：原样保留、不进清单。
    /// </summary>
    [Fact]
    public async Task 负向对照_ConstantOnly端口带过期link类型_仍按常量保留()
    {
        var r = await Run("end",
            inputs: new Dictionary<string, string> { ["result"] = "企业资料不全" },
            inputTypes: new Dictionary<string, string> { ["result"] = "link" },   // ★ 过期类型
            inputPorts: Ports(("result", "ConstantOnly")),                        // ★ 真声明
            sharedOutputs: new Dictionary<string, object>());

        Assert.Empty(r.UnresolvedInputs);
        Assert.Equal("企业资料不全", r.Output["result"]);
    }

    /// <summary>
    /// 端口<b>没声明</b> <c>BindMode</c>（<c>InputPorts</c> 缺失）⇒ <b>保守</b>沿用旧行为：
    /// 原样返回，⛔ 不置 null、不进清单。
    /// <para>理由：此时无法区分「常量」与「未解析连线」，误杀常量比漏报更糟。</para>
    /// </summary>
    [Fact]
    public async Task 保守_端口未声明bindMode时原样返回()
    {
        var r = await Run("end",
            inputs: new Dictionary<string, string> { ["result"] = "某个值" },
            inputTypes: new Dictionary<string, string> { ["result"] = "link" },
            inputPorts: new List<PortConfig>(),                                   // ★ 未声明
            sharedOutputs: new Dictionary<string, object>());

        Assert.Empty(r.UnresolvedInputs);
        Assert.Equal("某个值", r.Output["result"]);
    }

    // ══════════════════════════════════════════════════════════════
    // 3. 正常路径不受影响
    // ══════════════════════════════════════════════════════════════

    /// <summary>上游**确实执行过** ⇒ 正常取到值，且不进清单（证明修复不是「一律置空」）。</summary>
    [Fact]
    public async Task 上游已执行时_正常取值且不进清单()
    {
        var r = await Run("end",
            inputs: new Dictionary<string, string> { ["result"] = "src_n1" },
            inputTypes: new Dictionary<string, string> { ["result"] = "link" },
            inputPorts: Ports(("result", "LinkOrConstant")),
            sharedOutputs: new Dictionary<string, object> { ["src_n1"] = "上游的真实值" });

        Assert.Empty(r.UnresolvedInputs);
        Assert.Equal("上游的真实值", r.Output["result"]);
    }

    /// <summary><c>inputTypes</c> 声明为常量 ⇒ 无论 <c>BindMode</c> 是什么，都原样使用。</summary>
    [Fact]
    public async Task 声明为常量时_原样使用()
    {
        var r = await Run("end",
            inputs: new Dictionary<string, string> { ["result"] = "手填常量" },
            inputTypes: new Dictionary<string, string> { ["result"] = "constant" },
            inputPorts: Ports(("result", "LinkOrConstant")),
            sharedOutputs: new Dictionary<string, object>());

        Assert.Empty(r.UnresolvedInputs);
        Assert.Equal("手填常量", r.Output["result"]);
    }

    /// <summary>
    /// 负向对照（判据确实在起作用）：**同一份输入**，只改端口声明的 <c>BindMode</c>，
    /// 结果必须<b>不同</b> —— 证明上面几条不是碰巧同色。
    /// </summary>
    [Fact]
    public async Task 负向对照_同输入只改bindMode则结果不同()
    {
        Dictionary<string, string> inputs() => new() { ["result"] = "src_n1" };
        Dictionary<string, string> types() => new() { ["result"] = "link" };
        var noUpstream = new Dictionary<string, object>();

        var asLink = await Run("end", inputs(), types(), Ports(("result", "Link")), noUpstream);
        var asConstOnly = await Run("end", inputs(), types(), Ports(("result", "ConstantOnly")), noUpstream);

        Assert.Equal(new[] { "result" }, asLink.UnresolvedInputs);
        Assert.Empty(asConstOnly.UnresolvedInputs);
        Assert.NotEqual(asLink.Output["result"], asConstOnly.Output["result"]);
    }

    // ── 私有工具 ──

    private static List<PortConfig> Ports(params (string Name, string BindMode)[] ports) =>
        ports.Select(p => new PortConfig { Name = p.Name, BindMode = p.BindMode }).ToList();

    /// <summary>
    /// 走**真实引擎路径**（<c>NodeExecutor.ExecuteAsync</c>）执行一个节点。
    ///
    /// <para>⚠️ 必须走引擎 —— 待测逻辑就在引擎里。</para>
    /// <para>用 <c>nodeType = "end"</c>：它是纯本地节点（不碰 skill / AI / 取数器），
    /// 且会把 <c>resolvedInputs["result"]</c> 原样放进输出 ⇒ 可直接断言「值是不是节点 id」。
    /// 其余依赖传 <c>null!</c>（<c>end</c> 分支不会触碰它们）。</para>
    /// </summary>
    private static async Task<NodeExecutionResult> Run(
        string nodeType,
        Dictionary<string, string> inputs,
        Dictionary<string, string> inputTypes,
        List<PortConfig> inputPorts,
        Dictionary<string, object> sharedOutputs)
    {
        var executor = new NodeExecutor(
            skillRegistry: null!,
            aiNodeExecutor: null!,
            db: null!,
            wfLogger: new WorkflowLogger(NullLogger<WorkflowLogger>.Instance),
            logger: NullLogger<NodeExecutor>.Instance,
            resolver: null!);

        var node = new WorkflowNodeConfig
        {
            NodeId = "n1",
            NodeType = nodeType,
            Title = "测试节点",
            Inputs = inputs,
            InputTypes = inputTypes,
            InputPorts = inputPorts,
            Config = new Dictionary<string, object>()
        };

        return await executor.ExecuteAsync(
            node, "task1", "item1",
            sharedOutputs, new Dictionary<string, object>(), CancellationToken.None);
    }
}
