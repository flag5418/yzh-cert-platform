using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Admin.Services.Workflow.Skills.Fill;
using CertPlatform.Shared.Workflow.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CertPlatform.Admin.Tests.Workflow;

/// <summary>
/// ★ 入参完备性校验的「<b>锅的归属</b>」分类单测（2026-10-15 修）。
///
/// <para><b>被修的缺陷</b>：引擎 <c>SkillExecutor.CollectMissingInputs</c> 把<b>所有</b>
/// 「声明不允许为空、但实际为空」的参数一律报 <c>PRECONDITION_EMPTY</c>。而该码的语义是
/// 「<b>消费节点发现上游没给值</b>」，引擎动作 = <b>全局中断当前检查项 + 落补录缺口 + 标「不可判定」</b>
/// ⇒ 处置人是<b>企业</b>。</para>
///
/// <para>于是 <c>src_manual.anchor_code</c>（<c>ConstantOnly</c>，<b>只能由规则作者在画布上填</b>）
/// 为空时，系统会给企业开一个<b>它永远填不了</b>的补录缺口 —— 把「作者没配」伪装成「数据缺失」。
/// 实测报文：<c>{"Inputs":{"anchor_code":""}}</c> ⇒
/// <c>[PRECONDITION_EMPTY] src_manual 参数为空且该参数不允许为空: anchor_code</c>。
/// <i>（原举例 <c>stamp_judge_level.judge_code</c> 已于 2026-10-16 删除 —— 该标识改取节点名称，
/// 画布上不再有「只能作者填」的必填框。）</i></para>
///
/// <para><b>修后判据</b> = 这个值「有没有可能来自上游」：只能手填/选字典的
/// （<c>ConstantOnly</c>/<c>Enum</c>），或端口确实没来源的 ⇒ <c>CONFIG_ERROR</c>（处置人 = 规则作者）；
/// 端口确有来源、只是取到空 ⇒ 仍 <c>PRECONDITION_EMPTY</c>（数据缺口）。</para>
///
/// <para><b>⚠️ 本套测试必须带负向对照</b>：否则「恒返回 CONFIG_ERROR」也能全绿。</para>
/// </summary>
public class SkillParamCompletenessTests
{
    // ══════════════════════════════════════════════════════════════
    // 1. 上报的那个 case：ConstantOnly 参数为空 ⇒ 必须是配置错
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// ★ 回归：<c>src_manual.anchor_code</c> 为空 ⇒ <c>CONFIG_ERROR</c>（⛔ 不是 PRECONDITION_EMPTY）。
    /// <para>这是一条真实上报过的报文形状：<c>Inputs</c> 里一个 <c>ConstantOnly</c> 必填参数为空。</para>
    /// </summary>
    [Fact]
    public void ConstantOnly参数为空_报配置错而非数据缺口()
    {
        var r = Run(typeof(SrcManualSkill),
            inputs: new Dictionary<string, object>
            {
                ["anchor_code"] = ""             // ★ 空（ConstantOnly + Deny）
            },
            connectedPorts: Array.Empty<string>());

        Assert.False(r.Success);
        Assert.Equal(WorkflowErrorCodes.ConfigError, r.ErrorCode);

        // 消息要指向「配置」而不是「数据」
        Assert.Contains("配置不完整", r.Error);
        Assert.Contains("anchor_code", r.Error);

        // ⛔ 旧措辞必须消失（它读起来像「数据没到」，会把人引向「去补录」）
        Assert.DoesNotContain("参数为空且该参数不允许为空", r.Error);
        Assert.DoesNotContain("上游未提供必要数据", r.Error);
    }

    /// <summary>负向对照：<c>anchor_code</c> 填了 ⇒ 必须成功（证明上一条不是因为「恒失败」）。</summary>
    [Fact]
    public void 负向对照_填了anchorCode则正常执行()
    {
        var r = Run(typeof(SrcManualSkill),
            inputs: new Dictionary<string, object>
            {
                ["anchor_code"] = "锚点A"
            },
            connectedPorts: Array.Empty<string>());

        Assert.True(r.Success, r.Error);
        Assert.Null(r.ErrorCode);
    }

