using System.Linq;
using CertPlatform.Auditor.Services.Ent;
using Xunit;

namespace CertPlatform.Admin.Tests.Ent;

/// <summary>
/// ★ 按标准提取条款（P2）的<b>纯函数</b>单测：提示词渲染 / 条款清单 / 长文档分段 / 条款号自然序。
///
/// <para><b>为什么必须单测这四处</b>：它们的失败模式全是<b>静默</b>的 ——
/// <list type="bullet">
///   <item>占位符漏替换 ⇒ 模型收到字面量 <c>{{clause_list}}</c>，输出 0 条，不报错；</item>
///   <item>分段把一句话劈开 ⇒ 模型给的 <c>Anchor</c> 在原文里找不到，锚点率崩塌；</item>
///   <item>条款号按字符串排序 ⇒ <c>4.2.10</c> 排在 <c>4.2.4</c> 前面，前端列表顺序错乱；</item>
///   <item>清单格式多一个序号前缀 ⇒ 模型把序号当条款号抄回来，全部被「编号不在清单内」丢弃。</item>
/// </list>
/// 这些都<b>不会抛异常</b>，只能靠断言发现。</para>
/// </summary>
public class DocExtractTests
{
    // ═══════════════════════════════════════════════
    // ① 提示词渲染
    // ═══════════════════════════════════════════════

    [Fact]
    public void Render_ReplacesAllFivePlaceholders()
    {
        const string template =
            "标准={{standard_name}}|码={{standard_code}}|文档={{document_name}}\n" +
            "清单:\n{{clause_list}}\n正文:\n{{document_content}}";

        var text = DocExtractPromptService.Render(
            template,
            "ISO 13485 医疗器械质量管理体系（iso13485）",
            "iso13485",
            "质量手册.docx",
            "- 4.2.4 医疗器械文件控制",
            "# 质量手册\n正文内容");

        Assert.DoesNotContain("{{", text, System.StringComparison.Ordinal);
        Assert.Contains("ISO 13485 医疗器械质量管理体系（iso13485）", text, System.StringComparison.Ordinal);
        Assert.Contains("码=iso13485", text, System.StringComparison.Ordinal);
        Assert.Contains("质量手册.docx", text, System.StringComparison.Ordinal);
        Assert.Contains("- 4.2.4 医疗器械文件控制", text, System.StringComparison.Ordinal);
        Assert.Contains("# 质量手册", text, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Render_UnknownPlaceholder_IsLeftUntouched()
    {
        var text = DocExtractPromptService.Render(
            "a{{standard_name}}b{{not_a_placeholder}}c", "X", "iso9001", "f", "l", "d");

        Assert.Equal("aXb{{not_a_placeholder}}c", text);
    }

    [Fact]
    public void Render_NullInputs_DoNotThrow()
    {
        var text = DocExtractPromptService.Render(
            "{{standard_name}}/{{clause_list}}", null!, null!, null!, null!, null!);
        Assert.Equal("/", text);
    }

    // ═══════════════════════════════════════════════
    // ② 条款清单
    // ═══════════════════════════════════════════════

    [Fact]
    public void BuildClauseList_OneLinePerClause_NoIndexPrefix()
    {
        var list = DocExtractPromptService.BuildClauseList(new[]
        {
            ("4.2.4", "医疗器械文件控制"),
            ("7.5.1", "生产和服务提供的控制"),
        });

        Assert.Equal("- 4.2.4 医疗器械文件控制\n- 7.5.1 生产和服务提供的控制", list);
        // ⛔ 不能有「1. 」「(1)」之类序号：模型会把序号当条款号抄回来
        Assert.DoesNotContain("1. ", list, System.StringComparison.Ordinal);
    }

    [Fact]
    public void BuildClauseList_SkipsBlankNumbers_AndToleratesEmptyTitle()
    {
        var list = DocExtractPromptService.BuildClauseList(new[]
        {
            ("  ", "空号要跳过"),
            ("4.1", ""),
            ("4.2", "  "),
        });

        Assert.Equal("- 4.1\n- 4.2", list);
    }

    [Fact]
    public void BuildClauseList_EmptyInput_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, DocExtractPromptService.BuildClauseList(System.Array.Empty<(string, string)>()));
    }

    // ═══════════════════════════════════════════════
    // ③ 长文档分段
    // ═══════════════════════════════════════════════

    [Fact]
    public void SplitIntoChunks_ShortText_SingleChunk()
    {
        var chunks = ClauseExtractExecutor.SplitIntoChunks("短文本\n第二段", 100, 8);
        Assert.Single(chunks);
        Assert.Contains("第二段", chunks[0], System.StringComparison.Ordinal);
    }

    [Fact]
    public void SplitIntoChunks_LongText_SplitsOnParagraphBoundary_NoContentLost()
    {
        // 10 段 × 30 字，块宽 100 ⇒ 必须切成多块，且拼回来内容不丢（去空白后逐字相等）
        var paras = Enumerable.Range(1, 10)
            .Select(i => new string((char)('a' + i - 1), 30))
            .ToArray();
        var text = string.Join("\n", paras);

        var chunks = ClauseExtractExecutor.SplitIntoChunks(text, 100, 8);

        Assert.True(chunks.Count > 1);
        foreach (var c in chunks)
            Assert.True(c.Length <= 100, $"块长 {c.Length} 超过上限 100");

        var joined = string.Concat(chunks).Replace("\n", "", System.StringComparison.Ordinal);
        Assert.Equal(text.Replace("\n", "", System.StringComparison.Ordinal), joined);
    }

    [Fact]
    public void SplitIntoChunks_SingleParagraphOverLimit_HardSplit()
    {
        var text = new string('x', 250);
        var chunks = ClauseExtractExecutor.SplitIntoChunks(text, 100, 8);

        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, c => Assert.True(c.Length <= 100));
        Assert.Equal(text, string.Concat(chunks));
    }

    [Fact]
    public void SplitIntoChunks_RespectsMaxChunks()
    {
        var text = new string('y', 1000);
        var chunks = ClauseExtractExecutor.SplitIntoChunks(text, 100, 3);

        Assert.Equal(3, chunks.Count);
        // 只取前 3 块 ⇒ 内容必然少于原文（调用方必须把「超出部分未提取」写进 Message）
        Assert.True(string.Concat(chunks).Length < text.Length);
    }

    [Fact]
    public void SplitIntoChunks_EmptyText_NoChunks()
    {
        Assert.Empty(ClauseExtractExecutor.SplitIntoChunks("", 100, 8));
    }

    // ═══════════════════════════════════════════════
    // ④ 条款号自然序
    // ═══════════════════════════════════════════════

    [Fact]
    public void ClauseSortKey_NaturalOrder_NotLexicographic()
    {
        var numbers = new[] { "4.2.10", "4.2.4", "4.10", "4.2", "10.1" };
        var ordered = numbers.OrderBy(n => ClauseExtractExecutor.ClauseSortKey(n)).ToArray();

        Assert.Equal(new[] { "4.2", "4.2.4", "4.2.10", "4.10", "10.1" }, ordered);
    }

    [Fact]
    public void ClauseSortKey_NonNumericSegment_SortsLast_ButDoesNotThrow()
    {
        var numbers = new[] { "附录A", "4.1" };
        var ordered = numbers.OrderBy(n => ClauseExtractExecutor.ClauseSortKey(n)).ToArray();

        // 「附录A」解析不出数字 ⇒ 按 9999 兜底，排在数字条款之后（⛔ 不抛异常、不吞掉）
        Assert.Equal("4.1", ordered[0]);
        Assert.Equal("附录A", ordered[1]);
    }
}
