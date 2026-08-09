using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using YZH.Core.Extractor;
using YZH.Core.Extractor.Models;

namespace YZH.Core.Tests.Extractor
{
    /// <summary>
    /// 文件提取能力样例验证测试（基于 docs/历史文档/案例 真实样例）。
    /// <para>覆盖格式：docx（正文段落 + 表格）、xls / xlsx（逐表逐行逐格）、doc（已知限制：NPOI 无 HWPF 返回 Unsupported）、pdf（文本层探测）。</para>
    /// <para>验证产物：每次运行将摘要追加写入中间产物目录 temp/extractor-sample-verify.jsonl，供文档《文件数据提取能力落地-V1.md》验证记录节引用。</para>
    /// <para>状态：[DONE] 四类格式样例验证；[TODO:P2] .doc 转 .docx 后重新验证；[TODO:P1] PDF 中文 ToUnicode 回归。</para>
    /// </summary>
    public class FileExtractorSampleTests
    {
        private readonly ITestOutputHelper _output;

        /// <summary>样例根目录（docs/历史文档/案例）</summary>
        private const string SampleRoot = "/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/docs/历史文档/案例";

        /// <summary>验证记录落盘路径（中间产物目录，由环境信息提供）</summary>
        private static readonly string VerifyLogPath =
            "/Volumes/Expand/wangqingquan/Library/Application Support/com.tencent.mac.marvis/MarvisData/User/oAN1i2RaqWpTEqg6z3Zx_7XfP3xQ/workspace/conv_19fe566c3cc_80aebfd46fff/temp/extractor-sample-verify.jsonl";

        public FileExtractorSampleTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region docx：正文段落 + 表格