    // ══════════════════════════════════════════════════════════════
    // 2. Enum（字典选择）同样是「只能作者提供」
    // ══════════════════════════════════════════════════════════════

    /// <summary><c>compare.operator</c> 是 <c>Enum</c> + <c>Deny</c> ⇒ 空 = 作者没选 ⇒ 配置错。</summary>
    [Fact]
    public void Enum参数为空_报配置错()
    {
        var r = Run(typeof(CompareSkill),
            inputs: new Dictionary<string, object>
            {
                ["value_a"] = "1",
                ["value_b"] = "2",
                ["operator"] = ""               // ★ 空
            },
            connectedPorts: new[] { "value_a", "value_b" });

        Assert.False(r.Success);
        Assert.Equal(WorkflowErrorCodes.ConfigError, r.ErrorCode);
        Assert.Contains("operator", r.Error);
    }

    // ══════════════════════════════════════════════════════════════
    // 3. LinkOrConstant：端口「有没有来源」决定锅的归属
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 端口<b>确实没有来源</b>（连接性判据可用、且不含该端口）⇒ 谈不上「上游没给值」
    /// ⇒ 配置错。
    /// </summary>
    [Fact]
    public void 端口未接且无常量_报配置错()
    {
        var r = Run(typeof(CompareSkill),
            inputs: new Dictionary<string, object>
            {
                ["value_a"] = "",               // ★ 空，且不在 connectedPorts 里
                ["value_b"] = "2",
                ["operator"] = "=="
            },
            connectedPorts: new[] { "value_b", "operator" });   // 非空集合，但不含 value_a

        Assert.False(r.Success);
        Assert.Equal(WorkflowErrorCodes.ConfigError, r.ErrorCode);
        Assert.Contains("value_a", r.Error);
    }

    /// <summary>
    /// 端口<b>确有来源</b>（在 <c>ConnectedPorts</c> 里）但取到空 ⇒ 这是真·数据缺口
    /// ⇒ 必须保持 <c>PRECONDITION_EMPTY</c>（否则会把「合法的上游为空」误报成配置错）。
    /// </summary>
    [Fact]
    public void 端口已接但取到空_报数据缺口()
    {
        var r = Run(typeof(CompareSkill),
            inputs: new Dictionary<string, object>
            {
                ["value_a"] = "",               // ★ 空，但在 connectedPorts 里 ⇒ 接了没给值
                ["value_b"] = "2",
                ["operator"] = "=="
            },
            connectedPorts: new[] { "value_a", "value_b", "operator" });

        Assert.False(r.Success);
        Assert.Equal(WorkflowErrorCodes.PreconditionEmpty, r.ErrorCode);
        Assert.Contains("上游未提供必要数据", r.Error);
        Assert.Contains("value_a", r.Error);
    }

    /// <summary>
    /// 保守策略：连接性判据<b>缺失</b>（<c>ConnectedPorts</c> 为空集合 —— 如上下文未装配、
    /// 直接单测调用）⇒ <b>不误杀</b>，回落成数据缺口。
    /// </summary>
    [Fact]
    public void 连接性判据缺失时_保守回落为数据缺口()
    {
        var r = Run(typeof(CompareSkill),
            inputs: new Dictionary<string, object>
            {
                ["value_a"] = "",
                ["value_b"] = "2",
                ["operator"] = "=="
            },
            connectedPorts: Array.Empty<string>());   // ★ 判据不可用

        Assert.False(r.Success);
        Assert.Equal(WorkflowErrorCodes.PreconditionEmpty, r.ErrorCode);
    }

