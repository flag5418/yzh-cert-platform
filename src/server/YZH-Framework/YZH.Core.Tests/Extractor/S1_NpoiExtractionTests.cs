using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using YZH.Core.Extractor;
using YZH.Core.Extractor.Models;

namespace YZH.Core.Tests.Extractor
{
    /// <summary>
    /// Step 1 测试：NPOI 提取 Word/Excel 文档内容
    /// 使用 /docs/历史文档/案例 中的真实文件进行测试
    /// </summary>
    public class S1_NpoiExtractionTests
    {
        private readonly ITestOutputHelper _output;
        private readonly IFileExtractor _extractor;

        // 测试文件基础路径
        private readonly string _testFilesBasePath = "/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/docs/历史文档/案例";

        public S1_NpoiExtractionTests(ITestOutputHelper output)
        {
            _output = output;
            _extractor = new FileExtractorService();
        }

        #region Word (.docx) 提取测试

        [Theory]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/1质量手册/目标分解考核统计报表.docx")]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/2程序文件/XASL-QP-024 过程和产品监视测量程序.docx")]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/2程序文件/XASL-QP-030 医疗器械不良事件报告和再评价程序.docx")]
        public async Task Test_Word_Docx_Extract(string relativePath)
        {
            var filePath = Path.Combine(_testFilesBasePath, relativePath);
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"[SKIP] 文件不存在: {filePath}");
                return;
            }

            _output.WriteLine($"\n========== Word 提取测试: {Path.GetFileName(filePath)} ==========");

            var result = await _extractor.ExtractAsync(filePath);

            // 断言基本状态
            Assert.NotNull(result);
            Assert.Equal(ExtractSourceType.Word, result.SourceType);
            Assert.True(result.Status == ExtractStatus.Success || result.Status == ExtractStatus.Unsupported,
                $"提取状态异常: {result.Status}, 错误: {result.ErrorMessage}");

            // 输出提取结果
            _output.WriteLine($"状态: {result.Status}");
            _output.WriteLine($"段落数: {result.Sections.Count}");
            _output.WriteLine($"表格数: {result.Tables.Count}");
            _output.WriteLine($"耗时: {result.DurationMs}ms");

            if (result.Status == ExtractStatus.Success)
            {
                Assert.False(string.IsNullOrEmpty(result.FullText), "FullText 不应为空");
                Assert.True(result.Sections.Count > 0, "应至少有一个段落");

                // 输出前 500 字符预览
                var preview = result.FullText?.Length > 500
                    ? result.FullText.Substring(0, 500) + "..."
                    : result.FullText;
                _output.WriteLine($"\n内容预览:\n{preview}");

                // 输出段落结构
                _output.WriteLine($"\n段落结构 (前 5 个):");
                foreach (var section in result.Sections.Take(5))
                {
                    var content = section.Content?.Length > 100
                        ? section.Content.Substring(0, 100) + "..."
                        : section.Content;
                    _output.WriteLine($"  [{section.SectionType}] {content}");
                }

                // 输出表格结构
                if (result.Tables.Count > 0)
                {
                    _output.WriteLine($"\n表格结构:");
                    foreach (var table in result.Tables)
                    {
                        _output.WriteLine($"  表格 {table.TableIndex}: {table.Rows.Count} 行");
                        if (table.Rows.Count > 0)
                        {
                            _output.WriteLine($"    首行: {string.Join(" | ", table.Rows[0].Take(5))}");
                        }
                    }
                }
            }
            else
            {
                _output.WriteLine($"提取未成功: {result.Message}");
            }

            _output.WriteLine("========== 测试完成 ==========\n");
        }

        #endregion

        #region Word 页眉提取测试（封面表格位于 header*.xml）

        /// <summary>
        /// 认证体系文档的封面表格（文件编号/版本/生效日期）常位于 Word 页眉（header*.xml）
        /// 而非正文，提取器必须把页眉内容纳入提取结果，否则 AI 分析拿不到这些关键信息。
        /// </summary>
        [Fact]
        public async Task Test_Word_Header_Content_Extracted()
        {
            using var doc = new NPOI.XWPF.UserModel.XWPFDocument();

            // 在页眉创建封面表格（模拟 XASL-QM 质量手册：文件编号/版本/生效日期）
            var policy = doc.CreateHeaderFooterPolicy();
            var header = policy.CreateHeader(NPOI.XWPF.Model.XWPFHeaderFooterPolicy.DEFAULT);
            var headerTable = header.CreateTable(1, 3);
            headerTable.GetRow(0).GetCell(0).SetText("河北雄安尚龙医疗科技有限公司 质量手册");
            headerTable.GetRow(0).GetCell(1).SetText("文件编号：XASL-QM");
            headerTable.GetRow(0).GetCell(2).SetText("版本：A/0 生效日期：2026.02.05");

            // 正文
            doc.CreateParagraph().CreateRun().SetText("本手册规定了公司质量管理体系的各项要求。");

            // 注意：XWPFDocument.Write(stream) 会关闭传入的流，须先写入临时文件再提取
            var tempPath = Path.Combine(Path.GetTempPath(), $"header-test-{Guid.NewGuid():N}.docx");
            try
            {
                using (var fs = File.Create(tempPath))
                {
                    doc.Write(fs);
                }

                var result = await _extractor.ExtractAsync(tempPath);

            _output.WriteLine($"状态: {result.Status}, 段落数: {result.Sections.Count}, 表格数: {result.Tables.Count}");
            _output.WriteLine($"FullText 前 300 字:\n{(result.FullText?.Length > 300 ? result.FullText.Substring(0, 300) : result.FullText)}");

            Assert.Equal(ExtractStatus.Success, result.Status);
            // 页眉封面信息必须出现在提取结果中
            Assert.Contains("文件编号", result.FullText);
            Assert.Contains("XASL-QM", result.FullText);
            Assert.Contains("生效日期", result.FullText);
            Assert.Contains("2026.02.05", result.FullText);
            // 页眉内容应排在正文之前
            var headerIdx = result.FullText.IndexOf("文件编号", StringComparison.Ordinal);
            var bodyIdx = result.FullText.IndexOf("本手册规定了", StringComparison.Ordinal);
            Assert.True(headerIdx >= 0 && bodyIdx > headerIdx, $"页眉内容({headerIdx})应排在正文({bodyIdx})之前");
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        #endregion

        #region Excel (.xlsx) 提取测试

        [Theory]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/第一类医疗器械适用法律法规目录.xlsx")]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/质量类/XASL-QR-027 成品检验记录.xlsx")]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/质量类/XASL-QR-037 过程检验记录.xlsx")]
        [InlineData("CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/质量类/XASL-QR-006 监视和测量设备台账.xlsx")]
        public async Task Test_Excel_Xlsx_Extract(string relativePath)
        {
            var filePath = Path.Combine(_testFilesBasePath, relativePath);
            if (!File.Exists(filePath))
            {
                _output.WriteLine($"[SKIP] 文件不存在: {filePath}");
                return;
            }

            _output.WriteLine($"\n========== Excel 提取测试: {Path.GetFileName(filePath)} ==========");

            var result = await _extractor.ExtractAsync(filePath);

            // 断言基本状态
            Assert.NotNull(result);
            Assert.Equal(ExtractSourceType.Excel, result.SourceType);
            Assert.True(result.Status == ExtractStatus.Success || result.Status == ExtractStatus.Unsupported,
                $"提取状态异常: {result.Status}, 错误: {result.ErrorMessage}");

            // 输出提取结果
            _output.WriteLine($"状态: {result.Status}");
            _output.WriteLine($"段落数: {result.Sections.Count}");
            _output.WriteLine($"表格数: {result.Tables.Count}");
            _output.WriteLine($"耗时: {result.DurationMs}ms");

            if (result.Status == ExtractStatus.Success)
            {
                Assert.False(string.IsNullOrEmpty(result.FullText), "FullText 不应为空");
                Assert.True(result.Tables.Count > 0, "应至少有一个表格（工作表）");

                // 输出前 500 字符预览
                var preview = result.FullText?.Length > 500
                    ? result.FullText.Substring(0, 500) + "..."
                    : result.FullText;
                _output.WriteLine($"\n内容预览:\n{preview}");

                // 输出表格结构
                _output.WriteLine($"\n工作表结构:");
                foreach (var table in result.Tables)
                {
                    _output.WriteLine($"  工作表 '{table.SheetName}': {table.Rows.Count} 行 x {(table.Rows.Count > 0 ? table.Rows[0].Count : 0)} 列");
                    if (table.Rows.Count > 0)
                    {
                        // 输出表头（第一行）
                        _output.WriteLine($"    表头: {string.Join(" | ", table.Rows[0].Take(8))}");
                        // 输出数据样例（第二行）
                        if (table.Rows.Count > 1)
                        {
                            _output.WriteLine($"    样例: {string.Join(" | ", table.Rows[1].Take(8))}");
                        }
                    }
                }
            }
            else
            {
                _output.WriteLine($"提取未成功: {result.Message}");
            }

            _output.WriteLine("========== 测试完成 ==========\n");
        }

        #endregion

        #region 提取结果 JSON 格式验证

        [Fact]
        public async Task Test_ExtractResult_JsonStructure()
        {
            // 找一个确定的测试文件
            var testFile = Path.Combine(_testFilesBasePath,
                "CS河北雄安尚龙医疗科技有限公司13485体系材料/1质量手册/目标分解考核统计报表.docx");

            if (!File.Exists(testFile))
            {
                _output.WriteLine($"[SKIP] 测试文件不存在: {testFile}");
                return;
            }

            _output.WriteLine("\n========== 提取结果 JSON 结构验证 ==========");

            var result = await _extractor.ExtractAsync(testFile);

            // 验证结果可以序列化为 JSON（用于后续 AI 分析）
            var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            Assert.False(string.IsNullOrEmpty(json), "JSON 序列化结果不应为空");

            // 输出 JSON 结构概要
            _output.WriteLine($"JSON 长度: {json.Length} 字符");
            _output.WriteLine("\nJSON 结构预览 (前 2000 字符):");
            _output.WriteLine(json.Length > 2000 ? json.Substring(0, 2000) + "..." : json);

            // 验证关键字段存在
            Assert.Contains("fileName", json);
            Assert.Contains("sourceType", json);
            Assert.Contains("status", json);
            Assert.Contains("fullText", json);
            Assert.Contains("sections", json);
            Assert.Contains("tables", json);

            _output.WriteLine("\n========== 测试完成 ==========\n");
        }

        #endregion

        #region 批量测试统计

        [Fact]
        public async Task Test_Batch_Statistics()
        {
            _output.WriteLine("\n========== 批量提取统计 ==========");

            var testFiles = new[]
            {
                ("CS河北雄安尚龙医疗科技有限公司13485体系材料/1质量手册/目标分解考核统计报表.docx", "word"),
                ("CS河北雄安尚龙医疗科技有限公司13485体系材料/2程序文件/XASL-QP-024 过程和产品监视测量程序.docx", "word"),
                ("CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/第一类医疗器械适用法律法规目录.xlsx", "excel"),
                ("CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/质量类/XASL-QR-027 成品检验记录.xlsx", "excel"),
            };

            int successCount = 0;
            int failCount = 0;
            long totalDuration = 0;

            foreach (var (relativePath, type) in testFiles)
            {
                var filePath = Path.Combine(_testFilesBasePath, relativePath);
                if (!File.Exists(filePath))
                {
                    _output.WriteLine($"[SKIP] 不存在: {relativePath}");
                    continue;
                }

                try
                {
                    var result = await _extractor.ExtractAsync(filePath);
                    totalDuration += result.DurationMs;

                    if (result.Status == ExtractStatus.Success)
                    {
                        successCount++;
                        _output.WriteLine($"[✓] {Path.GetFileName(filePath)} - {result.Sections.Count} 段落, {result.Tables.Count} 表格, {result.DurationMs}ms");
                    }
                    else
                    {
                        failCount++;
                        _output.WriteLine($"[✗] {Path.GetFileName(filePath)} - {result.Status}: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    _output.WriteLine($"[✗] {Path.GetFileName(filePath)} - 异常: {ex.Message}");
                }
            }

            _output.WriteLine($"\n统计结果:");
            _output.WriteLine($"  成功: {successCount}");
            _output.WriteLine($"  失败: {failCount}");
            _output.WriteLine($"  总耗时: {totalDuration}ms");
            _output.WriteLine($"  平均耗时: {(successCount + failCount > 0 ? totalDuration / (successCount + failCount) : 0)}ms");

            // 至少有一个成功才算测试通过
            Assert.True(successCount > 0, "至少应有一个文件提取成功");

            _output.WriteLine("========== 测试完成 ==========\n");
        }

        #endregion
    }
}
