using System;
using CertPlatform.Shared.Entities.Doc;
using CertPlatform.Shared.Office;
using Xunit;

namespace CertPlatform.Admin.Tests.Office;

/// <summary>
/// 「试填」取值链 <see cref="PreviewValueFactory"/> 的契约测试（TODO 清单 B8 / E2）。
///
/// <para><b>★ 为什么必须钉住</b>：试填是后台「标准资料填写规则」页<b>唯一</b>能让人
/// 「配完规则当场看见效果」的入口（B7 端点的取值来源）。它一旦静默跑偏，
/// 后果不是「预览不好看」，而是<b>把错的规则显示成对的</b> ——
/// 实施人员会据此认为「配好了」，直到专家端跑真企业数据才发现不对。</para>
///
/// <para><b>★ 三级顺序（⛔ 不可颠倒）</b>：
/// ① 示例数据锚点 ⇒ 清空（合规铁律，与真实链逐字一致）
/// ② <c>OriginalText</c> 有值 ⇒ 回填（<b>解析失败不降级</b>）
/// ③ 否则按 <c>ValueType</c> 生成占位。</para>
///
/// <para><b>★ 本文件最要命的一条</b>：<c>ValueType=number</c> 而原文是「叁万元」时，
/// <b>必须落到 ③ 生成占位</b>，⛔ 不能把「叁万元」当文本填进去 ——
/// 那会让 Excel 里本该是数字的格子变成文本，<b>不报错但结果错</b>
/// （见 <see cref="FillValueFactory"/> 的类注释）。</para>
/// </summary>
public class PreviewValueFactoryTests
{
    // ─────────────────────────── 基础设施 ───────────────────────────

    private static DocTemplateAnchor Anchor(
        string? originalText = null,
        string valueType = "text",
        bool sampleData = false,
        string? numberFormat = null)
        => new()
        {
            AnchorType = "scalar",
            AnchorRef = "{{x}}",
            WriteMode = "overwrite",
            OriginalText = originalText,
            ValueType = valueType,
            NumberFormat = numberFormat,
            SampleData = sampleData,
        };

    // ─────────────────────── 零、锚点键为空 ───────────────────────

    /// <summary>键为空 ⇒ 直接失败（键是查值字典的唯一入口，空键必填不进去）</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_锚点键为空_直接失败(string key)
    {
        var r = PreviewValueFactory.Create(Anchor(originalText: "张三"), key);

        Assert.False(r.Ok);
        Assert.Null(r.Value);
        Assert.False(string.IsNullOrWhiteSpace(r.Error));
        Assert.Equal("锚点键为空", r.Source);
    }

    // ────────────────── 一、① 示例数据 ⇒ 清空（最高优先） ──────────────────

    /// <summary>示例数据锚点 ⇒ 清空 —— 与真实填充链<b>逐字一致</b>（25 号 Q-5 合规铁律）</summary>
    [Fact]
    public void Create_示例数据锚点_清空且不回填原文()
    {
        var r = PreviewValueFactory.Create(
            Anchor(originalText: "张三（上一家企业的真实姓名）", sampleData: true), "LEGAL_PERSON");

        Assert.True(r.Ok);
        Assert.NotNull(r.Value);
        Assert.Equal(FillValueKind.Text, r.Value!.Kind);
        // ★ 清空 = 空值。⚠️ 空值时 Text 是 null（不是 ""）—— 工厂的「空值」分支只落类型、
        //   不赋文本（见 FillValueFactory.TryCreate）。判「清空」要看「会被写出的内容」。
        Assert.True(string.IsNullOrEmpty(r.Value.Text));
        Assert.Equal(string.Empty, r.Value.ToDisplayText());
        Assert.Equal("示例数据（合规清空）", r.Source);
    }

    /// <summary>清空时<b>类型仍按声明给</b>（下游按类型落笔「写空」，⛔ 不是不写）</summary>
    [Fact]
    public void Create_示例数据锚点_类型仍按声明()
    {
        var r = PreviewValueFactory.Create(Anchor(valueType: "number", sampleData: true), "AMOUNT");

        Assert.True(r.Ok);
        Assert.Equal(FillValueKind.Number, r.Value!.Kind);
        Assert.Null(r.Value.Number);
    }

    /// <summary>★ ① 优先于 ② —— 示例数据锚点即使有原文也必须清（顺序不可颠倒）</summary>
    [Fact]
    public void Create_示例数据优先于原文回填()
    {
        var r = PreviewValueFactory.Create(
            Anchor(originalText: "1234", valueType: "number", sampleData: true), "AMOUNT");

        Assert.True(r.Ok);
        Assert.Null(r.Value!.Number);                      // ⛔ 没有把 1234 填回去
        Assert.Equal("示例数据（合规清空）", r.Source);
    }