        [Theory]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/3制度文件（可根据企业实际修改）/检验作业指导书/注塑作业指导书.docx")]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/2程序文件/XASL-QP-030 医疗器械不良事件报告和再评价程序.docx")]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/其它/XASL-OR-007 采购订单（可根据实际提供）.docx")]
        public async Task Docx_Should_Extract_Paragraph_And_Table(string relativePath)
        {
            var service = new FileExtractorService();
            var fullPath = Path.Combine(SampleRoot, relativePath);
            var result = await service.ExtractAsync(fullPath);

            Assert.Equal(ExtractStatus.Success, result.Status);
            Assert.Equal(ExtractSourceType.Word, result.SourceType);
            Assert.False(string.IsNullOrWhiteSpace(result.FullText));

            _output.WriteLine($"[docx][成功] {Path.GetFileName(fullPath)} | 段落/文本块数={result.SourceInfo.StructureCount} | 表格数={result.Tables.Count} | 耗时={result.DurationMs}ms");
            _output.WriteLine($"[docx][片段] {Truncate(result.FullText, 160)}");

            AppendVerifyLog(new
            {
                format = "docx",
                file = Path.GetFileName(fullPath),
                status = result.Status.ToString(),
                sourceType = result.SourceType.ToString(),
                structureCount = result.SourceInfo.StructureCount,
                tableCount = result.Tables.Count,
                fullTextLength = result.FullText?.Length ?? 0,
                durationMs = result.DurationMs,
                snippet = Truncate(result.FullText, 120)
            });
        }

        #endregion

        #region xls：OLE2 旧格式

        [Theory]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/3制度文件（可根据企业实际修改）/受控文件清单-三阶.xls")]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/其它/XASL-OR-004 员工培训档案.xls")]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/质量类/XASL-QR-010 质量记录清单.xls")]
        public async Task Xls_Should_Extract_Sheet_Cells(string relativePath)
        {
            var service = new FileExtractorService();
            var fullPath = Path.Combine(SampleRoot, relativePath);
            var result = await service.ExtractAsync(fullPath);

            Assert.Equal(ExtractStatus.Success, result.Status);
            Assert.Equal(ExtractSourceType.Excel, result.SourceType);
            Assert.True(result.Tables.Count >= 1, "应至少提取到 1 张工作表");
            Assert.True(result.Tables[0].Rows.Count >= 1, "工作表应至少含 1 行");

            var firstRow = string.Join(" | ", result.Tables[0].Rows.First().Where(c => !string.IsNullOrWhiteSpace(c)).Take(5));
            _output.WriteLine($"[xls][成功] {Path.GetFileName(fullPath)} | 工作表数={result.SourceInfo.StructureCount} | 首表首行={firstRow} | 耗时={result.DurationMs}ms");

            AppendVerifyLog(new
            {
                format = "xls",
                file = Path.GetFileName(fullPath),
                status = result.Status.ToString(),
                sourceType = result.SourceType.ToString(),
                sheetCount = result.SourceInfo.StructureCount,
                tableCount = result.Tables.Count,
                firstRow = firstRow,
                durationMs = result.DurationMs
            });
        }

        #endregion

        #region xlsx：OOXML 新格式

        [Theory]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/生产类/XASL-PR-027 生产过程自检记录.xlsx")]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/质量类/XASL-QR-027 成品检验记录.xlsx")]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/质量类/XASL-QR-006 监视和测量设备台账.xlsx")]
        public async Task Xlsx_Should_Extract_Sheet_Cells(string relativePath)
        {
            var service = new FileExtractorService();
            var fullPath = Path.Combine(SampleRoot, relativePath);
            var result = await service.ExtractAsync(fullPath);

            Assert.Equal(ExtractStatus.Success, result.Status);
            Assert.Equal(ExtractSourceType.Excel, result.SourceType);
            Assert.True(result.Tables.Count >= 1, "应至少提取到 1 张工作表");

            var firstRow = string.Join(" | ", result.Tables[0].Rows.First().Where(c => !string.IsNullOrWhiteSpace(c)).Take(5));
            _output.WriteLine($"[xlsx][成功] {Path.GetFileName(fullPath)} | 工作表数={result.SourceInfo.StructureCount} | 首表首行={firstRow} | 耗时={result.DurationMs}ms");

            AppendVerifyLog(new
            {
                format = "xlsx",
                file = Path.GetFileName(fullPath),
                status = result.Status.ToString(),
                sourceType = result.SourceType.ToString(),
                sheetCount = result.SourceInfo.StructureCount,
                tableCount = result.Tables.Count,
                firstRow = firstRow,
                durationMs = result.DurationMs
            });
        }

        #endregion

        #region doc：已知限制（NPOI 无 HWPF，预期 Unsupported）

        [Theory]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/3制度文件（可根据企业实际修改）/制度文件/XASL-QD-005 产品留样管理制度.doc")]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/3制度文件（可根据企业实际修改）/制度文件/XASL-QD-006 批号管理制度.doc")]
        public async Task Doc_Legacy_Should_Return_Unsupported(string relativePath)
        {
            var service = new FileExtractorService();
            var fullPath = Path.Combine(SampleRoot, relativePath);
            var result = await service.ExtractAsync(fullPath);

            // 已知限制：NPOI NuGet 包（2.3.0~2.7.2）均不含 HWPF 程序集，.doc 暂不支持（见 NpoiWordExtractor.cs 与落地文档 §十）
            Assert.Equal(ExtractStatus.Unsupported, result.Status);
            Assert.Equal(ExtractSourceType.Word, result.SourceType);
            Assert.False(string.IsNullOrWhiteSpace(result.Message));

            _output.WriteLine($"[doc][预期Unsupported] {Path.GetFileName(fullPath)} | Message={result.Message}");

            AppendVerifyLog(new
            {
                format = "doc",
                file = Path.GetFileName(fullPath),
                status = result.Status.ToString(),
                sourceType = result.SourceType.ToString(),
                message = result.Message
            });
        }

        #endregion

        #region pdf：文本层探测

        [Theory]
        [InlineData("E_Documents except for the above parts (2).pdf")]
        public async Task Pdf_Should_Detect_TextLayer(string relativePath)
        {
            var service = new FileExtractorService();
            var fullPath = Path.Combine(SampleRoot, relativePath);
            var result = await service.ExtractAsync(fullPath);

            // 文本层探测结果：有文本层 → Success；无文本层 → OcrRequired（两者均为合法出口）
            Assert.True(result.Status == ExtractStatus.Success || result.Status == ExtractStatus.OcrRequired,
                $"PDF 状态应为 Success 或 OcrRequired，实际 {result.Status}");

            _output.WriteLine($"[pdf][探测] {Path.GetFileName(fullPath)} | 状态={result.Status} | 页数={result.SourceInfo.StructureCount} | 有文本层={result.SourceInfo.HasTextLayer} | 需OCR={result.SourceInfo.OcrRequired} | 耗时={result.DurationMs}ms");
            if (!string.IsNullOrWhiteSpace(result.FullText))
            {
                _output.WriteLine($"[pdf][片段] {Truncate(result.FullText, 160)}");
            }

            AppendVerifyLog(new
            {
                format = "pdf",
                file = Path.GetFileName(fullPath),
                status = result.Status.ToString(),
                pageCount = result.SourceInfo.StructureCount,
                hasTextLayer = result.SourceInfo.HasTextLayer,
                ocrRequired = result.SourceInfo.OcrRequired,
                fullTextLength = result.FullText?.Length ?? 0,
                snippet = Truncate(result.FullText, 120),
                message = result.Message,
                durationMs = result.DurationMs
            });
        }

        #endregion

        #region 辅助

        private static string Truncate(string? text, int maxLen)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "(空)";
            }

            var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length <= maxLen ? normalized : normalized.Substring(0, maxLen) + "...";
        }

        private void AppendVerifyLog(object record)
        {
            try
            {
                var line = System.Text.Json.JsonSerializer.Serialize(record) + Environment.NewLine;
                System.IO.File.AppendAllText(VerifyLogPath, line, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                _output.WriteLine($"[verify-log][失败] {ex.Message}");
            }
        }

        #endregion
    }
}
