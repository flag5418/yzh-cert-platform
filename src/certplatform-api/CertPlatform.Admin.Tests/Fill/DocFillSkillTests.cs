using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using CertPlatform.Admin.Services.Workflow.Skills.Fill;
using CertPlatform.Shared.Office;
using Xunit;

namespace CertPlatform.Admin.Tests.Fill;

/// <summary>
/// 文档填写 Skill 的契约测试（<c>fill_cell</c> / <c>fill_table</c> + <see cref="FillValueFactory.Coerce"/>）。
///
/// <para><b>★ 本组测试守的三条底线</b>：</para>
/// <list type="number">
///   <item><b>⛔ 不静默覆盖</b> —— 同一锚点被两个来源写必须 <c>Fail</c>（否则结果依赖执行顺序）；</item>
///   <item><b>⛔ 不猜类型</b> —— 不认识的值形态返回 <c>null</c> ⇒ <c>Fail</c>，
///         ⛔ 不 <c>ToString()</c> 兜底（会让 Excel 数字静默变文本）；</item>
///   <item><b>⛔ 不落盘</b> —— 两个 Skill 只往 <see cref="FillSession.Request"/> 累积，
///         层 1 才落盘（保证「一份文档只落盘一次」）。</item>
/// </list>
/// </summary>
public class DocFillSkillTests
{
    // ── FillValueFactory.Coerce ────────────────────────────────────────────

    [Fact]
    public void Coerce_FillValue实例_透传并补锚点()
    {
        var fv = new FillValue { Kind = FillValueKind.Text, Text = "x" };

        var result = FillValueFactory.Coerce("company_name", fv);

        Assert.Same(fv, result);
        Assert.Equal("company_name", result!.AnchorCode);
    }

    [Fact]
    public void Coerce_FillValue实例_已有锚点不被覆盖()
    {
        var fv = new FillValue { AnchorCode = "keep_me", Text = "x" };

        var result = FillValueFactory.Coerce("other", fv);

        Assert.Equal("keep_me", result!.AnchorCode);
    }

    [Fact]
    public void Coerce_字符串_按文本()
    {
        var result = FillValueFactory.Coerce("a", "上海某某有限公司");

        Assert.Equal(FillValueKind.Text, result!.Kind);
        Assert.Equal("上海某某有限公司", result.Text);
    }

    [Fact]
    public void Coerce_整数_按数值_不静默变文本()
    {
        var result = FillValueFactory.Coerce("a", 1234);

        Assert.Equal(FillValueKind.Number, result!.Kind);
        Assert.Equal(1234d, result.Number);
        Assert.Null(result.Text);
    }

    [Fact]
    public void Coerce_布尔_按布尔()
    {
        var result = FillValueFactory.Coerce("a", true);

        Assert.Equal(FillValueKind.Bool, result!.Kind);
        Assert.True(result.Bool);
    }

    [Fact]
    public void Coerce_值描述字典_按声明的类型与格式()
    {
        var desc = new Dictionary<string, object>
        {
            ["value"] = "1234.5",
            ["kind"] = "number",
            ["format"] = "#,##0.00",
        };

        var result = FillValueFactory.Coerce("a", desc);

        Assert.Equal(FillValueKind.Number, result!.Kind);
        Assert.Equal(1234.5d, result.Number);
        Assert.Equal("#,##0.00", result.NumberFormat);
    }

    [Fact]
    public void Coerce_JsonElement_字符串()
    {
        using var doc = JsonDocument.Parse("\"hello\"");
        var result = FillValueFactory.Coerce("a", doc.RootElement);
        Assert.Equal(FillValueKind.Text, result!.Kind);
        Assert.Equal("hello", result.Text);
    }

    [Fact]
    public void Coerce_JsonElement_对象值描述()
    {
        using var doc = JsonDocument.Parse("{\"value\":\"2026-10-03\",\"kind\":\"date\"}");
        var result = FillValueFactory.Coerce("a", doc.RootElement);
        Assert.Equal(FillValueKind.Date, result!.Kind);
        Assert.Equal(new System.DateTime(2026, 10, 3), result.Date);
    }

