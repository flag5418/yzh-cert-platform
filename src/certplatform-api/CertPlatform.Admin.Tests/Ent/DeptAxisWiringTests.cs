using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CertPlatform.Auditor.Services.Expert;
using CertPlatform.Shared.Domain.Clauses;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent;

/// <summary>
/// RM3 部门轴接线的回归：<c>CertCheckContextBuilder.ApplyDeptAxis</c> 的行为 ——
/// 仅对「召回计划含 RM3（部门轴）」的条款执行；检查项 <c>DeptNames</c> ∩ 文档责任部门，命中即召回；
/// 记录先于文件、每条款最多 6 份；⛔ 不从文档反推条款号。
///
/// <para>任何一条倒了 ⇒ 记录类证据要么被漏召回（审核员看不到该部门的执行记录），
/// 要么被虚增（挂到不属于它的条款上），且都不报错。</para>
/// </summary>
public class DeptAxisWiringTests
{
    private const string RecordAnchor = "同部门记录";
    private const string FileAnchor = "同部门文件";

    private static readonly Type CandidateType =
        typeof(CertCheckContextBuilder).GetNestedType("DeptAxisCandidate", BindingFlags.NonPublic)!;

    private static readonly ConstructorInfo CandidateCtor = CandidateType
        .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
        .Single(ci => ci.GetParameters().Length == 5);

    private static ClauseContext Clause(string number) => new()
    {
        ClauseNumber = number,
        ClauseCode = number,
        Title = number
    };

    private static object Candidate(string fileCode, string[] depts, bool isRecord) => CandidateCtor.Invoke(
        new object[] { fileCode, fileCode + ".doc", new List<string>(depts), "摘录：" + fileCode, isRecord });

    private static IList Candidates(params object[] items)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(CandidateType))!;
        foreach (var i in items) list.Add(i);
        return list;
    }

    private static Dictionary<string, List<RecallPath>> PlanWithDeptAxis(params string[] clauseNumbers)
        => BuildPlan(RecallAxis.Dept, clauseNumbers);

    private static Dictionary<string, List<RecallPath>> PlanClauseAxisOnly(params string[] clauseNumbers)
        => BuildPlan(RecallAxis.Clause, clauseNumbers);

    private static Dictionary<string, List<RecallPath>> BuildPlan(string axis, string[] clauseNumbers)
    {
        var dict = new Dictionary<string, List<RecallPath>>(StringComparer.Ordinal);
        foreach (var n in clauseNumbers)
            dict[n] = new List<RecallPath> { new() { Axis = axis } };
        return dict;
    }

    private static void Apply(
        List<ClauseContext> clauses, IList candidates, string? deptNames,
        Dictionary<string, List<RecallPath>> recall)
    {
        var method = typeof(CertCheckContextBuilder)
            .GetMethod("ApplyDeptAxis", BindingFlags.Static | BindingFlags.NonPublic)!;
        method.Invoke(null, new object?[] { clauses, candidates, deptNames, recall });
    }

    [Fact]
    public void Rm3_MatchesByDeptName_MarksRecordAnchor()
    {
        var c = Clause("8.4.1");
        Apply(new List<ClauseContext> { c },
            Candidates(Candidate("f1", new[] { "采购部" }, true)),
            "采购部", PlanWithDeptAxis("8.4.1"));

        var slice = Assert.Single(c.Slices);
        Assert.Equal("f1", slice.FileCode);
        Assert.Equal(RecordAnchor, slice.Anchor);
    }

    [Fact]
    public void Rm3_NoMatch_WhenDeptDisjoint()
    {
        var c = Clause("8.4.1");
        Apply(new List<ClauseContext> { c },
            Candidates(Candidate("f1", new[] { "生产部" }, true)),
            "品质部", PlanWithDeptAxis("8.4.1"));

        Assert.Empty(c.Slices);
    }

    [Fact]
    public void Rm3_SkipsClause_WithoutDeptAxisInPlan()
    {
        // 计划只有 RM1 ⇒ 即便部门命中也不执行（⛔ 不从文档反推条款号 / 不自己猜轴）。
        var c = Clause("8.4.1");
        Apply(new List<ClauseContext> { c },
            Candidates(Candidate("f1", new[] { "采购部" }, true)),
            "采购部", PlanClauseAxisOnly("8.4.1"));

        Assert.Empty(c.Slices);
    }

    [Fact]
    public void Rm3_NoOp_WhenDeptNamesEmpty()
    {
        var c = Clause("8.4.1");
        Apply(new List<ClauseContext> { c },
            Candidates(Candidate("f1", new[] { "采购部" }, true)),
            null, PlanWithDeptAxis("8.4.1"));

        Assert.Empty(c.Slices);
    }

    [Fact]
    public void Rm3_RecordsBeforeFiles_AndCapAtSix()
    {
        var c = Clause("8.4.1");
        var items = new List<object>();
        for (var i = 0; i < 3; i++) items.Add(Candidate("rec" + i, new[] { "采购部" }, true));
        for (var i = 0; i < 5; i++) items.Add(Candidate("doc" + i, new[] { "采购部" }, false));

        Apply(new List<ClauseContext> { c }, Candidates(items.ToArray()), "采购部", PlanWithDeptAxis("8.4.1"));

        Assert.Equal(6, c.Slices.Count);
        Assert.Equal(3, c.Slices.Count(s => s.Anchor == RecordAnchor));
        Assert.Equal(3, c.Slices.Count(s => s.Anchor == FileAnchor));
    }

    [Fact]
    public void Rm3_NoDuplicate_WhenFileAlreadySliced()
    {
        var c = Clause("8.4.1");
        c.Slices.Add(new ClauseDocSlice { FileCode = "f1", Anchor = "8.4.1" });

        Apply(new List<ClauseContext> { c },
            Candidates(Candidate("f1", new[] { "采购部" }, true)),
            "采购部", PlanWithDeptAxis("8.4.1"));

        var slice = Assert.Single(c.Slices);
        Assert.Equal("8.4.1", slice.Anchor);
    }
}
