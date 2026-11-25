using System.Reflection;
using System.Text.Json;
using CertPlatform.Auditor.Entities.Dir;
using CertPlatform.Auditor.Services.Ent;
using CertPlatform.Auditor.Services.Expert;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent;

/// <summary>
/// 15 号「章作用与召回路径模型」接线的回归：<c>CertTaskService.ProjectDocSlices</c> 的
/// <c>ClauseRecallPlan</c> 决定两件新行为 ——
/// ① RM1 条款轴按章作用(Role) 做**体裁白名单过滤**（§4.1）；
/// ② 记录族(f2/f1f2) 把过程轴(RM2) 由"兜底"翻为**主轴**。
///
/// <para>⛔ 这里是"小规模验证"：用真实形态的提取 JSON 跑真函数，确认旧行为（recall=null）不变、
/// 新行为按裁定生效。任何一条倒了 ⇒ 审核员看到的证据会多认错体裁 / 少数过程记录，且都不报错。</para>
/// </summary>
public class RecallPathWiringTests
{
    /// <summary>记录画像 JSON；PrimaryClauses 决定 RM1 能不能命中，deptNames 决定 RM3 部门轴能不能命中。</summary>
    private static string RecordJson(string primaryClause, string processArea = "采购过程", string? deptNames = null)
    {
        var deptLine = string.IsNullOrWhiteSpace(deptNames)
            ? string.Empty
            : $"\n            \"DeptNames\": [\"{deptNames}\"],";
        return $$"""
        {
          "DocMeta": { "FileName": "01供应商评审表.doc" },
          "ExtractionMode": "record_profile",
          "Clauses": [],
          "ModeOutput": {
            "DocClass": "record",
            "ProcessArea": "{{processArea}}",{{deptLine}}
            "PrimaryClauses": ["{{primaryClause}}"],
            "Records": [{
              "RecordName": "供应商评审表",
              "RowCount": 3,
              "RecordState": "filled",
              "Samples": [{ "Date": "2026.6.10", "Values": ["供方资质：符合"] }],
              "Conclusion": "基本符合我公司合格供应商的要求。"
            }]
          }
        }
        """;
    }

    private static DocumentExtraction Doc(string json) => new()
    {
        Code = "row",
        FileCode = "file",
        ExtractStatus = "done",
        IsStale = false,
        ContentJson = json
    };

    /// <summary>
    /// 用反射构造私有嵌套类型 <c>ClauseRecallPlan</c>（生产代码唯一来源，避免复制口径）。
    /// <para><paramref name="processKeys"/> = 条款 <c>Process</c> 属性键（<c>purchase</c> 等码）——
    /// RM2 过程轴按「码 ∩ 码」匹配，故须给码；中文关键词由生产代码从码派生。</para>
    /// </summary>
    private static object Plan(
        IEnumerable<string>? processKeys,
        Dictionary<string, HashSet<string>>? allowedByClause,
        bool recordAxisActive,
        bool deptAxisActive = false,
        IEnumerable<string>? deptNames = null)
    {
        var type = typeof(CertTaskService).GetNestedType("ClauseRecallPlan", BindingFlags.NonPublic);
        Assert.NotNull(type);
        var plan = Activator.CreateInstance(type!)!;
        var keys = (processKeys ?? Array.Empty<string>()).ToList();
        type!.GetProperty("ProcessKeys")!.SetValue(plan, keys);
        type.GetProperty("ProcessKeywords")!.SetValue(plan, ProcessAreaRecall.KeywordsFor(keys));
        type.GetProperty("AllowedModesByClause")!.SetValue(
            plan, allowedByClause ?? new Dictionary<string, HashSet<string>>());
        type.GetProperty("RecordAxisActive")!.SetValue(plan, recordAxisActive);
        type.GetProperty("DeptAxisActive")!.SetValue(plan, deptAxisActive);
        type.GetProperty("DeptAxisDepts")!.SetValue(
            plan, new HashSet<string>(deptNames ?? Array.Empty<string>(), StringComparer.Ordinal));
        return plan;
    }

