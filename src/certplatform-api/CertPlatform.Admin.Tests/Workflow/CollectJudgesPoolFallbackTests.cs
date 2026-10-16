using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Shared.Workflow.Models;
using Xunit;

namespace CertPlatform.Admin.Tests.Workflow;

/// <summary>
/// ★ <c>collect_judges</c> 的<b>判定池回落</b>单测（2026-10-16 修）。
///
/// <para><b>被修的缺陷</b>：本节点是「多条 judge 汇聚」的兼容通道，而引擎的跨路径复用
/// （<c>WorkflowInterpreter.ExecutePathAsync</c>）使汇聚节点<b>只看得见第一条路径</b>；
/// 与此同时 <c>nc_conclusion_v2</c> 的口径是「<b>连线优先</b>，留空才读判定清单池」。
/// ⇒ 存量画布（<c>stamp → collect → nc_conclusion_v2</c>）会把<b>残缺清单盖过刚登记好的完整池</b>：
/// 结论基于残缺清单，<b>两边都不报错</b>（用户 2026-10-15 上报的正是这类「静默错结论」）。</para>
///
/// <para><b>修后口径</b>（与 <c>nc_conclusion_v2</c> 逐字同一条）：槽位优先；<b>槽位一条都没接到</b>时
/// 改用本次检查已登记的判定清单；<b>两个来源都空</b>才 Fail（<c>PRECONDITION_EMPTY</c>）。</para>
///
/// <para><b>⚠️ 本套必须带负向对照</b>：否则「无条件改用池」也能让回落用例全绿 ——
/// 而那会把「作者接错线」变成一个永远查不出来的静默覆盖。</para>
/// </summary>
public class CollectJudgesPoolFallbackTests
{
    // ══════════════════════════════════════════════════════════════
    // 1. 槽位全空 ⇒ 回落判定池
    // ══════════════════════════════════════════════════════════════

    /// <summary>槽位全空 + 池里有 2 条 ⇒ 成功，且 2 条一条不少（★ 不是 1 条 = 只看得见第一条路径的那个缺陷）。</summary>
    [Fact]
    public async Task 槽位全空且池有判定_改用池且条数不少()
    {
        var r = await CollectJudgesSkill.ExecuteAsync(
            judge_pool: new List<object> { Stamped("J1", "major", "fail"), Stamped("J2", "minor", "pass") });

        Assert.True(r.Success, r.Error);

        var judges = Judges(r);
        Assert.Equal(2, judges.Count);
        Assert.Equal(new[] { "J1", "J2" }, judges.Select(j => j.GetProperty("judge_code").GetString()));

        // ★ 诊断面：必须能看出「这批判定是从池来的」，否则用户无从判断汇聚是否收齐
        Assert.Equal("judge_pool", r.Outputs["judges_source"]);
        Assert.Equal(2, r.Outputs["judges_count"]);
    }

    /// <summary>回落必须是<b>可解释</b>的：带一条 warning（⛔ 不静默换源）。</summary>
    [Fact]
    public async Task 槽位全空且池有判定_带出来源说明()
    {
        var r = await CollectJudgesSkill.ExecuteAsync(
            judge_pool: new List<object> { Stamped("J1", "major", "fail") });

        Assert.True(r.Success, r.Error);
        Assert.Contains("已登记的判定清单", (string)r.Outputs["warning"]);
        // 成功率下调（0.9）—— 与「槽位正常采齐」区分开
        Assert.Equal(0.9, r.Confidence);
    }

    // ══════════════════════════════════════════════════════════════
    // 2. ★ 负向对照：槽位有值 ⇒ 池必须被忽略
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 槽位接到 1 条、池里有 5 条 ⇒ 只用槽位的那 1 条。
    /// <para>理由：池是「本次检查已登记的全部判定」，而槽位是<b>作者显式接线</b>的意图；
    /// 若此处改用池，等于把「接错线」变成静默覆盖（作者永远发现不了自己漏接了）。</para>
    /// </summary>
    [Fact]
    public async Task 负向对照_槽位有值时忽略池()
    {
        var r = await CollectJudgesSkill.ExecuteAsync(
            judge_1: Stamped("手接的", "major", "fail"),
            judge_pool: new List<object>
            {
                Stamped("池1", "major", "fail"), Stamped("池2", "major", "fail"),
                Stamped("池3", "major", "fail"), Stamped("池4", "major", "fail"),
                Stamped("池5", "major", "fail")
            });

        Assert.True(r.Success, r.Error);
        var judges = Judges(r);
        Assert.Single(judges);
        Assert.Equal("手接的", judges[0].GetProperty("judge_code").GetString());
        Assert.Equal("slots", r.Outputs["judges_source"]);
    }

    // ══════════════════════════════════════════════════════════════
    // 3. 两个来源都空 ⇒ 保持原语义（Fail 中断）
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 槽位全空 + 池为空/未装配 ⇒ 仍 <c>Fail(PRECONDITION_EMPTY)</c>。
    /// <para>这正是用户 2026-10-16 上报的报文（单节点测试「聚合多路judge」）——
    /// 单节点测试没有 item 级池，故本语义<b>必须保持</b>：无判定可算 ≠ 通过。</para>
    /// </summary>
    [Fact]
    public async Task 槽位全空且池为空_仍然失败()
    {
        var noPool = await CollectJudgesSkill.ExecuteAsync();
        var emptyPool = await CollectJudgesSkill.ExecuteAsync(judge_pool: new List<object>());

        Assert.False(noPool.Success);
        Assert.Equal(WorkflowErrorCodes.PreconditionEmpty, noPool.ErrorCode);
        Assert.False(emptyPool.Success);
        Assert.Equal(WorkflowErrorCodes.PreconditionEmpty, emptyPool.ErrorCode);
    }

    /// <summary>失败文案要同时点出<b>两个</b>来源为空（否则用户会以为「池里有判定却没被用上」）。</summary>
    [Fact]
    public async Task 失败文案同时说明两个来源均为空()
    {
        var r = await CollectJudgesSkill.ExecuteAsync();

        Assert.Contains("槽位全空", r.Error);
        Assert.Contains("未登记任何判定", r.Error);
    }

    // ── 私有工具 ──

    /// <summary>stamp_judge_level 登记进池的<b>真实形状</b>（= 该 skill 的主输出字典）</summary>
    private static Dictionary<string, object> Stamped(string judgeCode, string level, string verdict) => new()
    {
        ["judge_code"] = judgeCode,
        ["level"] = level,
        ["verdict"] = verdict,
        ["reason"] = "测试用",
        ["result"] = new Dictionary<string, object> { ["ok"] = verdict != "pass" },
        ["level_defaulted"] = false
    };

    private static List<JsonElement> Judges(SkillResult r)
    {
        var json = (string)r.Outputs["judges"];
        return JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();
    }
}
