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
    /// <summary>记录画像 JSON；PrimaryClauses 决定 RM1 能不能命中。</summary>
    private static string RecordJson(string primaryClause, string processArea = "采购过程")
    {
        return $$"""
        {
          "DocMeta": { "FileName": "01供应商评审表.doc" },
          "ExtractionMode": "record_profile",
          "Clauses": [],
          "ModeOutput": {
            "DocClass": "record",
            "ProcessArea": "{{processArea}}",
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

    /// <summary>用反射构造私有嵌套类型 <c>ClauseRecallPlan</c>（生产代码唯一来源，避免复制口径）。</summary>
    private static object Plan(
        IEnumerable<string>? keywords,
        Dictionary<string, HashSet<string>>? allowedByClause,
        bool recordAxisActive)
    {
        var type = typeof(CertTaskService).GetNestedType("ClauseRecallPlan", BindingFlags.NonPublic);
        Assert.NotNull(type);
        var plan = Activator.CreateInstance(type!)!;
        type!.GetProperty("ProcessKeywords")!.SetValue(plan, new HashSet<string>(keywords ?? Array.Empty<string>()));
        type.GetProperty("AllowedModesByClause")!.SetValue(
            plan, allowedByClause ?? new Dictionary<string, HashSet<string>>());
        type.GetProperty("RecordAxisActive")!.SetValue(plan, recordAxisActive);
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
            Plan(keywords, Allowed("8.4.1", "record_profile"), recordAxisActive: true));

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Anchor == "8.4.1");
        Assert.Contains(rows, r => r.Anchor == ProcessAreaRecall.RecordAnchor);
    }

    [Fact]
    public void Rm2_NonRecordFamily_StaysFallbackOnly()
    {
        // 非记录族：条款轴命中后过程轴退回"兜底"⇒ 不再追加过程行（避免制度族重复认过程记录）。
        var keywords = ProcessAreaRecall.KeywordsFor(new[] { "purchase" });
        var rows = Project(
            Doc(RecordJson("8.4.1")),
            new HashSet<string> { "8.4.1" },
            Plan(keywords, Allowed("8.4.1", "record_profile"), recordAxisActive: false));

        var row = Assert.Single(rows);
        Assert.Equal("8.4.1", row.Anchor);
    }

    [Fact]
    public void Rm2_StillRecall_WhenClauseAxisMisses()
    {
        // 条款轴没命中（无 allowed）⇒ 过程轴兜底生效；这是翻转前的既有行为，须保持。
        var keywords = ProcessAreaRecall.KeywordsFor(new[] { "purchase" });
        var rows = Project(
            Doc(RecordJson("8.4.1")),
            new HashSet<string> { "9.3.1" },
            Plan(keywords, null, recordAxisActive: false));

        var row = Assert.Single(rows);
        Assert.Equal(ProcessAreaRecall.RecordAnchor, row.Anchor);
    }

    // ── ③ 无计划（recall=null）保持旧口径 ─────────────────────────────────

    [Fact]
    public void NoPlan_BehavesAsBefore()
    {
        var rows = Project(Doc(RecordJson("8.4.1")), new HashSet<string> { "8.4.1" }, recall: null);
        var row = Assert.Single(rows);
        Assert.Equal("8.4.1", row.Anchor);
        Assert.Equal("unverified", row.SourceMatchStatus);
    }
}