    private static List<CertEvidenceRow> Project(DocumentExtraction ext, HashSet<string> clauses, object? recall)
    {
        var method = typeof(CertTaskService).GetMethod("ProjectDocSlices",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return (List<CertEvidenceRow>)method!.Invoke(null,
            new object?[] { ext, "item", clauses, 0, recall })!;
    }

    private static Dictionary<string, HashSet<string>> Allowed(string clause, params string[] modes)
        => new(StringComparer.Ordinal) { [clause] = new HashSet<string>(modes, StringComparer.OrdinalIgnoreCase) };

    // ── ① RM1 体裁白名单（§4.1）────────────────────────────────────────────

    [Fact]
    public void Rm1_DropsRecordProfile_WhenClauseRoleIsR1()
    {
        // 4.1 章作用 = r1 → 章作用白名单 = {clause_full, context_list}；
        // 一份 record_profile 文档自称命中 4.1 ⇒ 体裁不符，必须被过滤（否则把记录当制度证据）。
        var rows = Project(
            Doc(RecordJson("4.1")),
            new HashSet<string> { "4.1" },
            Plan(null, Allowed("4.1", "clause_full", "context_list"), recordAxisActive: false));

        Assert.Empty(rows);
    }

    [Fact]
    public void Rm1_KeepsRecordProfile_WhenClauseRoleIsR4()
    {
        // 4.4.2 章作用 = r4 → 白名单含 record_profile ⇒ 同样一份记录文档命中，予以保留。
        var rows = Project(
            Doc(RecordJson("4.4.2")),
            new HashSet<string> { "4.4.2" },
            Plan(null, Allowed("4.4.2", "record_profile", "work_instruction_citation", "document_control"),
                recordAxisActive: false));

        var row = Assert.Single(rows);
        Assert.Equal("4.4.2", row.Anchor);
    }

    [Fact]
    public void Rm1_LegacyDocWithoutMode_IsNotFiltered()
    {
        // 老文档无 ExtractionMode（归一为 legacy）⇒ 无从判定体裁，放行，不凭空丢证据。
        var json = RecordJson("4.1").Replace("\"ExtractionMode\": \"record_profile\",", "");
        var rows = Project(
            Doc(json),
            new HashSet<string> { "4.1" },
            Plan(null, Allowed("4.1", "clause_full", "context_list"), recordAxisActive: false));

        Assert.Single(rows);
    }

    // ── ② RM2 记录族主轴 ───────────────────────────────────────────────────

    [Fact]
    public void Rm2_RecordFamily_IsMainAxis_NotJustFallback()
    {
        // 记录族(f2)：条款轴已命中 1 行，过程轴仍须**额外**产出（记录类主轴）。
        var keywords = ProcessAreaRecall.KeywordsFor(new[] { "purchase" });
        Assert.Contains("采购", keywords);

        var rows = Project(
            Doc(RecordJson("8.4.1")),
            new HashSet<string> { "8.4.1" },
            Plan(new[] { "purchase" }, Allowed("8.4.1", "record_profile"), recordAxisActive: true));

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Anchor == "8.4.1");
        Assert.Contains(rows, r => r.Anchor == ProcessAreaRecall.RecordAnchor);
    }

    [Fact]
    public void Rm2_NonRecordFamily_StaysFallbackOnly()
    {
        // 非记录族：条款轴命中后过程轴退回"兜底"⇒ 不再追加过程行（避免制度族重复认过程记录）。
        var rows = Project(
            Doc(RecordJson("8.4.1")),
            new HashSet<string> { "8.4.1" },
            Plan(new[] { "purchase" }, Allowed("8.4.1", "record_profile"), recordAxisActive: false));

        var row = Assert.Single(rows);
        Assert.Equal("8.4.1", row.Anchor);
    }

    [Fact]
    public void Rm2_StillRecall_WhenClauseAxisMisses()
    {
        // 条款轴没命中（无 allowed）⇒ 过程轴兜底生效；这是翻转前的既有行为，须保持。
        var rows = Project(
            Doc(RecordJson("8.4.1")),
            new HashSet<string> { "9.3.1" },
            Plan(new[] { "purchase" }, null, recordAxisActive: false));

        var row = Assert.Single(rows);
        Assert.Equal(ProcessAreaRecall.RecordAnchor, row.Anchor);
    }

