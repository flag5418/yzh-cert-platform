using CertPlatform.Shared.Office;
using CertPlatform.Shared.Office.Word;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Xunit;

namespace CertPlatform.Admin.Tests.Office;

/// <summary>
/// <b>真实文件烟测</b> —— 用仓库里 <c>docs/90-归档/案例资料/</c> 下**真由 Word/Excel 生成**的
/// 机构体系文件跑一遍写入器。
///
/// <para><b>为什么不能只用 NPOI 自己造的文档做测试</b>：NPOI 生成的文档 run 结构「太干净」
/// （一个 run 一个 <c>&lt;w:t&gt;</c>、没有编号/主题字体/修订记录）。
/// 真实文件才带得出 W9（token 跨 run）、样式继承、编号域等真实形态
/// ⇒ 这层烟测是「封装能不能上生产」的最后一道门槛。</para>
///
/// <para>样例目录缺失时用例会**直接返回**（不在 CI 上报假失败）。</para>
/// </summary>
public class RealDocumentSmokeTests
{
    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static string? FindSample(params string[] extensions)
    {
        var root = FindRepoRoot();
        if (root == null) return null;

        var sampleDir = Path.Combine(root, "docs", "90-归档", "案例资料");
        if (!Directory.Exists(sampleDir)) return null;

        foreach (var ext in extensions)
        {
            var hit = Directory.EnumerateFiles(sampleDir, "*" + ext, SearchOption.AllDirectories)
                .FirstOrDefault(f => !f.Contains("~$"));
            if (hit != null) return hit;
        }

        return null;
    }

    [Fact]
    public void RealDocx_ProbeAnchor_FillsAndKeepsOriginalContent()
    {
        var path = FindSample(".docx");
        if (path == null) return;   // 样例目录缺失 ⇒ 跳过

        var original = File.ReadAllBytes(path);

        // ── 1. 在真实文档末尾追加一个探针段落（含跨 run 的锚点）──
        byte[] probeBytes;
        string? originalFirstParagraph = null;
        int originalParagraphCount;
        {
            using var doc = new WordFillWriterProbe(original, out originalFirstParagraph, out originalParagraphCount);
            probeBytes = doc.BuildProbeDocument();
        }

        // ── 2. 填充 ──
        var result = new WordFillWriter().Fill(new OfficeFillRequest
        {
            Template = probeBytes,
            Values = new Dictionary<string, FillValue>(StringComparer.Ordinal)
            {
                ["probe"] = new FillValue { AnchorCode = "probe", Text = "OK-2026" },
            },
        });

        Assert.True(result.Report.Verified, "真实文档填充后不应有残留锚点");
        Assert.Equal(1, result.Report.Resolved);

        // ── 3. 重新打开：探针被填上、且原有内容一字未变 ──
        using var reopened = new NPOI.XWPF.UserModel.XWPFDocument(new MemoryStream(result.Output));

        var last = reopened.Paragraphs[^1];
        var lastText = string.Concat(last.Runs.Select(r => r.GetText(0)));
        Assert.Equal("【探针】OK-2026", lastText);

        // ★ 段落数 = 原数 + 1（只多出我们追加的探针段）
        Assert.Equal(originalParagraphCount + 1, reopened.Paragraphs.Count);

        var firstText = string.Concat(reopened.Paragraphs[0].Runs.Select(r => r.GetText(0)));
        Assert.Equal(originalFirstParagraph, firstText);
    }

    [Fact]
    public void RealDocx_NoAnchors_PassThroughKeepsParagraphCount()
    {
        var path = FindSample(".docx");
        if (path == null) return;

        var original = File.ReadAllBytes(path);
        using var before = new NPOI.XWPF.UserModel.XWPFDocument(new MemoryStream(original));
        var beforeCount = before.Paragraphs.Count;

        var result = new WordFillWriter().Fill(new OfficeFillRequest { Template = original });

        using var after = new NPOI.XWPF.UserModel.XWPFDocument(new MemoryStream(result.Output));

        Assert.Equal(beforeCount, after.Paragraphs.Count);
        Assert.Equal(0, result.Report.Total);
        Assert.True(result.Report.Verified);
    }

    [Fact]
    public void RealXlsx_ProbeAnchor_FillsAndKeepsOtherCells()
    {
        var path = FindSample(".xlsx");
        if (path == null) return;

        var original = File.ReadAllBytes(path);

        // 在第一个空行写探针
        string probeA1;
        byte[] probeBytes;
        {
            var wb = new XSSFWorkbook(new MemoryStream(original));
            var sheet = wb.GetSheetAt(0);
            var probeRowIndex = sheet.LastRowNum + 1;
            var row = sheet.CreateRow(probeRowIndex);
            row.CreateCell(0).SetCellValue("【探针】{{probe}}");
            row.CreateCell(1).SetCellValue("{{qty}}");
            probeA1 = $"A{probeRowIndex + 1}";
            using var ms = new MemoryStream();
            wb.Write(ms, leaveOpen: false);
            probeBytes = ms.ToArray();
        }

        var result = new CertPlatform.Shared.Office.Excel.ExcelFillWriter().Fill(new OfficeFillRequest
        {
            Template = probeBytes,
            Values = new Dictionary<string, FillValue>(StringComparer.Ordinal)
            {
                ["probe"] = new FillValue { AnchorCode = "probe", Text = "OK-2026" },
                ["qty"] = new FillValue { AnchorCode = "qty", Kind = FillValueKind.Number, Number = 42 },
            },
        });

        Assert.True(result.Report.Verified);
        Assert.Equal(2, result.Report.Resolved);

        var reopened = new XSSFWorkbook(new MemoryStream(result.Output));
        var s = reopened.GetSheetAt(0);

        var probeCell = s.GetRow(s.LastRowNum).GetCell(0);
        Assert.Equal("【探针】OK-2026", probeCell.StringCellValue);
        Assert.Equal(CellType.Numeric, s.GetRow(s.LastRowNum).GetCell(1).CellType);
        Assert.Equal(42, s.GetRow(s.LastRowNum).GetCell(1).NumericCellValue, 6);

        Assert.Contains(probeA1, result.Report.Hits[0].Location);
    }

    /// <summary>把「打开真实文档 → 追加探针段 → 写回字节」这段样板封装掉，保持用例可读。</summary>
    private sealed class WordFillWriterProbe : IDisposable
    {
        private readonly NPOI.XWPF.UserModel.XWPFDocument _doc;

        internal WordFillWriterProbe(byte[] source, out string? firstParagraphText, out int paragraphCount)
        {
            _doc = new NPOI.XWPF.UserModel.XWPFDocument(new MemoryStream(source));

            firstParagraphText = _doc.Paragraphs.Count > 0
                ? string.Concat(_doc.Paragraphs[0].Runs.Select(r => r.GetText(0)))
                : null;
            paragraphCount = _doc.Paragraphs.Count;

            // ★ 刻意把锚点拆成两个 run，模拟 Word 的编辑历史（W9）
            var p = _doc.CreateParagraph();
            p.CreateRun().SetText("【探针】{{pro");
            p.CreateRun().SetText("be}}");
        }

        internal byte[] BuildProbeDocument()
        {
            using var ms = new MemoryStream();
            _doc.Write(ms);
            return ms.ToArray();
        }

        public void Dispose() => _doc.Dispose();
    }
}
