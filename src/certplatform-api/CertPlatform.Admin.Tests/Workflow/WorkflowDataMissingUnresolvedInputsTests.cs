using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Shared.Exceptions;
using CertPlatform.Shared.Workflow.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CertPlatform.Admin.Tests.Workflow;

/// <summary>
/// ★ 「未取到上游值的连线端口」<b>在失败/穿透路径上也不能丢</b>单测（2026-10-16 修）。
///
/// <para><b>被修的缺陷（用户 2026-10-16 上报）</b>：在 NC 配置页点「运行测试」测
/// 「聚合多路judge」（<c>collect_judges</c>）节点，响应只有一句</para>
/// <code>节点执行失败: Skill 'collect_judges' 执行失败[PRECONDITION_EMPTY]: …未收到任何 judge 结果</code>
/// <para>而<b>没有任何一个字</b>提到「这几个端口本次没取到值」。根因：<c>PRECONDITION_EMPTY</c> 会被
/// <c>NodeExecutor</c> 转成 <see cref="WorkflowDataMissingException"/> 且<b>按设计穿透</b>
/// （交给 <c>WorkflowInterpreter</c> 落补录缺口），于是挂在
/// <c>NodeExecutionResult.UnresolvedInputs</c> 上的诊断<b>永远回不到调用方</b>。</para>
///
/// <para><b>修后</b>：该清单随异常一起带出（<see cref="WorkflowDataMissingException.UnresolvedInputs"/>），
/// 单节点测试入口据此回传前端并写进错误文案。</para>
///
/// <para><b>⚠️ 本套带负向对照</b>：常量端口（<c>ConstantOnly</c>）必须**不进**清单 ——
/// 否则会把「已经填好的常量」也报成「未执行上游」。</para>
/// </summary>
public class WorkflowDataMissingUnresolvedInputsTests
{
    // ══════════════════════════════════════════════════════════════
    // 1. ★ 核心：PRECONDITION_EMPTY 穿透时清单必须还在
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 用户上报的场景复刻：8 个槽位全是「声明为连线」的端口、本次都取不到上游，
    /// skill 因入参为空报 <c>PRECONDITION_EMPTY</c>
    /// ⇒ 穿透出来的异常必须带着这 8 个端口名。
    /// </summary>
    [Fact]
    public async Task 前置为空的异常穿透时_携带未取到值的端口清单()
    {
        var ports = new[] { "judge_1", "judge_2" };

        var ex = await Assert.ThrowsAsync<WorkflowDataMissingException>(() =>
            RunSkill(WorkflowErrorCodes.PreconditionEmpty,
                inputs: ports.ToDictionary(p => p, _ => "stamp_judge_level_n1"),
                inputPorts: Ports(ports.Select(p => (p, "Link")).ToArray()),
                sharedOutputs: new Dictionary<string, object>()));   // ★ 单节点测试：不执行上游

        Assert.Equal(ports, ex.UnresolvedInputs);
        Assert.Equal("skill_param", ex.DataKind);
    }

    /// <summary>
    /// 负向对照（判据确实在起作用）：<b>同一份输入</b>，只把端口声明的 <c>BindMode</c> 从
    /// <c>Link</c> 换成 <c>ConstantOnly</c> ⇒ 常量被保留（原样返回）、<b>不进</b>清单。
    /// <para>理由：前端 <c>ConstantOnly</c> 端口是个纯输入框，恒带着过期的 <c>"link"</c> 类型过来；
    /// 若按类型判定，会把已经填好的常量一起打成「未取到值」。</para>
    /// </summary>
    [Fact]
    public async Task 负向对照_ConstantOnly端口不进清单()
    {
        var ex = await Assert.ThrowsAsync<WorkflowDataMissingException>(() =>
            RunSkill(WorkflowErrorCodes.PreconditionEmpty,
                inputs: new Dictionary<string, string> { ["judge_1"] = "企业资料不全" },
                inputPorts: Ports(("judge_1", "ConstantOnly")),      // ★ 真声明
                sharedOutputs: new Dictionary<string, object>()));

        // 空集合（⛔ 不是 null —— 引擎恒赋数组，消费方不必判 null）
        Assert.True(ex.UnresolvedInputs is null or { Count: 0 },
            "ConstantOnly 端口已填常量，不该被报成「未执行上游」：" +
            string.Join("、", ex.UnresolvedInputs ?? new List<string>()));
    }