    [Fact]
    public void Coerce_不可识别的对象_返回null_不ToString兜底()
    {
        var result = FillValueFactory.Coerce("a", new object());

        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Coerce_锚点为空_返回null(string anchor)
    {
        Assert.Null(FillValueFactory.Coerce(anchor, "x"));
    }

    // ── fill_cell ─────────────────────────────────────────────────────────

    [Fact]
    public async Task FillCell_正常写入_累积进会话且认领锚点()
    {
        var session = new FillSession();
        var values = new Dictionary<string, object> { ["company_name"] = "上海某某有限公司" };

        var result = await FillCellSkill.ExecuteAsync(session, values);

        Assert.True(result.Success);
        Assert.Equal(1, result.Outputs["written"]);
        Assert.Equal("上海某某有限公司", session.Request.Values["company_name"].Text);
        Assert.Equal("fill_cell", session.AnchorOwners["company_name"]);
        // ★ 不落盘：模板字节仍为空（层 1 才写文件）
        Assert.Empty(session.Template);
    }

    [Fact]
    public async Task FillCell_values为空_失败()
    {
        var result = await FillCellSkill.ExecuteAsync(new FillSession(), new Dictionary<string, object>());

        Assert.False(result.Success);
        Assert.Contains("values 不能为空", result.Error);
    }

    [Fact]
    public async Task FillCell_同一锚点重复赋值_失败_不静默覆盖()
    {
        var session = new FillSession();
        await FillCellSkill.ExecuteAsync(session, new Dictionary<string, object> { ["a"] = "first" });

        var result = await FillCellSkill.ExecuteAsync(session, new Dictionary<string, object> { ["a"] = "second" });

        Assert.False(result.Success);
        Assert.Contains("已被", result.Error);
        // 先写的值仍在
        Assert.Equal("first", session.Request.Values["a"].Text);
    }

    [Fact]
    public async Task FillCell_值形态不可识别_失败_不猜()
    {
        var values = new Dictionary<string, object> { ["a"] = new object() };

        var result = await FillCellSkill.ExecuteAsync(new FillSession(), values);

        Assert.False(result.Success);
        Assert.Contains("无法识别", result.Error);
    }

    [Fact]
    public async Task FillCell_session为空_自行新建_支持单项试跑()
    {
        var result = await FillCellSkill.ExecuteAsync(
            null, new Dictionary<string, object> { ["a"] = "x" });

        Assert.True(result.Success);
        var session = Assert.IsType<FillSession>(result.Outputs["session"]);
        Assert.Single(session.Request.Values);
    }

    [Fact]
    public async Task FillCell_写入轨迹()
    {
        var session = new FillSession();

        await FillCellSkill.ExecuteAsync(session, new Dictionary<string, object> { ["a"] = "x", ["b"] = "y" });

        Assert.Single(session.Trace);
        Assert.Contains("[fill_cell]", session.Trace[0]);
    }

    // ── fill_table ────────────────────────────────────────────────────────

    private static TablePayload SampleTable() => new()
    {
        TableTag = "items",
        Columns = new List<TableColumn>
        {
            new() { FieldCode = "name", ValueKind = "text" },
            new() { FieldCode = "qty", ValueKind = "number" },
        },
        Rows = new List<Dictionary<string, string?>>
        {
            new() { ["name"] = "培训A", ["qty"] = "3" },
            new() { ["name"] = "培训B", ["qty"] = "5" },
        },
    };

    [Fact]
    public async Task FillTable_Excel_按FileKind分派为ExcelRange()
    {
        var session = new FillSession { FileKind = "excel" };

        var result = await FillTableSkill.ExecuteAsync(session, SampleTable(), start_row: 2, start_col: 0, sheet_name: "Sheet1");

        Assert.True(result.Success);
        var region = Assert.Single(session.Request.Regions);
        Assert.Equal(OfficeRegionKind.ExcelRange, region.Kind);
        Assert.Equal(2, region.StartRow);
        Assert.Equal(0, region.StartCol);
        Assert.Equal("Sheet1", region.SheetName);
        Assert.Equal(2, region.Rows.Count);
        Assert.Equal(2, region.Rows[0].Count);
    }

    [Fact]
    public async Task FillTable_Word_按FileKind分派为WordTable()
    {
        var session = new FillSession { FileKind = "word" };

        var result = await FillTableSkill.ExecuteAsync(session, SampleTable());

        Assert.True(result.Success);
        var region = Assert.Single(session.Request.Regions);
        Assert.Equal(OfficeRegionKind.WordTable, region.Kind);
        Assert.Equal("items", region.TableTag);
    }

    [Fact]
    public async Task FillTable_列类型按列声明_数值列落数值()
    {
        var session = new FillSession { FileKind = "excel" };

        await FillTableSkill.ExecuteAsync(session, SampleTable());

        var row0 = session.Request.Regions[0].Rows[0];
        Assert.Equal(FillValueKind.Text, row0[0]!.Kind);
        Assert.Equal(FillValueKind.Number, row0[1]!.Kind);
        Assert.Equal(3d, row0[1]!.Number);
    }

    [Fact]
    public async Task FillTable_某列缺键_该格写空_不报错()
    {
        var table = SampleTable();
        table.Rows[0].Remove("qty");
        var session = new FillSession { FileKind = "excel" };

        var result = await FillTableSkill.ExecuteAsync(session, table);

        Assert.True(result.Success);
        Assert.Null(session.Request.Regions[0].Rows[0][1]);
    }

    [Fact]
    public async Task FillTable_列声明数值但值不是数字_失败_不静默写空()
    {
        var table = SampleTable();
        table.Rows[0]["qty"] = "三";
        var session = new FillSession { FileKind = "excel" };

        var result = await FillTableSkill.ExecuteAsync(session, table);

        Assert.False(result.Success);
        Assert.Contains("类型不符", result.Error);
    }

    [Fact]
    public async Task FillTable_columns为空_失败()
    {
        var table = SampleTable();
        table.Columns.Clear();

        var result = await FillTableSkill.ExecuteAsync(new FillSession(), table);

        Assert.False(result.Success);
        Assert.Contains("columns 不能为空", result.Error);
    }

    [Fact]
    public async Task FillTable_rows为空_失败()
    {
        var table = SampleTable();
        table.Rows.Clear();

        var result = await FillTableSkill.ExecuteAsync(new FillSession(), table);

        Assert.False(result.Success);
        Assert.Contains("rows 不能为空", result.Error);
    }

    [Fact]
    public async Task FillTable_已有区域_追加为第二个_不覆盖()
    {
        var session = new FillSession { FileKind = "excel" };
        await FillTableSkill.ExecuteAsync(session, SampleTable());

        await FillTableSkill.ExecuteAsync(session, SampleTable());

        Assert.Equal(2, session.Request.Regions.Count);
    }

    [Fact]
    public async Task FillTable_Excel未给起始行列_落0()
    {
        var session = new FillSession { FileKind = "excel" };

        await FillTableSkill.ExecuteAsync(session, SampleTable());

        var region = session.Request.Regions[0];
        Assert.Equal(0, region.StartRow);
        Assert.Equal(0, region.StartCol);
    }
}