    // ── ③ RM3 部门轴（15 号 §三）—— 认证检查端与专家端两条队列共用的投影口径 ──────

    [Fact]
    public void Rm3_DeptHit_ProducesDeptRecordAnchor()
    {
        // 记录类：检查项部门「采购部」∩ 文档责任部门「采购部」⇒ 额外按部门轴召回（记录锚点）。
        var rows = Project(
            Doc(RecordJson("8.4.1", deptNames: "采购部")),
            new HashSet<string> { "8.4.1" },
            Plan(null, Allowed("8.4.1", "record_profile"), recordAxisActive: false,
                deptAxisActive: true, deptNames: new[] { "采购部" }));

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Anchor == "8.4.1");
        Assert.Contains(rows, r => r.Anchor == DocDeptNames.DeptRecordAnchor);
    }

    [Fact]
    public void Rm3_DeptMiss_DoesNotAttach()
    {
        // 部门不交集 ⇒ 不挂（不虚增）。
        var rows = Project(
            Doc(RecordJson("8.4.1", deptNames: "生产部")),
            new HashSet<string> { "8.4.1" },
            Plan(null, Allowed("8.4.1", "record_profile"), recordAxisActive: false,
                deptAxisActive: true, deptNames: new[] { "采购部" }));

        var row = Assert.Single(rows);
        Assert.Equal("8.4.1", row.Anchor);
    }

    [Fact]
    public void Rm3_Inactive_DoesNothing()
    {
        // 解析器未 plan 部门轴（DeptAxisActive=false）⇒ 即使部门名相同也不召回（轴由解析器给，⛔ 不自己猜）。
        var rows = Project(
            Doc(RecordJson("8.4.1", deptNames: "采购部")),
            new HashSet<string> { "8.4.1" },
            Plan(null, Allowed("8.4.1", "record_profile"), recordAxisActive: false,
                deptAxisActive: false, deptNames: new[] { "采购部" }));

        var row = Assert.Single(rows);
        Assert.Equal("8.4.1", row.Anchor);
    }

    [Fact]
    public void Rm3_Skipped_WhenProcessAxisAlreadyFired()
    {
        // 过程轴（RM2）已召回同一份文档 ⇒ 部门轴不再重复（防虚增）。
        var rows = Project(
            Doc(RecordJson("8.4.1", deptNames: "采购部")),
            new HashSet<string> { "8.4.1" },
            Plan(new[] { "purchase" }, Allowed("8.4.1", "record_profile"), recordAxisActive: true,
                deptAxisActive: true, deptNames: new[] { "采购部" }));

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Anchor == "8.4.1");
        Assert.Contains(rows, r => r.Anchor == ProcessAreaRecall.RecordAnchor);
        Assert.DoesNotContain(rows, r => r.Anchor == DocDeptNames.DeptRecordAnchor);
    }

    [Fact]
    public void Rm3_FileMode_ProducesDeptFileAnchor()
    {
        // 非记录类（过程 / 作业指导）命中检查项部门 ⇒ 部门文件锚点。
        var json = RecordJson("8.4.1", deptNames: "采购部")
            .Replace("\"record_profile\"", "\"process_profile\"");
        var rows = Project(
            Doc(json),
            new HashSet<string> { "9.3.1" }, // 检查项条款与文档 PrimaryClauses 不交集 ⇒ 条款轴 0 行
            Plan(null, Allowed("8.4.1", "process_profile"), recordAxisActive: false,
                deptAxisActive: true, deptNames: new[] { "采购部" }));

        var row = Assert.Single(rows);
        Assert.Equal(DocDeptNames.DeptFileAnchor, row.Anchor);
    }

    // ── ④ 无计划（recall=null）保持旧口径 ─────────────────────────────────

    [Fact]
    public void NoPlan_BehavesAsBefore()
    {
        var rows = Project(Doc(RecordJson("8.4.1")), new HashSet<string> { "8.4.1" }, recall: null);
        var row = Assert.Single(rows);
        Assert.Equal("8.4.1", row.Anchor);
        Assert.Equal("unverified", row.SourceMatchStatus);
    }
}