    // ══════════════════════════════════════════════════════════════
    // 2. 非穿透路径：清单仍在（结果对象上）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// <c>CONFIG_ERROR</c> / <c>SYSTEM_ERROR</c> 走的是「<b>不抛、返回失败结果</b>」的路
    /// ⇒ 清单挂在 <c>NodeExecutionResult.UnresolvedInputs</c>（2026-10-15 已修），本用例防它回归。
    /// </summary>
    [Fact]
    public async Task 非穿透路径_失败结果上仍带端口清单()
    {
        var r = await RunSkill(
            WorkflowErrorCodes.ConfigError,
            inputs: new Dictionary<string, string> { ["judge_1"] = "stamp_judge_level_n1" },
            inputPorts: Ports(("judge_1", "Link")),
            sharedOutputs: new Dictionary<string, object>());

        Assert.False(r.Success);
        Assert.Equal(WorkflowErrorCodes.ConfigError, r.ErrorCode);
        Assert.Equal(new[] { "judge_1" }, r.UnresolvedInputs);
    }

    /// <summary>成功路径不受影响：上游已执行 ⇒ 无清单、无失败。</summary>
    [Fact]
    public async Task 成功路径_清单为空()
    {
        var r = await RunSkill(
            errorCode: null,   // ★ null = stub 返回成功
            inputs: new Dictionary<string, string> { ["judge_1"] = "stamp_judge_level_n1" },
            inputPorts: Ports(("judge_1", "Link")),
            sharedOutputs: new Dictionary<string, object> { ["stamp_judge_level_n1"] = "真实上游值" });

        Assert.True(r.Success, r.Error);
        Assert.Empty(r.UnresolvedInputs);
    }

    // ── 私有工具 ──

    private static List<PortConfig> Ports(params (string Name, string BindMode)[] ports) =>
        ports.Select(p => new PortConfig { Name = p.Name, BindMode = p.BindMode }).ToList();

    /// <summary>
    /// 走真实引擎路径执行一个 <c>skill</c> 节点；skill 注册表用 stub 直接返回指定分类码的失败
    /// （本用例要验的是<b>引擎</b>对失败/穿透的处理，不是某个 skill 的业务逻辑）。
    /// </summary>
    private static async Task<NodeExecutionResult> RunSkill(
        string? errorCode,
        Dictionary<string, string> inputs,
        List<PortConfig> inputPorts,
        Dictionary<string, object> sharedOutputs)
    {
        var executor = new NodeExecutor(
            skillRegistry: new StubSkillRegistry(errorCode),
            aiNodeExecutor: null!,
            db: null!,
            wfLogger: new WorkflowLogger(NullLogger<WorkflowLogger>.Instance),
            logger: NullLogger<NodeExecutor>.Instance,
            resolver: null!,
            core: null!,
            storage: null!);

        var node = new WorkflowNodeConfig
        {
            NodeId = "collect_judges_n1",
            NodeType = "skill",
            SkillCode = "collect_judges",
            Title = "聚合多路judge",
            Inputs = inputs,
            InputTypes = inputs.Keys.ToDictionary(k => k, _ => "link"),
            InputPorts = inputPorts,
            Config = new Dictionary<string, object>()
        };

        return await executor.ExecuteAsync(
            node, "task1", "item1", sharedOutputs,
            new Dictionary<string, object>(), CancellationToken.None);
    }

    /// <summary>固定的失败 skill（避免把 skill 业务逻辑混进引擎行为断言）</summary>
    private class StubSkillRegistry : ISkillRegistry
    {
        private readonly string? _errorCode;
        public StubSkillRegistry(string? errorCode) => _errorCode = errorCode;

        public Task<SkillMetadata?> LoadAsync(string skillCode, CancellationToken ct = default)
            => Task.FromResult<SkillMetadata?>(null);

        /// <summary><c>_errorCode</c> 为空 ⇒ 返回成功（供「成功路径不受影响」用例）</summary>
        public Task<SkillResult> ExecuteAsync(string skillCode, SkillContext context, CancellationToken ct = default)
            => Task.FromResult(_errorCode == null
                ? SkillResult.Ok(new Dictionary<string, object> { ["result"] = "stub 值" })
                : SkillResult.Fail($"stub: {skillCode} 失败", _errorCode));
    }
}