    // ───────────────────── 二、② 原文回填 ─────────────────────

    /// <summary>原文有值 ⇒ 回填，并如实标注来源</summary>
    [Fact]
    public void Create_原文有值_回填文本()
    {
        var r = PreviewValueFactory.Create(Anchor(originalText: "某某有限公司"), "ENT_NAME");

        Assert.True(r.Ok);
        Assert.Equal(FillValueKind.Text, r.Value!.Kind);
        Assert.Equal("某某有限公司", r.Value.Text);
        Assert.Equal("ENT_NAME", r.Value.AnchorCode);
        Assert.Equal("原始文字", r.Source);
        Assert.Equal("试填 · 原始文字", r.Value.Source);
    }

    /// <summary>原文两端空白 ⇒ 先 Trim（否则「   」会被当成有值）</summary>
    [Fact]
    public void Create_原文两端空白_先Trim()
    {
        var r = PreviewValueFactory.Create(Anchor(originalText: "  某某有限公司  "), "ENT_NAME");

        Assert.True(r.Ok);
        Assert.Equal("某某有限公司", r.Value!.Text);
        Assert.Equal("原始文字", r.Source);
    }

    /// <summary>原文为 null / 空白 ⇒ 不算「有原文」，落到 ③ 生成占位</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_原文为空_落到生成占位(string? originalText)
    {
        var r = PreviewValueFactory.Create(Anchor(originalText: originalText), "ENT_NAME");

        Assert.True(r.Ok);
        Assert.Equal("试填占位值", r.Source);
        Assert.Equal("【试填】ENT_NAME", r.Value!.Text);
    }

    /// <summary>原文按声明类型解析（date）</summary>
    [Fact]
    public void Create_原文按声明类型解析_日期()
    {
        var r = PreviewValueFactory.Create(
            Anchor(originalText: "2026-03-04", valueType: "date"), "ISSUE_DATE");

        Assert.True(r.Ok);
        Assert.Equal(FillValueKind.Date, r.Value!.Kind);
        Assert.Equal(new DateTime(2026, 3, 4), r.Value.Date);
        Assert.Equal("原始文字", r.Source);
    }

    /// <summary>原文按声明类型解析（number）+ 格式串透传</summary>
    [Fact]
    public void Create_原文按声明类型解析_数值带格式()
    {
        var r = PreviewValueFactory.Create(
            Anchor(originalText: "1,234.5", valueType: "number", numberFormat: "#,##0.00"), "AMOUNT");

        Assert.True(r.Ok);
        Assert.Equal(FillValueKind.Number, r.Value!.Kind);
        Assert.Equal(1234.5d, r.Value.Number!.Value, 6);
        Assert.Equal("#,##0.00", r.Value.NumberFormat);
    }

    // ────────── 三、★ 关键负例：解析失败 ⇒ 不降级，落到 ③ ──────────

    /// <summary>
    /// ★ 本文件最重要的一条：声明 <c>number</c> 但原文不是数字 ⇒ <b>不降级成文本</b>，
    /// 落到 ③ 生成占位。降级会让 Excel 数字变文本，<b>不报错但结果错</b>。
    /// </summary>
    [Theory]
    [InlineData("叁万元")]
    [InlineData("12abc")]
    [InlineData("ABC")]
    public void Create_声明number但原文非数字_不降级而落生成占位(string originalText)
    {
        var r = PreviewValueFactory.Create(
            Anchor(originalText: originalText, valueType: "number"), "AMOUNT");

        Assert.True(r.Ok);
        Assert.Equal(FillValueKind.Number, r.Value!.Kind);   // ★ 仍是数字类型
        Assert.Equal(1d, r.Value.Number!.Value);             // ★ 占位值 = 1
        Assert.Null(r.Value.Text);                           // ★ ⛔ 没有把原文塞进 Text
        Assert.Equal("试填占位值", r.Source);
    }

    /// <summary>声明 <c>date</c> 但原文不是日期 ⇒ 同样不降级</summary>
    [Fact]
    public void Create_声明date但原文非日期_不降级()
    {
        var r = PreviewValueFactory.Create(
            Anchor(originalText: "去年年底", valueType: "date"), "ISSUE_DATE");

        Assert.True(r.Ok);
        Assert.Equal(FillValueKind.Date, r.Value!.Kind);
        Assert.Equal(DateTime.Today, r.Value.Date!.Value.Date);   // 占位 = 今天
        Assert.Equal("试填占位值", r.Source);
    }

    // ────────────── 四、③ 按 ValueType 生成占位 ──────────────

