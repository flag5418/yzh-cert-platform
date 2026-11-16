using System.Reflection;
using System.Text.Json;
using CertPlatform.Auditor.Entities.Dir;
using CertPlatform.Auditor.Services.Expert;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent;

/// <summary>
/// <c>ModeOutput</c> 投影回归：记录类文档的 <c>Clauses[]</c> 恒为空，
/// 事实只在 <c>ModeOutput</c>，任何一条链（模型上下文 / 证据行）漏读或截断，
/// 审核员看到的就是"企业没有任何记录"（97 号 G1/G2 / AF-6/AF-7/AF-8）。
/// </summary>
public class ModeOutputProjectorTests
{
    private static string RecordJson()
    {
        var formFields = JsonSerializer.Serialize(Enumerable.Range(1, 30).Select(i => $"栏目{i}"));
        return $$"""
        {
          "DocMeta": { "FileName": "01供应商评审表.doc" },
          "ExtractionMode": "record_profile",
          "Clauses": [],
          "ModeOutput": {
            "DocClass": "record",
            "ProcessArea": "采购过程",
            "Metadata": { "DocCode": "KSY-QMS-P-15-02", "ApprovedBy": "潘明海" },
            "PrimaryClauses": ["8.4.1"],
            "Records": [{
              "RecordName": "供应商评审表",
              "RowCount": 3,
              "FormFields": {{formFields}},
              "RecordState": "filled",
              "Samples": [{ "Date": "2026.6.10", "Values": ["供方资质：符合", "供货能力：基本满足"] }],
              "Signoff": ["采购部|胥加娟|2026.6.10", "生产部|黄勇|2026.6.10", "总经理|潘明海|2026.6.10"],
              "Conclusion": "基本符合我公司合格供应商的要求。"
            }]
          }
        }
        """;
    }

    private static JsonElement ModeOutput(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone().GetProperty("ModeOutput");
    }

    [Fact]
    public void Excerpt_KeepsSignoffAndDocCode_EvenWithLongFormFields()
    {
        var text = ModeOutputProjector.Excerpt(ModeOutput(RecordJson()), "record_profile");

        Assert.Contains("KSY-QMS-P-15-02", text);
        Assert.Contains("采购部 胥加娟 2026.6.10", text);
        Assert.Contains("总经理 潘明海", text);
        Assert.Contains("基本符合我公司合格供应商的要求", text);
        Assert.Contains("供方资质：符合", text);
        Assert.Contains("2026.6.10", text);
        Assert.True(text.Length <= ModeOutputProjector.MaxExcerptChars);
    }

    [Fact]
    public void ClauseNumbers_DedupesPrimaryAndSupport()
    {
        using var doc = JsonDocument.Parse("""
        { "PrimaryClauses": ["8.4.1", " "], "SupportClauses": ["8.4.1", "8.4.2"] }
        """);
        Assert.Equal(new[] { "8.4.1", "8.4.2" }, ModeOutputProjector.ClauseNumbers(doc.RootElement));
    }

    [Fact]
    public void ClauseNumbers_SkipsHintInferredClauses()
    {
        // 兜底写进去的条款是代码猜的，不是文档事实（D1 决策）⇒ 存量数据也不许再靠它召回
        using var doc = JsonDocument.Parse("""
        { "PrimaryClauses": ["8.4.1"], "ClauseHintInferred": true }
        """);
        Assert.Empty(ModeOutputProjector.ClauseNumbers(doc.RootElement));
    }

    [Fact]
    public void RecordText_IgnoresNonRecordObjects()
    {
        using var doc = JsonDocument.Parse("""{ "Foo": 1 }""");
        Assert.Null(ModeOutputProjector.RecordText(doc.RootElement));
    }

