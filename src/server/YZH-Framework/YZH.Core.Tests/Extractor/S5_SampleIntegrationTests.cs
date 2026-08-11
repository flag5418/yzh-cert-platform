using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using YZH.Core.Extractor;
using YZH.Core.Extractor.Models;

namespace YZH.Core.Tests.Extractor
{
    /// <summary>
    /// S5 集成测试：用真实样例文件验证 TextSection / Sections / 结构化输出。
    /// </summary>
    public class S5_SampleIntegrationTests
    {
        private readonly ITestOutputHelper _output;
        private const string SampleRoot = "/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/docs/历史文档/案例";

        public S5_SampleIntegrationTests(ITestOutputHelper output) => _output = output;

        #region Word docx

        [Fact]
        public async Task WordDocx_Should_Produce_Sections()
        {
            var path = Path.Combine(SampleRoot,
                "CS河北雄安尚龙医疗科技有限公司13485体系材料/2程序文件/XASL-QP-030 医疗器械不良事件报告和再评价程序.docx");
            Assert.True(File.Exists(path), $"文件不存在: {path}");

            var result = await new FileExtractorService().ExtractAsync(path);

            Assert.Equal(ExtractStatus.Success, result.Status);
            Assert.Equal(ExtractSourceType.Word, result.SourceType);
            Assert.False(string.IsNullOrWhiteSpace(result.FullText));

            // Sections 验证
            Assert.NotEmpty(result.Sections);
            var paragraphs = result.Sections.Where(s => s.SectionType == "paragraph").ToList();
            var tables = result.Sections.Where(s => s.SectionType == "table").ToList();
            _output.WriteLine($"[Word] Sections={result.Sections.Count} paragraphs={paragraphs.Count} tables={tables.Count}");
            Assert.True(paragraphs.Count > 0, "Word 应至少包含 1 个 paragraph section");

            // 每个 section 有 content 和 position_info
            foreach (var sec in result.Sections)
            {
                Assert.False(string.IsNullOrWhiteSpace(sec.Content), $"Section {sec.SectionIndex} content 为空");
                Assert.True(sec.SectionIndex > 0);
            }

            // Tables 列表也应有内容
            Assert.NotEmpty(result.Tables);
            var tableRows = result.Tables[0].Rows;
            Assert.True(tableRows.Count > 0, "第一个表格应至少 1 行");

            // 验证 DocumentExtractSkill 输出格式
            var sectionsJson = JsonSerializer.Serialize(result.Sections, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            // 确保可反序列化
            var parsed = JsonSerializer.Deserialize<System.Text.Json.JsonElement>(sectionsJson);
            Assert.Equal(JsonValueKind.Array, parsed.ValueKind);
            _output.WriteLine($"[Word] sectionsJson length={sectionsJson.Length}");
        }

        [Fact]
        public async Task WordDocx_TableSection_Should_Have_Correct_Type()
        {
            var path = Path.Combine(SampleRoot,
                "CS河北雄安尚龙医疗科技有限公司13485体系材料/2程序文件/XASL-QP-030 医疗器械不良事件报告和再评价程序.docx");
            Assert.True(File.Exists(path), $"文件不存在: {path}");

            var result = await new FileExtractorService().ExtractAsync(path);

            var tableSections = result.Sections.Where(s => s.SectionType == "table").ToList();
            Assert.NotEmpty(tableSections);

            foreach (var ts in tableSections)
            {
                Assert.True(ts.Content.Contains("表格") || ts.Content.Contains("\t"), $"table section {ts.SectionIndex} content={ts.Content.Substring(0,Math.Min(50,ts.Content.Length))}");
                Assert.True(!string.IsNullOrWhiteSpace(ts.PositionInfo));
            }
        }

        #endregion

        #region Excel xlsx

        [Fact]
        public async Task ExcelXlsx_Should_Produce_Sections()
        {
            var path = Path.Combine(SampleRoot,
                "CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/生产类/XASL-PR-027 生产过程自检记录.xlsx");
            Assert.True(File.Exists(path), $"文件不存在: {path}");

            var result = await new FileExtractorService().ExtractAsync(path);

            Assert.Equal(ExtractStatus.Success, result.Status);
            Assert.Equal(ExtractSourceType.Excel, result.SourceType);
            Assert.False(string.IsNullOrWhiteSpace(result.FullText));

            // Sections 验证
            Assert.NotEmpty(result.Sections);
            var lineSections = result.Sections.Where(s => s.SectionType == "line").ToList();
            var tableSections = result.Sections.Where(s => s.SectionType == "table").ToList();
            _output.WriteLine($"[Excel] Sections={result.Sections.Count} lines={lineSections.Count} tables={tableSections.Count}");
            Assert.True(lineSections.Count > 0, "Excel 应至少包含 1 个 line section");

            // 每个 line section 有 SheetName 和 position_info
            foreach (var ls in lineSections)
            {
                Assert.False(string.IsNullOrWhiteSpace(ls.SheetName));
                Assert.False(string.IsNullOrWhiteSpace(ls.PositionInfo));
                Assert.True(ls.Content.Contains("\t"), $"line section {ls.SectionIndex} 应含 Tab 分隔符");
            }

            // Tables 列表
            Assert.NotEmpty(result.Tables);
            Assert.True(result.Tables[0].Rows.Count >= 1, "表格应至少 1 行"); // 至少 1 行
        }

        [Fact]
        public async Task ExcelXlsx_TableSection_Should_Have_SheetName()
        {
            var path = Path.Combine(SampleRoot,
                "CS河北雄安尚龙医疗科技有限公司13485体系材料/4记录文件/质量类/XASL-QR-006 监视和测量设备台账.xlsx");
            Assert.True(File.Exists(path), $"文件不存在: {path}");

            var result = await new FileExtractorService().ExtractAsync(path);

            var tableSections = result.Sections.Where(s => s.SectionType == "table").ToList();
            Assert.NotEmpty(tableSections);

            foreach (var ts in tableSections)
            {
                Assert.False(string.IsNullOrWhiteSpace(ts.SheetName));
                Assert.False(string.IsNullOrWhiteSpace(ts.PositionInfo));
                // position_info 应含 sheet 名
                Assert.Contains(ts.SheetName, ts.PositionInfo);
            }
        }

        #endregion

        #region DocumentExtractSkill 端到端输出

        [Fact]
        public async Task DocumentExtractSkill_Should_Output_Structured_JSON()
        {
            var path = Path.Combine(SampleRoot,
                "CS河北雄安尚龙医疗科技有限公司13485体系材料/2程序文件/XASL-QP-030 医疗器械不良事件报告和再评价程序.docx");
            Assert.True(File.Exists(path), $"文件不存在: {path}");

            var extractor = new FileExtractorService();
            var result = await extractor.ExtractAsync(path);

            // 模拟 DocumentExtractSkill 的输出格式
            var sectionsJson = JsonSerializer.Serialize(result.Sections, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var tablesJson = JsonSerializer.Serialize(result.Tables, new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // 验证 JSON 合法性
            var doc = System.Text.Json.JsonDocument.Parse(sectionsJson);
            Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);

            var tbl = System.Text.Json.JsonDocument.Parse(tablesJson);
            Assert.Equal(JsonValueKind.Array, tbl.RootElement.ValueKind);

            // 验证字段结构
            var sectionsElem = doc.RootElement;
            Assert.True(sectionsElem.GetArrayLength() > 0, "sections数组不为空");
            var firstSec = sectionsElem[0];
            var props = string.Join(", ", firstSec.EnumerateObject().Select(p => p.Name));
            _output.WriteLine($"[Debug] firstSec properties: [{props}]");
            Assert.True(firstSec.TryGetProperty("content", out _), "应有 content 字段");
            Assert.True(firstSec.TryGetProperty("sectionType", out _), "应有 sectionType 字段");
            Assert.True(firstSec.TryGetProperty("sectionIndex", out _), "应有 sectionIndex 字段");
            Assert.True(firstSec.TryGetProperty("positionInfo", out _), "应有 positionInfo 字段");

            _output.WriteLine($"[DocumentExtractSkill] sectionsJson={sectionsJson.Substring(0, Math.Min(200, sectionsJson.Length))}...");
            _output.WriteLine($"[DocumentExtractSkill] tablesJson={tablesJson.Substring(0, Math.Min(200, tablesJson.Length))}...");
        }

        #endregion
    }
}