    /// <summary>text ⇒ 「【试填】+ 锚点名」（带名字才回答得了「这个位置配的是谁」）</summary>
    [Fact]
    public void Create_生成占位_text_带锚点名()
    {
        var r = PreviewValueFactory.Create(Anchor(valueType: "text"), "ENT_NAME");

        Assert.True(r.Ok);
        Assert.Equal(FillValueKind.Text, r.Value!.Kind);
        Assert.Equal("【试填】ENT_NAME", r.Value.Text);
        Assert.Equal("试填 · 占位值（【试填】ENT_NAME）", r.Value.Source);
        Assert.Equal("试填占位值", r.Source);
    }

    /// <summary>number ⇒ 1</summary>
    [Fact]
    public void Create_生成占位_number_为1()
    {
        var r = PreviewValueFactory.Create(Anchor(valueType: "number"), "QTY");

        Assert.True(r.Ok);
        Assert.Equal(FillValueKind.Number, r.Value!.Kind);
        Assert.Equal(1d, r.Value.Number!.Value);
    }

    /// <summary>date ⇒ 今天（<c>yyyy-MM-dd</c>，InvariantCulture ⇒ 与服务器区域性无关）</summary>
    [Fact]
    public void Create_生成占位_date_为今天()
    {
        var r = PreviewValueFactory.Create(Anchor(valueType: "date"), "D");

        Assert.True(r.Ok);
        Assert.Equal(FillValueKind.Date, r.Value!.Kind);
        Assert.Equal(DateTime.Today, r.Value.Date!.Value.Date);
    }

    /// <summary>bool ⇒ 是</summary>
    [Fact]
    public void Create_生成占位_bool_为是()
    {
        var r = PreviewValueFactory.Create(Anchor(valueType: "bool"), "B");

        Assert.True(r.Ok);
        Assert.Equal(FillValueKind.Bool, r.Value!.Kind);
        Assert.True(r.Value.Bool);
    }

    /// <summary>enum ⇒ 按<b>文本</b>处理（<see cref="FillValueKind"/> 无 Enum 成员）</summary>
    [Fact]
    public void Create_生成占位_enum_按文本()
    {
        var r = PreviewValueFactory.Create(Anchor(valueType: "enum"), "LEVEL");

        Assert.True(r.Ok);
        Assert.Equal(FillValueKind.Text, r.Value!.Kind);
        Assert.Equal("【试填】LEVEL", r.Value.Text);
    }

    /// <summary>未知类型 / 未声明 ⇒ 按 text 兜底</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something_else")]
    public void Create_生成占位_未知类型按文本(string? valueType)
    {
        var r = PreviewValueFactory.Create(
            Anchor(valueType: valueType ?? "text"), "X");

        Assert.True(r.Ok);
        Assert.Equal(FillValueKind.Text, r.Value!.Kind);
        Assert.Equal("【试填】X", r.Value.Text);
    }

    /// <summary>占位值也吃声明的大小写与空白（<c>" NUMBER "</c> 仍是数字）</summary>
    [Fact]
    public void Create_生成占位_类型声明不区分大小写()
    {
        var r = PreviewValueFactory.Create(Anchor(valueType: " NUMBER "), "QTY");

        Assert.True(r.Ok);
        Assert.Equal(FillValueKind.Number, r.Value!.Kind);
    }

    // ────────────── 五、产物可辨识性（设计约束） ──────────────

    /// <summary>占位前缀固定为「【试填】」—— 让试填产物<b>不可能被误当成真实产物</b></summary>
    [Fact]
    public void PlaceholderPrefix_固定为试填()
    {
        Assert.Equal("【试填】", PreviewValueFactory.PlaceholderPrefix);
    }

    /// <summary>取到值时 <c>Source</c> 必非空（报告要如实标注「值从哪来」）</summary>
    [Theory]
    [InlineData("text", null, false, "试填占位值")]
    [InlineData("text", "原文", false, "原始文字")]
    [InlineData("text", null, true, "示例数据（合规清空）")]
    public void Create_成功时Source非空(string valueType, string? originalText, bool sampleData, string expected)
    {
        var r = PreviewValueFactory.Create(
            Anchor(originalText: originalText, valueType: valueType, sampleData: sampleData), "K");

        Assert.True(r.Ok);
        Assert.Equal(expected, r.Source);
    }

    /// <summary>失败时 <c>Value</c> 必为 null（⛔ 不给半个值）</summary>
    [Fact]
    public void Create_失败时Value为null()
    {
        var r = PreviewValueFactory.Create(Anchor(), "");

        Assert.False(r.Ok);
        Assert.Null(r.Value);
        Assert.NotNull(r.Error);
    }
}