    [Fact]
    public void EvidenceRows_ProjectModeOutput_WhenClausesEmpty()
    {
        var method = typeof(CertTaskService).GetMethod("ProjectDocSlices",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var rows = (List<CertEvidenceRow>)method!.Invoke(null, new object[]
        {
            new DocumentExtraction
            {
                Code = "row",
                FileCode = "file",
                ExtractStatus = "done",
                IsStale = false,
                ContentJson = RecordJson()
            },
            "item",
            new HashSet<string> { "8.4.1" },
            0,
            null
        })!;

        var row = Assert.Single(rows);
        Assert.Equal("8.4.1", row.Anchor);
        Assert.Equal("doc", row.CapturedIn);
        Assert.Equal("unverified", row.SourceMatchStatus);
        Assert.Contains("胥加娟", row.Snippet);
        Assert.Contains("KSY-QMS-P-15-02", row.Snippet);
    }

    [Fact]
    public void EvidenceRows_SkipModeOutput_WhenClauseNotRequested()
    {
        var method = typeof(CertTaskService).GetMethod("ProjectDocSlices",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var rows = (List<CertEvidenceRow>)method.Invoke(null, new object[]
        {
            new DocumentExtraction
            {
                Code = "row", FileCode = "file", ExtractStatus = "done", IsStale = false,
                ContentJson = RecordJson()
            },
            "item",
            new HashSet<string> { "9.3.1" },
            0,
            null
        })!;

        Assert.Empty(rows);
    }

    [Fact]
    public void EvidenceRows_StillProjectModeOutput_WhenClausesKeyMissing()
    {
        // ⛔ Clauses 键缺失时 ModeOutput 分支若在 Clauses 循环之后就永远跑不到（静默断链、无报错）
        var json = RecordJson().Replace("\"Clauses\": [],", "");
        Assert.DoesNotContain("\"Clauses\"", json);

        var method = typeof(CertTaskService).GetMethod("ProjectDocSlices",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var rows = (List<CertEvidenceRow>)method.Invoke(null, new object[]
        {
            new DocumentExtraction
            {
                Code = "row", FileCode = "file", ExtractStatus = "done", IsStale = false,
                ContentJson = json
            },
            "item",
            new HashSet<string> { "8.4.1" },
            0,
            null
        })!;

        Assert.Single(rows);
        Assert.Contains("KSY-QMS-P-15-02", rows[0].Snippet);
        Assert.Contains("胥加娟", rows[0].Snippet);
    }

    [Fact]
    public void Signoff_DedupesSamePerson_AndKeepsDatedEntry()
    {
        using var doc = JsonDocument.Parse("""
        { "Records": [{ "RecordName": "合格供应商清单", "RowCount": 1,
            "Signoff": ["采购部|胥加娟|2026.6.10", "采购部|胥加娟|", "总经理|潘明海|2026.6.10"] }] }
        """);

        var text = ModeOutputProjector.Excerpt(doc.RootElement, "record_profile");

        Assert.Equal(1, text.Split("胥加娟").Length - 1);
        // 2026-11-16 改为人读格式「部门 姓名 日期」，去重规则不变
        Assert.Contains("采购部 胥加娟 2026.6.10", text);
        Assert.Contains("总经理 潘明海 2026.6.10", text);
        Assert.DoesNotContain("采购部 胥加娟 |", text);
    }

    [Fact]
    public void Excerpt_SkipsAllEmptyMetadata()
    {
        using var doc = JsonDocument.Parse("""
        { "ReportSummary": "记录供应商评审", "Records": [{ "RecordName": "评审表" }],
          "Metadata": { "DocCode": "", "DocVersion": "", "EffectiveDate": "", "IssuedBy": "", "ApprovedBy": "" } }
        """);

        var text = ModeOutputProjector.Excerpt(doc.RootElement, "record_profile");

        Assert.DoesNotContain("表头编号/批准", text);
        Assert.DoesNotContain("\"DocCode\"", text);
        Assert.Contains("记录供应商评审", text);
    }

    [Fact]
    public void ForProjection_RecomputesStoredRowCount()
    {
        // 存量产物：模型自报 7 行，实际 Samples 只有 1 家 ⇒ 读时校正成 1，不需要重抽
        using var doc = JsonDocument.Parse("""
        {
          "ExtractionMode": "record_profile",
          "ModeOutput": {
            "PrimaryClauses": ["8.4.1"],
            "Records": [{
              "RecordName": "供应商评审表",
              "RowCount": 7,
              "RecordState": "filled",
              "Samples": [{ "Values": { "供应商名称": "甲", "总得分": "88", "级别": "A" } }]
            }]
          }
        }
        """);

        var projected = ModeOutputProjector.ForProjection(doc.RootElement.GetProperty("ModeOutput"), "record_profile");
        Assert.Equal(1, projected.GetProperty("Records")[0].GetProperty("RowCount").GetInt32());
        // 没有 Rows 时口径写清楚：按样本条数记，并在记录级留痕
        Assert.Contains("row_count_from_samples",
            projected.GetProperty("Records")[0].GetProperty("Warnings").ToString());

        // 非记录模式不动
        var untouched = ModeOutputProjector.ForProjection(doc.RootElement.GetProperty("ModeOutput"), "process_profile");
        Assert.Equal(7, untouched.GetProperty("Records")[0].GetProperty("RowCount").GetInt32());
    }

    [Fact]
    public void RecordText_DedupesSignoffAgainstSample()
    {
        // 样本已印了 3 人签署，记录级签署栏再印一遍会多出重复；只补"总经理"
        using var doc = JsonDocument.Parse("""
        { "RecordName": "评审表", "RowCount": 1,
          "Samples": [{ "Date": "2026.6.10", "Signoff": ["采购部|胥|2026.6.10", "生产部|黄|2026.6.10"] }],
          "Signoff": ["采购部|胥|2026.6.10", "生产部|黄|2026.6.10", "总经理|潘|2026.6.10"] }
        """);

        var text = ModeOutputProjector.RecordText(doc.RootElement);

        Assert.NotNull(text);
        // 签署按「人」去重：胥只出现一次，总经理补上一次（2026-11-16 改为人读格式"部门 姓名 日期"）
        Assert.Equal(1, text!.Split("胥").Length - 1);
        Assert.Contains("总经理 潘 2026.6.10", text);
        // ⛔ 不许出现 JSON 转义或字段名（审核员看不懂）
        Assert.DoesNotContain("\\u", text);
        Assert.DoesNotContain("RecordState=", text);
        Assert.DoesNotContain("RowCount=", text);
    }

    [Fact]
    public void RecordText_SpeaksAuditorLanguage()
    {
        // 审核员口径：记录名/部门/过程/条数/抽查值拉平/结论/签署，不出现 JSON 原文与字段名
        using var doc = JsonDocument.Parse("""
        { "RecordName": "顾客满意度调查表", "OwnerDept": "品质部", "ProcessName": "顾客满意监视与测量",
          "RecordState": "filled", "RowCount": 3, "Coverage": "3 家客户", "Period": "2026-08-15",
          "Samples": [{ "Date": "2026-08-15",
                        "Values": { "顾客名称": "常州摇旗精密模具有限公司", "总评": "95" },
                        "Conclusion": "95分（非常满意）",
                        "Signoff": ["客户代表|王先生|2026-08-15"] }],
          "Signoff": ["品质部|张红红|2026-08-15"] }
        """);

        var text = ModeOutputProjector.RecordText(doc.RootElement)!;

        Assert.Contains("记录 顾客满意度调查表", text);
        Assert.Contains("责任部门 品质部", text);
        Assert.Contains("共 3 条", text);
        Assert.Contains("顾客名称：常州摇旗精密模具有限公司", text);
        Assert.Contains("结论 95分（非常满意）", text);
        Assert.DoesNotContain("filled", text);
        Assert.DoesNotContain("Values=", text);
        Assert.DoesNotContain("{", text);
    }

    [Fact]
    public void RecordText_FlagsBlankRecordForSiteCheck()
    {
        using var doc = JsonDocument.Parse("""
        { "RecordName": "制程巡检报告", "RecordState": "blank", "RowCount": 0, "Samples": [] }
        """);

        var text = ModeOutputProjector.RecordText(doc.RootElement)!;
        Assert.Contains("表内没有数据行，需现场核实", text);
        Assert.DoesNotContain("共 0 条", text);
    }
}