    /// <summary>
    /// 负向对照（分类确实分流）：同一个 skill、同一份空输入，**只改连接性判据**，
    /// 错误码必须<b>不同</b> —— 证明上面两条不是碰巧同色。
    /// </summary>
    [Fact]
    public void 负向对照_同输入不同连接性判据得到不同错误码()
    {
        Dictionary<string, object> inputs() => new()
        {
            ["value_a"] = "",
            ["value_b"] = "2",
            ["operator"] = "=="
        };

        var notConnected = Run(typeof(CompareSkill), inputs(), new[] { "value_b", "operator" });
        var connected = Run(typeof(CompareSkill), inputs(), new[] { "value_a", "value_b", "operator" });

        Assert.Equal(WorkflowErrorCodes.ConfigError, notConnected.ErrorCode);
        Assert.Equal(WorkflowErrorCodes.PreconditionEmpty, connected.ErrorCode);
        Assert.NotEqual(notConnected.ErrorCode, connected.ErrorCode);
    }

    // ══════════════════════════════════════════════════════════════
    // 3. ★ 引擎信封形状（三层同形）—— 2026-10-16 删冗余键 outputs
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 引擎包装后的节点输出 = <b>业务键平铺 + 信封键</b>，且**不含冗余的 <c>outputs</c> 键**。
    /// <para>为什么删：业务键本就平铺到顶层，下游取端口走 <c>NodeExecutor.PickFromOutput</c>
    /// （按目标端口名取同名键 → <c>result</c> → 唯一键 → 兜底），全仓无任何读取方；
    /// 原 <c>outputs["outputs"] = business</c> 只是把业务键整包复制一份，撑大节点输出与
    /// <c>wf_node_execution</c> 落库体积。本用例锁死契约：一旦有人把该键加回来即失败。</para>
    /// </summary>
    [Fact]
    public void 引擎信封_含统一信封键且不含冗余outputs键()
    {
        // is_empty：无 [FromService] ⇒ 纯代码路径，结果可预期（"x" ⇒ 非空）
        var r = Run(typeof(IsEmptySkill),
            new Dictionary<string, object> { ["value"] = "x" },
            new[] { "value" });

        Assert.True(r.Success);

        // 信封键（三层同形）
        Assert.Contains("success", r.Outputs.Keys);
        Assert.Contains("errorCode", r.Outputs.Keys);
        Assert.Contains("error", r.Outputs.Keys);
        Assert.Contains("result", r.Outputs.Keys);

        // 业务键平铺（is_empty 自带 state / kind）
        Assert.Contains("state", r.Outputs.Keys);
        Assert.False((bool)r.Outputs["result"]);

        // ★ 冗余键已删
        Assert.DoesNotContain("outputs", r.Outputs.Keys);
    }

    // ── 私有工具 ──

    /// <summary>
    /// 走**真实引擎路径**（<c>SkillExecutor.ExecuteAsync</c>）执行一个 skill。
    /// <para>⚠️ 必须走引擎 —— 待测逻辑就在引擎里；直接调 skill 静态方法会绕过它。</para>
    /// </summary>
    private static SkillResult Run(Type skillType, Dictionary<string, object> inputs, IEnumerable<string> connectedPorts)
    {
        // 这些 skill 都没有 [FromService] 参数 ⇒ null! 提供者不会被触碰
        var executor = new SkillExecutor(null!, NullLogger<SkillExecutor>.Instance);

        var ports = new HashSet<string>(connectedPorts, StringComparer.Ordinal);

        var ctx = new SkillContext
        {
            Inputs = inputs,
            ConnectedPorts = ports,
            // [FromContext] 的绑定通道（与本套测试无关，但保持与 NodeExecutor 同形）
            ContextParams = new Dictionary<string, object> { [WorkflowContextKeys.ConnectedPorts] = ports },
            NodeId = "test_node",
            WorkflowInstanceId = "test:item"
        };

        return executor.ExecuteAsync(skillType.FullName!, "ExecuteAsync", ctx, CancellationToken.None)
            .GetAwaiter().GetResult();
    }
}
