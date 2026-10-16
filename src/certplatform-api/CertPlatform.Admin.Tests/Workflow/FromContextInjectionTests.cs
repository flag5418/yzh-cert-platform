using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CertPlatform.Admin.Services.Workflow.Skills;
using CertPlatform.Shared.Workflow.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CertPlatform.Admin.Tests.Workflow;

/// <summary>
/// ★ 运行上下文注入（[FromContext]）单测 —— 2026-10-15「judge 链路简化与运行上下文自动注入」改造
/// （设计 §4.1 / §4.4 / §4.5 第 1 批）。
///
/// <para>本批验收判据（设计文档原文）：</para>
/// <list type="number">
/// <item><b>键名一致性</b>：枚举全仓 [FromContext] 的 key，断言都在装配处出现
///   —— 键名是字符串，写错不报错、只是静默拿到 null（「配了不生效」）。</item>
/// <item><b>画布端口收窄</b>：nc_conclusion_v2 画布端口 4 → 1（只剩 judges_json）；
///   stamp 的 connectedPorts 不出现在画布上。</item>
/// <item><b>删除即红</b>：关键消费方（stamp → connectedPorts；nc_conclusion_v2 → 判定池 + 组织三编码）
///   被误删时立刻失败。</item>
/// </list>
/// </summary>
public class FromContextInjectionTests
{
    // ═══════════════════════════════════════════════
    // 1. 键名一致性（设计 §4.1：「落地时必须配」）
    // ═══════════════════════════════════════════════

    /// <summary>[FromContext] 的键必须 ∈ WorkflowContextKeys 已定义常量（= 装配方一定会写入的键）</summary>
    [Fact]
    public void 键名一致性_所有FromContext键都在WorkflowContextKeys中定义()
    {
        var declared = DeclaredContextKeys();
        var usages = ScanFromContextUsages();

        Assert.NotEmpty(usages);   // 防「扫描逻辑失效 ⇒ 测试永远绿」

        var unknown = usages.Where(u => !declared.Contains(u.Key)).ToList();
        Assert.True(unknown.Count == 0,
            "以下 [FromContext] 键名不在 WorkflowContextKeys 中定义（拼错不报错、只是静默拿到 null）：\n" +
            string.Join("\n", unknown.Select(u => $"  {u.Skill}.{u.Param} → \"{u.Key}\"")));
    }

    /// <summary>关键消费方确实声明了预期键（防误删回归）</summary>
    [Fact]
    public void 键名一致性_关键消费方声明了预期键()
    {
        var usages = ScanFromContextUsages();
        HashSet<string> KeysOf(string skill) => usages
            .Where(u => u.Skill == skill).Select(u => u.Key).ToHashSet(StringComparer.Ordinal);

        // stamp_judge_level：判定三段式的「连接性判据」来源（区分「没接」与「接到空」）
        Assert.Contains(WorkflowContextKeys.ConnectedPorts, KeysOf("stamp_judge_level"));

        // nc_conclusion_v2：判定清单池 + 组织三编码（画布上已移除，只能靠注入）
        var v2 = KeysOf("nc_conclusion_v2");
        Assert.Contains(WorkflowContextKeys.JudgePool, v2);
        Assert.Contains(WorkflowContextKeys.OrgCode, v2);
        Assert.Contains(WorkflowContextKeys.StandardCode, v2);
        Assert.Contains(WorkflowContextKeys.PhaseCode, v2);
    }

    // ═══════════════════════════════════════════════
    // 2. Analyze 效应：画布端口收窄（设计 §4.4）
    // ═══════════════════════════════════════════════

    /// <summary>nc_conclusion_v2 画布端口 4 → 1：只剩 judges_json（兼容通道）</summary>
    [Fact]
    public void 画布端口_nc结论判定v2只剩judges_json()
    {
        var meta = Analyze(typeof(NcConclusionV2Skill));
        var port = Assert.Single(meta.InputPorts);
        Assert.Equal("judges_json", port.Name);
    }

    /// <summary>stamp：5 个可见端口（connectedPorts 不可见）；verdict 保持可连线（LinkOrConstant）</summary>
    [Fact]
    public void 画布端口_stamp五个可见端口且无connectedPorts()
    {
        var meta = Analyze(typeof(StampJudgeLevelSkill));
        var names = meta.InputPorts.Select(p => p.Name).ToList();

        Assert.Equal(new[] { "result", "judge_code", "level", "verdict", "reason" }, names);
        Assert.DoesNotContain("connectedPorts", names);

        // ★ verdict 必须保持可连线 —— ConstantOnly 会让「一条 judge 三种结果 ⇒ 三个节点」回归
        var verdictPort = meta.InputPorts.First(p => p.Name == "verdict");
        Assert.Equal(SkillParamBindMode.LinkOrConstant.ToString(), verdictPort.BindMode);
    }

    // ── 私有工具 ──

    private static SkillMetadata Analyze(Type skillType)
    {
        // Analyze 是纯反射（不触碰 serviceProvider / logger）⇒ null! / NullLogger 安全
        var executor = new SkillExecutor(null!, NullLogger<SkillExecutor>.Instance);
        var meta = executor.Analyze(skillType.FullName!, "ExecuteAsync");
        Assert.NotNull(meta);
        return meta!;
    }

    /// <summary>WorkflowContextKeys 的全部 string 常量（反射收集 ⇒ 新增常量自动纳入，不手工维护两份）</summary>
    private static HashSet<string> DeclaredContextKeys() =>
        typeof(WorkflowContextKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);

    private static List<(string Skill, string Param, string Key)> ScanFromContextUsages()
    {
        var asm = typeof(SkillAttribute).Assembly;
        var list = new List<(string, string, string)>();

        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex)
        { types = ex.Types.Where(t => t != null).Cast<Type>().ToArray(); }

        foreach (var type in types)
        {
            var skillAttr = type.GetCustomAttribute<SkillAttribute>();
            if (skillAttr == null) continue;

            var method = type.GetMethod("ExecuteAsync", BindingFlags.Public | BindingFlags.Static);
            if (method == null) continue;

            foreach (var p in method.GetParameters())
            {
                var ctxAttr = p.GetCustomAttribute<FromContextAttribute>();
                if (ctxAttr == null) continue;
                var key = string.IsNullOrEmpty(ctxAttr.Key) ? p.Name! : ctxAttr.Key!;
                list.Add((skillAttr.Code, p.Name!, key));
            }
        }
        return list;
    }
}